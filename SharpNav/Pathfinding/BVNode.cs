#region License
/**
 * Copyright (c) 2014 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
 * Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE
 */
#endregion

using System;

using SharpNav.Geometry;

namespace SharpNav.Pathfinding
{
	public struct BVNode
	{
		public BBox3 bounds;
		public int index;
	}
}
