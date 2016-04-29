// Copyright (c) 2014-2015 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
// Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE

using System;
using System.Collections.Generic;

using SharpNav.Collections.Generic;
using SharpNav.Geometry;
using SharpNav.Pathfinding;

#if MONOGAME
using Vector3 = Microsoft.Xna.Framework.Vector3;
#elif OPENTK
using Vector3 = OpenTK.Vector3;
#elif SHARPDX
using Vector3 = SharpDX.Vector3;
#endif

namespace SharpNav.Crowds
{
	/// <summary>
	/// The Crowd class manages pathfinding for multiple agents simulatenously.
	/// </summary>
	public class Crowd
	{
		/// <summary>
		/// The maximum number of crowd avoidance configurations supported by the crowd manager
		/// </summary>
		private const int AgentMaxObstacleAvoidanceParams = 8;

		/// <summary>
		/// The maximum number of neighbors that a crowd agent can take into account for steering decisions
		/// </summary>
		private const int AgentMaxNeighbors = Agent.AgentMaxNeighbors;

		/// <summary>
		/// The maximum number of corners a crowd agent will look ahead in the path
		/// </summary>
		private const int AgentMaxCorners = 4;

		private const int MaxItersPerUpdate = 100;

		private int maxAgents;
		private Agent[] agents;
		private Agent[] activeAgents;
		private AgentAnimation[] agentAnims; 

		private PathQueue pathq;

		private ObstacleAvoidanceQuery.ObstacleAvoidanceParams[] obstacleQueryParams;
		private ObstacleAvoidanceQuery obstacleQuery;

		private ProximityGrid<Agent> grid;

		private Vector3 ext;

		private float maxAgentRadius;

		private int velocitySampleCount;

		private NavMeshQuery navQuery;
		private NavQueryFilter navQueryFilter;

		/// <summary>
		/// Initializes a new instance of the <see cref="Crowd" /> class.
		/// </summary>
		/// <param name="maxAgents">The maximum agents allowed</param>
		/// <param name="maxAgentRadius">The maximum radius for an agent</param>
		/// <param name="navMesh">The navigation mesh</param>
		public Crowd(int maxAgents, float maxAgentRadius, ref TiledNavMesh navMesh)
		{
			this.maxAgents = maxAgents;
			this.maxAgentRadius = maxAgentRadius;

			this.ext = new Vector3(maxAgentRadius * 2.0f, maxAgentRadius * 1.5f, maxAgentRadius * 2.0f);

			//initialize proximity grid
			this.grid = new ProximityGrid<Agent>(maxAgents * 4, maxAgentRadius * 3);

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

			this.pathq = new PathQueue(4096, ref navMesh);

			this.agents = new Agent[maxAgents];
			this.activeAgents = new Agent[maxAgents];
			this.agentAnims = new AgentAnimation[maxAgents];

			for (int i = 0; i < maxAgents; i++)
			{
				this.agents[i] = new Agent();
			}

			for (int i = 0; i < maxAgents; i++)
			{
				this.agentAnims[i].Active = false;
			}

			//allocate nav mesh query
			this.navQuery = new NavMeshQuery(navMesh, 512);
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

		public Agent GetAgent(int idx)
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
		public int AddAgent(Vector3 pos, AgentParams parameters)
		{
			//find empty slot
			int idx = -1;
			for (int i = 0; i < maxAgents; i++)
			{
				if (!agents[i].IsActive)
				{
					idx = i;
					break;
				}
			}

			if (idx == -1)
				return -1;

			agents[idx].UpdateAgentParameters(parameters);

			//find nearest position on the navmesh and place the agent there
			NavPoint nearest;
			navQuery.FindNearestPoly(ref pos, ref ext, out nearest);
			/*if (status == false)
			{
				nearest = pos;
				reference = 0;
			}*/

			agents[idx].Reset(nearest.Polygon, nearest.Position);
			agents[idx].IsActive = true;

			return idx;
		}

		/// <summary>
		/// The agent is deactivated and will no longer be processed. It can still be reused later.
		/// </summary>
		/// <param name="index">The agent's id</param>
		/// <returns>A value indicating whether the agent was successfully removed.</returns>
		public bool RemoveAgent(int index)
		{
			if (index < 0 || index >= maxAgents)
				return false;

			agents[index].IsActive = false;
			return true;
		}

		/// <summary>
		/// The crowd contains active and inactive agents. Only add all the active agents to a separate array.
		/// </summary>
		/// <param name="agents">The array of active agents</param>
		/// <returns>The number of active agents</returns>
		public int GetActiveAgents(Agent[] agents)
		{
			int n = 0;
			for (int i = 0; i < agents.Length; i++)
			{
				if (!agents[i].IsActive)
					continue;

				if (n < maxAgents)
					agents[n++] = agents[i];
			}

			return n;
		}

		/// <summary>
		/// Get the agent's index in the array
		/// </summary>
		/// <param name="agent">The agent</param>
		/// <returns>The index</returns>
		public int GetAgentIndex(Agent agent)
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

			int numAgents = GetActiveAgents(agents);

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
				Agent a = agents[i];

				Vector3 p = a.Position;
				float r = a.Parameters.Radius;
				grid.AddItem(a, p.X - r, p.Z - r, p.X + r, p.Z + r);
			}

			//get nearby navmesh segments and agents to collide with
			for (int i = 0; i < numAgents; i++)
			{
				if (agents[i].State != AgentState.Walking)
					continue;

				//update the collision boundary after certain distance has passed or if it has become invalid
				float updateThr = agents[i].Parameters.CollisionQueryRange * 0.25f;
				if (Vector3Extensions.Distance2D(agents[i].Position, agents[i].Boundary.Center) > updateThr * updateThr || !agents[i].Boundary.IsValid(navQuery))
				{
					agents[i].Boundary.Update(agents[i].Corridor.GetFirstPoly(), agents[i].Position, agents[i].Parameters.CollisionQueryRange, navQuery);
				}

				//query neighbor agents
				agents[i].NeighborCount = GetNeighbors(agents[i].Position, agents[i].Parameters.Height, agents[i].Parameters.CollisionQueryRange, agents[i], agents[i].Neighbors, AgentMaxNeighbors, agents, grid);

				for (int j = 0; j < agents[i].NeighborCount; j++)
					agents[i].Neighbors[j].Index = GetAgentIndex(agents[agents[i].Neighbors[j].Index]);
			}

			//find the next corner to steer to
			for (int i = 0; i < numAgents; i++)
			{
				if (agents[i].State != AgentState.Walking)
					continue;
				if (agents[i].TargetState == TargetState.None ||
					agents[i].TargetState == TargetState.Velocity)
					continue;

				//find corners for steering
				agents[i].Corridor.FindCorners(agents[i].Corners, navQuery);

				//check to see if the corner after the next corner is directly visible 
				if (((agents[i].Parameters.UpdateFlags & UpdateFlags.OptimizeVis) != 0) && agents[i].Corners.Count > 0)
				{
					Vector3 target = agents[i].Corners[Math.Min(1, agents[i].Corners.Count - 1)].Point.Position;
					agents[i].Corridor.OptimizePathVisibility(target, agents[i].Parameters.PathOptimizationRange, navQuery);
				}
			}

			//trigger off-mesh connections (depends on corners)
			for (int i = 0; i < numAgents; i++)
			{
				if (agents[i].State != AgentState.Walking)
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
					NavPolyId[] refs = new NavPolyId[2];
					if (agents[i].Corridor.MoveOverOffmeshConnection(agents[i].Corners[agents[i].Corners.Count - 1].Point.Polygon, refs, ref agentAnims[idx].StartPos, ref agentAnims[idx].EndPos, navQuery))
					{
						agentAnims[idx].InitPos = agents[i].Position;
						agentAnims[idx].PolyRef = refs[1];
						agentAnims[idx].Active = true;
						agentAnims[idx].T = 0.0f;
						agentAnims[idx].TMax = (Vector3Extensions.Distance2D(agentAnims[idx].StartPos, agentAnims[idx].EndPos)
							/ agents[i].Parameters.MaxSpeed) * 0.5f;

						agents[i].State = AgentState.Offmesh;
						agents[i].Corners.Clear();
						agents[i].NeighborCount = 0;
						continue;
					}
				}
			}

			//calculate steering
			for (int i = 0; i < numAgents; i++)
			{
				if (agents[i].State != AgentState.Walking)
					continue;
				if (agents[i].TargetState == TargetState.None)
					continue;

				Vector3 dvel = new Vector3(0, 0, 0);

				if (agents[i].TargetState == TargetState.Velocity)
				{
					dvel = agents[i].TargetPosition;
					agents[i].DesiredSpeed = agents[i].TargetPosition.Length();
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

					for (int j = 0; j < agents[i].NeighborCount; j++)
					{
						Agent nei = agents[agents[i].Neighbors[j].Index];

						Vector3 diff = agents[i].Position - nei.Position;
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
				if (agents[i].State != AgentState.Walking)
					continue;

				if ((agents[i].Parameters.UpdateFlags & UpdateFlags.ObstacleAvoidance) != 0)
				{
					this.obstacleQuery.Reset();

					//add neighhbors as obstacles
					for (int j = 0; j < agents[i].NeighborCount; j++)
					{
						Agent nei = agents[agents[i].Neighbors[j].Index];
						obstacleQuery.AddCircle(nei.Position, nei.Parameters.Radius, nei.Vel, nei.DesiredVel);
					}

					//append neighbor segments as obstacles
					for (int j = 0; j < agents[i].Boundary.SegCount; j++)
					{
						LocalBoundary.Segment s = agents[i].Boundary.Segs[j];
						if (Triangle3.Area2D(agents[i].Position, s.Start, s.End) < 0.0f)
							continue;
						obstacleQuery.AddSegment(s.Start, s.End);
					}

					//sample new safe velocity
					bool adaptive = true;
					int ns = 0;

					ObstacleAvoidanceQuery.ObstacleAvoidanceParams parameters = obstacleQueryParams[agents[i].Parameters.ObstacleAvoidanceType];

					if (adaptive)
					{
						ns = obstacleQuery.SampleVelocityAdaptive(agents[i].Position, agents[i].Parameters.Radius, agents[i].DesiredSpeed, agents[i].Vel, agents[i].DesiredVel, ref agents[i].NVel, parameters);
					}
					else
					{
						ns = obstacleQuery.SampleVelocityGrid(agents[i].Position, agents[i].Parameters.Radius, agents[i].DesiredSpeed, agents[i].Vel, agents[i].DesiredVel, ref agents[i].NVel, parameters);
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
				Agent ag = agents[i];

				if (ag.State != AgentState.Walking)
					continue;

				ag.Integrate(dt);
			}

			//handle collisions
			const float COLLISION_RESOLVE_FACTOR = 0.7f;

			for (int iter = 0; iter < 4; iter++)
			{
				for (int i = 0; i < numAgents; i++)
				{
					int idx0 = GetAgentIndex(agents[i]);

					if (agents[i].State != AgentState.Walking)
						continue;

					agents[i].Disp = new Vector3(0, 0, 0);

					float w = 0;

					for (int j = 0; j < agents[i].NeighborCount; j++)
					{
						Agent nei = agents[agents[i].Neighbors[j].Index];
						int idx1 = GetAgentIndex(nei);

						Vector3 diff = agents[i].Position - nei.Position;
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
					if (agents[i].State != AgentState.Walking)
						continue;

					//move along navmesh
					agents[i].Corridor.MovePosition(agents[i].Position, navQuery);

					//get valid constrained position back
					agents[i].Position = agents[i].Corridor.Pos;

					//if not using path, truncate the corridor to just one poly
					if (agents[i].TargetState == TargetState.None ||
						agents[i].TargetState == TargetState.Velocity)
					{
						agents[i].Corridor.Reset(agents[i].Corridor.GetFirstPoly(), agents[i].Position);
						agents[i].IsPartial = false;
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
						agents[i].State = AgentState.Walking;

						continue;
					}

					//update position
					float ta = agentAnims[i].TMax * 0.15f;
					float tb = agentAnims[i].TMax;
					if (agentAnims[i].T < ta)
					{
						float u = MathHelper.Normalize(agentAnims[i].T, 0.0f, ta);
						Vector3 lerpOut;
						Vector3.Lerp(ref agentAnims[i].InitPos, ref agentAnims[i].StartPos, u, out lerpOut);
						agents[i].Position = lerpOut;
					}
					else
					{
						float u = MathHelper.Normalize(agentAnims[i].T, ta, tb);
						Vector3 lerpOut;
						Vector3.Lerp(ref agentAnims[i].StartPos, ref agentAnims[i].EndPos, u, out lerpOut);
						agents[i].Position = lerpOut;
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
			Agent[] queue = new Agent[PATH_MAX_AGENTS];
			int numQueue = 0;
			Status status;

			//fire off new requests
			for (int i = 0; i < maxAgents; i++)
			{
				if (!agents[i].IsActive)
					continue;
				if (agents[i].State == AgentState.Invalid)
					continue;
				if (agents[i].TargetState == TargetState.None || agents[i].TargetState == TargetState.Velocity)
					continue;

				if (agents[i].TargetState == TargetState.Requesting)
				{
					Path path = agents[i].Corridor.NavPath;

					Vector3 reqPos = new Vector3();
					Path reqPath = new Path();

					//quick search towards the goal
					const int MAX_ITER = 20;
					NavPoint startPoint = new NavPoint(path[0], agents[i].Position);
					NavPoint endPoint = new NavPoint(agents[i].TargetRef, agents[i].TargetPosition);
					navQuery.InitSlicedFindPath(ref startPoint, ref endPoint, navQueryFilter, FindPathOptions.None);
					int tempInt = 0;
					navQuery.UpdateSlicedFindPath(MAX_ITER, ref tempInt);
					status = Status.Failure;
					if (agents[i].TargetReplan)
					{
						//try to use an existing steady path during replan if possible
						status = navQuery.FinalizedSlicedPathPartial(path, reqPath).ToStatus();
					}
					else
					{
						//try to move towards the target when the goal changes
						status = navQuery.FinalizeSlicedFindPath(reqPath).ToStatus();
					}

					if (status != Status.Failure && reqPath.Count > 0)
					{
						//in progress or succeed
						if (reqPath[reqPath.Count - 1] != agents[i].TargetRef)
						{
							//partial path, constrain target position in last polygon
							bool tempBool;
							status = navQuery.ClosestPointOnPoly(reqPath[reqPath.Count - 1], agents[i].TargetPosition, out reqPos, out tempBool).ToStatus();
							if (status == Status.Failure)
								reqPath.Clear();
						}
						else
						{
							reqPos = agents[i].TargetPosition;
						}
					}
					else
					{
						reqPath.Clear();
					}

					if (reqPath.Count == 0)
					{
						//could not find path, start the request from the current location
						reqPos = agents[i].Position;
						reqPath.Add(path[0]);
					}

					agents[i].Corridor.SetCorridor(reqPos, reqPath);
					agents[i].Boundary.Reset();
					agents[i].IsPartial = false;

					if (reqPath[reqPath.Count - 1] == agents[i].TargetRef)
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
				queue[i].TargetPathQueryIndex = pathq.Request(new NavPoint(queue[i].Corridor.GetLastPoly(), queue[i].Corridor.Target), new NavPoint(queue[i].TargetRef, queue[i].TargetPosition));
				if (queue[i].TargetPathQueryIndex != PathQueue.Invalid)
					queue[i].TargetState = TargetState.WaitingForPath;
			}

			//update requests
			pathq.Update(MaxItersPerUpdate);

			//process path results
			for (int i = 0; i < maxAgents; i++)
			{
				if (!agents[i].IsActive)
					continue;
				if (agents[i].TargetState == TargetState.None || agents[i].TargetState == TargetState.Velocity)
					continue;

				if (agents[i].TargetState == TargetState.WaitingForPath)
				{
					//poll path queue
					status = pathq.GetRequestStatus(agents[i].TargetPathQueryIndex);
					if (status == Status.Failure)
					{
						//path find failed, retry if the target location is still valid
						agents[i].TargetPathQueryIndex = PathQueue.Invalid;
						if (agents[i].TargetRef != NavPolyId.Null)
							agents[i].TargetState = TargetState.Requesting;
						else
							agents[i].TargetState = TargetState.Failed;
						agents[i].TargetReplanTime = 0.0f;
					}
					else if (status == Status.Success)
					{
						Path path = agents[i].Corridor.NavPath;

						//apply results
						Vector3 targetPos = new Vector3();
						targetPos = agents[i].TargetPosition;

						Path res;
						bool valid = true;
						status = pathq.GetPathResult(agents[i].TargetPathQueryIndex, out res).ToStatus();
						if (status == Status.Failure || res.Count == 0)
							valid = false;

						//Merge result and existing path
						if (valid && path[path.Count - 1] != res[0])
							valid = false;

						if (valid)
						{
							//put the old path infront of the old path
							if (path.Count > 1)
							{
								//make space for the old path
								//if ((path.Count - 1) + nres > maxPathResult)
									//nres = maxPathResult - (npath - 1);

								for (int j = 0; j < res.Count; j++)
									res[path.Count - 1 + j] = res[j];

								//copy old path in the beginning
								for (int j = 0; j < path.Count - 1; j++)
									res.Add(path[j]);

								//remove trackbacks
								res.RemoveTrackbacks();
							}

							//check for partial path
							if (res[res.Count - 1] != agents[i].TargetRef)
							{
								//partial path, constrain target position inside the last polygon
								Vector3 nearest;
								bool tempBool = false;
								status = navQuery.ClosestPointOnPoly(res[res.Count - 1], targetPos, out nearest, out tempBool).ToStatus();
								if (status == Status.Success)
									targetPos = nearest;
								else
									valid = false;
							}
						}

						if (valid)
						{
							//set current corridor
							agents[i].Corridor.SetCorridor(targetPos, res);
							
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
		public void UpdateTopologyOptimization(Agent[] agents, int numAgents, float dt)
		{
			if (numAgents == 0)
				return;

			const float OPT_TIME_THR = 0.5f; //seconds
			const int OPT_MAX_AGENTS = 1;
			Agent[] queue = new Agent[OPT_MAX_AGENTS];
			int nqueue = 0;

			for (int i = 0; i < numAgents; i++)
			{
				if (agents[i].State != AgentState.Walking)
					continue;
				if (agents[i].TargetState == TargetState.None ||
					agents[i].TargetState == TargetState.Velocity)
					continue;
				if ((agents[i].Parameters.UpdateFlags & UpdateFlags.OptimizeTopo) == 0)
					continue;
				agents[i].topologyOptTime += dt;
				if (agents[i].topologyOptTime >= OPT_TIME_THR)
					nqueue = AddToOptQueue(agents[i], queue, nqueue, OPT_MAX_AGENTS);
			}

			for (int i = 0; i < nqueue; i++)
			{
				queue[i].Corridor.OptimizePathTopology(navQuery, navQueryFilter);
				queue[i].topologyOptTime = 0.0f;
			}
		}

		/// <summary>
		/// Make sure that each agent is taking a valid path
		/// </summary>
		/// <param name="agents">The agent array</param>
		/// <param name="agentCount">The number of agents</param>
		/// <param name="dt">Time until next update</param>
		public void CheckPathValidity(Agent[] agents, int agentCount, float dt)
		{
			const int CHECK_LOOKAHEAD = 10;
			const float TARGET_REPLAN_DELAY = 1.0f; //seconds

			//Iterate through all the agents
			for (int i = 0; i < agentCount; i++)
			{
				Agent ag = agents[i];

				if (ag.State != AgentState.Walking)
					continue;

				if (ag.TargetState == TargetState.None || ag.TargetState == TargetState.Velocity)
					continue;

				ag.TargetReplanTime += dt;

				bool replan = false;

				//first check that the current location is valid
				NavPolyId agentRef = ag.Corridor.GetFirstPoly();
				Vector3 agentPos = ag.Position;
				if (!navQuery.IsValidPolyRef(agentRef))
				{
					//current location is not valid, try to reposition
					Vector3 nearest = agentPos;
					Vector3 pos = ag.Position;
					agentRef = NavPolyId.Null;
					NavPoint nearestPt;
					navQuery.FindNearestPoly(ref pos, ref ext, out nearestPt);
					nearest = nearestPt.Position;
					agentRef = nearestPt.Polygon;
					agentPos = nearestPt.Position;

					if (agentRef == NavPolyId.Null)
					{
						//could not find location in navmesh, set state to invalid
						ag.Corridor.Reset(NavPolyId.Null, agentPos);
						ag.IsPartial = false;
						ag.Boundary.Reset();
						ag.State = AgentState.Invalid;
						continue;
					}

					//make sure the first polygon is valid
					ag.Corridor.FixPathStart(agentRef, agentPos);
					ag.Boundary.Reset();
					ag.Position = agentPos;

					replan = true;
				}

				//try to recover move request position
				if (ag.TargetState != TargetState.None &&
					ag.TargetState != TargetState.Failed)
				{
					if (!navQuery.IsValidPolyRef(ag.TargetRef))
					{
						//current target is not valid, try to reposition
						Vector3 nearest = ag.TargetPosition;
						Vector3 tpos = ag.TargetPosition;
						ag.TargetRef = NavPolyId.Null;
						NavPoint nearestPt;
						navQuery.FindNearestPoly(ref tpos, ref ext, out nearestPt);
						ag.TargetRef = nearestPt.Polygon;
						nearest = nearestPt.Position;
						ag.TargetPosition = nearest;
						replan = true;
					}

					if (ag.TargetRef == NavPolyId.Null)
					{
						//failed to reposition target
						ag.Corridor.Reset(agentRef, agentPos);
						ag.IsPartial = false;
						ag.TargetState = TargetState.None;
					}
				}

				//if nearby corridor is not valid, replan
				if (!ag.Corridor.IsValid(CHECK_LOOKAHEAD, navQuery))
				{
					replan = true;
				}

				//if the end of the path is near and it is not the request location, replan
				if (ag.TargetState == TargetState.Valid)
				{
					if (ag.TargetReplanTime > TARGET_REPLAN_DELAY &&
						ag.Corridor.NavPath.Count < CHECK_LOOKAHEAD &&
						ag.Corridor.GetLastPoly() != ag.TargetRef)
						replan = true;
				}

				//try to replan path to goal
				if (replan)
				{
					if (ag.TargetState != TargetState.None)
					{
						ag.RequestMoveTargetReplan(ag.TargetRef, ag.TargetPosition);
					}
				}
			}
		}

		public bool OverOffmeshConnection(Agent ag, float radius)
		{
			if (ag.Corners.Count == 0)
				return false;

			bool offmeshConnection = ((ag.Corners[ag.Corners.Count - 1].Flags & StraightPathFlags.OffMeshConnection) != 0)
				? true : false;
			if (offmeshConnection)
			{
				float dist = Vector3Extensions.Distance2D(ag.Position, ag.Corners[ag.Corners.Count - 1].Point.Position);
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
		public void CalcSmoothSteerDirection(Agent ag, ref Vector3 dir)
		{
			if (ag.Corners.Count == 0)
			{
				dir = new Vector3(0, 0, 0);
				return;
			}

			int ip0 = 0;
			int ip1 = Math.Min(1, ag.Corners.Count - 1);
			Vector3 p0 = ag.Corners[ip0].Point.Position;
			Vector3 p1 = ag.Corners[ip1].Point.Position;

			Vector3 dir0 = p0 - ag.Position;
			Vector3 dir1 = p1 - ag.Position;
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
		public void CalcStraightSteerDirection(Agent ag, ref Vector3 dir)
		{
			if (ag.Corners.Count == 0)
			{
				dir = new Vector3(0, 0, 0);
				return;
			}

			dir = ag.Corners[0].Point.Position - ag.Position;
			dir.Y = 0;
			dir.Normalize();
		}

		/// <summary>
		/// Find the crowd agent's distance to its goal
		/// </summary>
		/// <param name="ag">Thw crowd agent</param>
		/// <param name="range">The maximum range</param>
		/// <returns>Distance to goal</returns>
		public float GetDistanceToGoal(Agent ag, float range)
		{
			if (ag.Corners.Count == 0)
				return range;

			bool endOfPath = ((ag.Corners[ag.Corners.Count - 1].Flags & StraightPathFlags.End) != 0) ? true : false;
			if (endOfPath)
				return Math.Min(Vector3Extensions.Distance2D(ag.Position, ag.Corners[ag.Corners.Count - 1].Point.Position), range);

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
		public int GetNeighbors(Vector3 pos, float height, float range, Agent skip, CrowdNeighbor[] result, int maxResult, Agent[] agents, ProximityGrid<Agent> grid)
		{
			int n = 0;

			const int MAX_NEIS = 32;
			Agent[] ids = new Agent[MAX_NEIS];
			int nids = grid.QueryItems(pos.X - range, pos.Z - range, pos.X + range, pos.Z + range, ids, MAX_NEIS);

			for (int i = 0; i < nids; i++)
			{
				Agent ag = ids[i];

				if (ag == skip)
					continue;

				//check for overlap
				Vector3 diff = pos - ag.Position;
				if (Math.Abs(diff.Y) >= (height + ag.Parameters.Height) / 2.0f)
					continue;
				diff.Y = 0;
				float distSqr = diff.LengthSquared();
				if (distSqr > range * range)
					continue;

				n = AddNeighbor(ids[i], distSqr, result, n, maxResult);
			}

			return n;
		}

		/// <summary>
		/// Add a CrowdNeighbor to the array
		/// </summary>
		/// <param name="agent">The neighbor</param>
		/// <param name="dist">Distance from current agent</param>
		/// <param name="neis">The neighbors array</param>
		/// <param name="nneis">The number of neighbors</param>
		/// <param name="maxNeis">The maximum number of neighbors allowed</param>
		/// <returns>An updated neighbor count</returns>
		public int AddNeighbor(Agent agent, float dist, CrowdNeighbor[] neis, int nneis, int maxNeis)
		{
			//insert neighbor based on distance
			int neiPos = 0;
			if (nneis == 0)
			{
				neiPos = nneis;
			}
			else if (dist >= neis[nneis - 1].Distance)
			{
				if (nneis >= maxNeis)
					return nneis;
				neiPos = nneis;
			}
			else
			{
				int i;
				for (i = 0; i < nneis; i++)
					if (dist <= neis[i].Distance)
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

			//TODO rework Crowd so that Agents are passed around instead of indices
			int index;
			for (index = 0; index < agents.Length; index++)
			{
				if (agent.Equals(agents[index]))
					break;
			}

			if (index == agents.Length)
				throw new IndexOutOfRangeException("Agent not in crowd.");

			var neighbor = new CrowdNeighbor();
			neighbor.Index = index;
			neighbor.Distance = dist;
			neis[neiPos] = neighbor;

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
		public int AddToPathQueue(Agent newag, Agent[] agents, int numAgents, int maxAgents)
		{
			//insert neighbor based on greatest time
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
		public int AddToOptQueue(Agent newag, Agent[] agents, int numAgents, int maxAgents)
		{
			//insert neighbor based on greatest time
			int slot = 0;
			if (numAgents == 0)
			{
				slot = numAgents;
			}
			else if (newag.topologyOptTime <= agents[numAgents - 1].topologyOptTime)
			{
				if (numAgents >= maxAgents)
					return numAgents;
				slot = numAgents;
			}
			else
			{
				int i;
				for (i = 0; i < numAgents; i++)
					if (newag.topologyOptTime >= agents[i].topologyOptTime)
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
	}

	/// <summary>
	/// A neighboring crowd agent
	/// </summary>
	public struct CrowdNeighbor
	{
		public int Index;
		public float Distance;
	}

	/// <summary>
	/// Settings for a particular crowd agent
	/// </summary>
	public struct AgentParams
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

	public struct AgentAnimation
	{
		public bool Active { get; set; }
		public Vector3 InitPos, StartPos, EndPos;
		public NavPolyId PolyRef;
		public float T, TMax;
	}
}
