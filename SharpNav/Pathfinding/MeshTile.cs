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
		public int salt; //counter describing modifications to the tile

		public int linksFreeList; //index to the next free link
		public PathfinderCommon.MeshHeader header;
		public Poly[] polys;
		public Vector3[] verts;
		public Link[] links;

		public PolyMeshDetail.MeshData[] detailMeshes;
		public Vector3[] detailVerts;
		public PolyMeshDetail.TriangleData[] detailTris;

		public BVNode[] bvTree; //bounding volume nodes

		public OffMeshConnection[] offMeshCons;

		public NavMeshBuilder data;
		public MeshTile next;
	}
}
