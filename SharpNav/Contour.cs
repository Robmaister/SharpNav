#region License
/**
 * Copyright (c) 2013 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
 * Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE
 */
#endregion

using System;
using System.Runtime.InteropServices;

namespace SharpNav
{
	/// <summary>
	/// A contour is formed from a region.
	/// </summary>
	public class Contour
	{
		//TODO properly encapsulate

		//simplified vertices have much less edges
		public SimplifiedVertex[] Vertices;

		//raw vertices derived directly from CompactHeightfield
		public RawVertex[] RawVertices;

		public int RegionId;
		public AreaFlags Area;

		//flags used in the build process
		private const int VertexBorderFlag = 0x10000;
		private const int AreaBorderFlag = 0x20000;

		//applied to region id field of contour vertices in order to extract region id
		private const int ContourRegionMask = 0xffff;

		public static void SetBorderVertex(ref int region)
		{
			region |= VertexBorderFlag;
		}

		public static void SetAreaBorder(ref int region)
		{
			region |= AreaBorderFlag;
		}

		public static bool IsBorderVertex(int r)
		{
			return (r & VertexBorderFlag) != 0;
		}

		public static bool IsAreaBorder(int r)
		{
			return (r & AreaBorderFlag) != 0;
		}

		public static bool IsSameArea(int region1, int region2)
		{
			return (region1 & AreaBorderFlag) == (region2 & AreaBorderFlag);
		}

		public static int ExtractRegionId(int r)
		{
			return r & ContourRegionMask;
		}

		public static bool IsSameRegion(int region1, int region2)
		{
			return ExtractRegionId(region1) == ExtractRegionId(region2);
		}

		public static bool CanTessellateWallEdges(ContourBuildFlags buildFlags)
		{
			return (buildFlags & ContourBuildFlags.TessellateWallEdges) != 0;
		}

		public static bool CanTessellateAreaEdges(ContourBuildFlags buildFlags)
		{
			return (buildFlags & ContourBuildFlags.TessellateAreaEdges) != 0;
		}

		public static bool CanTessellateEitherWallOrAreaEdges(ContourBuildFlags buildFlags)
		{
			return (buildFlags & (ContourBuildFlags.TessellateWallEdges | ContourBuildFlags.TessellateAreaEdges)) != 0;
		}

		public static int GetNewRegion(int region1, int region2)
		{
			return (region1 & (ContourRegionMask | AreaBorderFlag)) | (region2 & VertexBorderFlag);
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct RawVertex
		{
			public int X;
			public int Y;
			public int Z;
			public int RegionId;

			public RawVertex(int x, int y, int z, int region)
			{
				this.X = x;
				this.Y = y;
				this.Z = z;
				this.RegionId = region;
			}
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct SimplifiedVertex
		{
			public int X;
			public int Y;
			public int Z;
			public int RawVertexIndex;

			public SimplifiedVertex(int x, int y, int z, int rawVertex)
			{
				this.X = x;
				this.Y = y;
				this.Z = z;
				this.RawVertexIndex = rawVertex;
			}

			public SimplifiedVertex(RawVertex rawVert, int index)
			{
				this.X = rawVert.X;
				this.Y = rawVert.Y;
				this.Z = rawVert.Z;
				this.RawVertexIndex = index;
			}

			public static bool IsLeft(ref Contour.SimplifiedVertex a, ref Contour.SimplifiedVertex b, ref Contour.SimplifiedVertex c)
			{
				int area;
				Area2D(ref a, ref b, ref c, out area);
				return area < 0;
			}

			public static bool IsLeftOn(ref Contour.SimplifiedVertex a, ref Contour.SimplifiedVertex b, ref Contour.SimplifiedVertex c)
			{
				int area;
				Area2D(ref a, ref b, ref c, out area);
				return area <= 0;
			}

			/// <summary>
			/// True if and only if points A, B, and C are collinear.
			/// </summary>
			/// <param name="a">Point A.</param>
			/// <param name="b">Point B.</param>
			/// <param name="c">Point C.</param>
			/// <returns>A value indicating whether the points are collinear.</returns>
			public static bool IsCollinear(ref Contour.SimplifiedVertex a, ref Contour.SimplifiedVertex b, ref Contour.SimplifiedVertex c)
			{
				int area;
				Area2D(ref a, ref b, ref c, out area);
				return area == 0;
			}

			/// <summary>
			/// Gets the 2D area of the triangle ABC.
			/// </summary>
			/// <param name="a">Point A of triangle ABC.</param>
			/// <param name="b">Point B of triangle ABC.</param>
			/// <param name="c">Point C of triangle ABC.</param>
			/// <param name="area">The 2D area of the triangle.</param>
			public static void Area2D(ref Contour.SimplifiedVertex a, ref Contour.SimplifiedVertex b, ref Contour.SimplifiedVertex c, out int area)
			{
				area = (b.X - a.X) * (c.Z - a.Z) - (c.X - a.X) * (b.Z - a.Z);
			}

			/// <summary>
			/// Compares vertex equality in 2D.
			/// </summary>
			/// <param name="a">A vertex.</param>
			/// <param name="b">Another vertex.</param>
			/// <returns>A value indicating whether the X and Z components of both vertices are equal.</returns>
			public static bool Equal2D(ref Contour.SimplifiedVertex a, ref Contour.SimplifiedVertex b)
			{
				return a.X == b.X && a.Z == b.Z;
			}

			/// <summary>
			/// True if and only if segments AB and CD intersect, properly or improperly.
			/// </summary>
			/// <param name="a">Point A of segment AB.</param>
			/// <param name="b">Point B of segment AB.</param>
			/// <param name="c">Point C of segment CD.</param>
			/// <param name="d">Point D of segment CD.</param>
			/// <returns>A value indicating whether segments AB and CD intersect.</returns>
			public static bool Intersect(ref Contour.SimplifiedVertex a, ref Contour.SimplifiedVertex b, ref Contour.SimplifiedVertex c, ref Contour.SimplifiedVertex d)
			{
				if (IntersectProp(ref a, ref b, ref c, ref d))
					return true;
				else if (IsBetween(ref a, ref b, ref c)
					|| IsBetween(ref a, ref b, ref d)
					|| IsBetween(ref c, ref d, ref a)
					|| IsBetween(ref c, ref d, ref b))
					return true;
				else
					return false;
			}

			/// <summary>
			/// True if and only if segments AB and CD intersect properly.
			/// </summary>
			/// <remarks>
			/// Proper intersection: A point interior to both segments is shared. Properness determined by strict leftness.
			/// </remarks>
			/// <param name="a">Point A of segment AB.</param>
			/// <param name="b">Point B of segment AB.</param>
			/// <param name="c">Point C of segment CD.</param>
			/// <param name="d">Point D of segment CD.</param>
			/// <returns>A value indicating whether segements AB and CD are intersecting properly.</returns>
			public static bool IntersectProp(ref Contour.SimplifiedVertex a, ref Contour.SimplifiedVertex b, ref Contour.SimplifiedVertex c, ref Contour.SimplifiedVertex d)
			{
				//eliminate improper cases
				if (IsCollinear(ref a, ref b, ref c)
					|| IsCollinear(ref a, ref b, ref d)
					|| IsCollinear(ref c, ref d, ref a)
					|| IsCollinear(ref c, ref d, ref b))
					return false;

				return (IsLeft(ref a, ref b, ref c) ^ IsLeft(ref a, ref b, ref d))
					&& (IsLeft(ref c, ref d, ref a) ^ IsLeft(ref c, ref d, ref b));
			}

			/// <summary>
			/// True if and only if A, B, and C are collinear and point C lies on closed segment AB
			/// </summary>
			/// <param name="a">Point A of segment AB.</param>
			/// <param name="b">Point B of segment AB.</param>
			/// <param name="c">Point C.</param>
			/// <returns>A value indicating whether the three points are collinear with C in the middle.</returns>
			public static bool IsBetween(ref Contour.SimplifiedVertex a, ref Contour.SimplifiedVertex b, ref Contour.SimplifiedVertex c)
			{
				if (!IsCollinear(ref a, ref b, ref c))
					return false;

				if (a.X != b.X)
					return ((a.X <= c.X) && (c.X <= b.X)) || ((a.X >= c.X) && (c.X >= b.X));
				else
					return ((a.Z <= c.Z) && (c.Z <= b.Z)) || ((a.Z >= c.Z) && (c.Z >= b.Z));
			}

			/// <summary>
			/// true if and only if (v[i], v[j]) is a proper internal diagonal of polygon
			/// </summary>
			public static bool Diagonal(int i, int j, int n, Contour.SimplifiedVertex[] verts, int[] indices)
			{
				return InCone(i, j, n, verts, indices) && Diagonalie(i, j, n, verts, indices);
			}

			/// <summary>
			/// true if and only if diagonal (i, j) is strictly internal to polygon 
			/// in neighborhood of i endpoint
			/// </summary>
			public static bool InCone(int i, int j, int n, Contour.SimplifiedVertex[] verts, int[] indices)
			{
				int pi = indices[i] & 0x0fffffff;
				int pj = indices[j] & 0x0fffffff;
				int pi1 = indices[Next(i, n)] & 0x0fffffff;
				int pin1 = indices[Prev(i, n)] & 0x0fffffff;

				//if P[i] is convex vertex (i + 1 left or on (i - 1, i))
				if (IsLeftOn(ref verts[pin1], ref verts[pi], ref verts[pi1]))
					return IsLeft(ref verts[pi], ref verts[pj], ref verts[pin1]) && IsLeft(ref verts[pj], ref verts[pi], ref verts[pi1]);

				//assume (i - 1, i, i + 1) not collinear
				return !(IsLeftOn(ref verts[pi], ref verts[pj], ref verts[pi1]) && IsLeftOn(ref verts[pj], ref verts[pi], ref verts[pin1]));
			}

			/// <summary>
			/// true if and only if (v[i], v[j]) is internal or external diagonal
			/// ignoring edges incident to v[i] or v[j]
			/// </summary>
			public static bool Diagonalie(int i, int j, int n, SimplifiedVertex[] verts, int[] indices)
			{
				int d0 = indices[i] & 0x0fffffff;
				int d1 = indices[j] & 0x0fffffff;

				//for each edge (k, k + 1)
				for (int k = 0; k < n; k++)
				{
					int k1 = Next(k, n);

					//skip edges incident to i or j
					if (!((k == i) || (k1 == i) || (k == j) || (k1 == j)))
					{
						int p0 = indices[k] & 0x0fffffff;
						int p1 = indices[k1] & 0x0fffffff;

						if (Equal2D(ref verts[d0], ref verts[p0]) || Equal2D(ref verts[d1], ref verts[p0]) || Equal2D(ref verts[d0], ref verts[p1]) || Equal2D(ref verts[d1], ref verts[p1]))
							continue;

						if (Intersect(ref verts[d0], ref verts[d1], ref verts[p0], ref verts[p1]))
							return false;
					}
				}

				return true;
			}

			//HACK this is also in NavMesh, find a good place to move.
			private static int Prev(int i, int n) { return i - 1 >= 0 ? i - 1 : n - 1; }
			private static int Next(int i, int n) { return i + 1 < n ? i + 1 : 0; }
		}
	}
}
