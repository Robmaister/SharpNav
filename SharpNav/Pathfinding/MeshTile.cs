#region License
/**
 * Copyright (c) 2014 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
 * Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE
 */
#endregion

using System;

#if MONOGAME || XNA
using Microsoft.Xna.Framework;
#elif OPENTK
using OpenTK;
#elif SHARPDX
using SharpDX;
#elif UNITY3D
using UnityEngine;
#endif

namespace SharpNav.Pathfinding
{
	public class MeshTile
	{
		/// <summary>
		/// Counter describing modifications to the tile
		/// </summary>
		public int Salt { get; set; }

		/// <summary>
		/// Index to the next free link
		/// </summary>
		public int LinksFreeList { get; set; }
		public PathfinderCommon.NavMeshInfo Header { get; set; }
		public Poly[] Polys { get; set; }
		public Vector3[] Verts { get; set; }
		public Link[] Links { get; set; }

		public PolyMeshDetail.MeshData[] DetailMeshes { get; set; }
		public Vector3[] DetailVerts { get; set; }
		public PolyMeshDetail.TriangleData[] DetailTris { get; set; }

		public OffMeshConnection[] OffMeshConnections { get; set; }
		public BVNode[] BVTree { get; set; }

		public NavMeshBuilder Data { get; set; }
		public MeshTile Next { get; set; }
	}
}
