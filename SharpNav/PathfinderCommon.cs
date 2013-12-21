#region License
/**
 * Copyright (c) 2013 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
 * Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE
 */
#endregion

using System;
using System.Collections.Generic;
using SharpNav.Geometry;

#if MONOGAME || XNA
using Microsoft.Xna.Framework;
#elif OPENTK
using OpenTK;
#elif SHARPDX
using SharpDX;
#endif

namespace SharpNav
{
	/// <summary>
	/// Store constants, structs, methods in this single class so that other classes can access this information.
	/// </summary>
	public class PathfinderCommon
	{
		public const int VERTS_PER_POLYGON = 6; //max number of vertices

		//tile serialization constants used to detect compatibility of navigation tile data
		public const int NAVMESH_MAGIC = 'D' << 24 | 'N' << 16 | 'A' << 8 | 'V'; //magic number 
		public const int NAVMESH_VERSION = 7; //version number

		public const int EXT_LINK = 0x8000; //entity links to external entity
		public const uint NULL_LINK = 0xffffffff; //doesn't link to anything

		public const int MAX_AREAS = 64; //max number of user defined area ids

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

		public class Link
		{
			public uint reference; //neighbor reference (the one it's linked to)
			public uint next; //index of next link
			public int edge; //index of polygon edge
			public int side;
			public int bmin;
			public int bmax;
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

		public static uint NextPow2(uint v)
		{
			v--;
			v |= v >> 1;
			v |= v >> 2;
			v |= v >> 4;
			v |= v >> 8;
			v |= v >> 16;
			v++;

			return v;
		}

		public static uint Ilog2(uint v)
		{
			uint r;
			int shift;
			r = (uint)((v > 0xffff) ? 1 << 4 : 0 << 4); v >>= (int)r;
			shift = (v > 0xff) ? 1 << 3 : 0 << 3; v >>= shift; r |= (uint)shift;
			shift = (v > 0xf) ? 1 << 2 : 0 << 2; v >>= shift; r |= (uint)shift;
			shift = (v > 0x3) ? 1 << 1 : 0 << 1; v >>= shift; r |= (uint)shift;
			r |= (v >> 1);
			return r;
		}

		public static float VectorDot2D(Vector3 u, Vector3 v)
		{
			return u.X * v.X + u.Z * v.Z;
		}

		public static void VectorLinearInterpolation(ref Vector3 dest, Vector3 v1, Vector3 v2, float t)
		{
			dest = new Vector3();
			dest.X = v1.X + (v2.X - v1.X) * t;
			dest.Y = v1.Y + (v2.Y - v1.Y) * t;
			dest.Z = v1.Z + (v2.Z - v1.Z) * t;
		}

		public static bool DistancePointPolyEdgesSquare(Vector3 pt, Vector3[] verts, int nverts, float[] ed, float[] et)
		{
			bool c = false;

			for (int i = 0, j = nverts - 1; i < nverts; j = i++)
			{
				Vector3 vi = verts[i];
				Vector3 vj = verts[j];
				if (((vi.Z > pt.Z) != (vj.Z > pt.Z)) &&
					(pt.X < (vj.X - vi.X) * (pt.Z - vi.Z) / (vj.Z - vi.Z) + vi.X))
				{
					c = !c;
				}

				ed[j] = DistancePointSegmentSquare2D(pt, vj, vi, ref et[j]);
			}

			return c;
		}

		public static float DistancePointSegmentSquare2D(Vector3 pt, Vector3 p, Vector3 q, ref float t)
		{
			float pqx = q.X - p.X;
			float pqz = q.Z - p.Z;
			float dx = pt.X - p.X;
			float dz = pt.Z - p.Z;
			float d = pqx * pqx + pqz * pqz;
			t = pqx * dx + pqz * dz;

			if (d > 0)
				t /= d;

			if (t < 0)
				t = 0;
			else if (t > 1)
				t = 1;

			dx = p.X + t * pqx - pt.X;
			dz = p.Z + t * pqz - pt.Z;

			return dx * dx + dz * dz;
		}

		public static bool ClosestHeightPointTriangle(Vector3 p, Vector3 a, Vector3 b, Vector3 c, ref float h)
		{
			Vector3 v0 = c - a;
			Vector3 v1 = b - a;
			Vector3 v2 = p - a;

			float dot00 = VectorDot2D(v0, v0);
			float dot01 = VectorDot2D(v0, v1);
			float dot02 = VectorDot2D(v0, v2);
			float dot11 = VectorDot2D(v1, v1);
			float dot12 = VectorDot2D(v1, v2);

			//computer barycentric coordinates
			float invDenom = 1.0f / (dot00 * dot11 - dot01 * dot01);
			float u = (dot11 * dot02 - dot01 * dot12) * invDenom;
			float v = (dot00 * dot12 - dot01 * dot02) * invDenom;

			float EPS = 1E-4f;

			//if point lies inside triangle, return interpolated y-coordinate
			if (u >= -EPS && v >= -EPS && (u + v) <= 1 + EPS)
			{
				h = a.Y + v0.Y * u + v1.Y * v;
				return true;
			}

			return false;
		}
	}
}
