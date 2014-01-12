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
		public int areaAndtype = 0; //bit packed area id and polygon type

		public void SetArea(int a)
		{
			areaAndtype = (areaAndtype & 0xc0) | (a & 0x3f);
		}

		public void SetType(PolygonType t)
		{
			areaAndtype = (areaAndtype & 0x3f) | ((int)t << 6);
		}

		public int GetArea()
		{
			return areaAndtype & 0x3f;
		}

		public PolygonType GetPolyType()
		{
			return (PolygonType)(areaAndtype >> 6);
		}
	}
}
