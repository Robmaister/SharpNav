#region License
/**
 * Copyright (c) 2015 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
 * Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE
 */
#endregion

using System;
using System.Runtime.InteropServices;

using SharpNav.Geometry;

#if MONOGAME
using Microsoft.Xna.Framework;
#elif OPENTK
using OpenTK;
#elif SHARPDX
using SharpDX;
#endif

namespace SharpNav.Pathfinding
{
	[Serializable]
	[StructLayout(LayoutKind.Sequential)]
	public struct NavPoint
	{
		public static readonly NavPoint Null = new NavPoint(-1, Vector3.Zero);

		public int Polygon;
		public Vector3 Position;

		public NavPoint(int poly, Vector3 pos)
		{
			this.Polygon = poly;
			this.Position = pos;
		}
	}
}
