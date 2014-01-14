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
		private byte areaAndType; //bit packed area ID and polygon type.

		private const int PolyTypeMask = 0xc0;
		private const int AreaMask = 0x3f;

		public int Area
		{
			get
			{
				return areaAndType & AreaMask;
			}

			set
			{
				areaAndType = (byte)((areaAndType & PolyTypeMask) | ((value % PathfinderCommon.MAX_AREAS) & AreaMask));
			}
		}

		public PolygonType PolyType
		{
			get
			{
				return (PolygonType)(areaAndType >> 6);
			}
			set
			{
				areaAndType = (byte)((areaAndType & AreaMask) | ((int)value << 6));
			}
		}
	}
}
