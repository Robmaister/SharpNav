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

		internal static float PointToPolygonSquared(Vector3 point, Vector3[] verts, int vertCount)
		{
			float dmin = float.MaxValue;
			bool c = false;

			for (int i = 0, j = vertCount - 1; i < vertCount; j = i++)
			{
				Vector3 vi = verts[i];
				Vector3 vj = verts[j];

				if (((vi.Z > point.Z) != (vj.Z > point.Z)) && (point.X < (vj.X - vi.X) * (point.Z - vi.Z) / (vj.Z - vi.Z) + vi.X))
					c = !c;

				dmin = Math.Min(dmin, Distance.PointToSegment2DSquared(ref point, ref vj, ref vi));
			}

			return c ? -dmin : dmin;
		}

		//TOOD where did these come from?

		/// <summary>
		/// Finds the squared distance between a point and the nearest edge of a polygon.
		/// </summary>
		/// <param name="pt">A point.</param>
		/// <param name="verts">A set of vertices that define a polygon.</param>
		/// <param name="nverts">The number of vertices to use from <c>verts</c>.</param>
		/// <returns>The squared distance between a point and the nearest edge of a polygon.</returns>
		internal static float PointToPolygonEdgeSquared(Vector3 pt, Vector3[] verts, int nverts)
		{
			float dmin = float.MaxValue;
			for (int i = 0, j = nverts - 1; i < nverts; j = i++)
				dmin = Math.Min(dmin, Distance.PointToSegment2DSquared(ref pt, ref verts[j], ref verts[i]));

			return Containment.PointInPoly(pt, verts, nverts) ? -dmin : dmin;
		}

		/// <summary>
		/// Finds the distance between a point and the nearest edge of a polygon.
		/// </summary>
		/// <param name="pt">A point.</param>
		/// <param name="verts">A set of vertices that define a polygon.</param>
		/// <param name="nverts">The number of vertices to use from <c>verts</c>.</param>
		/// <param name="edgeDist">A buffer for edge distances to be stored in.</param>
		/// <param name="edgeT">A buffer for parametrization ratios to be stored in.</param>
		/// <returns>A value indicating whether the point is contained in the polygon.</returns>
		internal static bool PointToPolygonEdgeSquared(Vector3 pt, Vector3[] verts, int nverts, float[] edgeDist, float[] edgeT)
		{
			for (int i = 0, j = nverts - 1; i < nverts; j = i++)
				edgeDist[j] = PointToSegment2DSquared(ref pt, ref verts[j], ref verts[i], out edgeT[j]);

			return Containment.PointInPoly(pt, verts, nverts);
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
}
