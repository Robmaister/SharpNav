#region License
/**
 * Copyright (c) 2013-2014 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
 * Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

using SharpNav.Geometry;

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
	/// A class that filters geometry and applies an <see cref="AreaId"/> to it.
	/// </summary>
	public class AreaIdGenerator
	{
		private IEnumerable<Triangle3> tris;
		private int triCount;
		private List<Tuple<Func<Triangle3, bool>, AreaId>> conditions;
		private AreaId defaultArea;

		/// <summary>
		/// Initializes a new instance of the <see cref="SharpNav.AreaIdGenerator"/> class.
		/// </summary>
		/// <param name="verts">collection of Triangles.</param>
		/// <param name="triCount">The number of triangles to enumerate..</param>
		/// <param name="defaultArea">Default area.</param>
		private AreaIdGenerator(IEnumerable<Triangle3> verts, int triCount, AreaId defaultArea)
		{
			this.tris = verts;
			this.triCount = triCount;
			this.defaultArea = defaultArea;
			conditions = new List<Tuple<Func<Triangle3, bool>, AreaId>>();
		}

		/// <summary>
		/// Create instance from the specified triangles with specified area.
		/// </summary>
		/// <param name="tris">Collection of Triangles.</param>
		/// <param name="area">Area of Triangle.</param>
		/// <returns>A new AreaIdGenerator.</returns>
		public static AreaIdGenerator From(IEnumerable<Triangle3> tris, AreaId area)
		{
			return new AreaIdGenerator(tris, tris.Count(), area);
		}

		/// <summary>
		/// Create instance from triCount(a integer) specified triangles with specified area
		/// </summary>
		/// <param name="tris">Collection of Triangles.</param>
		/// <param name="triCount">The number of triangles to enumerate.</param>
		/// <param name="area">Area of Triangle.</param>
		/// <returns>A new AreaIdGenerator.</returns>
		public static AreaIdGenerator From(IEnumerable<Triangle3> tris, int triCount, AreaId area)
		{
			return new AreaIdGenerator(tris, triCount, area);
		}

		/// <summary>
		/// Create instance from every specified triangles in array of tris with specified area.
		/// </summary>
		/// <param name="tris">An array of triangles.</param>
		/// <param name="area">Area of Triangle.</param>
		/// <returns>A new AreaIdGenerator.</returns>
		public static AreaIdGenerator From(Triangle3[] tris, AreaId area)
		{
			return new AreaIdGenerator(TriangleEnumerable.FromTriangle(tris, 0, tris.Length), tris.Length, area);
		}

		/// <summary>
		/// Create instance from specified triangles in array of tris from tris[triOffset] to tris[triOffset+triCount] with specified area.
		/// </summary>
		/// <param name="tris">An array of triangles.</param>
		/// <param name="triOffset">Tri offset.</param>
		/// <param name="triCount">Tri count.</param>
		/// <param name="area">Area of Triangle.</param>
		/// <returns>A new AreaIdGenerator.</returns>
		public static AreaIdGenerator From(Triangle3[] tris, int triOffset, int triCount, AreaId area)
		{
			return new AreaIdGenerator(TriangleEnumerable.FromTriangle(tris, triOffset, triCount), triCount, area);
		}

		/// <summary>
		/// Create instance from the triangles created from points in verts with specified area 
		/// </summary>
		/// <param name="verts">An array of Vectors3.</param>
		/// <param name="area">Area of Triangle.</param>
		/// <returns>A new AreaIdGenerator.</returns>
		public static AreaIdGenerator From(Vector3[] verts, AreaId area)
		{
			return new AreaIdGenerator(TriangleEnumerable.FromVector3(verts, 0, 1, verts.Length / 3), verts.Length / 3, area);
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
		public static AreaIdGenerator From(Vector3[] verts, int vertOffset, int vertStride, int triCount, AreaId area)
		{
			return new AreaIdGenerator(TriangleEnumerable.FromVector3(verts, vertOffset, vertStride, triCount), triCount, area);
		}

		/// <summary>
		/// Create instance from the triangles created from points in verts with specified area
		/// </summary>
		/// <param name="verts">An array of vertices.</param>
		/// <param name="area">Area of Triangle.</param>
		/// <returns>A new AreaIdGenerator.</returns>
		public static AreaIdGenerator From(float[] verts, AreaId area)
		{
			return new AreaIdGenerator(TriangleEnumerable.FromFloat(verts, 0, 3, verts.Length / 9), verts.Length / 9, area);
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
		public static AreaIdGenerator From(float[] verts, int floatOffset, int floatStride, int triCount, AreaId area)
		{
			return new AreaIdGenerator(TriangleEnumerable.FromFloat(verts, floatOffset, floatStride, triCount), triCount, area);
		}

		/// <summary>
		/// Create instance from triangles created from points of verts which is created from array of index of vertices array with specified area 
		/// </summary>
		/// <param name="verts">An array of vertices.</param>
		/// <param name="inds">An array of indices.</param>
		/// <param name="area">Area of Triangle.</param>
		/// <returns>A new AreaIdGenerator.</returns>
		public static AreaIdGenerator From(Vector3[] verts, int[] inds, AreaId area)
		{
			return new AreaIdGenerator(TriangleEnumerable.FromIndexedVector3(verts, inds, 0, 1, 0, inds.Length / 3), inds.Length / 3, area);
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
		public static AreaIdGenerator From(Vector3[] verts, int[] inds, int vertOffset, int vertStride, int indexOffset, int triCount, AreaId area)
		{
			return new AreaIdGenerator(TriangleEnumerable.FromIndexedVector3(verts, inds, vertOffset, vertStride, indexOffset, triCount), triCount, area);
		}

		/// <summary>
		/// Create instance from triangles created from points of verts which is created from array of index of vertices array with specified area 
		/// </summary>
		/// <param name="verts">An array of vertices.</param>
		/// <param name="inds">An array of indices.</param>
		/// <param name="area">Area of Triangle.</param>
		/// <returns>A new AreaIdGenerator.</returns>
		public static AreaIdGenerator From(float[] verts, int[] inds, AreaId area)
		{
			return new AreaIdGenerator(TriangleEnumerable.FromIndexedFloat(verts, inds, 0, 3, 0, inds.Length / 3), inds.Length / 3, area);
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
		public static AreaIdGenerator From(float[] verts, int[] inds, int floatOffset, int floatStride, int indexOffset, int triCount, AreaId area)
		{
			return new AreaIdGenerator(TriangleEnumerable.FromIndexedFloat(verts, inds, floatOffset, floatStride, indexOffset, triCount), triCount, area);
		}

		/// <summary>
		/// Takes the mesh query, runs it, and outputs the result as an <see cref="AreaId[]"/>.
		/// </summary>
		/// <returns>The result of the query.</returns>
		public AreaId[] ToArray()
		{
			AreaId[] areas = new AreaId[triCount];

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
		public AreaIdGenerator MarkAboveSlope(float angle, AreaId area)
		{
			conditions.Add(Tuple.Create<Func<Triangle3, bool>, AreaId>(
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
		public AreaIdGenerator MarkBelowSlope(float angle, AreaId area)
		{
			conditions.Add(Tuple.Create<Func<Triangle3, bool>, AreaId>(
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
		public AreaIdGenerator MarkAtSlope(float angle, float range, AreaId area)
		{
			conditions.Add(Tuple.Create<Func<Triangle3, bool>, AreaId>(
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
		public AreaIdGenerator MarkBelowHeight(float y, AreaId area)
		{
			conditions.Add(Tuple.Create<Func<Triangle3, bool>, AreaId>(
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
		public AreaIdGenerator MarkAtHeight(float y, float radius, AreaId area)
		{
			throw new NotImplementedException();
		}

		/// <summary>
		/// Marks all triangles above a specified height with a sepcified area ID.
		/// </summary>
		/// <param name="y">The height threshold of a triangle.</param>
		/// <param name="area">The area ID to set for triangles above the threshold.</param>
		/// <returns>The same instance.</returns>
		public AreaIdGenerator MarkAboveHeight(float y, AreaId area)
		{
			conditions.Add(Tuple.Create<Func<Triangle3, bool>, AreaId>(
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
		public AreaIdGenerator MarkCustomFilter(Func<Triangle3, bool> func, AreaId area)
		{
			conditions.Add(Tuple.Create(func, area));

			return this;
		}
	}
}
