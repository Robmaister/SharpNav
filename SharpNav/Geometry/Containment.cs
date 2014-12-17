using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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
