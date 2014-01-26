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

			//Off-mesh connections don't have detail polygons
			if (tile.polys[indexPoly].PolyType == PolygonType.OffMeshConnection)
			{
				ClosestPointOnPolyOffMeshConnection(tile, poly, pos, out closest);
				return;
			}

			ClosestPointOnPolyBoundary(tile, poly, pos, out closest);

			float h;
			if (ClosestHeight(tile, indexPoly, pos, out h))
				closest.Y = h;
		}

		public static void ClosestPointOnPolyBoundary(MeshTile tile, Poly poly, Vector3 pos, out Vector3 closest)
		{
			//Clamp point to be inside the polygon
			Vector3[] verts = new Vector3[PathfinderCommon.VERTS_PER_POLYGON];
			float[] edgeDistance = new float[PathfinderCommon.VERTS_PER_POLYGON];
			float[] edgeT = new float[PathfinderCommon.VERTS_PER_POLYGON];
			int numPolyVerts = poly.vertCount;
			for (int i = 0; i < numPolyVerts; i++)
				verts[i] = tile.verts[poly.verts[i]];

			bool inside = MathHelper.Distance.PointToPolygonEdgeSquared(pos, verts, numPolyVerts, edgeDistance, edgeT);
			if (inside)
			{
				//Point is inside the polygon
				closest = pos;
			}
			else
			{
				//Point is outside the polygon
				//Clamp to nearest edge
				float minDistance = float.MaxValue;
				int minIndex = -1;
				for (int i = 0; i < numPolyVerts; i++)
				{
					if (edgeDistance[i] < minDistance)
					{
						minDistance = edgeDistance[i];
						minIndex = i;
					}
				}
				Vector3 va = verts[minIndex];
				Vector3 vb = verts[(minIndex + 1) % numPolyVerts];
				closest = Vector3.Lerp(va, vb, edgeT[minIndex]);
			}
		}

		public static bool ClosestHeight(MeshTile tile, int indexPoly, Vector3 pos, out float h)
		{
			Poly poly = tile.polys[indexPoly];
			PolyMeshDetail.MeshData pd = tile.detailMeshes[indexPoly];
			h = 0;

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

				if (MathHelper.Distance.PointToTriangle(pos, v[0], v[1], v[2], ref h))
					return true;
			}

			return false;
		}

		public static void ClosestPointOnPolyOffMeshConnection(MeshTile tile, Poly poly, Vector3 pos, out Vector3 closest)
		{
			Vector3 v0 = tile.verts[poly.verts[0]];
			Vector3 v1 = tile.verts[poly.verts[1]];
			float d0 = (pos - v0).Length();
			float d1 = (pos - v1).Length();
			float u = d0 / (d0 + d1);
			closest = Vector3.Lerp(v0, v1, u);
		}

		public static void RandomPointInConvexPoly(Vector3[] pts, int npts, float[] areas, float s, float t, out Vector3 pt)
		{
			//Calculate triangle areas
			float areaSum = 0.0f;
			float area;
			for (int i = 2; i < npts; i++)
			{
				Triangle3.Area2D(ref pts[0], ref pts[i - 1], ref pts[i], out area);
				areaSum += Math.Max(0.001f, area);
				areas[i] = area;
			}

			//Find sub triangle weighted by area
			float threshold = s * areaSum;
			float accumulatedArea = 0.0f;
			float u = 0.0f;
			int triangleVertex = 0;
			for (int i = 2; i < npts; i++)
			{
				float currentArea = areas[i];
				if (threshold >= accumulatedArea && threshold < (accumulatedArea + currentArea))
				{
					u = (threshold - accumulatedArea) / currentArea;
					triangleVertex = i;
					break;
				}

				accumulatedArea += currentArea;
			}

			float v = (float)Math.Sqrt(t);

			float a = 1 - v;
			float b = (1 - u) * v;
			float c = u * v;
			Vector3 pointA = pts[0];
			Vector3 pointB = pts[triangleVertex - 1];
			Vector3 pointC = pts[triangleVertex];

			pt = a * pointA + b * pointB + c * pointC;
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
