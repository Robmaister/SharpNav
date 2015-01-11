// Copyright (c) 2014 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
// Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE

using System;

namespace SharpNav.Pathfinding
{
	/// <summary>
	/// Flags representing the type of a navmesh polygon.
	/// </summary>
	[Flags]
	public enum PolygonType : byte
	{
		/// <summary>A polygon that is part of the navmesh.</summary>
		Ground = 0,

		/// <summary>An off-mesh connection consisting of two vertices.</summary>
		OffMeshConnection = 1
	}
}
