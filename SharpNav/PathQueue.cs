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
	public class PathQueue
	{
		public const byte PATHQ_INVALID = 0;

		private const int MAX_QUEUE = 8;
		private PathQuery[] queue; //size = MAX_QUEUE
		private uint nextHandle;
		private int maxPathSize;
		private int queueHead;
		private NavMeshQuery navquery;

		public PathQueue(int maxPathSize, int maxSearchNodeCount, ref TiledNavMesh nav)
		{
			this.navquery = new NavMeshQuery(nav, maxSearchNodeCount);

			this.maxPathSize = maxPathSize;
			for (int i = 0; i < MAX_QUEUE; i++)
			{
				queue[i].Reference = PATHQ_INVALID;
				queue[i].Path = new uint[maxPathSize];
			}

			this.queueHead = 0;
		}

		private struct PathQuery
		{
			public uint Reference;
			
			//path find start and end location
			public Vector3 StartPos, EndPos;
			public uint StartRef, EndRef;

			//result
			public uint[] Path;
			public int NPath;

			//state
			public int KeepAlive;
		}
	}
}
