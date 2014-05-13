#region License
/**
 * Copyright (c) 2014 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
 * Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE
 */
#endregion

using System;

namespace SharpNav.Pathfinding
{
	public class Link
	{
		/// <summary>
		/// Gets or sets the neighbor reference (the one it's linked to)
		/// </summary>
		public int Reference { get; set; }

		/// <summary>
		/// Gets or sets the index of next link
		/// </summary>
		public int Next { get; set; }

		/// <summary>
		/// Gets or sets the index of polygon edge
		/// </summary>
		public int Edge { get; set; }

		public int Side { get; set; }

		public int BMin { get; set; }

		public int BMax { get; set; }
	}
}
