#region License
/**
 * Copyright (c) 2013 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
 * Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE
 */
#endregion

using System;
using System.Collections.Generic;
using SharpNav.Geometry;

namespace SharpNav
{
	public class PathfinderCommon
	{
		public const int VERTS_PER_POLYGON = 6; //max number of vertices

		//tile serialization constants used to detect compatibility of navigation tile data
		public const int NAVMESH_MAGIC = 'D' << 24 | 'N' << 16 | 'A' << 8 | 'V'; //magic number 
		public const int NAVMESH_VERSION = 7; //version number

		public const int EXT_LINK = 0x8000; //entity links to external entity
		public const uint NULL_LINK = 0xffffffff; //doesn't link to anything

		public const int OFFMESH_CON_BIDIR = 1; //bidirectional

		public const int POLTYPE_GROUND = 0; //part of mesh surface
		public const int POLTYPE_OFFMESH_CONNECTION = 1; //off-mesh connection consisting of two vertices

		public class MeshHeader
		{
			public int magic; //tile magic number (used to identify data format)
			public int version;
			public int x;
			public int y;
			public int layer;
			public uint userId;
			public int polyCount;
			public int vertCount;
			public int maxLinkCount;
			public int detailMeshCount;

			public int detailVertCount;

			public int detailTriCount;
			public int bvNodeCount;
			public int offMeshConCount;
			public int offMeshBase; //index of first polygon which is off-mesh connection
			public float walkableHeight;
			public float walkableRadius;
			public float walkableClimb;
			public BBox3 bounds;

			public float bvQuantFactor; //bounding volume quantization facto
		}

		public class Poly
		{
			public uint firstLink; //index to first link in linked list
			public int[] verts; //indices of polygon's vertices
			public int[] neis; //packed data representing neighbor polygons references and flags for each edge
			public int flags; //user defined polygon flags
			public int vertCount;
			public int areaAndtype; //bit packed area id and polygon type

			public void SetArea(int a)
			{
				areaAndtype = (areaAndtype & 0xc0) | (a & 0x3f);
			}

			public void SetType(int t)
			{
				areaAndtype = (areaAndtype & 0x3f) | (t << 6);
			}

			public int GetArea()
			{
				return areaAndtype & 0x3f;
			}

			public int GetType()
			{
				return areaAndtype >> 6;
			}
		}

		public struct PolyDetail
		{
			public uint vertBase; //offset of vertices in some array
			public uint triBase; //offset of triangles in some array
			public int vertCount;
			public int triCount;
		}

		public struct OffMeshConnection
		{
			public Vector3[] pos; //the endpoints of the connection
			public float radius;
			public int poly;
			public int flags; //assigned flag from Poly
			public int side; //endpoint side
			public uint userId; //id of offmesh connection
		}

		public struct BVNode
		{
			public BBox3 bounds;
			public int index;
		}

		public struct NavMeshParams
		{
			public Vector3 origin;
			public float tileWidth;
			public float tileHeight;
			public int maxTiles;
			public int maxPolys;
		}

		public struct Link
		{
			public uint reference; //neighbor reference (the one it's linked to)
			public uint next; //index of next link
			public int edge; //index of polygon edge
			public int side;
			public BBox3 bounds;
		}

		public class MeshTile
		{
			public uint salt; //counter describing modifications to the tile

			public uint linkesFreeList; //index to the next free link
			public PathfinderCommon.MeshHeader header;
			public PathfinderCommon.Poly[] polys;
			public Vector3[] verts;
			public Link[] links;
			public PathfinderCommon.PolyDetail[] detailMeshes;

			public Vector3[] detailVerts;
			public NavMeshDetail.TrisInfo[] detailTris;

			public PathfinderCommon.BVNode[] bvTree; //bounding volume nodes

			public PathfinderCommon.OffMeshConnection[] offMeshCons;

			public NavMeshBuilder data;
			public int flags;
			public MeshTile next;
		}
	}
}
