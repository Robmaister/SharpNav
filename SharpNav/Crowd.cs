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

		//private ObstacleAvoidanceParams obstacleQueryParamrs[MAX_OBSTACLE_AVOIDANCE_PARAMS];
		//private ObstacleAvoidanceQuery[] obstacleQuery;

		//private ProximityGrid[] grid;

		private uint[] pathResult;
		private int maxPathResult;

		private Vector3 ext;

		//private QueryFilter m_filters[MAX_QUERY_FILTER_TYPE];

		private float maxAgentRadius;

		private int velocitySampleCount;

		private NavMeshQuery[] navquery;

		public Crowd(int maxAgents, float maxAgentRadius, ref NavMesh nav)
		{

		}
	}
}
