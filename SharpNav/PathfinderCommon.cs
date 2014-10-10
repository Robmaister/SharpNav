#region License
/**
 * Copyright (c) 2013-2014 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
 * Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE
 */
#endregion

using System;
using System.Linq;
using System.Collections.Generic;
using SharpNav.Geometry;
using SharpNav.Pathfinding;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;


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
	/// <summary>
	/// Store constants, structs, methods in this single class so that other classes can access this information.
	/// </summary>
	public class PathfinderCommon
	{
		public const int VERTS_PER_POLYGON = 6; //max number of vertices

		public const int EXT_LINK = unchecked((int)0x80000000); //entity links to external entity
		public const int NULL_LINK = unchecked((int)0xffffffff); //doesn't link to anything

		public const int STRAIGHTPATH_START = 0x01; //vertex is in start position of path
		public const int STRAIGHTPATH_END = 0x02; //vertex is in end position of path
		public const int STRAIGHTPATH_OFFMESH_CONNECTION = 0x04; //vertex is at start of an off-mesh connection

		public const int STRAIGHTPATH_AREA_CROSSINGS = 0x01; //add a vertex at each polygon edge crossing where area changes
		public const int STRAIGHTPATH_ALL_CROSSINGS = 0x02; //add a vertex at each polygon edge crossing

		public const int OFFMESH_CON_BIDIR = 1; //bidirectional

		/// <summary>
		/// Given a point, find the closest point on that poly.
		/// </summary>
		/// <param name="tile">The current tile.</param>
		/// <param name="poly">The current polygon.</param>
		/// <param name="pos">The current position</param>
		/// <param name="closest">Reference to the closest point</param>
		public static void ClosestPointOnPolyInTile(MeshTile tile, Poly poly, Vector3 pos, ref Vector3 closest)
		{
			int indexPoly = 0;
			for (int i = 0; i < tile.Polys.Length; i++)
			{
				if (tile.Polys[i] == poly)
				{
					indexPoly = i;
					break;
				}
			}

			ClosestPointOnPolyInTile(tile, indexPoly, pos, ref closest);
		}

		/// <summary>
		/// Given a point, find the closest point on that poly.
		/// </summary>
		/// <param name="tile">The current tile</param>
		/// <param name="indexPoly">The current poly's index</param>
		/// <param name="pos">The current position</param>
		/// <param name="closest">Reference to the closest point</param>
		public static void ClosestPointOnPolyInTile(MeshTile tile, int indexPoly, Vector3 pos, ref Vector3 closest)
		{
			Poly poly = tile.Polys[indexPoly];

			//Off-mesh connections don't have detail polygons
			if (tile.Polys[indexPoly].PolyType == PolygonType.OffMeshConnection)
			{
				ClosestPointOnPolyOffMeshConnection(tile, poly, pos, out closest);
				return;
			}

			ClosestPointOnPolyBoundary(tile, poly, pos, out closest);

			float h;
			if (ClosestHeight(tile, indexPoly, pos, out h))
				closest.Y = h;
		}

		/// <summary>
		/// Given a point, find the closest point on that poly.
		/// </summary>
		/// <param name="tile">The current tile.</param>
		/// <param name="poly">The current polygon.</param>
		/// <param name="pos">The current position</param>
		/// <param name="closest">Reference to the closest point</param>
		public static void ClosestPointOnPolyBoundary(MeshTile tile, Poly poly, Vector3 pos, out Vector3 closest)
		{
			//Clamp point to be inside the polygon
			Vector3[] verts = new Vector3[PathfinderCommon.VERTS_PER_POLYGON];
			float[] edgeDistance = new float[PathfinderCommon.VERTS_PER_POLYGON];
			float[] edgeT = new float[PathfinderCommon.VERTS_PER_POLYGON];
			int numPolyVerts = poly.VertCount;
			for (int i = 0; i < numPolyVerts; i++)
				verts[i] = tile.Verts[poly.Verts[i]];

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

		/// <summary>
		/// Find the distance from a point to a triangle.
		/// </summary>
		/// <param name="tile">Current mesh tile</param>
		/// <param name="indexPoly">Current polygon's index</param>
		/// <param name="pos">Current position</param>
		/// <param name="h">Resulting height</param>
		/// <returns>True, if a height is found. False, if otherwise.</returns>
		public static bool ClosestHeight(MeshTile tile, int indexPoly, Vector3 pos, out float h)
		{
			Poly poly = tile.Polys[indexPoly];
			PolyMeshDetail.MeshData pd = tile.DetailMeshes[indexPoly];

			//find height at the location
			for (int j = 0; j < tile.DetailMeshes[indexPoly].TriangleCount; j++)
			{
				PolyMeshDetail.TriangleData t = tile.DetailTris[pd.TriangleIndex + j];
				Vector3[] v = new Vector3[3];

				for (int k = 0; k < 3; k++)
				{
					if (t[k] < poly.VertCount)
						v[k] = tile.Verts[poly.Verts[t[k]]];
					else
						v[k] = tile.DetailVerts[pd.VertexIndex + (t[k] - poly.VertCount)];
				}

				if (MathHelper.Distance.PointToTriangle(pos, v[0], v[1], v[2], out h))
					return true;
			}

			h = float.MaxValue;
			return false;
		}

		/// <summary>
		/// Find the closest point on an offmesh connection, which is in between the two points.
		/// </summary>
		/// <param name="tile">Current mesh tile.</param>
		/// <param name="poly">Current polygon</param>
		/// <param name="pos">Current position</param>
		/// <param name="closest">Resulting point that is closest.</param>
		public static void ClosestPointOnPolyOffMeshConnection(MeshTile tile, Poly poly, Vector3 pos, out Vector3 closest)
		{
			Vector3 v0 = tile.Verts[poly.Verts[0]];
			Vector3 v1 = tile.Verts[poly.Verts[1]];
			float d0 = (pos - v0).Length();
			float d1 = (pos - v1).Length();
			float u = d0 / (d0 + d1);
			closest = Vector3.Lerp(v0, v1, u);
		}

		/// <summary>
		/// Generate an accurate sample of random points in the convex polygon and pick a point.
		/// </summary>
		/// <param name="pts">The polygon's points data</param>
		/// <param name="npts">The number of points</param>
		/// <param name="areas">The triangle areas</param>
		/// <param name="s">A random float</param>
		/// <param name="t">Another random float</param>
		/// <param name="pt">The resulting point</param>
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


		/// <summary>
		/// Contains information about a navigation mesh
		/// </summary>
		public class NavMeshInfo
		{
			public int X;
			public int Y;
			public int Layer;
			public uint UserId;
			public int PolyCount;
			public int VertCount;
			public int MaxLinkCount;

			public int DetailMeshCount;
			public int DetailVertCount;
			public int DetailTriCount;

			public int BvNodeCount;

			public int OffMeshConCount;
			public int OffMeshBase; //index of first polygon which is off-mesh connection

			public float WalkableHeight;
			public float WalkableRadius;
			public float WalkableClimb;
			public BBox3 Bounds;
			public float BvQuantFactor;

			/// <summary>
			/// Gets a serialized JSON object
			/// </summary>
			/*public JObject JSONObject
			{
				get
				{
					return new JObject(
						new JProperty("x", X),
						new JProperty("y", Y),
						new JProperty("layer", Layer),
						new JProperty("userId", UserId),
						new JProperty("polyCount", PolyCount),
						new JProperty("vertCount", VertCount),
						new JProperty("maxLinkCount", MaxLinkCount)
					);
				}
			}*/
		}
	}
}
