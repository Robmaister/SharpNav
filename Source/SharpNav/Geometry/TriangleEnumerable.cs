// Copyright (c) 2014-2015 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
// Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE

using System;
using System.Collections.Generic;

#if MONOGAME
using Vector3 = Microsoft.Xna.Framework.Vector3;
#elif OPENTK
using Vector3 = OpenTK.Vector3;
#elif SHARPDX
using Vector3 = SharpDX.Vector3;
#endif

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
		/// <param name="triangles">An array of triangles.</param>
		/// <param name="triOffset">The index of the first triangle to be enumerated.</param>
		/// <param name="triCount">The number of triangles to enumerate.</param>
		/// <returns>An enumerable collection of triangles.</returns>
		public static IEnumerable<Triangle3> FromTriangle(Triangle3[] triangles, int triOffset, int triCount)
		{
			for (int i = 0; i < triCount; i++)
				yield return triangles[triOffset + i];
		}

		/// <summary>
		/// Iterates over an array of <see cref="Vector3"/> with a specified offset, stride, and length.
		/// </summary>
		/// <param name="vertices">An array of vertices.</param>
		/// <param name="vertOffset">The index of the first vertex to be enumerated.</param>
		/// <param name="vertStride">The distance between the start of two triangles. A value of 0 means the data is tightly packed.</param>
		/// <param name="triCount">The number of triangles to enumerate.</param>
		/// <returns>An enumerable collection of triangles.</returns>
		public static IEnumerable<Triangle3> FromVector3(Vector3[] vertices, int vertOffset, int vertStride, int triCount)
		{
			Triangle3 tri;

			if (vertStride == 0)
				vertStride = 3;

			for (int i = 0; i < triCount; i++)
			{
				tri.A = vertices[i * vertStride + vertOffset];
				tri.B = vertices[i * vertStride + vertOffset + 1];
				tri.C = vertices[i * vertStride + vertOffset + 2];

				yield return tri;
			}
		}

		/// <summary>
		/// Iterates over an array of <see cref="float"/> with a specified offset, stride, and length.
		/// </summary>
		/// <param name="vertices">An array of vertices.</param>
		/// <param name="floatOffset">The index of the first float to be enumerated.</param>
		/// <param name="floatStride">The distance between the start of two vertices. A value of 0 means the data is tightly packed.</param>
		/// <param name="triCount">The number of triangles to enumerate.</param>
		/// <returns>An enumerable collection of triangles.</returns>
		public static IEnumerable<Triangle3> FromFloat(float[] vertices, int floatOffset, int floatStride, int triCount)
		{
			Triangle3 tri;

			if (floatStride == 0)
				floatStride = 3;

			for (int i = 0; i < triCount; i++)
			{
				int indA = i * (floatStride * 3) + floatOffset;
				int indB = indA + floatStride;
				int indC = indB + floatStride;

				tri.A.X = vertices[indA];
				tri.A.Y = vertices[indA + 1];
				tri.A.Z = vertices[indA + 2];

				tri.B.X = vertices[indB];
				tri.B.Y = vertices[indB + 1];
				tri.B.Z = vertices[indB + 2];

				tri.C.X = vertices[indC];
				tri.C.Y = vertices[indC + 1];
				tri.C.Z = vertices[indC + 2];

				yield return tri;
			}
		}

		/// <summary>
		/// Iterates over an array of <see cref="Vector3"/> indexed by an array of <see cref="int"/> with a specified offset, stride, and length.
		/// </summary>
		/// <param name="vertices">An array of vertices.</param>
		/// <param name="indices">An array of indices.</param>
		/// <param name="vertOffset">The index of the first vertex to be enumerated.</param>
		/// <param name="vertStride">The distance between the start of two triangles. A value of 0 means the data is tightly packed.</param>
		/// <param name="indexOffset">The index of the first index to be enumerated.</param>
		/// <param name="triCount">The number of triangles to enumerate.</param>
		/// <returns>An enumerable collection of triangles.</returns>
		public static IEnumerable<Triangle3> FromIndexedVector3(Vector3[] vertices, int[] indices, int vertOffset, int vertStride, int indexOffset, int triCount)
		{
			Triangle3 tri;

			for (int i = 0; i < triCount; i++)
			{
				int indA = vertOffset + indices[i * 3 + indexOffset] * vertStride;
				int indB = vertOffset + indices[i * 3 + indexOffset + 1] * vertStride;
				int indC = vertOffset + indices[i * 3 + indexOffset + 2] * vertStride;

				tri.A = vertices[indA];
				tri.B = vertices[indB];
				tri.C = vertices[indC];

				yield return tri;
			}
		}

		/// <summary>
		/// Iterates over an array of <see cref="float"/> indexed by an array of <see cref="int"/> with a specified offset, stride, and length.
		/// </summary>
		/// <param name="vertices">An array of vertices.</param>
		/// <param name="indices">An array of indices.</param>
		/// <param name="floatOffset">The index of the first float to be enumerated.</param>
		/// <param name="floatStride">The distance between the start of two vertices. A value of 0 means the data is tightly packed.</param>
		/// <param name="indexOffset">The index of the first index to be enumerated.</param>
		/// <param name="triCount">The number of triangles to enumerate.</param>
		/// <returns>An enumerable collection of triangles.</returns>
		public static IEnumerable<Triangle3> FromIndexedFloat(float[] vertices, int[] indices, int floatOffset, int floatStride, int indexOffset, int triCount)
		{
			Triangle3 tri;

			for (int i = 0; i < triCount; i++)
			{
				int indA = floatOffset + indices[i * 3 + indexOffset] * floatStride;
				int indB = floatOffset + indices[i * 3 + indexOffset + 1] * floatStride;
				int indC = floatOffset + indices[i * 3 + indexOffset + 2] * floatStride;

				tri.A.X = vertices[indA];
				tri.A.Y = vertices[indA + 1];
				tri.A.Z = vertices[indA + 2];

				tri.B.X = vertices[indB];
				tri.B.Y = vertices[indB + 1];
				tri.B.Z = vertices[indB + 2];

				tri.C.X = vertices[indC];
				tri.C.Y = vertices[indC + 1];
				tri.C.Z = vertices[indC + 2];

				yield return tri;
			}
		}

		/// <summary>
		/// Generates a bounding box for a collection of triangles.
		/// </summary>
		/// <param name="tris">The triangles to create a bounding box from.</param>
		/// <returns>A bounding box containing every triangle.</returns>
		public static BBox3 GetBoundingBox(this IEnumerable<Triangle3> tris)
		{
			return GetBoundingBox(tris, float.Epsilon * 2f);
		}

		/// <summary>
		/// Generates a bounding box for a collection of triangles.
		/// </summary>
		/// <param name="tris">The triangles to create a bounding box from.</param>
		/// <param name="padding">Padding to the bounding box</param>
		/// <returns>A bounding box containing every triangle.</returns>
		public static BBox3 GetBoundingBox(this IEnumerable<Triangle3> tris, float padding)
		{
			BBox3 bounds = new BBox3();
			Vector3 va, vb, vc;
			foreach (Triangle3 tri in tris)
			{
				va = tri.A;
				vb = tri.B;
				vc = tri.C;
				ApplyVertexToBounds(ref va, ref bounds);
				ApplyVertexToBounds(ref vb, ref bounds);
				ApplyVertexToBounds(ref vc, ref bounds);
			}

			//pad the bounding box a bit to make sure outer triangles are fully contained.
			ApplyPaddingToBounds(padding, ref bounds);

			return bounds;
		}

		/// <summary>
		/// Generates a bounding box for a collection of vectors.
		/// </summary>
		/// <param name="vecs">The vectors to create a bounding box from.</param>
		/// <returns>A bounding box containing every vector.</returns>
		public static BBox3 GetBoundingBox(this IEnumerable<Vector3> vecs)
		{
			BBox3 bounds = new BBox3();
			Vector3 v;
			foreach (Vector3 vec in vecs)
			{
				v = vec;
				ApplyVertexToBounds(ref v, ref bounds);
			}

			ApplyPaddingToBounds(1.0f, ref bounds);

			return bounds;
		}

		/// <summary>
		/// Adjusts a bounding box to include a vertex.
		/// </summary>
		/// <param name="v">The vertex to include.</param>
		/// <param name="b">The bounding box to adjust.</param>
		private static void ApplyVertexToBounds(ref Vector3 v, ref BBox3 b)
		{
			if (v.X < b.Min.X)
				b.Min.X = v.X;
			if (v.Y < b.Min.Y)
				b.Min.Y = v.Y;
			if (v.Z < b.Min.Z)
				b.Min.Z = v.Z;
			if (v.X > b.Max.X)
				b.Max.X = v.X;
			if (v.Y > b.Max.Y)
				b.Max.Y = v.Y;
			if (v.Z > b.Max.Z)
				b.Max.Z = v.Z;
		}

		/// <summary>
		/// Applies a padding to the bounding box.
		/// </summary>
		/// <param name="pad">The amount to pad the bounding box on all sides.</param>
		/// <param name="b">The bounding box to pad.</param>
		private static void ApplyPaddingToBounds(float pad, ref BBox3 b)
		{
			b.Min.X -= pad;
			b.Min.Y -= pad;
			b.Min.Z -= pad;
			b.Max.X += pad;
			b.Max.Y += pad;
			b.Max.Z += pad;
		}
	}
}
