// Copyright (c) 2014-2016 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
// Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE

using System;
using System.Collections.Generic;

namespace SharpNav.Pathfinding
{
	/// <summary>
	/// Flags representing the type of a navmesh polygon.
	/// </summary>
	[Flags]
	public enum NavPolyType : byte
	{
		/// <summary>A polygon that is part of the navmesh.</summary>
		Ground = 0,

		/// <summary>An off-mesh connection consisting of two vertices.</summary>
		OffMeshConnection = 1
	}

	/// <summary>
	/// Uses the PolyMesh polygon data for pathfinding
	/// </summary>
	public class NavPoly
	{
		public NavPoly()
		{
			Links = new List<Link>();
		}

		/// <summary>
		/// Gets or sets the polygon type (ground or offmesh)
		/// </summary>
		public NavPolyType PolyType { get; set; }

		public List<Link> Links { get; private set; }

		/// <summary>
		/// Gets or sets the indices of polygon's vertices
		/// </summary>
		public int[] Verts { get; set; }

		/// <summary>
		/// Gets or sets packed data representing neighbor polygons references and flags for each edge
		/// </summary>
		public int[] Neis { get; set; }

		/// <summary>
		/// Gets or sets a user defined polygon flags
		/// </summary>
		public object Tag { get; set; }

		/// <summary>
		/// Gets or sets the number of vertices
		/// </summary>
		public int VertCount { get; set; }

		/// <summary>
		/// Gets or sets the AreaId
		/// </summary>
		public Area Area { get; set; }
	}
}
