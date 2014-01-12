#region License
/**
 * Copyright (c) 2013-2014 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
 * Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace SharpNav
{
	/// <summary>
	/// A contour is formed from a region.
	/// </summary>
	public class Contour
	{
		//simplified vertices have much less edges
		public Vertex[] Vertices;

		//raw vertices derived directly from CompactHeightfield
		public RawVertex[] RawVertices;

		//applied to region id field of contour vertices in order to extract region id

		private int regionId;
		private AreaFlags area;

		public Contour(IEnumerable<Vertex> simplified, IEnumerable<RawVertex> verts, int reg, AreaFlags area, int borderSize)
		{
			Vertices = simplified.ToArray();
			RawVertices = verts.ToArray();
			regionId = reg;
			this.area = area;

			//remove offset
			if (borderSize > 0)
			{
				for (int j = 0; j < Vertices.Length; j++)
				{
					Vertices[j].X -= borderSize;
					Vertices[j].Z -= borderSize;
				}

				for (int j = 0; j < RawVertices.Length; j++)
				{
					RawVertices[j].X -= borderSize;
					RawVertices[j].Z -= borderSize;
				}
			}
		}

		//TODO operator overload == and != with null?
		public bool IsNull
		{
			get
			{
				if (Vertices.Length < 3)
					return true;

				return false;
			}
		}

		public AreaFlags Area
		{
			get
			{
				return area;
			}
		}

		public int RegionId
		{
			get
			{
				return regionId;
			}
		}

		/// <summary>
		/// Gets the 2D area of the contour. A positive area means the contour is going forwards, a negative
		/// area maens it is going backwards.
		/// </summary>
		public int Area2D
		{
			get
			{
				int area = 0;
				for (int i = 0, j = Vertices.Length - 1; i < Vertices.Length; j = i++)
				{
					Vertex vi = Vertices[i], vj = Vertices[j];
					area += vi.X * vj.Z - vj.X * vi.Z;
				}

				return (area + 1) / 2; 
			}
		}

		public void Merge(Contour contour)
		{
			int lengthA = Vertices.Length;
			int lengthB = contour.Vertices.Length;

			int ia, ib;
			GetClosestIndices(this, contour, out ia, out ib);

			//create a list with the capacity set to the max number of possible verts to avoid expanding the list.
			var newVerts = new List<Contour.Vertex>(Vertices.Length + contour.Vertices.Length + 2);

			//copy contour A
			for (int i = 0; i <= lengthA; i++)
				newVerts.Add(Vertices[(ia + i) % lengthA]);

			//add contour B (other contour) to contour A (this contour)
			for (int i = 0; i <= lengthB; i++)
				newVerts.Add(contour.Vertices[(ib + i) % lengthB]);

			Vertices = newVerts.ToArray();
		}

		/// <summary>
		/// Required to find closest indices for merging.
		/// </summary>
		/// <param name="vertsA">First set of vertices</param>
		/// <param name="vertsB">Second set of vertices</param>
		/// <param name="indexA">First index</param>
		/// <param name="indexB">Second index</param>
		private static void GetClosestIndices(Contour a, Contour b, out int indexA, out int indexB)
		{
			int closestDistance = int.MaxValue;
			int lengthA = a.Vertices.Length;
			int lengthB = b.Vertices.Length;

			indexA = -1;
			indexB = -1;

			for (int i = 0; i < lengthA; i++)
			{
				int vertA = i;
				int vertANext = (i + 1) % lengthA;
				int vertAPrev = (i + lengthA - 1) % lengthA;

				for (int j = 0; j < lengthB; j++)
				{
					int vertB = j;

					//vertB must be infront of vertA
					if (Contour.Vertex.IsLeft(ref a.Vertices[vertAPrev], ref a.Vertices[vertA], ref b.Vertices[vertB]) && Contour.Vertex.IsLeft(ref a.Vertices[vertA], ref a.Vertices[vertANext], ref b.Vertices[vertB]))
					{
						int dx = b.Vertices[vertB].X - a.Vertices[vertA].X;
						int dz = b.Vertices[vertB].Z - a.Vertices[vertA].Z;
						int tempDist = dx * dx + dz * dz;
						if (tempDist < closestDistance)
						{
							indexA = i;
							indexB = j;
							closestDistance = tempDist;
						}
					}
				}
			}
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
		public struct Vertex
		{
			public int X;
			public int Y;
			public int Z;
			public int RawVertexIndex;

			public Vertex(int x, int y, int z, int rawVertex)
			{
				this.X = x;
				this.Y = y;
				this.Z = z;
				this.RawVertexIndex = rawVertex;
			}

			public Vertex(RawVertex rawVert, int index)
			{
				this.X = rawVert.X;
				this.Y = rawVert.Y;
				this.Z = rawVert.Z;
				this.RawVertexIndex = index;
			}

			public static bool IsLeft(ref Contour.Vertex a, ref Contour.Vertex b, ref Contour.Vertex c)
			{
				int area;
				Area2D(ref a, ref b, ref c, out area);
				return area < 0;
			}

			public static bool IsLeftOn(ref Contour.Vertex a, ref Contour.Vertex b, ref Contour.Vertex c)
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
			public static bool IsCollinear(ref Contour.Vertex a, ref Contour.Vertex b, ref Contour.Vertex c)
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
			public static void Area2D(ref Contour.Vertex a, ref Contour.Vertex b, ref Contour.Vertex c, out int area)
			{
				area = (b.X - a.X) * (c.Z - a.Z) - (c.X - a.X) * (b.Z - a.Z);
			}

			/// <summary>
			/// Compares vertex equality in 2D.
			/// </summary>
			/// <param name="a">A vertex.</param>
			/// <param name="b">Another vertex.</param>
			/// <returns>A value indicating whether the X and Z components of both vertices are equal.</returns>
			public static bool Equal2D(ref Contour.Vertex a, ref Contour.Vertex b)
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
			public static bool Intersect(ref Contour.Vertex a, ref Contour.Vertex b, ref Contour.Vertex c, ref Contour.Vertex d)
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
			public static bool IntersectProp(ref Contour.Vertex a, ref Contour.Vertex b, ref Contour.Vertex c, ref Contour.Vertex d)
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
			public static bool IsBetween(ref Contour.Vertex a, ref Contour.Vertex b, ref Contour.Vertex c)
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
			public static bool Diagonal(int i, int j, int n, Contour.Vertex[] verts, int[] indices)
			{
				return InCone(i, j, n, verts, indices) && Diagonalie(i, j, n, verts, indices);
			}

			/// <summary>
			/// true if and only if diagonal (i, j) is strictly internal to polygon 
			/// in neighborhood of i endpoint
			/// </summary>
			public static bool InCone(int i, int j, int n, Contour.Vertex[] verts, int[] indices)
			{
				int pi = RemoveDiagonalFlag(indices[i]);
				int pj = RemoveDiagonalFlag(indices[j]);
				int pi1 = RemoveDiagonalFlag(indices[Next(i, n)]);
				int pin1 = RemoveDiagonalFlag(indices[Prev(i, n)]);

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
			public static bool Diagonalie(int i, int j, int n, Vertex[] verts, int[] indices)
			{
				int d0 = RemoveDiagonalFlag(indices[i]);
				int d1 = RemoveDiagonalFlag(indices[j]);

				//for each edge (k, k + 1)
				for (int k = 0; k < n; k++)
				{
					int k1 = Next(k, n);

					//skip edges incident to i or j
					if (!((k == i) || (k1 == i) || (k == j) || (k1 == j)))
					{
						int p0 = RemoveDiagonalFlag(indices[k]);
						int p1 = RemoveDiagonalFlag(indices[k1]);

						if (Equal2D(ref verts[d0], ref verts[p0]) || Equal2D(ref verts[d1], ref verts[p0]) || Equal2D(ref verts[d0], ref verts[p1]) || Equal2D(ref verts[d1], ref verts[p1]))
							continue;

						if (Intersect(ref verts[d0], ref verts[d1], ref verts[p0], ref verts[p1]))
							return false;
					}
				}

				return true;
			}

			//HACK this is also in NavMesh, find a good place to move.
			private static int RemoveDiagonalFlag(int index) { return index & 0x7fffffff; }
			private static int Prev(int i, int n) { return i - 1 >= 0 ? i - 1 : n - 1; }
			private static int Next(int i, int n) { return i + 1 < n ? i + 1 : 0; }
		}
	}
}
