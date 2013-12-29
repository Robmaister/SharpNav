#region License
/**
 * Copyright (c) 2013 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
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
		internal static float Length(this Vector3 v)
		{
			return v.Length;
		}

		internal static float LengthSquared(this Vector3 v)
		{
			return v.LengthSquared;
		}
#endif

		/// <summary>
		/// Calculate the dot (scalar) product of two vectors in the two dimensional xz plane
		/// </summary>
		/// <param name="left">First operand</param>
		/// <param name="right">Second operand</param>
		/// <returns>The dot product of the two inputs</returns>
		internal static void Dot2D(ref Vector3 left, ref Vector3 right, out float result)
		{
			result = left.X * right.X + left.Z * right.Z;
		}
	}
}
