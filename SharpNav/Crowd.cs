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

namespace SharpNav
{
	public class Crowd
	{
		/// <summary>
		/// The maximum number of neighbors that a crowd agent can take into account for steering decisions
		/// </summary>
		private const int CROWDAGENT_MAX_NEIGHBOURS = 6;

		/// <summary>
		/// The maximum number of corners a crowd agent will look ahead in the path
		/// </summary>
		private const int CROWDAGENT_MAX_CORNERS = 4;

		private const int MAX_ITERS_PER_UPDATE = 100;

		public enum CrowdAgentState 
		{
			Invalid,
			Walking,
			Offmesh
		}

		public enum MoveRequestState
		{
			CROWDAGENT_TARGET_NONE = 0,
			CROWDAGENT_TARGET_FAILED,
			CROWDAGENT_TARGET_VALID,
			CROWDAGENT_TARGET_REQUESTING,
			CROWDAGENT_TARGET_WAITING_FOR_QUEUE,
			CROWDAGENT_TARGET_WAITING_FOR_PATH,
			CROWDAGENT_TARGET_VELOCITY
		}

		public enum UpdateFlags
		{
			CROWD_ANTICIPATE_TURNS = 1,
			CROWD_OBSTACLE_AVOIDANCE = 2,
			CROWD_SEPARATION = 4,
			CROWD_OPTIMIZE_VIS = 8,
			CROWD_OPTIMIZE_TOPO = 16
		}

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

		//private QueryFilter m_filters[MAX_QUERY_FILTER_TYPE];

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
			grid = new ProximityGrid(maxAgents * 4, maxAgentRadius * 3);

			//allocate obstacle avoidance query
			this.obstacleQuery = new ObstacleAvoidanceQuery(6, 8);

			//initialize obstancle query params
			this.obstacleQueryParams = new ObstacleAvoidanceQuery.ObstacleAvoidanceParams[8];
			for (int i = 0; i < this.obstacleQueryParams.Length; i++)
			{
				ObstacleAvoidanceQuery.ObstacleAvoidanceParams parameters = this.obstacleQueryParams[i];
				parameters.VelBias = 0.4f;
				parameters.WeightDesVel = 2.0f;
				parameters.WeightCurVel = 0.75f;
				parameters.WeightSide = 0.75f;
				parameters.WeightToi = 2.5f;
				parameters.HorizTime = 2.5f;
				parameters.GridSize = 33;
				parameters.AdaptiveDivs = 7;
				parameters.AdaptiveRings = 2;
				parameters.AdaptiveDepth = 5;
				this.obstacleQueryParams[i] = parameters;
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
				this.agents[i].Active = false;
				this.agents[i].Corridor = new PathCorridor(maxPathResult);
			}

			for (int i = 0; i < maxAgents; i++)
			{
				this.agentAnims[i].Active = false;
			}

			//allocate nav mesh query
			this.navquery = new NavMeshQuery(nav, 512);
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

			CrowdAgent ag = agents[idx];

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

			ag.Corridor.Reset(reference, nearest);
			ag.Boundary.Reset();
			ag.Partial = false;

			ag.TopologyOptTime = 0;
			ag.TargetReplanTime = 0;
			ag.NNeis = 0;

			ag.DVel = new Vector3(0.0f, 0.0f, 0.0f);
			ag.NVel = new Vector3(0.0f, 0.0f, 0.0f);
			ag.Vel = new Vector3(0.0f, 0.0f, 0.0f);
			ag.NPos = nearest;

			ag.DesiredSpeed = 0;

			if (reference != 0)
				ag.State = CrowdAgentState.Walking;
			else
				ag.State = CrowdAgentState.Invalid;

			ag.TargetState = (byte)MoveRequestState.CROWDAGENT_TARGET_NONE;

			ag.Active = true;

			agents[idx] = ag;

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
		/// Change the move target ds
		/// </summary>
		/// <param name="idx"></param>
		/// <param name="reference"></param>
		/// <param name="pos"></param>
		/// <returns></returns>
		public bool RequestTargetMoveReplan(int idx, int reference, Vector3 pos)
		{
			if (idx < 0 || idx >= maxAgents)
				return false;

			CrowdAgent ag = agents[idx];

			//initialize request
			ag.TargetRef = reference;
			ag.TargetPos = pos;
			ag.TargetPathqRef = PathQueue.PATHQ_INVALID;
			ag.TargetReplan = true;
			if (ag.TargetRef != 0)
				ag.TargetState = MoveRequestState.CROWDAGENT_TARGET_REQUESTING;
			else
				ag.TargetState = MoveRequestState.CROWDAGENT_TARGET_FAILED;

			agents[idx] = ag;

			return true;
		}

		/// <summary>
		/// Request a new move target
		/// </summary>
		/// <param name="idx">The agent id</param>
		/// <param name="reference">The polygon reference</param>
		/// <param name="pos">The target's coordinates</param>
		/// <returns>True if request met, false if not</returns>
		public bool RequestTargetMove(int idx, int reference, Vector3 pos)
		{
			if (idx < 0 || idx >= maxAgents)
				return false;
			if (reference == 0)
				return false;

			CrowdAgent ag = agents[idx];

			//initialize request
			ag.TargetRef = reference;
			ag.TargetPos = pos;
			ag.TargetPathqRef = PathQueue.PATHQ_INVALID;
			ag.TargetReplan = false;
			if (ag.TargetRef != 0)
				ag.TargetState = MoveRequestState.CROWDAGENT_TARGET_REQUESTING;
			else
				ag.TargetState = MoveRequestState.CROWDAGENT_TARGET_FAILED;

			agents[idx] = ag;

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

			CrowdAgent ag = agents[idx];

			//initialize request
			ag.TargetRef = 0;
			ag.TargetPos = vel;
			ag.TargetPathqRef = PathQueue.PATHQ_INVALID;
			ag.TargetReplan = false;
			ag.TargetState = MoveRequestState.CROWDAGENT_TARGET_VELOCITY;

			agents[idx] = ag;

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

			CrowdAgent ag = agents[idx];

			//initialize request
			ag.TargetRef = 0;
			ag.TargetPos = new Vector3(0.0f, 0.0f, 0.0f);
			ag.TargetPathqRef = PathQueue.PATHQ_INVALID;
			ag.TargetReplan = false;
			ag.TargetState = (byte)MoveRequestState.CROWDAGENT_TARGET_NONE;

			agents[idx] = ag;

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

			int nagents = GetActiveAgents(agents, maxAgents);

			//check that all agents have valid paths
			CheckPathValidity(agents, nagents, dt);
			
			//update async move requests and path finder
			UpdateMoveRequest();

			//optimize path topology
			UpdateTopologyOptimization(agents, nagents, dt);

			//register agents to proximity grid
			grid.Clear();
			for (int i = 0; i < nagents; i++)
			{
				Vector3 p = agents[i].NPos;
				float r = agents[i].Parameters.Radius;
				grid.AddItem(i, p.X - r, p.Z - r, p.X + r, p.Z + r);
			}

			//get nearby navmesh segments and agents to collide with
			for (int i = 0; i < nagents; i++)
			{
				if (agents[i].State != CrowdAgentState.Walking)
					continue;

				//update the collision boundary after certain distance has passed or if it has become invalid
				float updateThr = agents[i].Parameters.CollisionQueryRange * 0.25f;
				if (Vector3Extensions.Distance2D(agents[i].NPos, agents[i].Boundary.GetCenter()) > updateThr * updateThr ||
					!agents[i].Boundary.IsValid(navquery))
				{
					agents[i].Boundary.Update(agents[i].Corridor.GetFirstPoly(), agents[i].NPos, 
						agents[i].Parameters.CollisionQueryRange, navquery);
				}

				//query neighbour agents
				agents[i].NNeis = GetNeighbours(agents[i].NPos, agents[i].Parameters.Height, agents[i].Parameters.CollisionQueryRange,
					agents[i], agents[i].neis, CROWDAGENT_MAX_NEIGHBOURS, agents, grid);

				for (int j = 0; j < agents[i].NNeis; j++)
					agents[i].neis[j].Idx = GetAgentIndex(agents[agents[i].neis[j].Idx]);
			}

			//find the next corner to steer to
			for (int i = 0; i < nagents; i++)
			{
				if (agents[i].State != CrowdAgentState.Walking)
					continue;
				if (agents[i].TargetState == MoveRequestState.CROWDAGENT_TARGET_NONE ||
					agents[i].TargetState == MoveRequestState.CROWDAGENT_TARGET_VELOCITY)
					continue;

				//find corners for steering
				agents[i].NCorners = agents[i].Corridor.FindCorners(agents[i].CornerVerts, agents[i].CornerFlags, agents[i].CornerPolys,
					CROWDAGENT_MAX_CORNERS, navquery);

				//check to see if the corner after the next corner is directly visible 
				if (((agents[i].Parameters.UpdateFlags & UpdateFlags.CROWD_OPTIMIZE_VIS) != 0) && agents[i].NCorners > 0)
				{
					Vector3 target = agents[i].CornerVerts[Math.Min(1, agents[i].NCorners - 1)];
					agents[i].Corridor.OptimizePathVisibility(target, agents[i].Parameters.PathOptimizationRange, navquery);
				}
			}

			//trigger off-mesh connections (depends on corners)
			for (int i = 0; i < nagents; i++)
			{
				if (agents[i].State != CrowdAgentState.Walking)
					continue;
				if (agents[i].TargetState == MoveRequestState.CROWDAGENT_TARGET_NONE ||
					agents[i].TargetState == MoveRequestState.CROWDAGENT_TARGET_VELOCITY)
					continue;

				//check
				float triggerRadius = agents[i].Parameters.Radius * 2.25f;
				if (OverOffmeshConnection(agents[i], triggerRadius))
				{
					//prepare to off-mesh connection
					int idx = i;
					
					//adjust the path over the off-mesh connection
					int[] refs = new int[2];
					if (agents[i].Corridor.MoveOverOffmeshConnection(agents[i].CornerPolys[agents[i].NCorners - 1], refs,
						ref agentAnims[idx].StartPos, ref agentAnims[idx].EndPos, navquery))
					{
						agentAnims[idx].InitPos = agents[i].NPos;
						agentAnims[idx].PolyRef = refs[1];
						agentAnims[idx].Active = true;
						agentAnims[idx].T = 0.0f;
						agentAnims[idx].TMax = (Vector3Extensions.Distance2D(agentAnims[idx].StartPos, agentAnims[idx].EndPos)
							/ agents[i].Parameters.MaxSpeed) * 0.5f;

						agents[i].State = CrowdAgentState.Offmesh;
						agents[i].NCorners = 0;
						agents[i].NNeis = 0;
						continue;
					}
				}
			}

			//TODO: fill in the rest of the details
		}

		/// <summary>
		/// Change the move requests for all the agents
		/// </summary>
		public void UpdateMoveRequest()
		{
			const int PATH_MAX_AGENTS = 8;
			CrowdAgent[] queue = new CrowdAgent[PATH_MAX_AGENTS];
			int nqueue = 0;
			bool boolStatus;

			//fire off new requests
			for (int i = 0; i < maxAgents; i++)
			{
				if (!agents[i].Active)
					continue;
				if (agents[i].State == (byte)CrowdAgentState.Invalid)
					continue;
				if (agents[i].TargetState == MoveRequestState.CROWDAGENT_TARGET_NONE ||
					agents[i].TargetState == MoveRequestState.CROWDAGENT_TARGET_VELOCITY)
					continue;

				if (agents[i].TargetState == MoveRequestState.CROWDAGENT_TARGET_REQUESTING)
				{
					int[] path = agents[i].Corridor.GetPath();
					int npath = agents[i].Corridor.GetPathCount();

					const int MAX_RES = 32;
					Vector3 reqPos = new Vector3();
					int[] reqPath = new int[MAX_RES];
					int reqPathCount = 0;

					//quick search towards the goal
					const int MAX_ITER = 20;
					navquery.InitSlicedFindPath((int)path[0], (int)agents[i].TargetRef, agents[i].NPos, agents[i].TargetPos);
					int tempInt = 0;
					navquery.UpdateSlicedFindPath(MAX_ITER, ref tempInt);
					boolStatus = false;
					if (agents[i].TargetReplan)
					{
						//try to use an existing steady path during replan if possible
						boolStatus = navquery.FinalizedSlicedPathPartial(path, npath, reqPath, ref reqPathCount, MAX_RES);
					}
					else
					{
						//try to move towards the target when the goal changes
						boolStatus = navquery.FinalizeSlicedFindPath(reqPath, ref reqPathCount, MAX_RES);
					}

					if (boolStatus != false && reqPathCount > 0)
					{
						//in progress or succeed
						if (reqPath[reqPathCount - 1] != agents[i].TargetRef)
						{
							//partial path, constrain target position in last polygon
							bool tempBool;
							boolStatus = navquery.ClosestPointOnPoly(reqPath[reqPathCount - 1], agents[i].TargetPos, out reqPos, out tempBool);
							if (boolStatus == false)
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
						reqPos = agents[i].NPos;
						reqPath[0] = path[0];
						reqPathCount = 1;
					}

					agents[i].Corridor.SetCorridor(reqPos, reqPath, reqPathCount);
					agents[i].Boundary.Reset();
					agents[i].Partial = false;

					if (reqPath[reqPathCount - 1] == agents[i].TargetRef)
					{
						agents[i].TargetState = MoveRequestState.CROWDAGENT_TARGET_VALID;
						agents[i].TargetReplanTime = 0.0f;
					}
					else
					{
						//the path is longer or potentially unreachable, full plan
						agents[i].TargetState = MoveRequestState.CROWDAGENT_TARGET_WAITING_FOR_QUEUE;
					}
				}

				if (agents[i].TargetState == MoveRequestState.CROWDAGENT_TARGET_WAITING_FOR_QUEUE)
				{
					nqueue = AddToPathQueue(agents[i], queue, nqueue, PATH_MAX_AGENTS);
				}
			}

			for (int i = 0; i < nqueue; i++)
			{
				queue[i].TargetPathqRef = pathq.Request(queue[i].Corridor.GetLastPoly(), queue[i].TargetRef, queue[i].Corridor.GetTarget(), queue[i].TargetPos);
				if (queue[i].TargetPathqRef != PathQueue.PATHQ_INVALID)
					queue[i].TargetState = MoveRequestState.CROWDAGENT_TARGET_WAITING_FOR_PATH;
			}

			//update requests
			pathq.Update(MAX_ITERS_PER_UPDATE);

			PathQueue.Status status;

			//process path results
			for (int i = 0; i < maxAgents; i++)
			{
				if (!agents[i].Active)
					continue;
				if (agents[i].TargetState == MoveRequestState.CROWDAGENT_TARGET_NONE ||
					agents[i].TargetState == MoveRequestState.CROWDAGENT_TARGET_VELOCITY)
					continue;

				if (agents[i].TargetState == MoveRequestState.CROWDAGENT_TARGET_WAITING_FOR_PATH)
				{
					//poll path queue
					status = pathq.GetRequestStatus(agents[i].TargetPathqRef);
					if (status == PathQueue.Status.FAILURE)
					{
						//path find failed, retry if the target location is still valid
						agents[i].TargetPathqRef = PathQueue.PATHQ_INVALID;
						if (agents[i].TargetRef != 0)
							agents[i].TargetState = MoveRequestState.CROWDAGENT_TARGET_REQUESTING;
						else
							agents[i].TargetState = MoveRequestState.CROWDAGENT_TARGET_FAILED;
						agents[i].TargetReplanTime = 0.0f;
					}
					else if (status == PathQueue.Status.SUCCESS)
					{
						int[] path = agents[i].Corridor.GetPath();
						int npath = agents[i].Corridor.GetPathCount();

						//apply results
						Vector3 targetPos = new Vector3();
						targetPos = agents[i].TargetPos;

						int[] res = new int[this.maxPathResult];
						for (int j = 0; j < this.maxPathResult; j++)
							res[i] = pathResult[j];
						bool valid = true;
						int nres = 0;
						boolStatus = pathq.GetPathResult(agents[i].TargetPathqRef, res, ref nres, maxPathResult);
						if (boolStatus == false || nres == 0)
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
								boolStatus = navquery.ClosestPointOnPoly(res[nres - 1], targetPos, out nearest, out tempBool);
								if (boolStatus)
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
							agents[i].TargetState = MoveRequestState.CROWDAGENT_TARGET_VALID;
						}
						else
						{
							//something went wrong
							agents[i].TargetState = MoveRequestState.CROWDAGENT_TARGET_FAILED;
						}

						agents[i].TargetReplanTime = 0.0f;
					}
				}
			}
		}

		public bool OverOffmeshConnection(CrowdAgent ag, float radius)
		{
			if (ag.NCorners == 0)
				return false;

			bool offmeshConnection = ((ag.CornerFlags[ag.NCorners - 1] & PathfinderCommon.STRAIGHTPATH_OFFMESH_CONNECTION) != 0) 
				? true : false;
			if (offmeshConnection)
			{
				float dist = Vector3Extensions.Distance2D(ag.NPos, ag.CornerVerts[ag.NCorners - 1]);
				if (dist * dist < radius * radius)
					return true;
			}

			return false;
		}

		public void UpdateTopologyOptimization(CrowdAgent[] agents, int nagents, float dt)
		{
			if (nagents == 0)
				return;

			const float OPT_TIME_THR = 0.5f; //seconds
			const int OPT_MAX_AGENTS = 1;
			CrowdAgent[] queue = new CrowdAgent[OPT_MAX_AGENTS];
			int nqueue = 0;

			for (int i = 0; i < nagents; i++)
			{
				if (agents[i].State != CrowdAgentState.Walking)
					continue;
				if (agents[i].TargetState == MoveRequestState.CROWDAGENT_TARGET_NONE ||
					agents[i].TargetState == MoveRequestState.CROWDAGENT_TARGET_VELOCITY)
					continue;
				if ((agents[i].Parameters.UpdateFlags & UpdateFlags.CROWD_OPTIMIZE_TOPO) == 0)
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

		public void CheckPathValidity(CrowdAgent[] agents, int nagents, float dt)
		{
			const int CHECK_LOOKAHEAD = 10;
			const float TARGET_REPLAN_DELAY = 1.0f; //seconds

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
				agentPos = agents[i].NPos;
				if (!navquery.IsValidPolyRef(agentRef))
				{
					//current location is not valid, try to reposition
					Vector3 nearest = agentPos;
					agentRef = 0;
					navquery.FindNearestPoly(ref agents[i].NPos, ref ext, out agentRef, out nearest);
					agentPos = nearest;

					if (agentRef == 0)
					{
						//could not find location in navmesh, set state to invalid
						agents[i].Corridor.Reset(0, agentPos);
						agents[i].Partial = false;
						agents[i].Boundary.Reset();
						agents[i].State = (byte)CrowdAgentState.Invalid;
						continue;
					}

					//make sure the first polygon is valid
					agents[i].Corridor.FixPathStart(agentRef, agentPos);
					agents[i].Boundary.Reset();
					agents[i].NPos = agentPos;

					replan = true;
				}

				if (agents[i].TargetState == MoveRequestState.CROWDAGENT_TARGET_NONE
					|| agents[i].TargetState == MoveRequestState.CROWDAGENT_TARGET_VELOCITY)
					continue;

				//try to recover move request position
				if (agents[i].TargetState != MoveRequestState.CROWDAGENT_TARGET_NONE &&
					agents[i].TargetState != MoveRequestState.CROWDAGENT_TARGET_FAILED)
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
						agents[i].TargetState = MoveRequestState.CROWDAGENT_TARGET_NONE;
					}
				}

				//if nearby corridor is not valid, replan
				if (!agents[i].Corridor.IsValid(CHECK_LOOKAHEAD, navquery))
				{
					replan = true;
				}

				//if the end of the path is near and it is not the request location, replan
				if (agents[i].TargetState == MoveRequestState.CROWDAGENT_TARGET_VALID)
				{
					if (agents[i].TargetReplanTime > TARGET_REPLAN_DELAY &&
						agents[i].Corridor.GetPathCount() < CHECK_LOOKAHEAD &&
						agents[i].Corridor.GetLastPoly() != agents[i].TargetRef)
						replan = true;
				}

				//try to replan path to goal
				if (replan)
				{
					if (agents[i].TargetState != MoveRequestState.CROWDAGENT_TARGET_NONE)
					{
						RequestTargetMoveReplan(idx, agents[i].TargetRef, agents[i].TargetPos);
					}
				}
			}
		}

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
				Vector3 diff = pos - ag.NPos;
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

		public int AddToPathQueue(CrowdAgent newag, CrowdAgent[] agents, int nagents, int maxAgents)
		{
			//insert neighbour based on greatest time
			int slot = 0;
			if (nagents == 0)
			{
				slot = nagents;
			}
			else if (newag.TargetReplanTime <= agents[nagents - 1].TargetReplanTime)
			{
				if (nagents >= maxAgents)
					return nagents;
				slot = nagents;
			}
			else
			{
				int i;
				for (i = 0; i < nagents; i++)
					if (newag.TargetReplanTime >= agents[i].TargetReplanTime)
						break;

				int tgt = i + 1;
				int n = Math.Min(nagents - i, maxAgents - tgt);

				if (n > 0)
				{
					for (int j = 0; j < n; j++)
						agents[tgt + j] = agents[i + j];
				}
				slot = i;
			}
			agents[slot] = newag;

			return Math.Min(nagents + 1, maxAgents);
		}

		public int AddToOptQueue(CrowdAgent newag, CrowdAgent[] agents, int nagents, int maxAgents)
		{
			//insert neighbor based on greatest time
			int slot = 0;
			if (nagents == 0)
			{
				slot = nagents;
			}
			else if (newag.TopologyOptTime <= agents[nagents - 1].TopologyOptTime)
			{
				if (nagents >= maxAgents)
					return nagents;
				slot = nagents;
			}
			else
			{
				int i;
				for (i = 0; i < nagents; i++)
					if (newag.TopologyOptTime >= agents[i].TopologyOptTime)
						break;

				int tgt = i + 1;
				int n = Math.Min(nagents - i, maxAgents - tgt);

				if (n > 0)
				{
					for (int j = 0; j < n; j++)
						agents[tgt + j] = agents[i + j];
				}
				slot = i;
			}

			agents[slot] = newag;

			return Math.Min(nagents + 1, maxAgents);
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
			public CrowdNeighbor[] neis;	//size = CROWDAGENT_MAX_NEIGHBOURS
			public int NNeis;
			public float DesiredSpeed;

			public Vector3 NPos;
			public Vector3 Disp;
			public Vector3 DVel;
			public Vector3 NVel;
			public Vector3 Vel;

			public CrowdAgentParams Parameters;

			public Vector3[] CornerVerts;	//size = CROWDAGENT_MAX_CORNERS
			public int[] CornerFlags;		//size = CROWDAGENT_MAX_CORNERS
			public int[] CornerPolys;		//size = CROWDAGENT_MAX_CORNERS

			public int NCorners;

			public MoveRequestState TargetState;
			public int TargetRef;
			public Vector3 TargetPos;
			public int TargetPathqRef;
			public bool TargetReplan;
			public float TargetReplanTime;
		}

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
