#region License
/**
 * Copyright (c) 2013-2014 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
 * Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE
 */
#endregion

using System;
using System.Collections.Generic;

using SharpNav.Geometry;
using SharpNav.Pathfinding;

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
		//public const int NAVMESH_MAGIC = 'D' << 24 | 'N' << 16 | 'A' << 8 | 'V'; //magic number 
		//public const int NAVMESH_VERSION = 7; //version number

		public const int EXT_LINK = 0x8000; //entity links to external entity
		public const int NULL_LINK = unchecked((int)0xffffffff); //doesn't link to anything

		public const int STRAIGHTPATH_START = 0x01; //vertex is in start position of path
		public const int STRAIGHTPATH_END = 0x02; //vertex is in end position of path
		public const int STRAIGHTPATH_OFFMESH_CONNECTION = 0x04; //vertex is at start of an off-mesh connection

		public const int STRAIGHTPATH_AREA_CROSSINGS = 0x01; //add a vertex at each polygon edge crossing where area changes
		public const int STRAIGHTPATH_ALL_CROSSINGS = 0x02; //add a vertex at each polygon edge crossing

		public const int OFFMESH_CON_BIDIR = 1; //bidirectional

		public static bool OverlapQuantBounds(Vector3 amin, Vector3 amax, Vector3 bmin, Vector3 bmax)
		{
			bool overlap = true;
			overlap = (amin.X > bmax.X || amax.X < bmin.X) ? false : overlap;
			overlap = (amin.Y > bmax.Y || amax.Y < bmin.Y) ? false : overlap;
			overlap = (amin.Z > bmax.Z || amax.Z < bmin.Z) ? false : overlap;
			return overlap;
		}

		public static void ClosestPointOnPolyInTile(MeshTile tile, Poly poly, Vector3 pos, ref Vector3 closest)
		{
			int indexPoly = 0;
			for (int i = 0; i < tile.polys.Length; i++)
			{
				if (tile.polys[i] == poly)
				{
					indexPoly = i;
					break;
				}
			}

			ClosestPointOnPolyInTile(tile, indexPoly, pos, ref closest);
		}

		public static void ClosestPointOnPolyInTile(MeshTile tile, int indexPoly, Vector3 pos, ref Vector3 closest)
		{
			Poly poly = tile.polys[indexPoly];

			//off-mesh connections don't have detail polygons
			if (tile.polys[indexPoly].PolyType == PolygonType.OffMeshConnection)
			{
				Vector3 v0 = tile.verts[poly.verts[0]];
				Vector3 v1 = tile.verts[poly.verts[1]];
				float d0 = (pos - v0).Length();
				float d1 = (pos - v1).Length();
				float u = d0 / (d0 + d1);
				closest = Vector3.Lerp(v0, v1, u);
				return;
			}

			PolyMeshDetail.MeshData pd = tile.detailMeshes[indexPoly];

			//clamp point to be inside the polygon
			Vector3[] verts = new Vector3[PathfinderCommon.VERTS_PER_POLYGON];
			float[] edged = new float[PathfinderCommon.VERTS_PER_POLYGON];
			float[] edget = new float[PathfinderCommon.VERTS_PER_POLYGON];
			int nv = poly.vertCount;
			for (int i = 0; i < nv; i++)
				verts[i] = tile.verts[poly.verts[i]];

			closest = pos;
			if (!MathHelper.Distance.PointToPolygonEdgeSquared(pos, verts, nv, edged, edget))
			{
				//point is outside polygon so clamp to nearest edge
				float dmin = float.MaxValue;
				int imin = -1;

				for (int i = 0; i < nv; i++)
				{
					if (edged[i] < dmin)
					{
						dmin = edged[i];
						imin = i;
					}
				}

				Vector3 va = verts[imin];
				Vector3 vb = verts[(imin + 1) % nv];
				closest = Vector3.Lerp(va, vb, edget[imin]);
			}

			//find height at the location
			for (int j = 0; j < tile.detailMeshes[indexPoly].TriangleCount; j++)
			{
				PolyMeshDetail.TriangleData t = tile.detailTris[pd.TriangleIndex + j];
				Vector3[] v = new Vector3[3];

				for (int k = 0; k < 3; k++)
				{
					if (t[k] < poly.vertCount)
						v[k] = tile.verts[poly.verts[t[k]]];
					else
						v[k] = tile.detailVerts[pd.VertexIndex + (t[k] - poly.vertCount)];
				}

				float h = 0;
				if (MathHelper.Distance.PointToTriangle(pos, v[0], v[1], v[2], ref h))
				{
					closest.Y = h;
					break;
				}
			}
		}

		public static void RandomPointInConvexPoly(Vector3[] pts, int npts, float[] areas, float s, float t, ref Vector3 pt)
		{
			//calculate triangle areas
			float areaSum = 0.0f;
			float area;
			for (int i = 2; i < npts; i++)
			{
				Triangle3.Area2D(ref pts[0], ref pts[i - 1], ref pts[i], out area);
				areaSum += Math.Max(0.001f, area);
				areas[i] = area;
			}

			//find sub triangle weighted by area
			float thr = s * areaSum;
			float acc = 0.0f;
			float u = 0.0f;
			int tri = 0;
			for (int i = 2; i < npts; i++)
			{
				float dacc = areas[i];
				if (thr >= acc && thr < (acc + dacc))
				{
					u = (thr - acc) / dacc;
					tri = i;
					break;
				}

				acc += dacc;
			}

			float v = (float)Math.Sqrt(t);

			float a = 1 - v;
			float b = (1 - u) * v;
			float c = u * v;
			Vector3 pa = pts[0];
			Vector3 pb = pts[tri - 1];
			Vector3 pc = pts[tri];

			pt.X = a * pa.X + b * pb.X + c * pc.X;
			pt.Y = a * pa.Y + b * pb.Y + c * pc.Y;
			pt.Z = a * pa.Z + b * pb.Z + c * pc.Z;
		}

		public class MeshHeader
		{
			//public int magic; //tile magic number (used to identify data format)
			//public int version;
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
	}
}
