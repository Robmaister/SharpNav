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

		enum CrowdAgentState 
		{
			CROWDAGENT_STATE_INVALID,
			CROWDAGENT_STATE_WALKING,
			CROWDAGENT_STATE_OFFMESH
		}

		enum MoveRequestState
		{
			CROWDAGENT_TARGET_NONE = 0,
			CROWDAGENT_TARGET_FAILED,
			CROWDAGENT_TARGET_VALID,
			CROWDAGENT_TARGET_REQUESTING,
			CROWDAGENT_TARGET_WAITING_FOR_QUEUE,
			CROWDAGENT_TARGET_WAITING_FOR_PATH,
			CROWDAGENT_TARGET_VELOCITY
		}

		private int maxAgents;
		private CrowdAgent[] agents;
		private CrowdAgent[] activeAgents;
		private CrowdAgentAnimation[] agentAnims; 

		private PathQueue pathq;

		private ObstacleAvoidanceParams[] obstacleQueryParams;
		//private ObstacleAvoidanceQuery[] obstacleQuery;

		//private ProximityGrid[] grid;

		private uint[] pathResult;
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
			this.pathResult = new uint[this.maxPathResult];

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

			ag.Corridor.Reset((uint)reference, nearest);
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
		public bool RequestTargetMoveReplan(int idx, uint reference, Vector3 pos)
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
				ag.TargetState = (byte)MoveRequestState.CROWDAGENT_TARGET_REQUESTING;
			else
				ag.TargetState = (byte)MoveRequestState.CROWDAGENT_TARGET_FAILED;

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
		public bool RequestTargetMove(int idx, uint reference, Vector3 pos)
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
				ag.TargetState = (byte)MoveRequestState.CROWDAGENT_TARGET_REQUESTING;
			else
				ag.TargetState = (byte)MoveRequestState.CROWDAGENT_TARGET_FAILED;

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
			ag.TargetState = (byte)MoveRequestState.CROWDAGENT_TARGET_VELOCITY;

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
		public struct CrowdAgent
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
			public uint[] CornerPolys;		//size = CROWDAGENT_MAX_CORNERS

			public int NCorners;

			public byte TargetState;
			public uint TargetRef;
			public Vector3 TargetPos;
			public uint TargetPathqRef;
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
			public uint PolyRef;
			public float T, TMax;
		}
	}
}
