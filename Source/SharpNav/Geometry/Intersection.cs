// Copyright (c) 2014-2015 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
// Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE

using System;

#if MONOGAME
using Vector3 = Microsoft.Xna.Framework.Vector3;
#elif OPENTK
using Vector3 = OpenTK.Vector3;
#elif SHARPDX
using Vector3 = SharpDX.Vector3;
#endif

namespace SharpNav.Geometry
{
	/// <summary>
	/// Contains helper methods to check for intersection between two objects.
	/// </summary>
	internal static class Intersection
	{
		/// <summary>
		/// Determines whether two 2D segments AB and CD are intersecting.
		/// </summary>
		/// <param name="a">The endpoint A of segment AB.</param>
		/// <param name="b">The endpoint B of segment AB.</param>
		/// <param name="c">The endpoint C of segment CD.</param>
		/// <param name="d">The endpoint D of segment CD.</param>
		/// <returns>A value indicating whether the two segments are intersecting.</returns>
		internal static bool SegmentSegment2D(ref Vector3 a, ref Vector3 b, ref Vector3 c, ref Vector3 d)
		{
			float a1, a2, a3;

			Vector3Extensions.Cross2D(ref a, ref b, ref d, out a1);
			Vector3Extensions.Cross2D(ref a, ref b, ref c, out a2);

			if (a1 * a2 < 0.0f)
			{
				Vector3Extensions.Cross2D(ref c, ref d, ref a, out a3);
				float a4 = a3 + a2 - a1;

				if (a3 * a4 < 0.0f)
					return true;
			}

			return false;
		}

		/// <summary>
		/// Determines whether two 2D segments AB and CD are intersecting.
		/// </summary>
		/// <param name="a">The endpoint A of segment AB.</param>
		/// <param name="b">The endpoint B of segment AB.</param>
		/// <param name="c">The endpoint C of segment CD.</param>
		/// <param name="d">The endpoint D of segment CD.</param>
		/// <param name="s">The normalized dot product between CD and AC on the XZ plane.</param>
		/// <param name="t">The normalized dot product between AB and AC on the XZ plane.</param>
		/// <returns>A value indicating whether the two segments are intersecting.</returns>
		internal static bool SegmentSegment2D(ref Vector3 a, ref Vector3 b, ref Vector3 c, ref Vector3 d, out float s, out float t)
		{
			Vector3 u = b - a;
			Vector3 v = d - c;
			Vector3 w = a - c;

			float magnitude;
			Vector3Extensions.PerpDotXZ(ref u, ref v, out magnitude);

			if (Math.Abs(magnitude) < 1e-6f)
			{
				//TODO is NaN the best value to set here?
				s = float.NaN;
				t = float.NaN;
				return false;
			}

			Vector3Extensions.PerpDotXZ(ref v, ref w, out s);
			Vector3Extensions.PerpDotXZ(ref u, ref w, out t);
			s /= magnitude;
			t /= magnitude;

			return true;
		}

		/// <summary>
		/// Determines whether two polygons A and B are intersecting
		/// </summary>
		/// <param name="polya">Polygon A's vertices</param>
		/// <param name="npolya">Number of vertices for polygon A</param>
		/// <param name="polyb">Polygon B's vertices</param>
		/// <param name="npolyb">Number of vertices for polygon B</param>
		/// <returns>True if intersecting, false if not</returns>
		internal static bool PolyPoly2D(Vector3[] polya, int npolya, Vector3[] polyb, int npolyb)
		{
			const float EPS = 1E-4f;

			for (int i = 0, j = npolya - 1; i < npolya; j = i++)
			{
				Vector3 va = polya[j];
				Vector3 vb = polya[i];
				Vector3 n = new Vector3(va.X - vb.X, 0.0f, va.Z - vb.Z);
				float amin, amax, bmin, bmax;
				ProjectPoly(n, polya, npolya, out amin, out amax);
				ProjectPoly(n, polyb, npolyb, out bmin, out bmax);
				if (!OverlapRange(amin, amax, bmin, bmax, EPS))
				{
					//found separating axis
					return false;
				}
			}

			for (int i = 0, j = npolyb - 1; i < npolyb; j = i++)
			{
				Vector3 va = polyb[j];
				Vector3 vb = polyb[i];
				Vector3 n = new Vector3(va.X - vb.X, 0.0f, va.Z - vb.Z);
				float amin, amax, bmin, bmax;
				ProjectPoly(n, polya, npolya, out amin, out amax);
				ProjectPoly(n, polyb, npolyb, out bmin, out bmax);
				if (!OverlapRange(amin, amax, bmin, bmax, EPS))
				{
					//found separating axis
					return false;
				}
			}

			return true;
		}

		/// <summary>
		/// Determines whether the segment interesects with the polygon.
		/// </summary>
		/// <param name="p0">Segment's first endpoint</param>
		/// <param name="p1">Segment's second endpoint</param>
		/// <param name="verts">Polygon's vertices</param>
		/// <param name="nverts">The number of vertices in the polygon</param>
		/// <param name="tmin">Parameter t minimum</param>
		/// <param name="tmax">Parameter t maximum</param>
		/// <param name="segMin">Minimum vertex index</param>
		/// <param name="segMax">Maximum vertex index</param>
		/// <returns>True if intersect, false if not</returns>
		internal static bool SegmentPoly2D(Vector3 p0, Vector3 p1, Vector3[] verts, int nverts, out float tmin, out float tmax, out int segMin, out int segMax)
		{
			const float Epsilon = 0.00000001f;

			tmin = 0;
			tmax = 1;
			segMin = -1;
			segMax = -1;

			Vector3 dir = p1 - p0;

			for (int i = 0, j = nverts - 1; i < nverts; j = i++)
			{
				Vector3 edge = verts[i] - verts[j];
				Vector3 diff = p0 - verts[j];
				float n = edge.Z * diff.X - edge.X * diff.Z;
				float d = dir.Z * edge.X - dir.X * edge.Z;
				if (Math.Abs(d) < Epsilon)
				{
					//S is nearly parallel to this edge
					if (n < 0)
						return false;
					else
						continue;
				}

				float t = n / d;
				if (d < 0)
				{
					//segment S is entering across this edge
					if (t > tmin)
					{
						tmin = t;
						segMin = j;

						//S enters after leaving the polygon
						if (tmin > tmax)
							return false;
					}
				}
				else
				{
					//segment S is leaving across this edge
					if (t < tmax)
					{
						tmax = t;
						segMax = j;

						//S leaves before entering the polygon
						if (tmax < tmin)
							return false;
					}
				}
			}

			return true;
		}

		internal static void ProjectPoly(Vector3 axis, Vector3[] poly, int npoly, out float rmin, out float rmax)
		{
			Vector3Extensions.Dot2D(ref axis, ref poly[0], out rmin);
			Vector3Extensions.Dot2D(ref axis, ref poly[0], out rmax);
			for (int i = 1; i < npoly; i++)
			{
				float d;
				Vector3Extensions.Dot2D(ref axis, ref poly[i], out d);
				rmin = Math.Min(rmin, d);
				rmax = Math.Max(rmax, d);
			}
		}

		internal static bool OverlapRange(float amin, float amax, float bmin, float bmax, float eps)
		{
			return ((amin + eps) > bmax || (amax - eps) < bmin) ? false : true;
		}
	}
}
