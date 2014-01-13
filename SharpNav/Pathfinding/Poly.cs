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

		public int Area;
		public PolygonType PolyType;

		//There exists a limit to the number of different areas 
		//Keep the area value in bounds
		public void SetArea(int a)
		{
			Area = a % PathfinderCommon.MAX_AREAS;
		}
	}
}
