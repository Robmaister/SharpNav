#region License
/**
 * Copyright (c) 2014 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
 * Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE
 */
#endregion

using System;
using System.Collections.Generic;

namespace SharpNav.Geometry
{
	//TODO array bounds checking to catch out of bounds exceptions early.
	//TODO index arrays of other integral types (byte, sbyte, short, ushort, uint) - possibly make generic?

	/// <summary>
	/// A static class that generates an <see cref="IEnumerable{T}"/> of <see cref="Triangle3"/> using iterator blocks.
	/// </summary>
	public static class TriangleEnumerable
	{
		/// <summary>
		/// Iterates over an array of <see cref="Triangle3"/> with a specified offset and length.
		/// </summary>
		/// <param name="tris">An array of triangles.</param>
		/// <param name="triOffset">The index of the first triangle to be enumerated.</param>
		/// <param name="triCount">The number of triangles to enumerate.</param>
		/// <returns>An enumerable collection of triangles.</returns>
		public static IEnumerable<Triangle3> FromTriangle(Triangle3[] tris, int triOffset, int triCount)
		{
			for (int i = 0; i < triCount; i++)
				yield return tris[triOffset + i];
		}

		/// <summary>
		/// Iterates over an array of <see cref="Vector3"/> with a specified offset, stride, and length.
		/// </summary>
		/// <param name="verts">An array of vertices.</param>
		/// <param name="vertOffset">The index of the first vertex to be enumerated.</param>
		/// <param name="vertStride">The distance between the start of two triangles. A value of 0 means the data is tightly packed.</param>
		/// <param name="triCount">The number of triangles to enumerate.</param>
		/// <returns>An enumerable collection of triangles.</returns>
		public static IEnumerable<Triangle3> FromVector3(Vector3[] verts, int vertOffset, int vertStride, int triCount)
		{
			Triangle3 tri;

			if (vertStride == 0)
				vertStride = 3;

			for (int i = 0; i < triCount; i++)
			{
				tri.A = verts[i * vertStride + vertOffset];
				tri.B = verts[i * vertStride + vertOffset + 1];
				tri.C = verts[i * vertStride + vertOffset + 2];

				yield return tri;
			}
		}

		/// <summary>
		/// Iterates over an array of <see cref="float"/> with a specified offset, stride, and length.
		/// </summary>
		/// <param name="verts">An array of vertices.</param>
		/// <param name="floatOffset">The index of the first float to be enumerated.</param>
		/// <param name="floatStride">The distance between the start of two vertices. A value of 0 means the data is tightly packed.</param>
		/// <param name="triCount">The number of triangles to enumerate.</param>
		/// <returns>An enumerable collection of triangles.</returns>
		public static IEnumerable<Triangle3> FromFloat(float[] verts, int floatOffset, int floatStride, int triCount)
		{
			Triangle3 tri;

			if (floatStride == 0)
				floatStride = 3;

			for (int i = 0; i < triCount; i++)
			{
				int indA = i * floatStride + floatOffset;
				int indB = indA + floatStride;
				int indC = indB + floatStride;

				tri.A.X = verts[indA];
				tri.A.Y = verts[indA + 1];
				tri.A.Z = verts[indA + 2];

				tri.B.X = verts[indB];
				tri.B.Y = verts[indB + 1];
				tri.B.Z = verts[indB + 2];

				tri.C.X = verts[indC];
				tri.C.Y = verts[indC + 1];
				tri.C.Z = verts[indC + 2];

				yield return tri;
			}
		}

		/// <summary>
		/// Iterates over an array of <see cref="Vector3"/> indexed by an array of <see cref="int"/> with a specified offset, stride, and length.
		/// </summary>
		/// <param name="verts">An array of vertices.</param>
		/// <param name="inds">An array of indices.</param>
		/// <param name="vertOffset">The index of the first vertex to be enumerated.</param>
		/// <param name="vertStride">The distance between the start of two triangles. A value of 0 means the data is tightly packed.</param>
		/// <param name="indexOffset">The index of the first index to be enumerated.</param>
		/// <param name="triCount">The number of triangles to enumerate.</param>
		/// <returns>An enumerable collection of triangles.</returns>
		public static IEnumerable<Triangle3> FromIndexedVector3(Vector3[] verts, int[] inds, int vertOffset, int vertStride, int indexOffset, int triCount)
		{
			Triangle3 tri;

			for (int i = 0; i < triCount; i++)
			{
				int indA = vertOffset + inds[i * 3 + indexOffset] * vertStride;
				int indB = vertOffset + inds[i * 3 + indexOffset + 1] * vertStride;
				int indC = vertOffset + inds[i * 3 + indexOffset + 2] * vertStride;

				tri.A = verts[indA];
				tri.B = verts[indB];
				tri.C = verts[indC];

				yield return tri;
			}
		}

		/// <summary>
		/// Iterates over an array of <see cref="float"/> indexed by an array of <see cref="int"/> with a specified offset, stride, and length.
		/// </summary>
		/// <param name="verts">An array of vertices.</param>
		/// <param name="inds">An array of indices.</param>
		/// <param name="floatOffset">The index of the first float to be enumerated.</param>
		/// <param name="floatStride">The distance between the start of two vertices. A value of 0 means the data is tightly packed.</param>
		/// <param name="indexOffset">The index of the first index to be enumerated.</param>
		/// <param name="triCount">The number of triangles to enumerate.</param>
		/// <returns>An enumerable collection of triangles.</returns>
		public static IEnumerable<Triangle3> FromIndexedFloat(float[] verts, int[] inds, int floatOffset, int floatStride, int indexOffset, int triCount)
		{
			Triangle3 tri;

			for (int i = 0; i < triCount; i++)
			{
				int indA = floatOffset + inds[i * 3 + indexOffset] * floatStride;
				int indB = floatOffset + inds[i * 3 + indexOffset + 1] * floatStride;
				int indC = floatOffset + inds[i * 3 + indexOffset + 2] * floatStride;

				tri.A.X = verts[indA];
				tri.A.Y = verts[indA + 1];
				tri.A.Z = verts[indA + 2];

				tri.B.X = verts[indB];
				tri.B.Y = verts[indB + 1];
				tri.B.Z = verts[indB + 2];

				tri.C.X = verts[indC];
				tri.C.Y = verts[indC + 1];
				tri.C.Z = verts[indC + 2];

				yield return tri;
			}
		}
	}
}
