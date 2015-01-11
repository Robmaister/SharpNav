// Copyright (c) 2015 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
// Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE

using System;
using System.Runtime.InteropServices;

using SharpNav.Geometry;

#if MONOGAME
using Vector3 = Microsoft.Xna.Framework.Vector3;
#elif OPENTK
using Vector3 = OpenTK.Vector3;
#elif SHARPDX
using Vector3 = SharpDX.Vector3;
#endif

namespace SharpNav.Pathfinding
{
	/// <summary>
	/// A point in a navigation mesh.
	/// </summary>
	[Serializable]
	[StructLayout(LayoutKind.Sequential)]
	public struct NavPoint
	{
		/// <summary>
		/// A null point that isn't associated with any polygon.
		/// </summary>
		public static readonly NavPoint Null = new NavPoint(0, Vector3.Zero);

		/// <summary>
		/// A reference to the polygon this point is on.
		/// </summary>
		public int Polygon;

		/// <summary>
		/// The 3d position of the point.
		/// </summary>
		public Vector3 Position;

		/// <summary>
		/// Initializes a new instance of the <see cref="NavPoint"/> struct.
		/// </summary>
		/// <param name="poly">The polygon that the point is on.</param>
		/// <param name="pos">The 3d position of the point.</param>
		public NavPoint(int poly, Vector3 pos)
		{
			this.Polygon = poly;
			this.Position = pos;
		}
	}
}
