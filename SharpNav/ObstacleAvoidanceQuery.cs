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
	public class ObstacleAvoidanceQuery
	{
		private const int MAX_PATTERN_DIVS = 32;
		private const int MAX_PATTERN_RINGS = 4;

		private ObstacleAvoidanceParams parameters;
		private float invHorizTime;
		private float vmax;
		private float invVmax;

		private int maxCircles;
		private ObstacleCircle[] circles;
		private int ncircles;

		private int maxSegments;
		private ObstacleSegment[] segments;
		private int nsegments;

		public ObstacleAvoidanceQuery(int maxCircles, int maxSegments)
		{
			this.maxCircles = maxCircles;
			this.ncircles = 0;
			this.circles = new ObstacleCircle[this.maxCircles];

			this.maxSegments = maxSegments;
			this.nsegments = 0;
			this.segments = new ObstacleSegment[this.maxSegments];
		}

		public void Reset()
		{
			ncircles = 0;
			nsegments = 0;
		}

		private struct ObstacleCircle
		{
			/// <summary>
			/// The position of the obstacle
			/// </summary>
			public Vector3 P;

			/// <summary>
			/// The velocity of the obstacle
			/// </summary>
			public Vector3 Vel;

			/// <summary>
			/// The velocity of the obstacle
			/// </summary>
			public Vector3 DVel;

			/// <summary>
			/// The radius of the obstacle
			/// </summary>
			public float Rad;

			/// <summary>
			/// Used for side selection during sampling
			/// </summary>
			public Vector3 Dp, Np;
		}

		private struct ObstacleSegment
		{
			/// <summary>
			/// Endpoints of the obstacle segment
			/// </summary>
			public Vector3 P, Q;

			public bool Touch;
		}

		public struct ObstacleAvoidanceParams
		{
			public float VelBias;
			public float WeightDesVel;
			public float WeightCurVel;
			public float WeightSide;
			public float WeightToi;
			public float HorizTime;
			public int GridSize;
			public int AdaptiveDivs;
			public int AdaptiveRings;
			public int AdaptiveDepth;
		}
	}
}
