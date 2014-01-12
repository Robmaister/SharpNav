#region License
/**
 * Copyright (c) 2014 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
 * Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE
 */
#endregion

using System;

#if MONOGAME || XNA
using Microsoft.Xna.Framework;
#elif OPENTK
using OpenTK;
#elif SHARPDX
using SharpDX;
#endif

namespace SharpNav.Pathfinding
{
	public struct NavMeshParams
	{
		public Vector3 origin;
		public float tileWidth;
		public float tileHeight;
		public int maxTiles;
		public int maxPolys;
	}
}
