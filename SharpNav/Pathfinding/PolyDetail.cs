#region License
/**
 * Copyright (c) 2014 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
 * Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE
 */
#endregion

using System;

namespace SharpNav.Pathfinding
{
	public struct PolyDetail
	{
		public int vertBase; //offset of vertices in some array
		public int triBase; //offset of triangles in some array
		public int vertCount;
		public int triCount;
	}
}
