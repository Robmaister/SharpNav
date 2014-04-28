#region License
/**
 * Copyright (c) 2014 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
 * Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE
 */
#endregion

using System;

namespace SharpNav.Pathfinding
{
	public class Poly
	{
		private PolygonType polyType;

		/// <summary>
		/// Index to first link in linked list
		/// </summary>
		public int FirstLink { get; set; }

		/// <summary>
		/// Indices of polygon's vertices
		/// </summary>
		public int[] Verts { get; set; }

		/// <summary>
		/// Packed data representing neighbor polygons references and flags for each edge
		/// </summary>
		public int[] Neis { get; set; }

		//TODO turn flags into a Tag object, which is more standard for C#

		/// <summary>
		/// User defined polygon flags
		/// </summary>
		public int Flags { get; set; }

		public int VertCount { get; set; }

		public AreaId Area { get; set; }

		public PolygonType PolyType { get { return polyType; } set { polyType = value; } }
	}
}
