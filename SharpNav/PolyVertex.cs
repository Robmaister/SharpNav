// Copyright (c) 2014-2015 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
// Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace SharpNav
{
	/// <summary>
	/// A vertex inside a <see cref="PolyMesh"/>.
	/// </summary>
	[StructLayout(LayoutKind.Sequential)]
	public struct PolyVertex : IEquatable<PolyVertex>
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
		/// Initializes a new instance of the <see cref="PolyVertex"/> struct.
		/// </summary>
		/// <param name="x">The X coordinate.</param>
		/// <param name="y">The Y coordinate.</param>
		/// <param name="z">The Z coordinate.</param>
		public PolyVertex(int x, int y, int z)
		{
			X = x;
			Y = y;
			Z = z;
		}

		/// <summary>
		/// Calculates the component-wise minimum of two vertices.
		/// </summary>
		/// <param name="a">A vertex.</param>
		/// <param name="b">Another vertex.</param>
		/// <returns>The component-wise minimum of the two vertices.</returns>
		public static PolyVertex ComponentMin(PolyVertex a, PolyVertex b)
		{
			PolyVertex v;
			ComponentMin(ref a, ref b, out v);
			return v;
		}

		/// <summary>
		/// Calculates the component-wise minimum of two vertices.
		/// </summary>
		/// <param name="a">A vertex.</param>
		/// <param name="b">Another vertex.</param>
		/// <param name="result">The component-wise minimum of the two vertices.</param>
		public static void ComponentMin(ref PolyVertex a, ref PolyVertex b, out PolyVertex result)
		{
			result.X = a.X < b.X ? a.X : b.X;
			result.Y = a.Y < b.Y ? a.Y : b.Y;
			result.Z = a.Z < b.Z ? a.Z : b.Z;
		}

		/// <summary>
		/// Calculates the component-wise maximum of two vertices.
		/// </summary>
		/// <param name="a">A vertex.</param>
		/// <param name="b">Another vertex.</param>
		/// <returns>The component-wise maximum of the two vertices.</returns>
		public static PolyVertex ComponentMax(PolyVertex a, PolyVertex b)
		{
			PolyVertex v;
			ComponentMax(ref a, ref b, out v);
			return v;
		}

		/// <summary>
		/// Calculates the component-wise maximum of two vertices.
		/// </summary>
		/// <param name="a">A vertex.</param>
		/// <param name="b">Another vertex.</param>
		/// <param name="result">The component-wise maximum of the two vertices.</param>
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

			return (!IsLeft(ref a, ref b, ref c) ^ !IsLeft(ref a, ref b, ref d))
				&& (!IsLeft(ref c, ref d, ref a) ^ !IsLeft(ref c, ref d, ref b));
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

		/// <summary>
		/// Compares two vertices for equality.
		/// </summary>
		/// <param name="left">A vertex.</param>
		/// <param name="right">Another vertex.</param>
		/// <returns>A value indicating whether the two vertices are equal.</returns>
		public static bool operator ==(PolyVertex left, PolyVertex right)
		{
			return left.Equals(right);
		}

		/// <summary>
		/// Compares two vertices for inequality.
		/// </summary>
		/// <param name="left">A vertex.</param>
		/// <param name="right">Another vertex.</param>
		/// <returns>A value indicating whether the two vertices are not equal.</returns>
		public static bool operator !=(PolyVertex left, PolyVertex right)
		{
			return !(left == right);
		}

		/// <summary>
		/// Compares another <see cref="PolyVertex"/> with this instance for equality.
		/// </summary>
		/// <param name="other">The other instance.</param>
		/// <returns>A value indicating whether the two vertices are equal.</returns>
		public bool Equals(PolyVertex other)
		{
			return X == other.X && Y == other.Y && Z == other.Z;
		}

		/// <summary>
		/// Compares an object with this instance for equality.
		/// </summary>
		/// <param name="obj">An object.</param>
		/// <returns>A value indicating whether the object is equal to this instance.</returns>
		public override bool Equals(object obj)
		{
			PolyVertex? p = obj as PolyVertex?;
			if (p.HasValue)
				return this.Equals(p.Value);

			return false;
		}

		/// <summary>
		/// Gets a hash code unique to the contents of this instance.
		/// </summary>
		/// <returns>A hash code.</returns>
		public override int GetHashCode()
		{
			//TODO write a better hashcode
			return X.GetHashCode() ^ Y.GetHashCode() ^ Z.GetHashCode();
		}

		/// <summary>
		/// Gets a human-readable version of the vertex.
		/// </summary>
		/// <returns>A string.</returns>
		public override string ToString()
		{
			return "(" + X + ", " + Y + ", " + Z + ")";
		}

		/// <summary>
		/// An implementation of <see cref="IEqualityComparer{T}"/> of <see cref="PolyVertex"/> that allows for the
		/// Y coordinates of two vertices to be within a specified range and still be considered equal.
		/// </summary>
		internal class RoughYEqualityComparer : IEqualityComparer<PolyVertex>
		{
			private const int HashConstX = unchecked((int)0x8da6b343);
			private const int HashConstZ = unchecked((int)0xcb1ab31f);

			private int epsilonY;

			/// <summary>
			/// Initializes a new instance of the <see cref="RoughYEqualityComparer"/> class.
			/// </summary>
			/// <param name="epsilonY">The range of Y values in which two vertices are considered equal.</param>
			public RoughYEqualityComparer(int epsilonY)
			{
				this.epsilonY = epsilonY;
			}

			/// <summary>
			/// Compares two vertices for equality.
			/// </summary>
			/// <param name="left">A vertex.</param>
			/// <param name="right">Another vertex.</param>
			/// <returns>A value indicating whether the two vertices are equal.</returns>
			public bool Equals(PolyVertex left, PolyVertex right)
			{
				return left.X == right.X && (Math.Abs(left.Y - right.Y) <= epsilonY) && left.Z == right.Z;
			}

			/// <summary>
			/// Gets a unique hash code for the contents of a <see cref="PolyVertex"/> instance.
			/// </summary>
			/// <param name="obj">A vertex.</param>
			/// <returns>A hash code.</returns>
			public int GetHashCode(PolyVertex obj)
			{
				return HashConstX * obj.X + HashConstZ * obj.Z;
			}
		}
	}
}
