// Copyright (c) 2015 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
// Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE

using System;

namespace SharpNav.Pathfinding
{
	/// <summary>
	/// Flags for choosing how the path is built.
	/// </summary>
	[Flags]
	public enum PathBuildFlags
	{
		/// <summary>
		/// Build normally.
		/// </summary>
		None = 0x00,

		/// <summary>
		/// Adds a vertex to the path at each polygon edge crossing, but only when the areas of the two polygons are
		/// different
		/// </summary>
		AreaCrossingVertices = 0x01,

		/// <summary>
		/// Adds a vertex to the path at each polygon edge crossing.
		/// </summary>
		AllCrossingVertices = 0x02
	}
}
