#region License
/**
 * Copyright (c) 2014 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
 * Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE
 */
#endregion

using System;

namespace SharpNav
{
	public enum Direction
	{
		West = 0,
		North = 1,
		East = 2,
		South = 3
	}

	public static class DirectionExtensions
	{
		private static readonly int[] OffsetsX = { -1, 0, 1, 0 };
		private static readonly int[] OffsetsY = { 0, 1, 0, -1 };

		/// <summary>
		/// Gets an X offset.
		/// </summary>
		/// <remarks>
		/// The directions cycle between the following, starting from 0: west, north, east, south.
		/// </remarks>
		/// <param name="dir">The direction.</param>
		/// <returns>The offset for the X coordinate.</returns>
		public static int HorizontalOffset(this Direction dir)
		{
			return OffsetsX[(int)dir];
		}

		/// <summary>
		/// Get a Y offset.
		/// </summary>
		/// <remarks>
		/// The directions cycle between the following, starting from 0: west, north, east, south.
		/// </remarks>
		/// <param name="dir">The direction.</param>
		/// <returns>The offset for the Y coordinate.</returns>
		public static int VerticalOffset(this Direction dir)
		{
			return OffsetsY[(int)dir];
		}

		public static Direction NextClockwise(this Direction dir)
		{
			switch (dir)
			{
				case Direction.West:
					return Direction.North;
				case Direction.North:
					return Direction.East;
				case Direction.East:
					return Direction.South;
				case Direction.South:
					return Direction.West;
				default:
					throw new ArgumentException("dir isn't a valid Direction.");
			}
		}

		public static Direction NextCounterClockwise(this Direction dir)
		{
			switch (dir)
			{
				case Direction.West:
					return Direction.South;
				case Direction.South:
					return Direction.East;
				case Direction.East:
					return Direction.North;
				case Direction.North:
					return Direction.West;
				default:
					throw new ArgumentException("dir isn't a valid Direction.");
			}
		}
	}
}
