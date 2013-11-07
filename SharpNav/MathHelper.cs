#region License
/**
 * Copyright (c) 2013 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
 * Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpNav
{
	internal static class MathHelper
	{
		private static readonly int[] DirOffsetsX = { -1, 0, 1, 0 };
		private static readonly int[] DirOffsetsY = { 0, 1, 0, -1 };

		/// <summary>
		/// Gets an X offset.
		/// </summary>
		/// <remarks>
		/// The directions cycle between the following, starting from 0: west, north, east, south.
		/// </remarks>
		/// <param name="dir">The direction.</param>
		/// <returns>The offset for the X coordinate.</returns>
		internal static int GetDirOffsetX(int dir)
		{
			return DirOffsetsX[dir % 4];
		}

		/// <summary>
		/// Get a Y offset.
		/// </summary>
		/// <remarks>
		/// The directions cycle between the following, starting from 0: west, north, east, south.
		/// </remarks>
		/// <param name="dir">The direction.</param>
		/// <returns>The offset for the Y coordinate.</returns>
		internal static int GetDirOffsetY(int dir)
		{
			return DirOffsetsY[dir % 4];
		}

		internal static int Clamp(int val, int min, int max)
		{
			return val < min ? min : (val > max ? max : val);
		}

		internal static void Clamp(ref int val, int min, int max)
		{
			val = val < min ? min : (val > max ? max : val);
		}

		internal static uint Clamp(uint val, uint min, uint max)
		{
			return val < min ? min : (val > max ? max : val);
		}

		internal static void Clamp(ref uint val, uint min, uint max)
		{
			val = val < min ? min : (val > max ? max : val);
		}
	}
}
