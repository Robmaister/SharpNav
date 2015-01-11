// Copyright (c) 2013-2015 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
// Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

using SharpNav.Geometry;

#if MONOGAME
using Vector3 = Microsoft.Xna.Framework.Vector3;
#elif OPENTK
using Vector3 = OpenTK.Vector3;
#elif SHARPDX
using Vector3 = SharpDX.Vector3;
#endif

namespace SharpNav
{
	/// <summary>
	/// A class that filters geometry and applies an <see cref="Area"/> to it.
	/// </summary>
	public class AreaGenerator
	{
		private IEnumerable<Triangle3> tris;
		private int triCount;
		private List<Tuple<Func<Triangle3, bool>, Area>> conditions;
		private Area defaultArea;

		/// <summary>
		/// Initializes a new instance of the <see cref="SharpNav.AreaGenerator"/> class.
		/// </summary>
		/// <param name="verts">collection of Triangles.</param>
		/// <param name="triCount">The number of triangles to enumerate..</param>
		/// <param name="defaultArea">Default area.</param>
		private AreaGenerator(IEnumerable<Triangle3> verts, int triCount, Area defaultArea)
		{
			this.tris = verts;
			this.triCount = triCount;
			this.defaultArea = defaultArea;
			conditions = new List<Tuple<Func<Triangle3, bool>, Area>>();
		}

		/// <summary>
		/// Create instance from the specified triangles with specified area.
		/// </summary>
		/// <param name="tris">Collection of Triangles.</param>
		/// <param name="area">Area of Triangle.</param>
		/// <returns>A new AreaIdGenerator.</returns>
		public static AreaGenerator From(IEnumerable<Triangle3> tris, Area area)
		{
			return new AreaGenerator(tris, tris.Count(), area);
		}

		/// <summary>
		/// Create instance from triCount(a integer) specified triangles with specified area
		/// </summary>
		/// <param name="tris">Collection of Triangles.</param>
		/// <param name="triCount">The number of triangles to enumerate.</param>
		/// <param name="area">Area of Triangle.</param>
		/// <returns>A new AreaIdGenerator.</returns>
		public static AreaGenerator From(IEnumerable<Triangle3> tris, int triCount, Area area)
		{
			return new AreaGenerator(tris, triCount, area);
		}

		/// <summary>
		/// Create instance from every specified triangles in array of tris with specified area.
		/// </summary>
		/// <param name="tris">An array of triangles.</param>
		/// <param name="area">Area of Triangle.</param>
		/// <returns>A new AreaIdGenerator.</returns>
		public static AreaGenerator From(Triangle3[] tris, Area area)
		{
			return new AreaGenerator(TriangleEnumerable.FromTriangle(tris, 0, tris.Length), tris.Length, area);
		}

		/// <summary>
		/// Create instance from specified triangles in array of tris from tris[triOffset] to tris[triOffset+triCount] with specified area.
		/// </summary>
		/// <param name="tris">An array of triangles.</param>
		/// <param name="triOffset">Tri offset.</param>
		/// <param name="triCount">Tri count.</param>
		/// <param name="area">Area of Triangle.</param>
		/// <returns>A new AreaIdGenerator.</returns>
		public static AreaGenerator From(Triangle3[] tris, int triOffset, int triCount, Area area)
		{
			return new AreaGenerator(TriangleEnumerable.FromTriangle(tris, triOffset, triCount), triCount, area);
		}

		/// <summary>
		/// Create instance from the triangles created from points in verts with specified area 
		/// </summary>
		/// <param name="verts">An array of Vectors3.</param>
		/// <param name="area">Area of Triangle.</param>
		/// <returns>A new AreaIdGenerator.</returns>
		public static AreaGenerator From(Vector3[] verts, Area area)
		{
			return new AreaGenerator(TriangleEnumerable.FromVector3(verts, 0, 1, verts.Length / 3), verts.Length / 3, area);
		}

		/// <summary>
		/// Create instance from the triangles created from points start from verts[0*vertStride+vertOffset] to verts[(triCount-1)*vertStride+vertOffset]with specified area
		/// </summary>
		/// <param name="verts">An array of Vectors3.</param>
		/// <param name="vertOffset">The index of the first Vectex to be enumerated.</param>
		/// <param name="vertStride">The distance between the start of two triangles. A value of 0 means the data is tightly packed.</param>
		/// <param name="triCount">The number of triangles to enumerate..</param>
		/// <param name="area">Area of Triangle.</param>
		/// <returns>A new AreaIdGenerator.</returns>
		public static AreaGenerator From(Vector3[] verts, int vertOffset, int vertStride, int triCount, Area area)
		{
			return new AreaGenerator(TriangleEnumerable.FromVector3(verts, vertOffset, vertStride, triCount), triCount, area);
		}

		/// <summary>
		/// Create instance from the triangles created from points in verts with specified area
		/// </summary>
		/// <param name="verts">An array of vertices.</param>
		/// <param name="area">Area of Triangle.</param>
		/// <returns>A new AreaIdGenerator.</returns>
		public static AreaGenerator From(float[] verts, Area area)
		{
			return new AreaGenerator(TriangleEnumerable.FromFloat(verts, 0, 3, verts.Length / 9), verts.Length / 9, area);
		}

		/// <summary>
		/// Create instance from the triangles created from points start from verts[0*vertStride+vertOffset] to verts[(triCount-1)*vertStride+vertOffset]with specified area.
		/// </summary>
		/// <param name="verts">An array of vertices.</param>
		/// <param name="floatOffset">The index of the first float to be enumerated.</param>
		/// <param name="floatStride">The distance between the start of two vertices. A value of 0 means the data is tightly packed.</param>
		/// <param name="triCount">The number of triangles to enumerate.</param>
		/// <param name="area">Area of Triangle.</param>
		/// <returns>A new AreaIdGenerator.</returns>
		public static AreaGenerator From(float[] verts, int floatOffset, int floatStride, int triCount, Area area)
		{
			return new AreaGenerator(TriangleEnumerable.FromFloat(verts, floatOffset, floatStride, triCount), triCount, area);
		}

		/// <summary>
		/// Create instance from triangles created from points of verts which is created from array of index of vertices array with specified area 
		/// </summary>
		/// <param name="verts">An array of vertices.</param>
		/// <param name="inds">An array of indices.</param>
		/// <param name="area">Area of Triangle.</param>
		/// <returns>A new AreaIdGenerator.</returns>
		public static AreaGenerator From(Vector3[] verts, int[] inds, Area area)
		{
			return new AreaGenerator(TriangleEnumerable.FromIndexedVector3(verts, inds, 0, 1, 0, inds.Length / 3), inds.Length / 3, area);
		}

		/// <summary>
		/// Create instance from triangles created from points of verts which is created from array of index of vertices array with specified area 
		/// </summary>
		/// <param name="verts">An array of vertices.</param>
		/// <param name="inds">An array of indices.</param>
		/// <param name="vertOffset">The index of the first vertex to be enumerated.</param>
		/// <param name="vertStride">The distance between the start of two triangles. A value of 0 means the data is tightly packed.</param>
		/// <param name="indexOffset">The index of the first index to be enumerated.</param>
		/// <param name="triCount">The number of triangles to enumerate.</param>
		/// <param name="area">Area of Triangle.</param>
		/// <returns>A new AreaIdGenerator.</returns>
		public static AreaGenerator From(Vector3[] verts, int[] inds, int vertOffset, int vertStride, int indexOffset, int triCount, Area area)
		{
			return new AreaGenerator(TriangleEnumerable.FromIndexedVector3(verts, inds, vertOffset, vertStride, indexOffset, triCount), triCount, area);
		}

		/// <summary>
		/// Create instance from triangles created from points of verts which is created from array of index of vertices array with specified area 
		/// </summary>
		/// <param name="verts">An array of vertices.</param>
		/// <param name="inds">An array of indices.</param>
		/// <param name="area">Area of Triangle.</param>
		/// <returns>A new AreaIdGenerator.</returns>
		public static AreaGenerator From(float[] verts, int[] inds, Area area)
		{
			return new AreaGenerator(TriangleEnumerable.FromIndexedFloat(verts, inds, 0, 3, 0, inds.Length / 3), inds.Length / 3, area);
		}

		/// <summary>
		/// Create instance from triangles created from points of verts which is created from array of index of vertices array with specified area 
		/// </summary>
		/// <param name="verts">An array of vertices.</param>
		/// <param name="inds">An array of indices.</param>
		/// <param name="floatOffset">The index of the first float to be enumerated.</param>
		/// <param name="floatStride">The distance between the start of two vertices. A value of 0 means the data is tightly packed.</param>
		/// <param name="indexOffset">The index of the first index to be enumerated.</param>
		/// <param name="triCount">The number of triangles to enumerate.</param>
		/// <param name="area">Area of Triangle.</param>
		/// <returns>A new AreaIdGenerator.</returns>
		public static AreaGenerator From(float[] verts, int[] inds, int floatOffset, int floatStride, int indexOffset, int triCount, Area area)
		{
			return new AreaGenerator(TriangleEnumerable.FromIndexedFloat(verts, inds, floatOffset, floatStride, indexOffset, triCount), triCount, area);
		}

		/// <summary>
		/// Takes the mesh query, runs it, and outputs the result as an array of <see cref="Area"/>.
		/// </summary>
		/// <returns>The result of the query.</returns>
		public Area[] ToArray()
		{
			Area[] areas = new Area[triCount];

			int i = 0;
			foreach (var tri in tris)
			{
				areas[i] = defaultArea;

				foreach (var condition in conditions)
					if (condition.Item1(tri))
						areas[i] = condition.Item2;

				i++;
			}

			return areas;
		}

		/// <summary>
		/// Marks all triangles above a specified angle with a sepcified area ID.
		/// </summary>
		/// <param name="angle">The minimum angle in radians.</param>
		/// <param name="area">The area ID to set for triangles above the slope.</param>
		/// <returns>The same instance.</returns>
		public AreaGenerator MarkAboveSlope(float angle, Area area)
		{
			conditions.Add(Tuple.Create<Func<Triangle3, bool>, Area>(
				tri =>
				{
					Vector3 n = tri.Normal;
					float a;
					Vector3Extensions.CalculateSlopeAngle(ref n, out a);
					return a <= angle;
				},
				area));

			return this;
		}

		/// <summary>
		/// Marks all triangles below a specified angle with a sepcified area ID.
		/// </summary>
		/// <param name="angle">The maximum angle.</param>
		/// <param name="area">The area ID to set for triangles below the slope.</param>
		/// <returns>The same instance.</returns>
		public AreaGenerator MarkBelowSlope(float angle, Area area)
		{
			conditions.Add(Tuple.Create<Func<Triangle3, bool>, Area>(
				tri =>
				{
					Vector3 n = tri.Normal;
					float a;
					Vector3Extensions.CalculateSlopeAngle(ref n, out a);
					return a >= angle;
				},
				area));

			return this;
		}

		/// <summary>
		/// Marks all triangles around a specified angle with a sepcified area ID.
		/// </summary>
		/// <param name="angle">The angle.</param>
		/// <param name="range">The maximum allowed difference between the angle and a triangle's angle.</param>
		/// <param name="area">The area ID to set for triangles around the slope.</param>
		/// <returns>The same instance.</returns>
		public AreaGenerator MarkAtSlope(float angle, float range, Area area)
		{
			conditions.Add(Tuple.Create<Func<Triangle3, bool>, Area>(
				tri =>
				{
					Vector3 n = tri.Normal;
					float a;
					Vector3Extensions.CalculateSlopeAngle(ref n, out a);
					return a >= angle - range && a <= angle + range;
				},
				area));

			return this;
		}

		/// <summary>
		/// Marks all triangles below a specified height with a sepcified area ID.
		/// </summary>
		/// <param name="y">The height threshold of a triangle.</param>
		/// <param name="area">The area ID to set for triangles below the threshold.</param>
		/// <returns>The same instance.</returns>
		public AreaGenerator MarkBelowHeight(float y, Area area)
		{
			conditions.Add(Tuple.Create<Func<Triangle3, bool>, Area>(
				tri =>
				{
					if (tri.A.Y <= y || tri.B.Y <= y || tri.C.Y <= y)
						return true;

					return false;
				},
				area));

			return this;
		}

		/// <summary>
		/// Marks all triangles around a specified height with a sepcified area ID.
		/// </summary>
		/// <param name="y">The height value.</param>
		/// <param name="radius">The maximum allowed difference between the height and a triangle's height.</param>
		/// <param name="area">The area ID to set for triangles around the height.</param>
		/// <returns>The same instance.</returns>
		public AreaGenerator MarkAtHeight(float y, float radius, Area area)
		{
			throw new NotImplementedException();
		}

		/// <summary>
		/// Marks all triangles above a specified height with a sepcified area ID.
		/// </summary>
		/// <param name="y">The height threshold of a triangle.</param>
		/// <param name="area">The area ID to set for triangles above the threshold.</param>
		/// <returns>The same instance.</returns>
		public AreaGenerator MarkAboveHeight(float y, Area area)
		{
			conditions.Add(Tuple.Create<Func<Triangle3, bool>, Area>(
				tri =>
				{
					if (tri.A.Y >= y || tri.B.Y >= y || tri.C.Y >= y)
						return true;

					return false;
				},
				area));

			return this;
		}

		/// <summary>
		/// Marks all triangles that meet a specified condition with a specified area ID.
		/// </summary>
		/// <param name="func">The condition to be tested on each triangle.</param>
		/// <param name="area">The area ID to set for triangles that match the condition.</param>
		/// <returns>The same instance.</returns>
		public AreaGenerator MarkCustomFilter(Func<Triangle3, bool> func, Area area)
		{
			conditions.Add(Tuple.Create(func, area));

			return this;
		}
	}
}
