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
		private static readonly int[] dirOffsetsX = { -1, 0, 1, 0 };
		private static readonly int[] dirOffsetsY = { 0, 1, 0, -1 };

		/// <summary>
		/// Get X offset
		/// Directions: west, north, east, south
		/// </summary>
		/// <param name="dir">direction</param>
		/// <returns>The offset</returns>
		internal static int GetDirOffsetX(int dir)
		{
			return dirOffsetsX[dir % 4];
		}

		/// <summary>
		/// Get Y offset
		/// Directions: west, north, east, south
		/// </summary>
		/// <param name="dir">direction</param>
		/// <returns></returns>
		internal static int GetDirOffsetY(int dir)
		{
			return dirOffsetsY[dir % 4];
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
