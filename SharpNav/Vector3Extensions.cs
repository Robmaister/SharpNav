#region License
/**
 * Copyright (c) 2013-2014 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
 * Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE
 */
#endregion

using System;

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

		internal static void ComponentMin(ref Vector3 left, ref Vector3 right, out Vector3 result)
		{
#if OPENTK || STANDALONE
			Vector3.ComponentMin(ref left, ref right, out result);
#else
			Vector3.Min(ref left, ref right, out result);
#endif
		}

		internal static void ComponentMax(ref Vector3 left, ref Vector3 right, out Vector3 result)
		{
#if OPENTK || STANDALONE
			Vector3.ComponentMax(ref left, ref right, out result);
#else
			Vector3.Max(ref left, ref right, out result);
#endif
		}

		/// <summary>
		/// Calculate the dot product of two vectors projected onto the XZ plane.
		/// </summary>
		/// <param name="left">A vector.</param>
		/// <param name="right">Another vector</param>
		/// <param name="result">The dot product of the two vectors.</param>
		internal static void Dot2D(ref Vector3 left, ref Vector3 right, out float result)
		{
			result = left.X * right.X + left.Z * right.Z;
		}

		internal static void Cross2D(ref Vector3 p1, ref Vector3 p2, ref Vector3 p3, out float result)
		{
			float u1 = p2.X - p1.X;
			float v1 = p2.Z - p1.Z;
			float u2 = p3.X - p1.X;
			float v2 = p3.Z - p1.Z;

			result = u1 * v2 - v1 * u2;
		}

		public static void PerpDotXZ(ref Vector3 a, ref Vector3 b, out float result)
		{
			result = a.X * b.Z - a.Z * b.X;
		}
	}
}
