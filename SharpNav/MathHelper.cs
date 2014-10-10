#region License
/**
 * Copyright (c) 2013-2014 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
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
#elif UNITY3D
using UnityEngine;
#endif

namespace SharpNav
{
	/// <summary>
	/// A class where all the small, miscellaneous math functions are stored.
	/// </summary>
	internal static class MathHelper
	{
		/// <summary>
		/// A static buffer of floats used in <see cref="ClipPolygonToPlane"/>. Created here to prevent extra heap
		/// allocation every time the method is called.
		/// </summary>
		private static float[] distances = new float[12];

		/// <summary>
		/// Clamps an integer value to be within a specified range.
		/// </summary>
		/// <param name="val">The value to clamp.</param>
		/// <param name="min">The inclusive minimum of the range.</param>
		/// <param name="max">The inclusive maximum of the range.</param>
		/// <returns>The clamped value.</returns>
		internal static int Clamp(int val, int min, int max)
		{
			return val < min ? min : (val > max ? max : val);
		}

		/// <summary>
		/// Clamps an integer value to be within a specified range.
		/// </summary>
		/// <param name="val">The value to clamp.</param>
		/// <param name="min">The inclusive minimum of the range.</param>
		/// <param name="max">The inclusive maximum of the range.</param>
		internal static void Clamp(ref int val, int min, int max)
		{
			val = val < min ? min : (val > max ? max : val);
		}

		/// <summary>
		/// Clamps an integer value to be within a specified range.
		/// </summary>
		/// <param name="val">The value to clamp.</param>
		/// <param name="min">The inclusive minimum of the range.</param>
		/// <param name="max">The inclusive maximum of the range.</param>
		/// <returns>The clamped value.</returns>
		internal static uint Clamp(uint val, uint min, uint max)
		{
			return val < min ? min : (val > max ? max : val);
		}

		/// <summary>
		/// Clamps an integer value to be within a specified range.
		/// </summary>
		/// <param name="val">The value to clamp.</param>
		/// <param name="min">The inclusive minimum of the range.</param>
		/// <param name="max">The inclusive maximum of the range.</param>
		internal static void Clamp(ref uint val, uint min, uint max)
		{
			val = val < min ? min : (val > max ? max : val);
		}

		/// <summary>
		/// Clamps an integer value to be within a specified range.
		/// </summary>
		/// <param name="val">The value to clamp.</param>
		/// <param name="min">The inclusive minimum of the range.</param>
		/// <param name="max">The inclusive maximum of the range.</param>
		/// <returns>The clamped value.</returns>
		internal static float Clamp(float val, float min, float max)
		{
			return val < min ? min : (val > max ? max : val);
		}

		/// <summary>
		/// Clamps an integer value to be within a specified range.
		/// </summary>
		/// <param name="val">The value to clamp.</param>
		/// <param name="min">The inclusive minimum of the range.</param>
		/// <param name="max">The inclusive maximum of the range.</param>
		internal static void Clamp(ref float val, float min, float max)
		{
			val = val < min ? min : (val > max ? max : val);
		}

		/// <summary>
		/// Normalizes a value in a specified range to be between 0 and 1.
		/// </summary>
		/// <param name="t">The value</param>
		/// <param name="t0">The lower bound of the range.</param>
		/// <param name="t1">The upper bound of the range.</param>
		/// <returns>A normalized value.</returns>
		public static float Normalize(float t, float t0, float t1)
		{
			return MathHelper.Clamp((t - t0) / (t1 - t0), 0.0f, 1.0f);
		}

		/// <summary>
		/// Calculates the next highest power of two.
		/// </summary>
		/// <remarks>
		/// This is a minimal method meant to be fast. There is a known edge case where an input of 0 will output 0
		/// instead of the mathematically correct value of 1. It will not be fixed.
		/// </remarks>
		/// <param name="v">A value.</param>
		/// <returns>The next power of two after the value.</returns>
		internal static int NextPowerOfTwo(int v)
		{
			v--;
			v |= v >> 1;
			v |= v >> 2;
			v |= v >> 4;
			v |= v >> 8;
			v |= v >> 16;
			v++;

			return v;
		}

		/// <summary>
		/// Calculates the next highest power of two.
		/// </summary>
		/// <remarks>
		/// This is a minimal method meant to be fast. There is a known edge case where an input of 0 will output 0
		/// instead of the mathematically correct value of 1. It will not be fixed.
		/// </remarks>
		/// <param name="v">A value.</param>
		/// <returns>The next power of two after the value.</returns>
		internal static uint NextPowerOfTwo(uint v)
		{
			v--;
			v |= v >> 1;
			v |= v >> 2;
			v |= v >> 4;
			v |= v >> 8;
			v |= v >> 16;
			v++;

			return v;
		}

		/// <summary>
		/// Calculates the binary logarithm of the input.
		/// </summary>
		/// <remarks>
		/// Inputs 0 and below have undefined output.
		/// </remarks>
		/// <param name="v">A value.</param>
		/// <returns>The binary logarithm of v.</returns>
		internal static int Log2(int v)
		{
			int r;
			int shift;

			r = (v > 0xffff) ? 1 << 4 : 0 << 4;
			v >>= r;

			shift = (v > 0xff) ? 1 << 3 : 0 << 3;
			v >>= shift;
			r |= shift;

			shift = (v > 0xf) ? 1 << 2 : 0 << 2;
			v >>= shift;
			r |= shift;

			shift = (v > 0x3) ? 1 << 1 : 0 << 1;
			v >>= shift;
			r |= shift;

			r |= v >> 1;

			return r;
		}

		/// <summary>
		/// Calculates the binary logarithm of the input.
		/// </summary>
		/// <remarks>
		/// An input of 0 has an undefined output.
		/// </remarks>
		/// <param name="v">A value.</param>
		/// <returns>The binary logarithm of v.</returns>
		internal static uint Log2(uint v)
		{
			uint r;
			int shift;

			r = (uint)((v > 0xffff) ? 1 << 4 : 0 << 4);
			v >>= (int)r;

			shift = (v > 0xff) ? 1 << 3 : 0 << 3;
			v >>= shift;
			r |= (uint)shift;

			shift = (v > 0xf) ? 1 << 2 : 0 << 2;
			v >>= shift;
			r |= (uint)shift;

			shift = (v > 0x3) ? 1 << 1 : 0 << 1;
			v >>= shift;
			r |= (uint)shift;

			r |= v >> 1;

			return r;
		}

		/// <summary>
		/// Clips a polygon to a plane using the Sutherland-Hodgman algorithm.
		/// </summary>
		/// <param name="inVertices">The input array of vertices.</param>
		/// <param name="outVertices">The output array of vertices.</param>
		/// <param name="numVerts">The number of vertices to read from the arrays.</param>
		/// <param name="planeX">The clip plane's X component.</param>
		/// <param name="planeZ">The clip plane's Z component.</param>
		/// <param name="planeD">The clip plane's D component.</param>
		/// <returns>The number of vertices stored in outVertices.</returns>
		internal static int ClipPolygonToPlane(Vector3[] inVertices, Vector3[] outVertices, int numVerts, float planeX, float planeZ, float planeD)
		{
			for (int i = 0; i < numVerts; i++)
				distances[i] = planeX * inVertices[i].X + planeZ * inVertices[i].Z + planeD;

			int m = 0;
			Vector3 temp;
			for (int i = 0, j = numVerts - 1; i < numVerts; j = i, i++)
			{
				bool inj = distances[j] >= 0;
				bool ini = distances[i] >= 0;

				if (inj != ini)
				{
					float s = distances[j] / (distances[j] - distances[i]);

					Vector3.Subtract(ref inVertices[i], ref inVertices[j], out temp);
					Vector3.Multiply(ref temp, s, out temp);
					Vector3.Add(ref inVertices[j], ref temp, out outVertices[m]);
					m++;
				}

				if (ini)
				{
					outVertices[m] = inVertices[i];
					m++;
				}
			}

			return m;
		}

		/// <summary>
		/// Determines whether a point is inside a polygon.
		/// </summary>
		/// <param name="pt">A point.</param>
		/// <param name="verts">A set of vertices that define a polygon.</param>
		/// <param name="nverts">The number of vertices to use from <see cref="verts"/>.</param>
		/// <returns>A value indicating whether the point is contained within the polygon.</returns>
		internal static bool IsPointInPoly(Vector3 pt, Vector3[] verts, int nverts)
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

		/// <summary>
		/// Contains helper methods to calculate the distance between two objects.
		/// </summary>
		internal static class Distance
		{
			/// <summary>
			/// Find the 3D distance between a point (x, y, z) and a segment PQ
			/// </summary>
			/// <param name="pt">The coordinate of the point.</param>
			/// <param name="p">The coordinate of point P in the segment PQ.</param>
			/// <param name="q">The coordinate of point Q in the segment PQ.</param>
			/// <returns>The distance between the point and the segment.</returns>
			internal static float PointToSegmentSquared(ref Vector3 pt, ref Vector3 p, ref Vector3 q)
			{
				//distance from P to Q
				Vector3 pq = q - p;

				//disance from P to the lone point
				float dx = pt.X - p.X;
				float dy = pt.Y - p.Y;
				float dz = pt.Z - p.Z;

				float segmentMagnitudeSquared = pq.LengthSquared();
				float t = pq.X * dx + pq.Y * dy + pq.Z * dz;

				if (segmentMagnitudeSquared > 0)
					t /= segmentMagnitudeSquared;

				//keep t between 0 and 1
				if (t < 0)
					t = 0;
				else if (t > 1)
					t = 1;

				dx = p.X + t * pq.X - pt.X;
				dy = p.Y + t * pq.Y - pt.Y;
				dz = p.Z + t * pq.Z - pt.Z;

				return dx * dx + dy * dy + dz * dz;
			}

			/// <summary>
			/// Find the 2d distance between a point (x, z) and a segment PQ, where P is (px, pz) and Q is (qx, qz).
			/// </summary>
			/// <param name="x">The X coordinate of the point.</param>
			/// <param name="z">The Z coordinate of the point.</param>
			/// <param name="px">The X coordinate of point P in the segment PQ.</param>
			/// <param name="pz">The Z coordinate of point P in the segment PQ.</param>
			/// <param name="qx">The X coordinate of point Q in the segment PQ.</param>
			/// <param name="qz">The Z coordinate of point Q in the segment PQ.</param>
			/// <returns>The distance between the point and the segment.</returns>
			internal static float PointToSegment2DSquared(int x, int z, int px, int pz, int qx, int qz)
			{
				float segmentDeltaX = qx - px;
				float segmentDeltaZ = qz - pz;
				float dx = x - px;
				float dz = z - pz;
				float segmentMagnitudeSquared = segmentDeltaX * segmentDeltaX + segmentDeltaZ * segmentDeltaZ;
				float t = segmentDeltaX * dx + segmentDeltaZ * dz;

				//normalize?
				if (segmentMagnitudeSquared > 0)
					t /= segmentMagnitudeSquared;

				//0 < t < 1
				if (t < 0)
					t = 0;
				else if (t > 1)
					t = 1;

				dx = px + t * segmentDeltaX - x;
				dz = pz + t * segmentDeltaZ - z;

				return dx * dx + dz * dz;
			}
			
			/// <summary>
			/// Find the 2d distance between a point and a segment PQ
			/// </summary>
			/// <param name="pt">The coordinate of the point.</param>
			/// <param name="p">The coordinate of point P in the segment PQ.</param>
			/// <param name="q">The coordinate of point Q in the segment PQ.</param>
			/// <returns>The distance between the point and the segment.</returns>
			internal static float PointToSegment2DSquared(ref Vector3 pt, ref Vector3 p, ref Vector3 q)
			{
				float t = 0;
				return PointToSegment2DSquared(ref pt, ref p, ref q, out t);
			}

			/// <summary>
			/// Find the 2d distance between a point and a segment PQ
			/// </summary>
			/// <param name="pt">The coordinate of the point.</param>
			/// <param name="p">The coordinate of point P in the segment PQ.</param>
			/// <param name="q">The coordinate of point Q in the segment PQ.</param>
			/// <param name="t">Parameterization ratio t</param>
			/// <returns>The distance between the point and the segment.</returns>
			internal static float PointToSegment2DSquared(ref Vector3 pt, ref Vector3 p, ref Vector3 q, out float t)
			{
				//distance from P to Q in the xz plane
				float segmentDeltaX = q.X - p.X;
				float segmentDeltaZ = q.Z - p.Z;

				//distance from P to lone point in xz plane
				float dx = pt.X - p.X;
				float dz = pt.Z - p.Z;

				float segmentMagnitudeSquared = segmentDeltaX * segmentDeltaX + segmentDeltaZ * segmentDeltaZ;
				t = segmentDeltaX * dx + segmentDeltaZ * dz;

				if (segmentMagnitudeSquared > 0)
					t /= segmentMagnitudeSquared;

				//keep t between 0 and 1
				if (t < 0)
					t = 0;
				else if (t > 1)
					t = 1;

				dx = p.X + t * segmentDeltaX - pt.X;
				dz = p.Z + t * segmentDeltaZ - pt.Z;

				return dx * dx + dz * dz;
			}

			/// <summary>
			/// Finds the squared distance between a point and the nearest edge of a polygon.
			/// </summary>
			/// <param name="pt">A point.</param>
			/// <param name="verts">A set of vertices that define a polygon.</param>
			/// <param name="nverts">The number of vertices to use from <see cref="verts"/>.</param>
			/// <returns>The squared distance between a point and the nearest edge of a polygon.</returns>
			internal static float PointToPolygonEdgeSquared(Vector3 pt, Vector3[] verts, int nverts)
			{
				float dmin = float.MaxValue;
				for (int i = 0, j = nverts - 1; i < nverts; j = i++)
					dmin = Math.Min(dmin, MathHelper.Distance.PointToSegment2DSquared(ref pt, ref verts[j], ref verts[i]));

				return IsPointInPoly(pt, verts, nverts) ? -dmin : dmin;
			}

			/// <summary>
			/// Finds the distance between a point and the nearest edge of a polygon.
			/// </summary>
			/// <param name="pt">A point.</param>
			/// <param name="verts">A set of vertices that define a polygon.</param>
			/// <param name="nverts">The number of vertices to use from <see cref="verts"/>.</param>
			/// <param name="edgeDist">A buffer for edge distances to be stored in.</param>
			/// <param name="edgeT">A buffer for parametrization ratios to be stored in.</param>
			/// <returns>A value indicating whether the point is contained in the polygon.</returns>
			internal static bool PointToPolygonEdgeSquared(Vector3 pt, Vector3[] verts, int nverts, float[] edgeDist, float[] edgeT)
			{
				for (int i = 0, j = nverts - 1; i < nverts; j = i++)
					edgeDist[j] = PointToSegment2DSquared(ref pt, ref verts[j], ref verts[i], out edgeT[j]);

				return IsPointInPoly(pt, verts, nverts);
			}

			/// <summary>
			/// Finds the distance between a point and triangle ABC.
			/// </summary>
			/// <param name="p">A point.</param>
			/// <param name="a">The first vertex of the triangle.</param>
			/// <param name="b">The second vertex of the triangle.</param>
			/// <param name="c">The third vertex of the triangle.</param>
			/// <returns>The distnace between the point and the triangle.</returns>
			internal static float PointToTriangle(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
			{
				//If the point lies inside the triangle, return the interpolated y-coordinate
				float h;
				if (PointToTriangle(p, a, b, c, out h))
				{
					return Math.Abs(h - p.Y);
				}

				return float.MaxValue;
			}

			/// <summary>
			/// Finds the distance between a point and triangle ABC.
			/// </summary>
			/// <param name="p">A point.</param>
			/// <param name="a">The first vertex of the triangle.</param>
			/// <param name="b">The second vertex of the triangle.</param>
			/// <param name="c">The third vertex of the triangle.</param>
			/// <param name="height">The height between the point and the triangle.</param>
			/// <returns>A value indicating whether the point is contained within the triangle.</returns>
			internal static bool PointToTriangle(Vector3 p, Vector3 a, Vector3 b, Vector3 c, out float height)
			{
				Vector3 v0 = c - a;
				Vector3 v1 = b - a;
				Vector3 v2 = p - a;

				float dot00, dot01, dot02, dot11, dot12;

				Vector3Extensions.Dot2D(ref v0, ref v0, out dot00);
				Vector3Extensions.Dot2D(ref v0, ref v1, out dot01);
				Vector3Extensions.Dot2D(ref v0, ref v2, out dot02);
				Vector3Extensions.Dot2D(ref v1, ref v1, out dot11);
				Vector3Extensions.Dot2D(ref v1, ref v2, out dot12);

				//compute barycentric coordinates
				float invDenom = 1.0f / (dot00 * dot11 - dot01 * dot01);
				float u = (dot11 * dot02 - dot01 * dot12) * invDenom;
				float v = (dot00 * dot12 - dot01 * dot02) * invDenom;

				const float EPS = 1E-4f;

				//if point lies inside triangle, return interpolated y-coordinate
				if (u >= -EPS && v >= -EPS && (u + v) <= 1 + EPS)
				{
					height = a.Y + v0.Y * u + v1.Y * v;
					return true;
				}

				height = float.MaxValue;
				return false;
			}
		}

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
				float EPS = 0.00000001f;

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
					if (Math.Abs(d) < EPS)
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
}
