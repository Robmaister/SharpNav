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
		private int maxAgents;
		//private CrowdAgent[] agents;
		//private CrowdAgent[][] activeAgents;
		//private CrowdAgentAnimation[] agentAnims; 

		//private PathQueue pathq;

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

		public Crowd(int maxAgents, float maxAgentRadius, ref NavMesh nav)
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
			}

			//TODO: allocate temp buffer for merging paths

			//allocate nav mesh query
			this.navquery = new NavMeshQuery(nav, 512);
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
	}
}
