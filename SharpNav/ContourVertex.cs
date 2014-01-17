using System;
using System.Runtime.InteropServices;

namespace SharpNav
{
	/// <summary>
	/// A <see cref="ContourVertex"/> is a vertex that stores 3 integer coordinates and a region ID, and is used to build <see cref="Contour"/>s.
	/// </summary>
	[StructLayout(LayoutKind.Sequential)]
	public struct ContourVertex
	{
		/// <summary>
		/// The X coordinate.
		/// </summary>
		public int X;

		/// <summary>
		/// The Y coordinate.
		/// </summary>
		public int Y;

		/// <summary>
		/// The Z coordinate.
		/// </summary>
		public int Z;

		/// <summary>
		/// The region that the vertex belongs to.
		/// </summary>
		public int RegionId;

		/// <summary>
		/// Initializes a new instance of the <see cref="ContourVertex"/> struct.
		/// </summary>
		/// <param name="x">The X coordinate.</param>
		/// <param name="y">The Y coordinate.</param>
		/// <param name="z">The Z coordinate.</param>
		/// <param name="region">The region ID.</param>
		public ContourVertex(int x, int y, int z, int region)
		{
			this.X = x;
			this.Y = y;
			this.Z = z;
			this.RegionId = region;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="ContourVertex"/> struct as a copy.
		/// </summary>
		/// <param name="vert">The original vertex.</param>
		/// <param name="index">The index of the original vertex, which is temporarily stored in the <see cref="RegionId"/> field.</param>
		public ContourVertex(ContourVertex vert, int index)
		{
			this.X = vert.X;
			this.Y = vert.Y;
			this.Z = vert.Z;
			this.RegionId = index;
		}

		/// <summary>
		/// Gets the leftness of a triangle formed from 3 contour vertices.
		/// </summary>
		/// <param name="a">The first vertex.</param>
		/// <param name="b">The second vertex.</param>
		/// <param name="c">The third vertex.</param>
		/// <returns>A value indicating the leftness of the triangle.</returns>
		public static bool IsLeft(ref ContourVertex a, ref ContourVertex b, ref ContourVertex c)
		{
			int area;
			Area2D(ref a, ref b, ref c, out area);
			return area < 0;
		}

		/// <summary>
		/// Gets the leftness (left or on) of a triangle formed from 3 contour vertices.
		/// </summary>
		/// <param name="a">The first vertex.</param>
		/// <param name="b">The second vertex.</param>
		/// <param name="c">The third vertex.</param>
		/// <returns>A value indicating whether the triangle is left or on.</returns>
		public static bool IsLeftOn(ref ContourVertex a, ref ContourVertex b, ref ContourVertex c)
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
		public static bool IsCollinear(ref ContourVertex a, ref ContourVertex b, ref ContourVertex c)
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
		public static void Area2D(ref ContourVertex a, ref ContourVertex b, ref ContourVertex c, out int area)
		{
			area = (b.X - a.X) * (c.Z - a.Z) - (c.X - a.X) * (b.Z - a.Z);
		}

		/// <summary>
		/// Compares vertex equality in 2D.
		/// </summary>
		/// <param name="a">A vertex.</param>
		/// <param name="b">Another vertex.</param>
		/// <returns>A value indicating whether the X and Z components of both vertices are equal.</returns>
		public static bool Equal2D(ref ContourVertex a, ref ContourVertex b)
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
		public static bool Intersect(ref ContourVertex a, ref ContourVertex b, ref ContourVertex c, ref ContourVertex d)
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
		public static bool IntersectProp(ref ContourVertex a, ref ContourVertex b, ref ContourVertex c, ref ContourVertex d)
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
		public static bool IsBetween(ref ContourVertex a, ref ContourVertex b, ref ContourVertex c)
		{
			if (!IsCollinear(ref a, ref b, ref c))
				return false;

			if (a.X != b.X)
				return ((a.X <= c.X) && (c.X <= b.X)) || ((a.X >= c.X) && (c.X >= b.X));
			else
				return ((a.Z <= c.Z) && (c.Z <= b.Z)) || ((a.Z >= c.Z) && (c.Z >= b.Z));
		}
	}
}
