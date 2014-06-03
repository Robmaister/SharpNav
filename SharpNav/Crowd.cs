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

		private enum CrowdAgentState 
		{
			CROWDAGENT_STATE_INVALID,
			CROWDAGENT_STATE_WALKING,
			CROWDAGENT_STATE_OFFMESH
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

		private enum UpdateFlags
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

		private ObstacleAvoidanceParams[] obstacleQueryParams;
		//private ObstacleAvoidanceQuery[] obstacleQuery;

		//private ProximityGrid[] grid;

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

			//TODO: initialize proximity grid
			
			//TODO: allocate obstacle avoidance query

			//initialize obstancle query params
			this.obstacleQueryParams = new ObstacleAvoidanceParams[8];
			for (int i = 0; i < this.obstacleQueryParams.Length; i++)
			{
				ObstacleAvoidanceParams parameters = this.obstacleQueryParams[i];
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
				ag.State = (byte)CrowdAgentState.CROWDAGENT_STATE_WALKING;
			else
				ag.State = (byte)CrowdAgentState.CROWDAGENT_STATE_INVALID;

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

		public void UpdateMoveRequest()
		{
			const int PATH_MAX_AGENTS = 8;
			CrowdAgent[] queue = new CrowdAgent[PATH_MAX_AGENTS];
			int nqueue = 0;
			bool boolStatus;

			//fire off new requests
			for (int i = 0; i < maxAgents; i++)
			{
				CrowdAgent ag = agents[i];

				if (!ag.Active)
					continue;
				if (ag.State == (byte)CrowdAgentState.CROWDAGENT_STATE_INVALID)
					continue;
				if (ag.TargetState == MoveRequestState.CROWDAGENT_TARGET_NONE ||
					ag.TargetState == MoveRequestState.CROWDAGENT_TARGET_VELOCITY)
					continue;

				if (ag.TargetState == MoveRequestState.CROWDAGENT_TARGET_REQUESTING)
				{
					int[] path = ag.Corridor.GetPath();
					int npath = ag.Corridor.GetPathCount();

					const int MAX_RES = 32;
					Vector3 reqPos = new Vector3();
					int[] reqPath = new int[MAX_RES];
					int reqPathCount = 0;

					//quick search towards the goal
					const int MAX_ITER = 20;
					navquery.InitSlicedFindPath((int)path[0], (int)ag.TargetRef, ag.NPos, ag.TargetPos);
					int tempInt = 0;
					navquery.UpdateSlicedFindPath(MAX_ITER, ref tempInt);
					boolStatus = false;
					if (ag.TargetReplan)
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
						if (reqPath[reqPathCount - 1] != ag.TargetRef)
						{
							//partial path, constrain target position in last polygon
							bool tempBool;
							boolStatus = navquery.ClosestPointOnPoly(reqPath[reqPathCount - 1], ag.TargetPos, out reqPos, out tempBool);
							if (boolStatus == false)
								reqPathCount = 0;

						}
						else
						{
							reqPos = ag.TargetPos;
						}
					}
					else
					{
						reqPathCount = 0;
					}

					if (reqPathCount == 0)
					{
						//could not find path, start the request from the current location
						reqPos = ag.NPos;
						reqPath[0] = path[0];
						reqPathCount = 1;
					}

					ag.Corridor.SetCorridor(reqPos, reqPath, reqPathCount);
					ag.Boundary.Reset();
					ag.Partial = false;

					if (reqPath[reqPathCount - 1] == ag.TargetRef)
					{
						ag.TargetState = MoveRequestState.CROWDAGENT_TARGET_VALID;
						ag.TargetReplanTime = 0.0f;
					}
					else
					{
						//the path is longer or potentially unreachable, full plan
						ag.TargetState = MoveRequestState.CROWDAGENT_TARGET_WAITING_FOR_QUEUE;
					}
				}

				if (ag.TargetState == MoveRequestState.CROWDAGENT_TARGET_WAITING_FOR_QUEUE)
				{
					nqueue = AddToPathQueue(ag, queue, nqueue, PATH_MAX_AGENTS);
				}
					
				agents[i] = ag;
			}

			for (int i = 0; i < nqueue; i++)
			{
				CrowdAgent ag = queue[i];

				ag.TargetPathqRef = pathq.Request(ag.Corridor.GetLastPoly(), ag.TargetRef, ag.Corridor.GetTarget(), ag.TargetPos);
				if (ag.TargetPathqRef != PathQueue.PATHQ_INVALID)
					ag.TargetState = MoveRequestState.CROWDAGENT_TARGET_WAITING_FOR_PATH;

				queue[i] = ag;
			}

			//update requests
			pathq.Update(MAX_ITERS_PER_UPDATE);

			PathQueue.Status status;

			//process path results
			for (int i = 0; i < maxAgents; i++)
			{
				CrowdAgent ag = agents[i];

				if (!ag.Active)
					continue;
				if (ag.TargetState == MoveRequestState.CROWDAGENT_TARGET_NONE ||
					ag.TargetState == MoveRequestState.CROWDAGENT_TARGET_VELOCITY)
					continue;

				if (ag.TargetState == MoveRequestState.CROWDAGENT_TARGET_WAITING_FOR_PATH)
				{
					//poll path queue
					status = pathq.GetRequestStatus(ag.TargetPathqRef);
					if (status == PathQueue.Status.FAILURE)
					{
						//path find failed, retry if the target location is still valid
						ag.TargetPathqRef = PathQueue.PATHQ_INVALID;
						if (ag.TargetRef != 0)
							ag.TargetState = MoveRequestState.CROWDAGENT_TARGET_REQUESTING;
						else
							ag.TargetState = MoveRequestState.CROWDAGENT_TARGET_FAILED;
						ag.TargetReplanTime = 0.0f;
					}
					else if (status == PathQueue.Status.SUCCESS)
					{
						int[] path = ag.Corridor.GetPath();
						int npath = ag.Corridor.GetPathCount();

						//apply results
						Vector3 targetPos = new Vector3();
						targetPos = ag.TargetPos;

						int[] res = new int[this.maxPathResult];
						for (int j = 0; j < this.maxPathResult; j++)
							res[i] = pathResult[j];
						bool valid = true;
						int nres = 0;
						boolStatus = pathq.GetPathResult(ag.TargetPathqRef, res, ref nres, maxPathResult);
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
							if (res[nres - 1] != ag.TargetRef)
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
							ag.Corridor.SetCorridor(targetPos, res, nres);
							//forced to update boundary
							ag.Boundary.Reset();
							ag.TargetState = MoveRequestState.CROWDAGENT_TARGET_VALID;
						}
						else
						{
							//something went wrong
							ag.TargetState = MoveRequestState.CROWDAGENT_TARGET_FAILED;
						}

						ag.TargetReplanTime = 0.0f;
					}
				}

				agents[i] = ag;
			}
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
				CrowdAgent ag = agents[i];

				if (ag.State != (byte)CrowdAgentState.CROWDAGENT_STATE_WALKING)
					continue;
				if (ag.TargetState == MoveRequestState.CROWDAGENT_TARGET_NONE ||
					ag.TargetState == MoveRequestState.CROWDAGENT_TARGET_VELOCITY)
					continue;
				if ((ag.Parameters.UpdateFlags & (byte)UpdateFlags.CROWD_OPTIMIZE_TOPO) == 0)
					continue;
				ag.TopologyOptTime += dt;
				if (ag.TopologyOptTime >= OPT_TIME_THR)
					nqueue = AddToOptQueue(ag, queue, nqueue, OPT_MAX_AGENTS);

				agents[i] = ag;
			}

			for (int i = 0; i < nqueue; i++)
			{
				CrowdAgent ag = queue[i];

				ag.Corridor.OptimizePathTopology(navquery);
				ag.TopologyOptTime = 0.0f;

				queue[i] = ag;
			}
		}

		public void CheckPathValidity(CrowdAgent[] agents, int nagents, float dt)
		{
			const int CHECK_LOOKAHEAD = 10;
			const float TARGET_REPLAN_DELAY = 1.0f; //seconds

			for (int i = 0; i < nagents; i++)
			{
				CrowdAgent ag = agents[i];

				if (ag.State != (byte)CrowdAgentState.CROWDAGENT_STATE_WALKING)
					continue;

				ag.TargetReplanTime += dt;

				bool replan = false;

				//first check that the current location is valid
				int idx = GetAgentIndex(ag);
				Vector3 agentPos = new Vector3();
				int agentRef = ag.Corridor.GetFirstPoly();
				agentPos = ag.NPos;
				if (!navquery.IsValidPolyRef(agentRef))
				{
					//current location is not valid, try to reposition
					Vector3 nearest = agentPos;
					agentRef = 0;
					navquery.FindNearestPoly(ref ag.NPos, ref ext, out agentRef, out nearest);
					agentPos = nearest;

					if (agentRef == 0)
					{
						//could not find location in navmesh, set state to invalid
						ag.Corridor.Reset(0, agentPos);
						ag.Partial = false;
						ag.Boundary.Reset();
						ag.State = (byte)CrowdAgentState.CROWDAGENT_STATE_INVALID;
						continue;
					}

					//make sure the first polygon is valid
					ag.Corridor.FixPathStart(agentRef, agentPos);
					ag.Boundary.Reset();
					ag.NPos = agentPos;

					replan = true;
				}

				if (ag.TargetState == MoveRequestState.CROWDAGENT_TARGET_NONE
					|| ag.TargetState == MoveRequestState.CROWDAGENT_TARGET_VELOCITY)
					continue;

				//try to recover move request position
				if (ag.TargetState != MoveRequestState.CROWDAGENT_TARGET_NONE &&
					ag.TargetState != MoveRequestState.CROWDAGENT_TARGET_FAILED)
				{
					if (!navquery.IsValidPolyRef(ag.TargetRef))
					{
						//current target is not valid, try to reposition
						Vector3 nearest = ag.TargetPos;
						ag.TargetRef = 0;
						navquery.FindNearestPoly(ref ag.TargetPos, ref ext, out ag.TargetRef, out nearest);
						ag.TargetPos = nearest;
						replan = true;
					}
					if (ag.TargetRef == 0)
					{
						//failed to reposition target
						ag.Corridor.Reset(agentRef, agentPos);
						ag.Partial = false;
						ag.TargetState = MoveRequestState.CROWDAGENT_TARGET_NONE;
					}
				}

				//if nearby corridor is not valid, replan
				if (!ag.Corridor.IsValid(CHECK_LOOKAHEAD, navquery))
				{
					replan = true;
				}

				//if the end of the path is near and it is not the request location, replan
				if (ag.TargetState == MoveRequestState.CROWDAGENT_TARGET_VALID)
				{
					if (ag.TargetReplanTime > TARGET_REPLAN_DELAY &&
						ag.Corridor.GetPathCount() < CHECK_LOOKAHEAD &&
						ag.Corridor.GetLastPoly() != ag.TargetRef)
						replan = true;
				}

				//try to replan path to goal
				if (replan)
				{
					if (ag.TargetState != MoveRequestState.CROWDAGENT_TARGET_NONE)
					{
						RequestTargetMoveReplan(idx, ag.TargetRef, ag.TargetPos);
					}
				}

				agents[i] = ag;
			}
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

		public int GetAgentIndex(CrowdAgent agent)
		{
			for (int i = 0; i < agents.Length; i++)
			{
				if (agents[i] == agent)
					return i;
			}

			return -1;
		}

		private struct ObstacleAvoidanceParams
		{
			public float VelBias;
			public float WeightDesVel;
			public float WeightCurVel;
			public float WeightSide;
			public float WeightToi;
			public float HorizTime;
			public byte GridSize;
			public byte AdaptiveDivs;
			public byte AdaptiveRings;
			public byte AdaptiveDepth;
		}

		/// <summary>
		/// A crowd agent is a unit that moves across the navigation mesh
		/// </summary>
		public class CrowdAgent
		{
			public bool Active;
			public byte State;
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
			public byte[] CornerFlags;		//size = CROWDAGENT_MAX_CORNERS
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

			public byte UpdateFlags;
			
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
