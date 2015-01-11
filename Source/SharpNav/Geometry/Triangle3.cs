// Copyright (c) 2013-2015 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
// Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE

using System;
using System.Runtime.InteropServices;

#if MONOGAME
using Vector3 = Microsoft.Xna.Framework.Vector3;
#elif OPENTK
using Vector3 = OpenTK.Vector3;
#elif SHARPDX
using Vector3 = SharpDX.Vector3;
#endif

namespace SharpNav.Geometry
{
	/// <summary>
	/// A 3d triangle.
	/// </summary>
	[Serializable]
	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public struct Triangle3 : IEquatable<Triangle3>
	{
		#region Fields

		/// <summary>
		/// The first point.
		/// </summary>
		public Vector3 A;

		/// <summary>
		/// The second point.
		/// </summary>
		public Vector3 B;

		/// <summary>
		/// The third point.
		/// </summary>
		public Vector3 C;

		#endregion

		#region Constructors

		/// <summary>
		/// Initializes a new instance of the <see cref="Triangle3"/> struct.
		/// </summary>
		/// <param name="a">The second point.</param>
		/// <param name="b">The first point.</param>
		/// <param name="c">The third point.</param>
		public Triangle3(Vector3 a, Vector3 b, Vector3 c)
		{
			A = a;
			B = b;
			C = c;
		}

		#endregion

		#region Properties

		/// <summary>
		/// Gets the directed line segment from <see cref="A"/> to <see cref="B"/>.
		/// </summary>
		public Vector3 AB
		{
			get
			{
				Vector3 result;
				Vector3.Subtract(ref B, ref A, out result);
				return result;
			}
		}

		/// <summary>
		/// Gets the directed line segment from <see cref="A"/> to <see cref="C"/>.
		/// </summary>
		public Vector3 AC
		{
			get
			{
				Vector3 result;
				Vector3.Subtract(ref C, ref A, out result);
				return result;
			}
		}

		/// <summary>
		/// Gets the directed line segment from <see cref="B"/> to <see cref="A"/>.
		/// </summary>
		public Vector3 BA
		{
			get
			{
				Vector3 result;
				Vector3.Subtract(ref A, ref B, out result);
				return result;
			}
		}

		/// <summary>
		/// Gets the directed line segment from <see cref="B"/> to <see cref="C"/>.
		/// </summary>
		public Vector3 BC
		{
			get
			{
				Vector3 result;
				Vector3.Subtract(ref C, ref B, out result);
				return result;
			}
		}

		/// <summary>
		/// Gets the directed line segment from <see cref="C"/> to <see cref="A"/>.
		/// </summary>
		public Vector3 CA
		{
			get
			{
				Vector3 result;
				Vector3.Subtract(ref A, ref C, out result);
				return result;
			}
		}

		/// <summary>
		/// Gets the directed line segment from <see cref="C"/> to <see cref="B"/>.
		/// </summary>
		public Vector3 CB
		{
			get
			{
				Vector3 result;
				Vector3.Subtract(ref B, ref C, out result);
				return result;
			}
		}

		/// <summary>
		/// Gets the area of the triangle.
		/// </summary>
		public float Area
		{
			get
			{
				return Vector3.Cross(AB, AC).Length() * 0.5f;
			}
		}

		/// <summary>
		/// Gets the perimeter of the triangle.
		/// </summary>
		public float Perimeter
		{
			get
			{
				return AB.Length() + AC.Length() + BC.Length();
			}
		}

		/// <summary>
		/// Gets the centroid of the triangle.
		/// </summary>
		public Vector3 Centroid
		{
			get
			{
				const float OneThird = 1f / 3f;
				return A * OneThird + B * OneThird + C * OneThird;
			}
		}

		/// <summary>
		/// Gets the <see cref="Triangle3"/>'s surface normal. Assumes clockwise ordering of A, B, and C.
		/// </summary>
		public Vector3 Normal
		{
			get
			{
				return Vector3.Normalize(Vector3.Cross(AB, AC));
			}
		}

		#endregion

		#region Operators

		/// <summary>
		/// Compares two <see cref="Triangle3"/>'s for equality.
		/// </summary>
		/// <param name="left">The first triangle.</param>
		/// <param name="right">The second triangle.</param>
		/// <returns>A value indicating whether the two triangles are equal.</returns>
		public static bool operator ==(Triangle3 left, Triangle3 right)
		{
			return left.Equals(right);
		}

		/// <summary>
		/// Compares two <see cref="Triangle3"/>'s for inequality.
		/// </summary>
		/// <param name="left">The first triangle.</param>
		/// <param name="right">The second triangle.</param>
		/// <returns>A value indicating whether the two triangles are not equal.</returns>
		public static bool operator !=(Triangle3 left, Triangle3 right)
		{
			return !left.Equals(right);
		}

		#endregion

		#region Methods

		/// <summary>
		/// Calculates the bounding box of a triangle.
		/// </summary>
		/// <param name="tri">A triangle.</param>
		/// <returns>The triangle's bounding box.</returns>
		public static BBox3 GetBoundingBox(Triangle3 tri)
		{
			BBox3 b;
			GetBoundingBox(ref tri, out b);
			return b;
		}

		/// <summary>
		/// Calculates the bounding box of a triangle.
		/// </summary>
		/// <param name="tri">A triangle.</param>
		/// <param name="bbox">The triangle's bounding box.</param>
		public static void GetBoundingBox(ref Triangle3 tri, out BBox3 bbox)
		{
			GetBoundingBox(ref tri.A, ref tri.B, ref tri.C, out bbox);
		}

		/// <summary>
		/// Calculates the bounding box of a triangle from its vertices.
		/// </summary>
		/// <param name="a">The first vertex.</param>
		/// <param name="b">The second vertex.</param>
		/// <param name="c">The third vertex.</param>
		/// <param name="bbox">The bounding box between the points.</param>
		public static void GetBoundingBox(ref Vector3 a, ref Vector3 b, ref Vector3 c, out BBox3 bbox)
		{
			Vector3 min = a, max = a;

			if (b.X < min.X) min.X = b.X;
			if (b.Y < min.Y) min.Y = b.Y;
			if (b.Z < min.Z) min.Z = b.Z;
			if (c.X < min.X) min.X = c.X;
			if (c.Y < min.Y) min.Y = c.Y;
			if (c.Z < min.Z) min.Z = c.Z;

			if (b.X > max.X) max.X = b.X;
			if (b.Y > max.Y) max.Y = b.Y;
			if (b.Z > max.Z) max.Z = b.Z;
			if (c.X > max.X) max.X = c.X;
			if (c.Y > max.Y) max.Y = c.Y;
			if (c.Z > max.Z) max.Z = c.Z;

			bbox.Min = min;
			bbox.Max = max;
		}

		/// <summary>
		/// Gets the area of the triangle projected onto the XZ-plane.
		/// </summary>
		/// <param name="a">The first point.</param>
		/// <param name="b">The second point.</param>
		/// <param name="c">The third point.</param>
		/// <param name="area">The calculated area.</param>
		public static void Area2D(ref Vector3 a, ref Vector3 b, ref Vector3 c, out float area)
		{
			float abx = b.X - a.X;
			float abz = b.Z - a.Z;
			float acx = c.X - a.X;
			float acz = c.Z - a.Z;
			area = acx * abz - abx * acz;
		}

		/// <summary>
		/// Gets the area of the triangle projected onto the XZ-plane.
		/// </summary>
		/// <param name="a">The first point.</param>
		/// <param name="b">The second point.</param>
		/// <param name="c">The third point.</param>
		/// <returns>The calculated area.</returns>
		public static float Area2D(Vector3 a, Vector3 b, Vector3 c)
		{
			float result;
			Area2D(ref a, ref b, ref c, out result);
			return result;
		}

		/// <summary>
		/// Checks for equality with another <see cref="Triangle3"/>.
		/// </summary>
		/// <param name="other">The other triangle.</param>
		/// <returns>A value indicating whether other is equivalent to the triangle.</returns>
		public bool Equals(Triangle3 other)
		{
			return
				A.Equals(other.A) &&
				B.Equals(other.B) &&
				C.Equals(other.C);
		}

		/// <summary>
		/// Checks for equality with another object.
		/// </summary>
		/// <param name="obj">The other object.</param>
		/// <returns>A value indicating whether other is equivalent to the triangle.</returns>
		public override bool Equals(object obj)
		{
			Triangle3? other = obj as Triangle3?;

			if (other.HasValue)
				return this.Equals(other.Value);
			else
				return false;
		}

		/// <summary>
		/// Gets a unique hash code for the triangle.
		/// </summary>
		/// <returns>A hash code.</returns>
		public override int GetHashCode()
		{
			//see http://stackoverflow.com/a/263416/1122135
			int hash = 17;
			hash = hash * 23 + A.GetHashCode();
			hash = hash * 23 + B.GetHashCode();
			hash = hash * 23 + C.GetHashCode();
			return hash;
		}

		/// <summary>
		/// Converts the triangle's data into a human-readable format.
		/// </summary>
		/// <returns>A string containing the triangle's data.</returns>
		public override string ToString()
		{
			return "(" + A.ToString() + ", " + B.ToString() + ", " + C.ToString() + ")";
		}

		#endregion
	}
}
