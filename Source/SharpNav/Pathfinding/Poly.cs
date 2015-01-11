// Copyright (c) 2014 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
// Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE

namespace SharpNav.Pathfinding
{
	/// <summary>
	/// Uses the PolyMesh polygon data for pathfinding
	/// </summary>
	public class Poly
	{
		/// <summary>
		/// Polygon type
		/// </summary>
		private PolygonType polyType;

		/// <summary>
		/// Gets or sets the index to first link in linked list
		/// </summary>
		public int FirstLink { get; set; }

		/// <summary>
		/// Gets or sets the indices of polygon's vertices
		/// </summary>
		public int[] Verts { get; set; }

		/// <summary>
		/// Gets or sets packed data representing neighbor polygons references and flags for each edge
		/// </summary>
		public int[] Neis { get; set; }

		//TODO turn flags into a Tag object, which is more standard for C#

		/// <summary>
		/// Gets or sets a user defined polygon flags
		/// </summary>
		public int Flags { get; set; }

		/// <summary>
		/// Gets or sets the number of vertices
		/// </summary>
		public int VertCount { get; set; }

		/// <summary>
		/// Gets or sets the AreaId
		/// </summary>
		public Area Area { get; set; }

		/// <summary>
		/// Gets or sets the polygon type (ground or offmesh)
		/// </summary>
		public PolygonType PolyType
		{
			get
			{
				return polyType;
			}

			set
			{
				polyType = value;
			}
		}
	}
}
