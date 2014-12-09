using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SharpNav
{
	[Flags]
	public enum EdgeFlags : byte
	{
		None = 0x0,
		West = 0x1,
		North = 0x2,
		East = 0x4,
		South = 0x8,
		All = West | North | East | South
	}

	public static class EdgeFlagsHelper
	{
		/// <summary>
		/// Sets the bit for a direction to 1 in a specified byte.
		/// </summary>
		/// <param name="flag">The byte containing flags.</param>
		/// <param name="dir">The direction to add.</param>
		public static void AddEdge(ref EdgeFlags edges, Direction dir)
		{
			edges |= (EdgeFlags)(1 << (int)dir);
		}

		/// <summary>
		/// Flips all the bits used for flags in a byte.
		/// </summary>
		/// <param name="flag">The byte containing flags.</param>
		public static void FlipEdges(ref EdgeFlags flag)
		{
			flag ^= EdgeFlags.All;
		}

		/// <summary>
		/// Determines whether the bit for a direction is set in a byte.
		/// </summary>
		/// <param name="flag">The byte containing flags.</param>
		/// <param name="dir">The direction to check for.</param>
		/// <returns>A value indicating whether the flag for the specified direction is set.</returns>
		public static bool IsConnected(ref EdgeFlags flag, Direction dir)
		{
			return (flag & (EdgeFlags)(1 << (int)dir)) != EdgeFlags.None;
		}

		/// <summary>
		/// Sets the bit for a direction to 0 in a specified byte.
		/// </summary>
		/// <param name="flag">The byte containing flags.</param>
		/// <param name="dir">The direction to remove.</param>
		public static void RemoveEdge(ref EdgeFlags flag, Direction dir)
		{
			flag &= (EdgeFlags)(~(1 << (int)dir)); // remove visited edges
		}
	}
}
