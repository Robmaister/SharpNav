// Copyright (c) 2014 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
// Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE

#if MONOGAME || XNA
using Microsoft.Xna.Framework;
#elif OPENTK
using OpenTK;
#elif SHARPDX
using SharpDX;
#elif UNITY3D
using UnityEngine;
#endif

namespace SharpNav.Geometry
{
	internal class Containment
	{
		/// <summary>
		/// Determines whether a point is inside a polygon.
		/// </summary>
		/// <param name="pt">A point.</param>
		/// <param name="verts">A set of vertices that define a polygon.</param>
		/// <param name="nverts">The number of vertices to use from <see cref="verts"/>.</param>
		/// <returns>A value indicating whether the point is contained within the polygon.</returns>
		internal static bool PointInPoly(Vector3 pt, Vector3[] verts, int nverts)
		{
			bool c = false;

			for (int i = 0, j = nverts - 1; i < nverts; j = i++)
			{
				Vector3 vi = verts[i];
				Vector3 vj = verts[j];
				if (((vi.Z > pt.Z) != (vj.Z > pt.Z)) &&
					(pt.X < (vj.X - vi.X) * (pt.Z - vi.Z) / (vj.Z - vi.Z) + vi.X))
				{
					c = !c;
				}
			}

			return c;
		}
	}
}
