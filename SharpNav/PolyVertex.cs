using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace SharpNav
{
	[StructLayout(LayoutKind.Sequential)]
	public struct PolyVertex : IEquatable<PolyVertex>
	{
		public int X;
		public int Y;
		public int Z;

		public PolyVertex(int x, int y, int z)
		{
			X = x;
			Y = y;
			Z = z;
		}

		public static PolyVertex ComponentMin(PolyVertex a, PolyVertex b)
		{
			PolyVertex v;
			ComponentMin(ref a, ref b, out v);
			return v;
		}

		public static void ComponentMin(ref PolyVertex a, ref PolyVertex b, out PolyVertex result)
		{
			result.X = a.X < b.X ? a.X : b.X;
			result.Y = a.Y < b.Y ? a.Y : b.Y;
			result.Z = a.Z < b.Z ? a.Z : b.Z;
		}

		public static PolyVertex ComponentMax(PolyVertex a, PolyVertex b)
		{
			PolyVertex v;
			ComponentMax(ref a, ref b, out v);
			return v;
		}

		public static void ComponentMax(ref PolyVertex a, ref PolyVertex b, out PolyVertex result)
		{
			result.X = a.X > b.X ? a.X : b.X;
			result.Y = a.Y > b.Y ? a.Y : b.Y;
			result.Z = a.Z > b.Z ? a.Z : b.Z;
		}

		/// <summary>
		/// Gets the leftness of a triangle formed from 3 contour vertices.
		/// </summary>
		/// <param name="a">The first vertex.</param>
		/// <param name="b">The second vertex.</param>
		/// <param name="c">The third vertex.</param>
		/// <returns>A value indicating the leftness of the triangle.</returns>
		public static bool IsLeft(ref PolyVertex a, ref PolyVertex b, ref PolyVertex c)
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
		public static bool IsLeftOn(ref PolyVertex a, ref PolyVertex b, ref PolyVertex c)
		{
			int area;
			Area2D(ref a, ref b, ref c, out area);
			return area <= 0;
		}

		/// <summary>
		/// Compares vertex equality in 2D.
		/// </summary>
		/// <param name="a">A vertex.</param>
		/// <param name="b">Another vertex.</param>
		/// <returns>A value indicating whether the X and Z components of both vertices are equal.</returns>
		public static bool Equal2D(ref PolyVertex a, ref PolyVertex b)
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
		public static bool Intersect(ref PolyVertex a, ref PolyVertex b, ref PolyVertex c, ref PolyVertex d)
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
		public static bool IntersectProp(ref PolyVertex a, ref PolyVertex b, ref PolyVertex c, ref PolyVertex d)
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
		public static bool IsBetween(ref PolyVertex a, ref PolyVertex b, ref PolyVertex c)
		{
			if (!IsCollinear(ref a, ref b, ref c))
				return false;

			if (a.X != b.X)
				return ((a.X <= c.X) && (c.X <= b.X)) || ((a.X >= c.X) && (c.X >= b.X));
			else
				return ((a.Z <= c.Z) && (c.Z <= b.Z)) || ((a.Z >= c.Z) && (c.Z >= b.Z));
		}

		/// <summary>
		/// True if and only if points A, B, and C are collinear.
		/// </summary>
		/// <param name="a">Point A.</param>
		/// <param name="b">Point B.</param>
		/// <param name="c">Point C.</param>
		/// <returns>A value indicating whether the points are collinear.</returns>
		public static bool IsCollinear(ref PolyVertex a, ref PolyVertex b, ref PolyVertex c)
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
		public static void Area2D(ref PolyVertex a, ref PolyVertex b, ref PolyVertex c, out int area)
		{
			area = (b.X - a.X) * (c.Z - a.Z) - (c.X - a.X) * (b.Z - a.Z);
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

		public bool Equals(PolyVertex other)
		{
			return X == other.X && Y == other.Y && Z == other.Z;
		}

		public static bool operator ==(PolyVertex left, PolyVertex right)
		{
			return left.Equals(right);
		}

		public static bool operator !=(PolyVertex left, PolyVertex right)
		{
			return !(left == right);
		}

		public override bool Equals(object obj)
		{
			PolyVertex? p = obj as PolyVertex?;
			if (p.HasValue)
				return Equals(p.Value);

			return false;
		}

		public override int GetHashCode()
		{
			//TODO write a better hashcode
			return X.GetHashCode() ^ Y.GetHashCode() ^ Z.GetHashCode();
		}

		public override string ToString()
		{
			return "(" + X + ", " + Y + ", " + Z + ")";
		}

		internal class RoughYEqualityComparer : IEqualityComparer<PolyVertex>
		{
			private const int HashConstX = unchecked((int)0x8da6b343);
			private const int HashConstZ = unchecked((int)0xcb1ab31f);

			private float epsilonY;

			public RoughYEqualityComparer(float epsilonY)
			{
				this.epsilonY = epsilonY;
			}

			public bool Equals(PolyVertex left, PolyVertex right)
			{
				return left.X == right.X && (Math.Abs(left.Y - right.Y) <= epsilonY) && left.Z == right.Z;
			}

			public int GetHashCode(PolyVertex obj)
			{
				return HashConstX * obj.X + HashConstZ * obj.Z;
			}
		}
	}
}
