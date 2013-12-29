#region License
/**
 * Copyright (c) 2013 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
 * Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE
 */
#endregion

#if MONOGAME || XNA
using Microsoft.Xna.Framework;
#elif OPENTK
using OpenTK;
#elif SHARPDX
using SharpDX;
#endif

namespace SharpNav
{
	/// <summary>
	/// A class where all the small, miscellaneous math functions are stored.
	/// </summary>
	internal static class MathHelper
	{
		private static readonly int[] DirOffsetsX = { -1, 0, 1, 0 };
		private static readonly int[] DirOffsetsY = { 0, 1, 0, -1 };

		/// <summary>
		/// Gets an X offset.
		/// </summary>
		/// <remarks>
		/// The directions cycle between the following, starting from 0: west, north, east, south.
		/// </remarks>
		/// <param name="dir">The direction.</param>
		/// <returns>The offset for the X coordinate.</returns>
		internal static int GetDirOffsetX(int dir)
		{
			return DirOffsetsX[dir % 4];
		}

		/// <summary>
		/// Get a Y offset.
		/// </summary>
		/// <remarks>
		/// The directions cycle between the following, starting from 0: west, north, east, south.
		/// </remarks>
		/// <param name="dir">The direction.</param>
		/// <returns>The offset for the Y coordinate.</returns>
		internal static int GetDirOffsetY(int dir)
		{
			return DirOffsetsY[dir % 4];
		}

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
		/// Calculates the next highest power of two.
		/// </summary>
		/// <param name="v">A value.</param>
		/// <returns>The next power of two after the value.</returns>
		public static int NextPowerOfTwo(int v)
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
		/// <param name="v">A value.</param>
		/// <returns>The next power of two after the value.</returns>
		public static uint NextPowerOfTwo(uint v)
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

		public static int Log2(int v)
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

		public static uint Log2(uint v)
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
			float[] distances = new float[12];
			for (int i = 0; i < numVerts; i++)
				distances[i] = planeX * inVertices[i].X + planeZ * inVertices[i].Z + planeD;

			int m = 0;
			for (int i = 0, j = numVerts - 1; i < numVerts; j = i, i++)
			{
				bool inj = distances[j] >= 0;
				bool ini = distances[i] >= 0;

				if (inj != ini)
				{
					float s = distances[j] / (distances[j] - distances[i]);
					outVertices[m].X = inVertices[j].X + (inVertices[i].X - inVertices[j].X) * s;
					outVertices[m].Y = inVertices[j].Y + (inVertices[i].Y - inVertices[j].Y) * s;
					outVertices[m].Z = inVertices[j].Z + (inVertices[i].Z - inVertices[j].Z) * s;
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
		/// Find the 3D distance between a point (x, y, z) and a segment PQ
		/// </summary>
		/// <param name="pt">The coordinate of the point.</param>
		/// <param name="p">The coordinate of point P in the segment PQ.</param>
		/// <param name="q">The coordinate of point Q in the segment PQ.</param>
		/// <returns>The distance between the point and the segment.</returns>
		internal static float DistanceFromPointToSegment(ref Vector3 pt, ref Vector3 p, ref Vector3 q)
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
		/// Find the 2d distance between a point and a segment PQ
		/// </summary>
		/// <param name="pt">The coordinate of the point.</param>
		/// <param name="p">The coordinate of point P in the segment PQ.</param>
		/// <param name="q">The coordinate of point Q in the segment PQ.</param>
		/// <returns>The distance between the point and the segment.</returns>
		internal static float DistanceFromPointToSegment2D(ref Vector3 pt, ref Vector3 p, ref Vector3 q)
		{
			//distance from P to Q in the xz plane
			float segmentDeltaX = q.X - p.X;
			float segmentDeltaZ = q.Z - p.Z;

			//distance from P to lone point in xz plane
			float dx = pt.X - p.X;
			float dz = pt.Z - p.Z;

			float segmentMagnitudeSquared = segmentDeltaX * segmentDeltaX + segmentDeltaZ * segmentDeltaZ;
			float t = segmentDeltaX * dx + segmentDeltaZ * dz;

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
		/// Find the 2d distance between a point (x, z) and a segment PQ, where P is (px, pz) and Q is (qx, qz).
		/// </summary>
		/// <param name="x">The X coordinate of the point.</param>
		/// <param name="z">The Z coordinate of the point.</param>
		/// <param name="px">The X coordinate of point P in the segment PQ.</param>
		/// <param name="pz">The Z coordinate of point P in the segment PQ.</param>
		/// <param name="qx">The X coordinate of point Q in the segment PQ.</param>
		/// <param name="qz">The Z coordinate of point Q in the segment PQ.</param>
		/// <returns>The distance between the point and the segment.</returns>
		internal static float DistanceFromPointToSegment2D(int x, int z, int px, int pz, int qx, int qz)
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
	}
}
