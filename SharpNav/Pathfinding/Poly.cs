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
		public int firstLink; //index to first link in linked list
		public int[] verts; //indices of polygon's vertices
		public int[] neis; //packed data representing neighbor polygons references and flags for each edge
		public int flags; //user defined polygon flags
		public int vertCount;
		private AreaId area;
		private PolygonType polyType;

		public AreaId Area { get { return area; } set { area = value; } }

		public PolygonType PolyType { get { return polyType; } set { polyType = value; } }
	}
}
