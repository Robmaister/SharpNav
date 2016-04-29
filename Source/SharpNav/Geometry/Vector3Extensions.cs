// Copyright (c) 2013-2015 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
// Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE

using System;
using System.Collections.Generic;

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
	/// A class that provides extension methods to fix discrepancies between Vector3 implementations.
	/// </summary>
	internal static class Vector3Extensions
	{
#if OPENTK

		/// <summary>
		/// Gets the length of a <see cref="Vector3"/>.
		/// </summary>
		/// <param name="v">A vector.</param>
		/// <returns>The length of the vector.</returns>
		internal static float Length(this Vector3 v)
		{
			return v.Length;
		}

		/// <summary>
		/// Gets the squared length of a <see cref="Vector3"/>. This avoids the square root operation
		/// and is suitable for comparisons.
		/// </summary>
		/// <param name="v">A vector.</param>
		/// <returns>The length of the vector.</returns>
		internal static float LengthSquared(this Vector3 v)
		{
			return v.LengthSquared;
		}

#endif

#if UNITY3D
		internal static float Length(this Vector3 v)
		{
			return v.magnitude;
		}

		internal static float LengthSquared(this Vector3 v)
		{
			return v.sqrMagnitude;
		}
#endif

		/// <summary>
		/// Calculates the component-wise minimum of two vectors.
		/// </summary>
		/// <param name="left">A vector.</param>
		/// <param name="right">Another vector.</param>
		/// <param name="result">The component-wise minimum of the two vectors.</param>
		internal static void ComponentMin(ref Vector3 left, ref Vector3 right, out Vector3 result)
		{
#if OPENTK || STANDALONE
			Vector3.ComponentMin(ref left, ref right, out result);
#elif UNITY3D
			result = Vector3.Min(left, right);
#else
			Vector3.Min(ref left, ref right, out result);
#endif
		}

		/// <summary>
		/// Calculates the component-wise maximum of two vectors.
		/// </summary>
		/// <param name="left">A vector.</param>
		/// <param name="right">Another vector.</param>
		/// <param name="result">The component-wise maximum of the two vectors.</param>
		internal static void ComponentMax(ref Vector3 left, ref Vector3 right, out Vector3 result)
		{
#if OPENTK || STANDALONE
			Vector3.ComponentMax(ref left, ref right, out result);
#elif UNITY3D
			result = Vector3.Min(left, right);
#else
			Vector3.Max(ref left, ref right, out result);
#endif
		}

		/// <summary>
		/// Calculates the distance between two points on the XZ plane.
		/// </summary>
		/// <param name="a">A point.</param>
		/// <param name="b">Another point.</param>
		/// <returns>The distance between the two points.</returns>
		internal static float Distance2D(Vector3 a, Vector3 b)
		{
			float result;
			Distance2D(ref a, ref b, out result);
			return result;
		}

		/// <summary>
		/// Calculates the distance between two points on the XZ plane.
		/// </summary>
		/// <param name="a">A point.</param>
		/// <param name="b">Another point.</param>
		/// <param name="dist">The distance between the two points.</param>
		internal static void Distance2D(ref Vector3 a, ref Vector3 b, out float dist)
		{
			float dx = b.X - a.X;
			float dz = b.Z - a.Z;
			dist = (float)Math.Sqrt(dx * dx + dz * dz);
		}

		/// <summary>
		/// Calculates the dot product of two vectors projected onto the XZ plane.
		/// </summary>
		/// <param name="left">A vector.</param>
		/// <param name="right">Another vector</param>
		/// <param name="result">The dot product of the two vectors.</param>
		internal static void Dot2D(ref Vector3 left, ref Vector3 right, out float result)
		{
			result = left.X * right.X + left.Z * right.Z;
		}

		/// <summary>
		/// Calculates the dot product of two vectors projected onto the XZ plane.
		/// </summary>
		/// <param name="left">A vector.</param>
		/// <param name="right">Another vector</param>
		/// <returns>The dot product</returns>
		internal static float Dot2D(ref Vector3 left, ref Vector3 right)
		{
			return left.X * right.X + left.Z * right.Z;
		}

		/// <summary>
		/// Calculates the cross product of two vectors (formed from three points)
		/// </summary>
		/// <param name="p1">The first point</param>
		/// <param name="p2">The second point</param>
		/// <param name="p3">The third point</param>
		/// <returns>The 2d cross product</returns>
		internal static float Cross2D(Vector3 p1, Vector3 p2, Vector3 p3)
		{
			float result;
			Cross2D(ref p1, ref p2, ref p3, out result);
			return result;
		}

		/// <summary>
		/// Calculates the cross product of two vectors (formed from three points)
		/// </summary>
		/// <param name="p1">The first point</param>
		/// <param name="p2">The second point</param>
		/// <param name="p3">The third point</param>
		/// <param name="result">The 2d cross product</param>
		internal static void Cross2D(ref Vector3 p1, ref Vector3 p2, ref Vector3 p3, out float result)
		{
			float u1 = p2.X - p1.X;
			float v1 = p2.Z - p1.Z;
			float u2 = p3.X - p1.X;
			float v2 = p3.Z - p1.Z;

			result = u1 * v2 - v1 * u2;
		}

		/// <summary>
		/// Calculates the perpendicular dot product of two vectors projected onto the XZ plane.
		/// </summary>
		/// <param name="a">A vector.</param>
		/// <param name="b">Another vector.</param>
		/// <param name="result">The perpendicular dot product on the XZ plane.</param>
		internal static void PerpDotXZ(ref Vector3 a, ref Vector3 b, out float result)
		{
			result = a.X * b.Z - a.Z * b.X;
		}

		internal static void CalculateSlopeAngle(ref Vector3 vec, out float angle)
		{
			Vector3 up = Vector3.UnitY;
			float dot;
			Vector3.Dot(ref vec, ref up, out dot);
			angle = (float)Math.Acos(dot);
		}

		internal static bool AlmostEqual(ref Vector3 a, ref Vector3 b)
		{
			float threshold = (1.0f / 16384.0f);
			return AlmostEqual(ref a, ref b, threshold);
		}

		internal static bool AlmostEqual(ref Vector3 a, ref Vector3 b, float threshold)
		{
			float threshSq = threshold * threshold;
			float distSq = (b - a).LengthSquared();

			return distSq < threshold;
		}
	}
}
