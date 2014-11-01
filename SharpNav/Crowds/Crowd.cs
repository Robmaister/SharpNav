#region License
/**
 * Copyright (c) 2014 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
 * Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE
 */
#endregion

using System;
using System.Collections.Generic;

using SharpNav.Collections.Generic;
using SharpNav.Geometry;

#if MONOGAME || XNA
using Microsoft.Xna.Framework;
#elif OPENTK
using OpenTK;
#elif SHARPDX
using SharpDX;
#elif UNITY3D
using UnityEngine;
#endif

namespace SharpNav.Crowds
{
	/// <summary>
	/// The Crowd class manages pathfinding for multiple agents simulatenously.
	/// </summary>
	public class Crowd
	{
		/// <summary>
		/// The maximum number of neighbors that a crowd agent can take into account for steering decisions
		/// </summary>
		private const int AgentMaxNeighbours = 6;

		/// <summary>
		/// The maximum number of corners a crowd agent will look ahead in the path
		/// </summary>
		private const int AgentMaxCorners = 4;

		/// <summary>
		/// The maximum number of crowd avoidance configurations supported by the crowd manager
		/// </summary>
		private const int AgentMaxObstacleAvoidanceParams = 8;

		private const int MaxItersPerUpdate = 100;

		private int maxAgents;
		private CrowdAgent[] agents;
		private CrowdAgent[] activeAgents;
		private CrowdAgentAnimation[] agentAnims; 

		private PathQueue pathq;

		private ObstacleAvoidanceQuery.ObstacleAvoidanceParams[] obstacleQueryParams;
		private ObstacleAvoidanceQuery obstacleQuery;

		private ProximityGrid grid;

		private int[] pathResult;
		private int maxPathResult;

		private Vector3 ext;

		private float maxAgentRadius;

		private int velocitySampleCount;

		private NavMeshQuery navquery;

		/// <summary>
		/// Initializes a new instance of the <see cref="Crowd" /> class.
		/// </summary>
		/// <param name="maxAgents">The maximum agents allowed</param>
		/// <param name="maxAgentRadius">The maximum radius for an agent</param>
		/// <param name="nav">The navigation mesh</param>
		public Crowd(int maxAgents, float maxAgentRadius, ref TiledNavMesh nav)
		{
			this.maxAgents = maxAgents;
			this.maxAgentRadius = maxAgentRadius;

			this.ext = new Vector3(maxAgentRadius * 2.0f, maxAgentRadius * 1.5f, maxAgentRadius * 2.0f);

			//initialize proximity grid
			this.grid = new ProximityGrid(maxAgents * 4, maxAgentRadius * 3);

			//allocate obstacle avoidance query
			this.obstacleQuery = new ObstacleAvoidanceQuery(6, 8);

			//initialize obstancle query params
			this.obstacleQueryParams = new ObstacleAvoidanceQuery.ObstacleAvoidanceParams[AgentMaxObstacleAvoidanceParams];
			for (int i = 0; i < this.obstacleQueryParams.Length; i++)
			{
				this.obstacleQueryParams[i].VelBias = 0.4f;
				this.obstacleQueryParams[i].WeightDesVel = 2.0f;
				this.obstacleQueryParams[i].WeightCurVel = 0.75f;
				this.obstacleQueryParams[i].WeightSide = 0.75f;
				this.obstacleQueryParams[i].WeightToi = 2.5f;
				this.obstacleQueryParams[i].HorizTime = 2.5f;
				this.obstacleQueryParams[i].GridSize = 33;
				this.obstacleQueryParams[i].AdaptiveDivs = 7;
				this.obstacleQueryParams[i].AdaptiveRings = 2;
				this.obstacleQueryParams[i].AdaptiveDepth = 5;
			}

			//allocate temp buffer for merging paths
			this.maxPathResult = 256;
			this.pathResult = new int[this.maxPathResult];

			this.pathq = new PathQueue(maxPathResult, 4096, ref nav);

			this.agents = new CrowdAgent[maxAgents];
			this.activeAgents = new CrowdAgent[maxAgents];
			this.agentAnims = new CrowdAgentAnimation[maxAgents];

			for (int i = 0; i < maxAgents; i++)
			{
				this.agents[i] = new CrowdAgent();
				this.agents[i].Active = false;
				this.agents[i].Corridor = new PathCorridor(maxPathResult);
				this.agents[i].Boundary = new LocalBoundary();
				this.agents[i].Neis = new CrowdNeighbor[32];
				this.agents[i].CornerVerts = new Vector3[AgentMaxCorners];
				this.agents[i].CornerFlags = new int[AgentMaxCorners];
				this.agents[i].CornerPolys = new int[AgentMaxCorners];
			}

			for (int i = 0; i < maxAgents; i++)
			{
				this.agentAnims[i].Active = false;
			}

			//allocate nav mesh query
			this.navquery = new NavMeshQuery(nav, 512);
		}

		public ObstacleAvoidanceQuery.ObstacleAvoidanceParams GetObstacleAvoidanceParams(int idx)
		{
			if (idx >= 0 && idx < AgentMaxObstacleAvoidanceParams)
				return obstacleQueryParams[idx];

			return new ObstacleAvoidanceQuery.ObstacleAvoidanceParams();
		}

		public void SetObstacleAvoidanceParams(int idx, ObstacleAvoidanceQuery.ObstacleAvoidanceParams parameters)
		{
			if (idx >= 0 && idx < AgentMaxObstacleAvoidanceParams)
				obstacleQueryParams[idx] = parameters;
		}

		public int GetAgentCount()
		{
			return maxAgents;
		}

		public CrowdAgent GetAgent(int idx)
		{
			if (idx < 0 || idx >= maxAgents)
				return null;

			return agents[idx];
		}

		/// <summary>
		/// Add an agent to the crowd.
		/// </summary>
		/// <param name="pos">The agent's position</param>
		/// <param name="parameters">The settings</param>
		/// <returns>The id of the agent (-1 if there is no empty slot)</returns>
		public int AddAgent(Vector3 pos, CrowdAgentParams parameters)
		{
			//find empty slot
			int idx = -1;
			for (int i = 0; i < maxAgents; i++)
			{
				if (!agents[i].Active)
				{
					idx = i;
					break;
				}
			}

			if (idx == -1)
				return -1;

			UpdateAgentParameters(idx, parameters);

			//find nearest position on the navmesh and place the agent there
			Vector3 nearest;
			int reference = 0;
			nearest = pos;
			bool status = navquery.FindNearestPoly(ref pos, ref ext, out reference, out nearest);
			if (status == false)
			{
				nearest = pos;
				reference = 0;
			}

			agents[idx].Corridor.Reset(reference, nearest);
			agents[idx].Boundary.Reset();
			agents[idx].Partial = false;

			agents[idx].TopologyOptTime = 0;
			agents[idx].TargetReplanTime = 0;
			agents[idx].NumNeis = 0;

			agents[idx].DesiredVel = new Vector3(0.0f, 0.0f, 0.0f);
			agents[idx].NVel = new Vector3(0.0f, 0.0f, 0.0f);
			agents[idx].Vel = new Vector3(0.0f, 0.0f, 0.0f);
			agents[idx].CurrentPos = nearest;

			agents[idx].DesiredSpeed = 0;

			if (reference != 0)
				agents[idx].State = CrowdAgentState.Walking;
			else
				agents[idx].State = CrowdAgentState.Invalid;

			agents[idx].TargetState = TargetState.None;

			agents[idx].Active = true;

			return idx;
		}

		/// <summary>
		/// The agent is deactivated and will no longer be processed. It can still be reused later.
		/// </summary>
		/// <param name="idx">The agent's id</param>
		public void RemoveAgent(int idx)
		{
			if (idx >= 0 && idx < maxAgents)
			{
				agents[idx].Active = false;
			}
		}

		/// <summary>
		/// Change the move target
		/// </summary>
		/// <param name="idx">The agent id</param>
		/// <param name="reference">The polygon reference</param>
		/// <param name="pos">The target's coordinates</param>
		/// <returns>True if request met, false if not</returns>
		public bool RequestMoveTargetReplan(int idx, int reference, Vector3 pos)
		{
			if (idx < 0 || idx >= maxAgents)
				return false;

			//initialize request
			agents[idx].TargetRef = reference;
			agents[idx].TargetPos = pos;
			agents[idx].TargetPathqRef = PathQueue.PATHQ_INVALID;
			agents[idx].TargetReplan = true;
			if (agents[idx].TargetRef != 0)
				agents[idx].TargetState = TargetState.Requesting;
			else
				agents[idx].TargetState = TargetState.Failed;

			return true;
		}

		/// <summary>
		/// Request a new move target
		/// </summary>
		/// <param name="idx">The agent id</param>
		/// <param name="reference">The polygon reference</param>
		/// <param name="pos">The target's coordinates</param>
		/// <returns>True if request met, false if not</returns>
		public bool RequestMoveTarget(int idx, int reference, Vector3 pos)
		{
			if (idx < 0 || idx >= maxAgents)
				return false;
			if (reference == 0)
				return false;

			//initialize request
			agents[idx].TargetRef = reference;
			agents[idx].TargetPos = pos;
			agents[idx].TargetPathqRef = PathQueue.PATHQ_INVALID;
			agents[idx].TargetReplan = false;
			if (agents[idx].TargetRef != 0)
				agents[idx].TargetState = TargetState.Requesting;
			else
				agents[idx].TargetState = TargetState.Failed;

			return true;
		}

		/// <summary>
		/// Request a new move velocity
		/// </summary>
		/// <param name="idx">The agent's id</param>
		/// <param name="vel">The agent's velocity</param>
		/// <returns>True if request met, false if not</returns>
		public bool RequestMoveVelocity(int idx, Vector3 vel)
		{
			if (idx < 0 || idx >= maxAgents)
				return false;

			//initialize request
			agents[idx].TargetRef = 0;
			agents[idx].TargetPos = vel;
			agents[idx].TargetPathqRef = PathQueue.PATHQ_INVALID;
			agents[idx].TargetReplan = false;
			agents[idx].TargetState = TargetState.Velocity;

			return true;
		}

		/// <summary>
		/// Reset the move target of an agent
		/// </summary>
		/// <param name="idx">The agent's id</param>
		/// <returns>True if the agent exists, false if not</returns>
		public bool ResetMoveTarget(int idx)
		{
			if (idx < 0 || idx >= maxAgents)
				return false;

			//initialize request
			agents[idx].TargetRef = 0;
			agents[idx].TargetPos = new Vector3(0.0f, 0.0f, 0.0f);
			agents[idx].TargetPathqRef = PathQueue.PATHQ_INVALID;
			agents[idx].TargetReplan = false;
			agents[idx].TargetState = TargetState.None;

			return true;
		}

		/// <summary>
		/// The crowd contains active and inactive agents. Only add all the active agents to a separate array.
		/// </summary>
		/// <param name="agents">The array of active agents</param>
		/// <param name="maxAgents">The maximum agents allowed</param>
		/// <returns>The number of active agents</returns>
		public int GetActiveAgents(CrowdAgent[] agents, int maxAgents)
		{
			int n = 0;
			for (int i = 0; i < maxAgents; i++)
			{
				if (!agents[i].Active)
					continue;

				if (n < maxAgents)
					agents[n++] = agents[i];
			}

			return n;
		}

		/// <summary>
		/// Modify the agent parameters
		/// </summary>
		/// <param name="idx">The agent's id</param>
		/// <param name="parameters">The new parameters</param>
		public void UpdateAgentParameters(int idx, CrowdAgentParams parameters)
		{
			if (idx < 0 || idx >= maxAgents)
				return;
			agents[idx].Parameters = parameters;
		}

		/// <summary>
		/// Get the agent's index in the array
		/// </summary>
		/// <param name="agent">The agent</param>
		/// <returns>The index</returns>
		public int GetAgentIndex(CrowdAgent agent)
		{
			for (int i = 0; i < agents.Length; i++)
			{
				if (agents[i] == agent)
					return i;
			}

			return -1;
		}

		/// <summary>
		/// Update the crowd pathfinding periodically 
		/// </summary>
		/// <param name="dt">Th time until the next update</param>
		public void Update(float dt)
		{
			velocitySampleCount = 0;

			int numAgents = GetActiveAgents(agents, maxAgents);

			//check that all agents have valid paths
			CheckPathValidity(agents, numAgents, dt);
			
			//update async move requests and path finder
			UpdateMoveRequest();

			//optimize path topology
			UpdateTopologyOptimization(agents, numAgents, dt);

			//register agents to proximity grid
			grid.Clear();
			for (int i = 0; i < numAgents; i++)
			{
				Vector3 p = agents[i].CurrentPos;
				float r = agents[i].Parameters.Radius;
				grid.AddItem(i, p.X - r, p.Z - r, p.X + r, p.Z + r);
			}

			//get nearby navmesh segments and agents to collide with
			for (int i = 0; i < numAgents; i++)
			{
				if (agents[i].State != CrowdAgentState.Walking)
					continue;

				//update the collision boundary after certain distance has passed or if it has become invalid
				float updateThr = agents[i].Parameters.CollisionQueryRange * 0.25f;
				if (Vector3Extensions.Distance2D(agents[i].CurrentPos, agents[i].Boundary.Center) > updateThr * updateThr ||
					!agents[i].Boundary.IsValid(navquery))
				{
					agents[i].Boundary.Update(agents[i].Corridor.GetFirstPoly(), agents[i].CurrentPos, 
						agents[i].Parameters.CollisionQueryRange, navquery);
				}

				//query neighbour agents
				agents[i].NumNeis = GetNeighbours(agents[i].CurrentPos, agents[i].Parameters.Height, agents[i].Parameters.CollisionQueryRange,
					agents[i], agents[i].Neis, AgentMaxNeighbours, agents, grid);

				for (int j = 0; j < agents[i].NumNeis; j++)
					agents[i].Neis[j].Idx = GetAgentIndex(agents[agents[i].Neis[j].Idx]);
			}

			//find the next corner to steer to
			for (int i = 0; i < numAgents; i++)
			{
				if (agents[i].State != CrowdAgentState.Walking)
					continue;
				if (agents[i].TargetState == TargetState.None ||
					agents[i].TargetState == TargetState.Velocity)
					continue;

				//find corners for steering
				agents[i].NumCorners = agents[i].Corridor.FindCorners(
					agents[i].CornerVerts, agents[i].CornerFlags, agents[i].CornerPolys, AgentMaxCorners, navquery);

				//check to see if the corner after the next corner is directly visible 
				if (((agents[i].Parameters.UpdateFlags & UpdateFlags.OptimizeVis) != 0) && agents[i].NumCorners > 0)
				{
					Vector3 target = agents[i].CornerVerts[Math.Min(1, agents[i].NumCorners - 1)];
					agents[i].Corridor.OptimizePathVisibility(target, agents[i].Parameters.PathOptimizationRange, navquery);
				}
			}

			//trigger off-mesh connections (depends on corners)
			for (int i = 0; i < numAgents; i++)
			{
				if (agents[i].State != CrowdAgentState.Walking)
					continue;
				if (agents[i].TargetState == TargetState.None ||
					agents[i].TargetState == TargetState.Velocity)
					continue;

				//check
				float triggerRadius = agents[i].Parameters.Radius * 2.25f;
				if (OverOffmeshConnection(agents[i], triggerRadius))
				{
					//prepare to off-mesh connection
					int idx = i;
					
					//adjust the path over the off-mesh connection
					int[] refs = new int[2];
					if (agents[i].Corridor.MoveOverOffmeshConnection(agents[i].CornerPolys[agents[i].NumCorners - 1], 
						refs, ref agentAnims[idx].StartPos, ref agentAnims[idx].EndPos, navquery))
					{
						agentAnims[idx].InitPos = agents[i].CurrentPos;
						agentAnims[idx].PolyRef = refs[1];
						agentAnims[idx].Active = true;
						agentAnims[idx].T = 0.0f;
						agentAnims[idx].TMax = (Vector3Extensions.Distance2D(agentAnims[idx].StartPos, agentAnims[idx].EndPos)
							/ agents[i].Parameters.MaxSpeed) * 0.5f;

						agents[i].State = CrowdAgentState.Offmesh;
						agents[i].NumCorners = 0;
						agents[i].NumNeis = 0;
						continue;
					}
				}
			}

			//calculate steering
			for (int i = 0; i < numAgents; i++)
			{
				if (agents[i].State != CrowdAgentState.Walking)
					continue;
				if (agents[i].TargetState == TargetState.None)
					continue;

				Vector3 dvel = new Vector3(0, 0, 0);

				if (agents[i].TargetState == TargetState.Velocity)
				{
					dvel = agents[i].TargetPos;
					agents[i].DesiredSpeed = agents[i].TargetPos.Length();
				}
				else
				{
					//calculate steering direction
					if ((agents[i].Parameters.UpdateFlags & UpdateFlags.AnticipateTurns) != 0)
						CalcSmoothSteerDirection(agents[i], ref dvel);
					else
						CalcStraightSteerDirection(agents[i], ref dvel);

					//calculate speed scale, which tells the agent to slowdown at the end of the path
					float slowDownRadius = agents[i].Parameters.Radius * 2;
					float speedScale = GetDistanceToGoal(agents[i], slowDownRadius) / slowDownRadius;

					agents[i].DesiredSpeed = agents[i].Parameters.MaxSpeed;
					dvel = dvel * (agents[i].DesiredSpeed * speedScale);
				}

				//separation
				if ((agents[i].Parameters.UpdateFlags & UpdateFlags.Separation) != 0)
				{
					float separationDist = agents[i].Parameters.CollisionQueryRange;
					float invSeparationDist = 1.0f / separationDist;
					float separationWeight = agents[i].Parameters.SeparationWeight;

					float w = 0;
					Vector3 disp = new Vector3(0, 0, 0);

					for (int j = 0; j < agents[i].NumNeis; j++)
					{
						CrowdAgent nei = agents[agents[i].Neis[j].Idx];

						Vector3 diff = agents[i].CurrentPos - nei.CurrentPos;
						diff.Y = 0;

						float distSqr = diff.LengthSquared();
						if (distSqr < 0.00001f)
							continue;
						if (distSqr > separationDist * separationDist)
							continue;
						float dist = (float)Math.Sqrt(distSqr);
						float weight = separationWeight * (1.0f - (dist * invSeparationDist) * (dist * invSeparationDist));

						disp = disp + diff * (weight / dist);
						w += 1.0f;
					}

					if (w > 0.0001f)
					{
						//adjust desired veloctiy
						dvel = dvel + disp * (1.0f / w);

						//clamp desired velocity to desired speed
						float speedSqr = dvel.LengthSquared();
						float desiredSqr = agents[i].DesiredSpeed * agents[i].DesiredSpeed;
						if (speedSqr > desiredSqr)
							dvel = dvel * (desiredSqr / speedSqr);
					}
				}

				//set the desired velocity
				agents[i].DesiredVel = dvel;
			}

			//velocity planning
			for (int i = 0; i < numAgents; i++)
			{
				if (agents[i].State != CrowdAgentState.Walking)
					continue;

				if ((agents[i].Parameters.UpdateFlags & UpdateFlags.ObstacleAvoidance) != 0)
				{
					this.obstacleQuery.Reset();

					//add neighhbors as obstacles
					for (int j = 0; j < agents[i].NumNeis; j++)
					{
						CrowdAgent nei = agents[agents[i].Neis[j].Idx];
						obstacleQuery.AddCircle(nei.CurrentPos, nei.Parameters.Radius, nei.Vel, nei.DesiredVel);
					}

					//append neighbour segments as obstacles
					for (int j = 0; j < agents[i].Boundary.SegCount; j++)
					{
						LocalBoundary.Segment s = agents[i].Boundary.Segs[j];
						if (Triangle3.Area2D(agents[i].CurrentPos, s.Start, s.End) < 0.0f)
							continue;
						obstacleQuery.AddSegment(s.Start, s.End);
					}

					//sample new safe velocity
					bool adaptive = true;
					int ns = 0;

					ObstacleAvoidanceQuery.ObstacleAvoidanceParams parameters = obstacleQueryParams[agents[i].Parameters.ObstacleAvoidanceType];

					if (adaptive)
					{
						ns = obstacleQuery.SampleVelocityAdaptive(agents[i].CurrentPos, agents[i].Parameters.Radius, agents[i].DesiredSpeed,
							agents[i].Vel, agents[i].DesiredVel, ref agents[i].NVel, parameters);
					}
					else
					{
						ns = obstacleQuery.SampleVelocityGrid(agents[i].CurrentPos, agents[i].Parameters.Radius, agents[i].DesiredSpeed,
							agents[i].Vel, agents[i].DesiredVel, ref agents[i].NVel, parameters);
					}

					this.velocitySampleCount += ns;
				}
				else
				{
					//if not using velocity planning, new velocity is directly the desired velocity
					agents[i].NVel = agents[i].DesiredVel;
				}
			}

			//integrate
			for (int i = 0; i < numAgents; i++)
			{
				if (agents[i].State != CrowdAgentState.Walking)
					continue;

				agents[i].Integrate(dt);
			}

			//handle collisions
			const float COLLISION_RESOLVE_FACTOR = 0.7f;

			for (int iter = 0; iter < 4; iter++)
			{
				for (int i = 0; i < numAgents; i++)
				{
					int idx0 = GetAgentIndex(agents[i]);

					if (agents[i].State != CrowdAgentState.Walking)
						continue;

					agents[i].Disp = new Vector3(0, 0, 0);

					float w = 0;

					for (int j = 0; j < agents[i].NumNeis; j++)
					{
						CrowdAgent nei = agents[agents[i].Neis[j].Idx];
						int idx1 = GetAgentIndex(nei);

						Vector3 diff = agents[i].CurrentPos - nei.CurrentPos;
						diff.Y = 0;

						float dist = diff.LengthSquared();
						if (dist > (agents[i].Parameters.Radius + nei.Parameters.Radius) * (agents[i].Parameters.Radius + nei.Parameters.Radius))
							continue;
						dist = (float)Math.Sqrt(dist);
						float pen = (agents[i].Parameters.Radius + nei.Parameters.Radius) - dist;
						if (dist < 0.0001f)
						{
							//agents on top of each other, try to choose diverging separation directions
							if (idx0 > idx1)
								diff = new Vector3(-agents[i].DesiredVel.Z, 0, agents[i].DesiredVel.X);
							else
								diff = new Vector3(agents[i].DesiredVel.Z, 0, -agents[i].DesiredVel.X);
							pen = 0.01f;
						}
						else
						{
							pen = (1.0f / dist) * (pen * 0.5f) * COLLISION_RESOLVE_FACTOR;
						}

						agents[i].Disp = agents[i].Disp + diff * pen;

						w += 1.0f;
					}

					if (w > 0.0001f)
					{
						float iw = 1.0f / w;
						agents[i].Disp = agents[i].Disp * iw;
					}
				}

				for (int i = 0; i < numAgents; i++)
				{
					if (agents[i].State != CrowdAgentState.Walking)
						continue;

					//move along navmesh
					agents[i].Corridor.MovePosition(agents[i].CurrentPos, navquery);

					//get valid constrained position back
					agents[i].CurrentPos = agents[i].Corridor.Pos;

					//if not using path, truncate the corridor to just one poly
					if (agents[i].TargetState == TargetState.None ||
						agents[i].TargetState == TargetState.Velocity)
					{
						agents[i].Corridor.Reset(agents[i].Corridor.GetFirstPoly(), agents[i].CurrentPos);
						agents[i].Partial = false;
					}
				}

				//update agents using offmesh connections
				for (int i = 0; i < maxAgents; i++)
				{
					if (!agentAnims[i].Active)
						continue;

					agentAnims[i].T += dt;
					if (agentAnims[i].T > agentAnims[i].TMax)
					{
						//reset animation
						agentAnims[i].Active = false;

						//prepare agent for walking
						agents[i].State = CrowdAgentState.Walking;

						continue;
					}

					//update position
					float ta = agentAnims[i].TMax * 0.15f;
					float tb = agentAnims[i].TMax;
					if (agentAnims[i].T < ta)
					{
						float u = MathHelper.Normalize(agentAnims[i].T, 0.0f, ta);
						Vector3.Lerp(ref agentAnims[i].InitPos, ref agentAnims[i].StartPos, u, out agents[i].CurrentPos);
					}
					else
					{
						float u = MathHelper.Normalize(agentAnims[i].T, ta, tb);
						Vector3.Lerp(ref agentAnims[i].StartPos, ref agentAnims[i].EndPos, u, out agents[i].CurrentPos);
					}

					agents[i].Vel = new Vector3(0, 0, 0);
					agents[i].DesiredVel = new Vector3(0, 0, 0);
				}
			}
		}

		/// <summary>
		/// Change the move requests for all the agents
		/// </summary>
		public void UpdateMoveRequest()
		{
			const int PATH_MAX_AGENTS = 8;
			CrowdAgent[] queue = new CrowdAgent[PATH_MAX_AGENTS];
			int numQueue = 0;
			Status status;

			//fire off new requests
			for (int i = 0; i < maxAgents; i++)
			{
				if (!agents[i].Active)
					continue;
				if (agents[i].State == CrowdAgentState.Invalid)
					continue;
				if (agents[i].TargetState == TargetState.None || agents[i].TargetState == TargetState.Velocity)
					continue;

				if (agents[i].TargetState == TargetState.Requesting)
				{
					int[] path = agents[i].Corridor.Path;
					int npath = agents[i].Corridor.PathCount;

					const int MAX_RES = 32;
					Vector3 reqPos = new Vector3();
					int[] reqPath = new int[MAX_RES];
					int reqPathCount = 0;

					//quick search towards the goal
					const int MAX_ITER = 20;
					navquery.InitSlicedFindPath(path[0], agents[i].TargetRef, agents[i].CurrentPos, agents[i].TargetPos);
					int tempInt = 0;
					navquery.UpdateSlicedFindPath(MAX_ITER, ref tempInt);
					status = Status.Failure;
					if (agents[i].TargetReplan)
					{
						//try to use an existing steady path during replan if possible
						status = navquery.FinalizedSlicedPathPartial(path, npath, reqPath, ref reqPathCount, MAX_RES).ToStatus();
					}
					else
					{
						//try to move towards the target when the goal changes
						status = navquery.FinalizeSlicedFindPath(reqPath, ref reqPathCount, MAX_RES).ToStatus();
					}

					if (status != Status.Failure && reqPathCount > 0)
					{
						//in progress or succeed
						if (reqPath[reqPathCount - 1] != agents[i].TargetRef)
						{
							//partial path, constrain target position in last polygon
							bool tempBool;
							status = navquery.ClosestPointOnPoly(reqPath[reqPathCount - 1], agents[i].TargetPos, out reqPos, out tempBool).ToStatus();
							if (status == Status.Failure)
								reqPathCount = 0;
						}
						else
						{
							reqPos = agents[i].TargetPos;
						}
					}
					else
					{
						reqPathCount = 0;
					}

					if (reqPathCount == 0)
					{
						//could not find path, start the request from the current location
						reqPos = agents[i].CurrentPos;
						reqPath[0] = path[0];
						reqPathCount = 1;
					}

					agents[i].Corridor.SetCorridor(reqPos, reqPath, reqPathCount);
					agents[i].Boundary.Reset();
					agents[i].Partial = false;

					if (reqPath[reqPathCount - 1] == agents[i].TargetRef)
					{
						agents[i].TargetState = TargetState.Valid;
						agents[i].TargetReplanTime = 0.0f;
					}
					else
					{
						//the path is longer or potentially unreachable, full plan
						agents[i].TargetState = TargetState.WaitingForQueue;
					}
				}

				if (agents[i].TargetState == TargetState.WaitingForQueue)
				{
					numQueue = AddToPathQueue(agents[i], queue, numQueue, PATH_MAX_AGENTS);
				}
			}

			for (int i = 0; i < numQueue; i++)
			{
				queue[i].TargetPathqRef = pathq.Request(queue[i].Corridor.GetLastPoly(), queue[i].TargetRef, queue[i].Corridor.Target, queue[i].TargetPos);
				if (queue[i].TargetPathqRef != PathQueue.PATHQ_INVALID)
					queue[i].TargetState = TargetState.WaitingForPath;
			}

			//update requests
			pathq.Update(MaxItersPerUpdate);

			//process path results
			for (int i = 0; i < maxAgents; i++)
			{
				if (!agents[i].Active)
					continue;
				if (agents[i].TargetState == TargetState.None || agents[i].TargetState == TargetState.Velocity)
					continue;

				if (agents[i].TargetState == TargetState.WaitingForPath)
				{
					//poll path queue
					status = pathq.GetRequestStatus(agents[i].TargetPathqRef);
					if (status == Status.Failure)
					{
						//path find failed, retry if the target location is still valid
						agents[i].TargetPathqRef = PathQueue.PATHQ_INVALID;
						if (agents[i].TargetRef != 0)
							agents[i].TargetState = TargetState.Requesting;
						else
							agents[i].TargetState = TargetState.Failed;
						agents[i].TargetReplanTime = 0.0f;
					}
					else if (status == Status.Success)
					{
						int[] path = agents[i].Corridor.Path;
						int npath = agents[i].Corridor.PathCount;

						//apply results
						Vector3 targetPos = new Vector3();
						targetPos = agents[i].TargetPos;

						int[] res = new int[this.maxPathResult];
						for (int j = 0; j < this.maxPathResult; j++)
							res[i] = pathResult[j];
						bool valid = true;
						int nres = 0;
						status = pathq.GetPathResult(agents[i].TargetPathqRef, res, ref nres, maxPathResult).ToStatus();
						if (status == Status.Failure || nres == 0)
							valid = false;

						//Merge result and existing path
						if (valid && path[npath - 1] != res[0])
							valid = false;

						if (valid)
						{
							//put the old path infront of the old path
							if (npath > 1)
							{
								//make space for the old path
								if ((npath - 1) + nres > maxPathResult)
									nres = maxPathResult - (npath - 1);

								for (int j = 0; j < nres; j++)
									res[npath - 1 + j] = res[j];

								//copy old path in the beginning
								for (int j = 0; j < npath - 1; j++)
									res[j] = path[j];
								nres += npath - 1;

								//remove trackbacks
								for (int j = 0; j < nres; j++)
								{
									if (j - 1 >= 0 && j + 1 < nres)
									{
										if (res[j - 1] == res[j + 1])
										{
											for (int k = 0; k < nres - (j + 1); k++)
												res[j - 1 + k] = res[j + 1 + k];
											nres -= 2;
											j -= 2;
										}
									}
								}
							}

							//check for partial path
							if (res[nres - 1] != agents[i].TargetRef)
							{
								//partial path, constrain target position inside the last polygon
								Vector3 nearest;
								bool tempBool = false;
								status = navquery.ClosestPointOnPoly(res[nres - 1], targetPos, out nearest, out tempBool).ToStatus();
								if (status == Status.Success)
									targetPos = nearest;
								else
									valid = false;
							}
						}

						if (valid)
						{
							//set current corridor
							agents[i].Corridor.SetCorridor(targetPos, res, nres);
							
							//forced to update boundary
							agents[i].Boundary.Reset();
							agents[i].TargetState = TargetState.Valid;
						}
						else
						{
							//something went wrong
							agents[i].TargetState = TargetState.Failed;
						}

						agents[i].TargetReplanTime = 0.0f;
					}
				}
			}
		}

		/// <summary>
		/// Reoptimize the path corridor for all agents
		/// </summary>
		/// <param name="agents">The agents array</param>
		/// <param name="numAgents">The number of agents</param>
		/// <param name="dt">Time until next update</param>
		public void UpdateTopologyOptimization(CrowdAgent[] agents, int numAgents, float dt)
		{
			if (numAgents == 0)
				return;

			const float OPT_TIME_THR = 0.5f; //seconds
			const int OPT_MAX_AGENTS = 1;
			CrowdAgent[] queue = new CrowdAgent[OPT_MAX_AGENTS];
			int nqueue = 0;

			for (int i = 0; i < numAgents; i++)
			{
				if (agents[i].State != CrowdAgentState.Walking)
					continue;
				if (agents[i].TargetState == TargetState.None ||
					agents[i].TargetState == TargetState.Velocity)
					continue;
				if ((agents[i].Parameters.UpdateFlags & UpdateFlags.OptimizeTopo) == 0)
					continue;
				agents[i].TopologyOptTime += dt;
				if (agents[i].TopologyOptTime >= OPT_TIME_THR)
					nqueue = AddToOptQueue(agents[i], queue, nqueue, OPT_MAX_AGENTS);
			}

			for (int i = 0; i < nqueue; i++)
			{
				queue[i].Corridor.OptimizePathTopology(navquery);
				queue[i].TopologyOptTime = 0.0f;
			}
		}

		/// <summary>
		/// Make sure that each agent is taking a valid path
		/// </summary>
		/// <param name="agents">The agent array</param>
		/// <param name="nagents">The number of agents</param>
		/// <param name="dt">Time until next update</param>
		public void CheckPathValidity(CrowdAgent[] agents, int nagents, float dt)
		{
			const int CHECK_LOOKAHEAD = 10;
			const float TARGET_REPLAN_DELAY = 1.0f; //seconds

			//Iterate through all the agents
			for (int i = 0; i < nagents; i++)
			{
				if (agents[i].State != CrowdAgentState.Walking)
					continue;

				agents[i].TargetReplanTime += dt;

				bool replan = false;

				//first check that the current location is valid
				int idx = GetAgentIndex(agents[i]);
				Vector3 agentPos = new Vector3();
				int agentRef = agents[i].Corridor.GetFirstPoly();
				agentPos = agents[i].CurrentPos;
				if (!navquery.IsValidPolyRef(agentRef))
				{
					//current location is not valid, try to reposition
					Vector3 nearest = agentPos;
					agentRef = 0;
					navquery.FindNearestPoly(ref agents[i].CurrentPos, ref ext, out agentRef, out nearest);
					agentPos = nearest;

					if (agentRef == 0)
					{
						//could not find location in navmesh, set state to invalid
						agents[i].Corridor.Reset(0, agentPos);
						agents[i].Partial = false;
						agents[i].Boundary.Reset();
						agents[i].State = CrowdAgentState.Invalid;
						continue;
					}

					//make sure the first polygon is valid
					agents[i].Corridor.FixPathStart(agentRef, agentPos);
					agents[i].Boundary.Reset();
					agents[i].CurrentPos = agentPos;

					replan = true;
				}

				if (agents[i].TargetState == TargetState.None
					|| agents[i].TargetState == TargetState.Velocity)
					continue;

				//try to recover move request position
				if (agents[i].TargetState != TargetState.None &&
					agents[i].TargetState != TargetState.Failed)
				{
					if (!navquery.IsValidPolyRef(agents[i].TargetRef))
					{
						//current target is not valid, try to reposition
						Vector3 nearest = agents[i].TargetPos;
						agents[i].TargetRef = 0;
						navquery.FindNearestPoly(ref agents[i].TargetPos, ref ext, out agents[i].TargetRef, out nearest);
						agents[i].TargetPos = nearest;
						replan = true;
					}

					if (agents[i].TargetRef == 0)
					{
						//failed to reposition target
						agents[i].Corridor.Reset(agentRef, agentPos);
						agents[i].Partial = false;
						agents[i].TargetState = TargetState.None;
					}
				}

				//if nearby corridor is not valid, replan
				if (!agents[i].Corridor.IsValid(CHECK_LOOKAHEAD, navquery))
				{
					replan = true;
				}

				//if the end of the path is near and it is not the request location, replan
				if (agents[i].TargetState == TargetState.Valid)
				{
					if (agents[i].TargetReplanTime > TARGET_REPLAN_DELAY &&
						agents[i].Corridor.PathCount < CHECK_LOOKAHEAD &&
						agents[i].Corridor.GetLastPoly() != agents[i].TargetRef)
						replan = true;
				}

				//try to replan path to goal
				if (replan)
				{
					if (agents[i].TargetState != TargetState.None)
					{
						RequestMoveTargetReplan(idx, agents[i].TargetRef, agents[i].TargetPos);
					}
				}
			}
		}

		public bool OverOffmeshConnection(CrowdAgent ag, float radius)
		{
			if (ag.NumCorners == 0)
				return false;

			bool offmeshConnection = ((ag.CornerFlags[ag.NumCorners - 1] & PathfinderCommon.STRAIGHTPATH_OFFMESH_CONNECTION) != 0)
				? true : false;
			if (offmeshConnection)
			{
				float dist = Vector3Extensions.Distance2D(ag.CurrentPos, ag.CornerVerts[ag.NumCorners - 1]);
				if (dist * dist < radius * radius)
					return true;
			}

			return false;
		}

		/// <summary>
		/// Calculate a vector based off of the map
		/// </summary>
		/// <param name="ag">The agent</param>
		/// <param name="dir">The resulting steer direction</param>
		public void CalcSmoothSteerDirection(CrowdAgent ag, ref Vector3 dir)
		{
			if (ag.NumCorners == 0)
			{
				dir = new Vector3(0, 0, 0);
				return;
			}

			int ip0 = 0;
			int ip1 = Math.Min(1, ag.NumCorners - 1);
			Vector3 p0 = ag.CornerVerts[ip0];
			Vector3 p1 = ag.CornerVerts[ip1];

			Vector3 dir0 = p0 - ag.CurrentPos;
			Vector3 dir1 = p1 - ag.CurrentPos;
			dir0.Y = 0;
			dir1.Y = 0;

			float len0 = dir0.Length();
			float len1 = dir1.Length();
			if (len1 > 0.001f)
				dir1 = dir1 * 1.0f / len1;

			dir.X = dir0.X - dir1.X * len0 * 0.5f;
			dir.Y = 0;
			dir.Z = dir0.Z - dir1.Z * len0 * 0.5f;

			dir.Normalize();
		}

		/// <summary>
		/// Calculate a straight vector to the destination
		/// </summary>
		/// <param name="ag">The agent</param>
		/// <param name="dir">The resulting steer direction</param>
		public void CalcStraightSteerDirection(CrowdAgent ag, ref Vector3 dir)
		{
			if (ag.NumCorners == 0)
			{
				dir = new Vector3(0, 0, 0);
				return;
			}

			dir = ag.CornerVerts[0] - ag.CurrentPos;
			dir.Y = 0;
			dir.Normalize();
		}

		/// <summary>
		/// Find the crowd agent's distance to its goal
		/// </summary>
		/// <param name="ag">Thw crowd agent</param>
		/// <param name="range">The maximum range</param>
		/// <returns>Distance to goal</returns>
		public float GetDistanceToGoal(CrowdAgent ag, float range)
		{
			if (ag.NumCorners == 0)
				return range;

			bool endOfPath = ((ag.CornerFlags[ag.NumCorners - 1] & PathfinderCommon.STRAIGHTPATH_END) != 0) ? true : false;
			if (endOfPath)
				return Math.Min(Vector3Extensions.Distance2D(ag.CurrentPos, ag.CornerVerts[ag.NumCorners - 1]), range);

			return range;
		}

		/// <summary>
		/// Get the crowd agent's neighbors.
		/// </summary>
		/// <param name="pos">Current position</param>
		/// <param name="height">The height</param>
		/// <param name="range">The range to search within</param>
		/// <param name="skip">The current crowd agent</param>
		/// <param name="result">The neihbors array</param>
		/// <param name="maxResult">The maximum number of neighbors that can be stored</param>
		/// <param name="agents">Array of all crowd agents</param>
		/// <param name="grid">The ProximityGrid</param>
		/// <returns>The number of neighbors</returns>
		public int GetNeighbours(Vector3 pos, float height, float range, CrowdAgent skip, CrowdNeighbor[] result, int maxResult, CrowdAgent[] agents, ProximityGrid grid)
		{
			int n = 0;

			const int MAX_NEIS = 32;
			int[] ids = new int[MAX_NEIS];
			int nids = grid.QueryItems(pos.X - range, pos.Z - range, pos.X + range, pos.Z + range, ids, MAX_NEIS);

			for (int i = 0; i < nids; i++)
			{
				CrowdAgent ag = agents[ids[i]];

				if (ag == skip)
					continue;

				//check for overlap
				Vector3 diff = pos - ag.CurrentPos;
				if (Math.Abs(diff.Y) >= (height + ag.Parameters.Height) / 2.0f)
					continue;
				diff.Y = 0;
				float distSqr = diff.LengthSquared();
				if (distSqr > range * range)
					continue;

				n = AddNeighbour(ids[i], distSqr, result, n, maxResult);
			}

			return n;
		}

		/// <summary>
		/// Add a CrowdNeighbor to the array
		/// </summary>
		/// <param name="idx">The neighbor's id</param>
		/// <param name="dist">Distance from current agent</param>
		/// <param name="neis">The neighbors array</param>
		/// <param name="nneis">The number of neighbors</param>
		/// <param name="maxNeis">The maximum number of neighbors allowed</param>
		/// <returns>An updated neighbor count</returns>
		public int AddNeighbour(int idx, float dist, CrowdNeighbor[] neis, int nneis, int maxNeis)
		{
			//insert neighbour based on distance
			int neiPos = 0;
			if (nneis == 0)
			{
				neiPos = nneis;
			}
			else if (dist >= neis[nneis - 1].Dist)
			{
				if (nneis >= maxNeis)
					return nneis;
				neiPos = nneis;
			}
			else
			{
				int i;
				for (i = 0; i < nneis; i++)
					if (dist <= neis[i].Dist)
						break;

				int tgt = i + 1;
				int n = Math.Min(nneis - i, maxNeis - tgt);

				if (n > 0)
				{
					for (int j = 0; j < n; j++)
						neis[tgt + j] = neis[i + j];
				}

				neiPos = i;
			}

			neis[neiPos] = new CrowdNeighbor();
			neis[neiPos].Idx = idx;
			neis[neiPos].Dist = dist;

			return Math.Min(nneis + 1, maxNeis);
		}

		/// <summary>
		/// Add the CrowdAgent to the path queue
		/// </summary>
		/// <param name="newag">The new CrowdAgent</param>
		/// <param name="agents">The current CrowdAgent array</param>
		/// <param name="numAgents">The number of CrowdAgents</param>
		/// <param name="maxAgents">The maximum number of agents allowed</param>
		/// <returns>An updated agent count</returns>
		public int AddToPathQueue(CrowdAgent newag, CrowdAgent[] agents, int numAgents, int maxAgents)
		{
			//insert neighbour based on greatest time
			int slot = 0;
			if (numAgents == 0)
			{
				slot = numAgents;
			}
			else if (newag.TargetReplanTime <= agents[numAgents - 1].TargetReplanTime)
			{
				if (numAgents >= maxAgents)
					return numAgents;
				slot = numAgents;
			}
			else
			{
				int i;
				for (i = 0; i < numAgents; i++)
					if (newag.TargetReplanTime >= agents[i].TargetReplanTime)
						break;

				int tgt = i + 1;
				int n = Math.Min(numAgents - i, maxAgents - tgt);

				if (n > 0)
				{
					for (int j = 0; j < n; j++)
						agents[tgt + j] = agents[i + j];
				}

				slot = i;
			}

			agents[slot] = newag;

			return Math.Min(numAgents + 1, maxAgents);
		}

		/// <summary>
		/// Add the CrowdAgent to the optimization queue
		/// </summary>
		/// <param name="newag">The new CrowdAgent</param>
		/// <param name="agents">The current CrowdAgent array</param>
		/// <param name="numAgents">The number of CrowdAgents</param>
		/// <param name="maxAgents">The maximum number of agents allowed</param>
		/// <returns>An updated agent count</returns>
		public int AddToOptQueue(CrowdAgent newag, CrowdAgent[] agents, int numAgents, int maxAgents)
		{
			//insert neighbor based on greatest time
			int slot = 0;
			if (numAgents == 0)
			{
				slot = numAgents;
			}
			else if (newag.TopologyOptTime <= agents[numAgents - 1].TopologyOptTime)
			{
				if (numAgents >= maxAgents)
					return numAgents;
				slot = numAgents;
			}
			else
			{
				int i;
				for (i = 0; i < numAgents; i++)
					if (newag.TopologyOptTime >= agents[i].TopologyOptTime)
						break;

				int tgt = i + 1;
				int n = Math.Min(numAgents - i, maxAgents - tgt);

				if (n > 0)
				{
					for (int j = 0; j < n; j++)
						agents[tgt + j] = agents[i + j];
				}

				slot = i;
			}

			agents[slot] = newag;

			return Math.Min(numAgents + 1, maxAgents);
		}

		/// <summary>
		/// A crowd agent is a unit that moves across the navigation mesh
		/// </summary>
		public class CrowdAgent
		{
			public bool Active;
			public CrowdAgentState State;
			public bool Partial;
			public PathCorridor Corridor;
			public LocalBoundary Boundary;
			public float TopologyOptTime;
			public CrowdNeighbor[] Neis;	//size = CROWDAGENT_MAX_NEIGHBOURS
			public int NumNeis;
			public float DesiredSpeed;

			public Vector3 CurrentPos;
			public Vector3 Disp;
			public Vector3 DesiredVel;
			public Vector3 NVel;
			public Vector3 Vel;

			public CrowdAgentParams Parameters;

			public Vector3[] CornerVerts;	//size = CROWDAGENT_MAX_CORNERS
			public int[] CornerFlags;		//size = CROWDAGENT_MAX_CORNERS
			public int[] CornerPolys;		//size = CROWDAGENT_MAX_CORNERS

			public int NumCorners;

			public TargetState TargetState;
			public int TargetRef;
			public Vector3 TargetPos;
			public int TargetPathqRef;
			public bool TargetReplan;
			public float TargetReplanTime;

			/// <summary>
			/// Update the position after a certain time 'dt'
			/// </summary>
			/// <param name="dt">Time that passed</param>
			public void Integrate(float dt)
			{
				//fake dyanmic constraint
				float maxDelta = Parameters.MaxAcceleration * dt;
				Vector3 dv = NVel - Vel;
				float ds = dv.Length();
				if (ds > maxDelta)
					dv = dv * (maxDelta / ds);
				Vel = Vel + dv;

				//integrate
				if (Vel.Length() > 0.0001f)
					CurrentPos = CurrentPos + Vel * dt;
				else
					Vel = new Vector3(0, 0, 0);
			}
		}

		/// <summary>
		/// A neighboring crowd agent
		/// </summary>
		public struct CrowdNeighbor
		{
			public int Idx;
			public float Dist;
		}

		/// <summary>
		/// Settings for a particular crowd agent
		/// </summary>
		public struct CrowdAgentParams
		{
			public float Radius;
			public float Height;
			public float MaxAcceleration;
			public float MaxSpeed;

			public float CollisionQueryRange;

			public float PathOptimizationRange;

			public float SeparationWeight;

			public UpdateFlags UpdateFlags;
			
			public byte ObstacleAvoidanceType;

			public byte QueryFilterType;


		}

		private struct CrowdAgentAnimation
		{
			public bool Active;
			public Vector3 InitPos, StartPos, EndPos;
			public int PolyRef;
			public float T, TMax;
		}
	}
}
