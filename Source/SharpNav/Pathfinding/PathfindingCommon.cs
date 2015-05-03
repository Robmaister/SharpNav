// Copyright (c) 2013-2015 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
// Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE

using System;

using SharpNav.Geometry;

#if MONOGAME
using Vector3 = Microsoft.Xna.Framework.Vector3;
#elif OPENTK
using Vector3 = OpenTK.Vector3;
#elif SHARPDX
using Vector3 = SharpDX.Vector3;
#endif

namespace SharpNav.Pathfinding
{
	/// <summary>
	/// Store constants, structs, methods in this single class so that other classes can access this information.
	/// </summary>
	public class PathfindingCommon
	{
		public const int VERTS_PER_POLYGON = 6; //max number of vertices

		public const int STRAIGHTPATH_START = 0x01; //vertex is in start position of path
		public const int STRAIGHTPATH_END = 0x02; //vertex is in end position of path
		public const int STRAIGHTPATH_OFFMESH_CONNECTION = 0x04; //vertex is at start of an off-mesh connection

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
		}
	}
}
