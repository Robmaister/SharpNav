// Copyright (c) 2013-2015 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
// Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

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
	/// <content>
	/// This file contains various methods to rasterize objects into voxel data.
	/// </content>
	public partial class Heightfield
	{
		//private ConcurrentQueue<Tuple<int, int, Span>> spanQueue;

		/// <summary>
		/// Rasterizes several triangles at once from an indexed array with per-triangle area flags.
		/// </summary>
		/// <param name="verts">An array of vertices.</param>
		/// <param name="inds">An array of indices.</param>
		/// <param name="areas">An array of area flags, one for each triangle.</param>
		public void RasterizeTrianglesIndexedWithAreas(Vector3[] verts, int[] inds, Area[] areas)
		{
			RasterizeTrianglesIndexedWithAreas(verts, inds, 0, 1, 0, inds.Length / 3, areas);
		}

		/// <summary>
		/// Rasterizes several triangles at once from an indexed array with per-triangle area flags.
		/// </summary>
		/// <param name="verts">An array of vertices.</param>
		/// <param name="inds">An array of indices.</param>
		/// <param name="vertOffset">An offset into the vertex array.</param>
		/// <param name="vertStride">The number of array elements that make up a vertex. A value of 0 is interpreted as tightly-packed data (one Vector3 per vertex).</param>
		/// <param name="indexOffset">An offset into the index array.</param>
		/// <param name="triCount">The number of triangles to rasterize.</param>
		/// <param name="areas">An array of area flags, one for each triangle.</param>
		public void RasterizeTrianglesIndexedWithAreas(Vector3[] verts, int[] inds, int vertOffset, int vertStride, int indexOffset, int triCount, Area[] areas)
		{
			int indexEnd = triCount * 3 + indexOffset;

			if (verts == null)
				throw new ArgumentNullException("verts");

			if (inds == null)
				throw new ArgumentNullException("inds");

			if (indexEnd > inds.Length)
				throw new ArgumentOutOfRangeException("indexCount", "The specified index offset and length end outside the provided index array.");

			if (vertOffset < 0)
				throw new ArgumentOutOfRangeException("vertOffset", "vertOffset must be greater than or equal to 0.");

			if (vertStride < 0)
				throw new ArgumentOutOfRangeException("vertStride", "vertStride must be greater than or equal to 0.");
			else if (vertStride == 0)
				vertStride = 1;

			if (areas.Length < triCount)
				throw new ArgumentException("There must be at least as many AreaFlags as there are triangles.", "areas");

			for (int i = indexOffset, j = 0; i < indexEnd; i += 3, j++)
			{
				int indA = inds[i] * vertStride + vertOffset;
				int indB = inds[i + 1] * vertStride + vertOffset;
				int indC = inds[i + 2] * vertStride + vertOffset;

				RasterizeTriangle(ref verts[indA], ref verts[indB], ref verts[indC], areas[j]);
			}
		}

		/// <summary>
		/// Rasterizes several triangles at once from an indexed array with per-triangle area flags.
		/// </summary>
		/// <param name="verts">An array of vertices.</param>
		/// <param name="inds">An array of indices.</param>
		/// <param name="areas">An array of area flags, one for each triangle.</param>
		public void RasterizeTrianglesIndexedWithAreas(float[] verts, int[] inds, Area[] areas)
		{
			RasterizeTrianglesIndexedWithAreas(verts, inds, 0, 3, 0, inds.Length / 3, areas);
		}

		/// <summary>
		/// Rasterizes several triangles at once from an indexed array with per-triangle area flags.
		/// </summary>
		/// <param name="verts">An array of vertices.</param>
		/// <param name="inds">An array of indices.</param>
		/// <param name="floatOffset">An offset into the vertex array.</param>
		/// <param name="floatStride">The number of array elements that make up a vertex. A value of 0 is interpreted as tightly-packed data (3 floats per vertex).</param>
		/// <param name="indexOffset">An offset into the index array.</param>
		/// <param name="triCount">The number of triangles to rasterize.</param>
		/// <param name="areas">An array of area flags, one for each triangle.</param>
		public void RasterizeTrianglesIndexedWithAreas(float[] verts, int[] inds, int floatOffset, int floatStride, int indexOffset, int triCount, Area[] areas)
		{
			int indexEnd = triCount * 3 + indexOffset;

			if (verts == null)
				throw new ArgumentNullException("verts");

			if (inds == null)
				throw new ArgumentNullException("inds");

			if (indexEnd > inds.Length)
				throw new ArgumentOutOfRangeException("indexCount", "The specified index offset and length end outside the provided index array.");

			if (floatOffset < 0)
				throw new ArgumentOutOfRangeException("floatOffset", "floatOffset must be greater than or equal to 0.");

			if (floatStride < 0)
				throw new ArgumentOutOfRangeException("floatStride", "floatStride must be greater than or equal to 0.");
			else if (floatStride == 0)
				floatStride = 3;

			if (areas.Length < triCount)
				throw new ArgumentException("There must be at least as many AreaFlags as there are triangles.", "areas");

			Vector3 a, b, c;

			for (int i = indexOffset, j = 0; i < indexEnd; i += 3, j++)
			{
				int indA = inds[i] * floatStride + floatOffset;
				int indB = inds[i + 1] * floatStride + floatOffset;
				int indC = inds[i + 2] * floatStride + floatOffset;

				a.X = verts[indA];
				a.Y = verts[indA + 1];
				a.Z = verts[indA + 2];

				b.X = verts[indB];
				b.Y = verts[indB + 1];
				b.Z = verts[indB + 2];

				c.X = verts[indC];
				c.Y = verts[indC + 1];
				c.Z = verts[indC + 2];

				RasterizeTriangle(ref a, ref b, ref c, areas[j]);
			}
		}

		/// <summary>
		/// Rasterizes several triangles at once with per-triangle area flags.
		/// </summary>
		/// <param name="tris">An array of triangles.</param>
		/// <param name="areas">An array of area flags, one for each triangle.</param>
		public void RasterizeTrianglesWithAreas(Triangle3[] tris, Area[] areas)
		{
			RasterizeTrianglesWithAreas(tris, 0, tris.Length, areas);
		}

		/// <summary>
		/// Rasterizes several triangles at once with per-triangle area flags.
		/// </summary>
		/// <param name="tris">An array of triangles.</param>
		/// <param name="triOffset">An offset into the array.</param>
		/// <param name="triCount">The number of triangles to rasterize, starting from the offset.</param>
		/// <param name="areas">An array of area flags, one for each triangle.</param>
		public void RasterizeTrianglesWithAreas(Triangle3[] tris, int triOffset, int triCount, Area[] areas)
		{
			int triEnd = triOffset + triCount;

			if (tris == null)
				throw new ArgumentNullException("verts");

			if (triOffset < 0)
				throw new ArgumentOutOfRangeException("triOffset", "triOffset must be greater than or equal to 0.");

			if (triCount < 0)
				throw new ArgumentOutOfRangeException("triCount", "triCount must be greater than or equal to 0.");

			if (triEnd > tris.Length)
				throw new ArgumentOutOfRangeException("triCount", "The specified offset and count end outside the bounds of the provided array.");

			if (areas.Length < triCount)
				throw new ArgumentException("There must be at least as many AreaFlags as there are triangles.", "areas");

			for (int i = triOffset, j = 0; i < triEnd; i++, j++)
				RasterizeTriangle(ref tris[i].A, ref tris[i].B, ref tris[i].C, areas[j]);
		}

		/// <summary>
		/// Rasterizes several triangles at once with per-triangle area flags.
		/// </summary>
		/// <remarks>
		/// If the length of the array is not a multiple of 3, the extra vertices at the end will be skipped.
		/// </remarks>
		/// <param name="verts">An array of vertices.</param>
		/// <param name="areas">An array of area flags, one for each triangle.</param>
		public void RasterizeTrianglesWithAreas(Vector3[] verts, Area[] areas)
		{
			RasterizeTrianglesWithAreas(verts, 0, 1, verts.Length / 3, areas);
		}

		/// <summary>
		/// Rasterizes several triangles at once with per-triangle area flags.
		/// </summary>
		/// <param name="verts">An array of vertices.</param>
		/// <param name="vertOffset">An offset into the array.</param>
		/// <param name="vertStride">The number of array elements that make up a vertex. A value of 0 is interpreted as tightly-packed data (1 Vector3 per vertex).</param>
		/// <param name="triCount">The number of triangles to rasterize, starting from the offset.</param>
		/// <param name="areas">An array of area flags, one for each triangle.</param>
		public void RasterizeTrianglesWithAreas(Vector3[] verts, int vertOffset, int vertStride, int triCount, Area[] areas)
		{
			if (verts == null)
				throw new ArgumentNullException("verts");

			if (vertOffset < 0)
				throw new ArgumentOutOfRangeException("vertOffset", "vertOffset must be greater than or equal to 0.");

			if (triCount < 0)
				throw new ArgumentOutOfRangeException("triCount", "triCount must be greater than or equal to 0.");

			if (vertStride < 0)
				throw new ArgumentOutOfRangeException("vertStride", "vertStride must be greater than or equal to 0.");
			else if (vertStride == 0)
				vertStride = 1;

			int vertEnd = triCount * vertStride + vertOffset;

			if (vertEnd > verts.Length)
				throw new ArgumentOutOfRangeException("triCount", "The specified offset, count, and stride end outside the bounds of the provided array.");

			if (areas.Length < triCount)
				throw new ArgumentException("There must be at least as many AreaFlags as there are triangles.", "areas");

			for (int i = vertOffset, j = 0; i < vertEnd; i += vertStride * 3, j++)
				RasterizeTriangle(ref verts[i], ref verts[i + vertStride], ref verts[i + vertStride * 2], areas[j]);
		}

		/// <summary>
		/// Rasterizes several triangles at once with per-triangle area flags.
		/// </summary>
		/// <remarks>
		/// If the length of the array is not a multiple of 9, the extra floats at the end will be skipped.
		/// </remarks>
		/// <param name="verts">An array of vertices.</param>
		/// <param name="areas">An array of area flags, one for each triangle.</param>
		public void RasterizeTrianglesWithAreas(float[] verts, Area[] areas)
		{
			RasterizeTrianglesWithAreas(verts, 0, 3, verts.Length / 9, areas);
		}

		/// <summary>
		/// Rasterizes several triangles at once with per-triangle area flags.
		/// </summary>
		/// <param name="verts">An array of vertices.</param>
		/// <param name="floatOffset">An offset into the array.</param>
		/// <param name="floatStride">The number of array elements that make up a vertex. A value of 0 is interpreted as tightly-packed data (3 floats per vertex).</param>
		/// <param name="triCount">The number of triangles to rasterize.</param>
		/// <param name="areas">An array of area flags, one for each triangle.</param>
		public void RasterizeTrianglesWithAreas(float[] verts, int floatOffset, int floatStride, int triCount, Area[] areas)
		{
			if (verts == null)
				throw new ArgumentNullException("verts");

			if (floatOffset < 0)
				throw new ArgumentOutOfRangeException("floatOffset", "floatOffset must be greater than or equal to 0.");

			if (triCount < 0)
				throw new ArgumentOutOfRangeException("triCount", "triCount must be greater than or equal to 0.");

			if (floatStride < 0)
				throw new ArgumentOutOfRangeException("floatStride", "floatStride must be a positive integer.");
			else if (floatStride == 0)
				floatStride = 3;

			int floatEnd = triCount * (floatStride * 3) + floatOffset;

			if (floatEnd > verts.Length)
				throw new ArgumentOutOfRangeException("triCount", "The specified offset, count, and stride end outside the bounds of the provided array.");

			if (areas.Length < triCount)
				throw new ArgumentException("There must be at least as many AreaFlags as there are triangles.", "areas");

			Vector3 a, b, c;

			for (int i = floatOffset, j = 0; i < floatEnd; i += floatStride * 3, j++)
			{
				int floatStride2 = floatStride * 2;

				a.X = verts[i];
				a.Y = verts[i + 1];
				a.Z = verts[i + 2];

				b.X = verts[i + floatStride];
				b.Y = verts[i + floatStride + 1];
				b.Z = verts[i + floatStride + 2];

				c.X = verts[i + floatStride2];
				c.Y = verts[i + floatStride2 + 1];
				c.Z = verts[i + floatStride2 + 2];

				RasterizeTriangle(ref a, ref b, ref c, areas[j]);
			}
		}

		/// <summary>
		/// Rasterizes several triangles at once from an indexed array.
		/// </summary>
		/// <param name="verts">An array of vertices.</param>
		/// <param name="inds">An array of indices.</param>
		public void RasterizeTrianglesIndexed(Vector3[] verts, int[] inds)
		{
			RasterizeTrianglesIndexed(verts, inds, 0, 1, 0, inds.Length / 3, Area.Default);
		}

		/// <summary>
		/// Rasterizes several triangles at once from an indexed array.
		/// </summary>
		/// <param name="verts">An array of vertices.</param>
		/// <param name="inds">An array of indices.</param>
		/// <param name="area">The area flags for all the triangles.</param>
		public void RasterizeTrianglesIndexed(Vector3[] verts, int[] inds, Area area)
		{
			RasterizeTrianglesIndexed(verts, inds, 0, 1, 0, inds.Length / 3, area);
		}

		/// <summary>
		/// Rasterizes several triangles at once from an indexed array.
		/// </summary>
		/// <param name="verts">An array of vertices.</param>
		/// <param name="inds">An array of indices.</param>
		/// <param name="vertOffset">An offset into the vertex array.</param>
		/// <param name="vertStride">The number of array elements that make up a vertex. A value of 0 is interpreted as tightly-packed data (one Vector3 per vertex).</param>
		/// <param name="indexOffset">An offset into the index array.</param>
		/// <param name="triCount">The number of triangles to rasterize.</param>
		public void RasterizeTrianglesIndexed(Vector3[] verts, int[] inds, int vertOffset, int vertStride, int indexOffset, int triCount)
		{
			RasterizeTrianglesIndexed(verts, inds, vertOffset, vertStride, indexOffset, triCount, Area.Default);
		}

		/// <summary>
		/// Rasterizes several triangles at once from an indexed array.
		/// </summary>
		/// <param name="verts">An array of vertices.</param>
		/// <param name="inds">An array of indices.</param>
		/// <param name="vertOffset">An offset into the vertex array.</param>
		/// <param name="vertStride">The number of array elements that make up a vertex. A value of 0 is interpreted as tightly-packed data (one Vector3 per vertex).</param>
		/// <param name="indexOffset">An offset into the index array.</param>
		/// <param name="triCount">The number of triangles to rasterize.</param>
		/// <param name="area">The area flags for all of the triangles.</param>
		public void RasterizeTrianglesIndexed(Vector3[] verts, int[] inds, int vertOffset, int vertStride, int indexOffset, int triCount, Area area)
		{
			int indexEnd = triCount * 3 + indexOffset;

			if (verts == null)
				throw new ArgumentNullException("verts");

			if (inds == null)
				throw new ArgumentNullException("inds");

			if (indexEnd > inds.Length)
				throw new ArgumentOutOfRangeException("indexCount", "The specified index offset and length end outside the provided index array.");

			if (vertOffset < 0)
				throw new ArgumentOutOfRangeException("vertOffset", "vertOffset must be greater than or equal to 0.");

			if (vertStride < 0)
				throw new ArgumentOutOfRangeException("vertStride", "vertStride must be greater than or equal to 0.");
			else if (vertStride == 0)
				vertStride = 1;

			for (int i = indexOffset; i < indexEnd; i += 3)
			{
				int indA = inds[i] * vertStride + vertOffset;
				int indB = inds[i + 1] * vertStride + vertOffset;
				int indC = inds[i + 2] * vertStride + vertOffset;

				RasterizeTriangle(ref verts[indA], ref verts[indB], ref verts[indC], area);
			}
		}

		/// <summary>
		/// Rasterizes several triangles at once from an indexed array.
		/// </summary>
		/// <param name="verts">An array of vertices.</param>
		/// <param name="inds">An array of indices.</param>
		public void RasterizeTrianglesIndexed(float[] verts, int[] inds)
		{
			RasterizeTrianglesIndexed(verts, inds, 0, 3, 0, inds.Length / 3, Area.Default);
		}

		/// <summary>
		/// Rasterizes several triangles at once from an indexed array.
		/// </summary>
		/// <param name="verts">An array of vertices.</param>
		/// <param name="inds">An array of indices.</param>
		/// <param name="area">The area flags for all the triangles.</param>
		public void RasterizeTrianglesIndexed(float[] verts, int[] inds, Area area)
		{
			RasterizeTrianglesIndexed(verts, inds, 0, 3, 0, inds.Length / 3, area);
		}

		/// <summary>
		/// Rasterizes several triangles at once from an indexed array.
		/// </summary>
		/// <param name="verts">An array of vertices.</param>
		/// <param name="inds">An array of indices.</param>
		/// <param name="floatOffset">An offset into the vertex array.</param>
		/// <param name="floatStride">The number of array elements that make up a vertex. A value of 0 is interpreted as tightly-packed data (3 floats per vertex).</param>
		/// <param name="indexOffset">An offset into the index array.</param>
		/// <param name="triCount">The number of triangles to rasterize.</param>
		public void RasterizeTrianglesIndexed(float[] verts, int[] inds, int floatOffset, int floatStride, int indexOffset, int triCount)
		{
			RasterizeTrianglesIndexed(verts, inds, floatOffset, floatStride, indexOffset, triCount, Area.Default);
		}

		/// <summary>
		/// Rasterizes several triangles at once from an indexed array.
		/// </summary>
		/// <param name="verts">An array of vertices.</param>
		/// <param name="inds">An array of indices.</param>
		/// <param name="floatOffset">An offset into the vertex array.</param>
		/// <param name="floatStride">The number of array elements that make up a vertex. A value of 0 is interpreted as tightly-packed data (3 floats per vertex).</param>
		/// <param name="indexOffset">An offset into the index array.</param>
		/// <param name="triCount">The number of triangles to rasterize.</param>
		/// <param name="area">The area flags for all of the triangles.</param>
		public void RasterizeTrianglesIndexed(float[] verts, int[] inds, int floatOffset, int floatStride, int indexOffset, int triCount, Area area)
		{
			int indexEnd = triCount * 3 + indexOffset;

			if (verts == null)
				throw new ArgumentNullException("verts");

			if (inds == null)
				throw new ArgumentNullException("inds");

			if (indexEnd > inds.Length)
				throw new ArgumentOutOfRangeException("indexCount", "The specified index offset and length end outside the provided index array.");

			if (floatOffset < 0)
				throw new ArgumentOutOfRangeException("floatOffset", "floatOffset must be greater than or equal to 0.");

			if (floatStride < 0)
				throw new ArgumentOutOfRangeException("floatStride", "floatStride must be greater than or equal to 0.");
			else if (floatStride == 0)
				floatStride = 3;

			Vector3 a, b, c;

			for (int i = indexOffset; i < indexEnd; i += 3)
			{
				int indA = inds[i] * floatStride + floatOffset;
				int indB = inds[i + 1] * floatStride + floatOffset;
				int indC = inds[i + 2] * floatStride + floatOffset;

				a.X = verts[indA];
				a.Y = verts[indA + 1];
				a.Z = verts[indA + 2];

				b.X = verts[indB];
				b.Y = verts[indB + 1];
				b.Z = verts[indB + 2];

				c.X = verts[indC];
				c.Y = verts[indC + 1];
				c.Z = verts[indC + 2];

				RasterizeTriangle(ref a, ref b, ref c, area);
			}
		}

		/// <summary>
		/// Rasterizes several triangles at once.
		/// </summary>
		/// <param name="tris">A collection of triangles.</param>
		public void RasterizeTriangles(IEnumerable<Triangle3> tris)
		{
			RasterizeTriangles(tris, Area.Default);
		}

		/// <summary>
		/// Rasterizes several triangles at once.
		/// </summary>
		/// <param name="tris">A collection of triangles.</param>
		/// <param name="area">The area flags for all of the triangles.</param>
		public void RasterizeTriangles(IEnumerable<Triangle3> tris, Area area)
		{
			Parallel.ForEach(tris, t =>
			{
				RasterizeTriangle(ref t, area);
			});
		}

		/// <summary>
		/// Rasterizes several triangles at once.
		/// </summary>
		/// <param name="tris">An array of triangles.</param>
		public void RasterizeTriangles(Triangle3[] tris)
		{
			RasterizeTriangles(tris, 0, tris.Length, Area.Default);
		}

		/// <summary>
		/// Rasterizes several triangles at once.
		/// </summary>
		/// <param name="tris">An array of triangles.</param>
		/// <param name="area">The area flags for all of the triangles.</param>
		public void RasterizeTriangles(Triangle3[] tris, Area area)
		{
			RasterizeTriangles(tris, 0, tris.Length, area);
		}

		/// <summary>
		/// Rasterizes several triangles at once.
		/// </summary>
		/// <param name="tris">An array of triangles.</param>
		/// <param name="triOffset">An offset into the array.</param>
		/// <param name="triCount">The number of triangles to rasterize, starting from the offset.</param>
		public void RasterizeTriangles(Triangle3[] tris, int triOffset, int triCount)
		{
			RasterizeTriangles(tris, triOffset, triCount, Area.Default);
		}

		/// <summary>
		/// Rasterizes several triangles at once.
		/// </summary>
		/// <param name="tris">An array of triangles.</param>
		/// <param name="triOffset">An offset into the array.</param>
		/// <param name="triCount">The number of triangles to rasterize, starting from the offset.</param>
		/// <param name="area">The area flags for all of the triangles.</param>
		public void RasterizeTriangles(Triangle3[] tris, int triOffset, int triCount, Area area)
		{
			int triEnd = triOffset + triCount;

			if (tris == null)
				throw new ArgumentNullException("verts");

			if (triOffset < 0)
				throw new ArgumentOutOfRangeException("triOffset", "triOffset must be greater than or equal to 0.");

			if (triCount < 0)
				throw new ArgumentOutOfRangeException("triCount", "triCount must be greater than or equal to 0.");

			if (triEnd > tris.Length)
				throw new ArgumentOutOfRangeException("triCount", "The specified offset and count end outside the bounds of the provided array.");

			int numBatches = 8;
			int threads = (triCount / numBatches) + 1;

			/*spanQueue = new ConcurrentQueue<Tuple<int, int, Span>>();
			bool allProcessed;

			var task = Task.Factory.StartNew(() =>
			{
				while (true)
				{
					if (spanQueue.IsEmpty)
						Thread.Sleep(1);

					Tuple<int, int, Span> spanEntry;
					while (spanQueue.TryDequeue(out spanEntry))
						cells[spanEntry.Item2 * width + spanEntry.Item1].AddSpan(spanEntry.Item3);
				}
			});*/

			Parallel.For(0, threads, i =>
			{
				int start = triOffset + i * numBatches;
				int end = triOffset + (i + 1) * numBatches;
				if (end > triEnd)
					end = triEnd;

				for (int j = start; j < end; j++)
				{
					Triangle3 t = tris[j];
					RasterizeTriangle(ref t.A, ref t.B, ref t.C, area);
				}
			});
		}

		/// <summary>
		/// Rasterizes several triangles at once.
		/// </summary>
		/// <remarks>
		/// If the length of the array is not a multiple of 3, the extra vertices at the end will be skipped.
		/// </remarks>
		/// <param name="verts">An array of vertices.</param>
		public void RasterizeTriangles(Vector3[] verts)
		{
			RasterizeTriangles(verts, 0, 1, verts.Length / 3, Area.Default);
		}

		/// <summary>
		/// Rasterizes several triangles at once.
		/// </summary>
		/// <remarks>
		/// If the length of the array is not a multiple of 3, the extra vertices at the end will be skipped.
		/// </remarks>
		/// <param name="verts">An array of vertices.</param>
		/// <param name="area">The area flags for all of the triangles.</param>
		public void RasterizeTriangles(Vector3[] verts, Area area)
		{
			RasterizeTriangles(verts, 0, 1, verts.Length / 3, area);
		}

		/// <summary>
		/// Rasterizes several triangles at once.
		/// </summary>
		/// <param name="verts">An array of vertices.</param>
		/// <param name="vertOffset">An offset into the array.</param>
		/// <param name="vertStride">The number of array elements that make up a vertex. A value of 0 is interpreted as tightly-packed data (1 Vector3 per vertex).</param>
		/// <param name="triCount">The number of triangles to rasterize, starting from the offset.</param>
		public void RasterizeTriangles(Vector3[] verts, int vertOffset, int vertStride, int triCount)
		{
			RasterizeTriangles(verts, vertOffset, vertStride, triCount, Area.Default);
		}

		/// <summary>
		/// Rasterizes several triangles at once.
		/// </summary>
		/// <param name="verts">An array of vertices.</param>
		/// <param name="vertOffset">An offset into the array.</param>
		/// <param name="vertStride">The number of array elements that make up a vertex. A value of 0 is interpreted as tightly-packed data (1 Vector3 per vertex).</param>
		/// <param name="triCount">The number of triangles to rasterize, starting from the offset.</param>
		/// <param name="area">The area flags for all of the triangles.</param>
		public void RasterizeTriangles(Vector3[] verts, int vertOffset, int vertStride, int triCount, Area area)
		{
			int vertEnd = triCount * vertStride + vertOffset;

			if (verts == null)
				throw new ArgumentNullException("verts");

			if (vertOffset < 0)
				throw new ArgumentOutOfRangeException("vertOffset", "vertOffset must be greater than or equal to 0.");

			if (triCount < 0)
				throw new ArgumentOutOfRangeException("triCount", "triCount must be greater than or equal to 0.");

			if (vertStride < 0)
				throw new ArgumentOutOfRangeException("vertStride", "vertStride must be greater than or equal to 0.");
			else if (vertStride == 0)
				vertStride = 1;

			if (vertEnd > verts.Length)
				throw new ArgumentOutOfRangeException("triCount", "The specified offset, count, and stride end outside the bounds of the provided array.");

			Parallel.For(0, triCount, i =>
			{
				i = vertOffset + (i * vertStride * 3);
				RasterizeTriangle(ref verts[i], ref verts[i + vertStride], ref verts[i + vertStride * 2], area);
			});
		}

		/// <summary>
		/// Rasterizes several triangles at once.
		/// </summary>
		/// <remarks>
		/// If the length of the array is not a multiple of 9, the extra floats at the end will be skipped.
		/// </remarks>
		/// <param name="verts">An array of vertices.</param>
		public void RasterizeTriangles(float[] verts)
		{
			RasterizeTriangles(verts, 0, 3, verts.Length / 9, Area.Default);
		}

		/// <summary>
		/// Rasterizes several triangles at once.
		/// </summary>
		/// <remarks>
		/// If the length of the array is not a multiple of 9, the extra floats at the end will be skipped.
		/// </remarks>
		/// <param name="verts">An array of vertices.</param>
		/// <param name="area">The area flags for all of the triangles.</param>
		public void RasterizeTriangles(float[] verts, Area area)
		{
			RasterizeTriangles(verts, 0, 3, verts.Length / 9, area);
		}

		/// <summary>
		/// Rasterizes several triangles at once.
		/// </summary>
		/// <param name="verts">An array of vertices.</param>
		/// <param name="floatOffset">An offset into the array.</param>
		/// <param name="floatStride">The number of array elements that make up a vertex. A value of 0 is interpreted as tightly-packed data (3 floats per vertex).</param>
		/// <param name="triCount">The number of triangles to rasterize.</param>
		public void RasterizeTriangles(float[] verts, int floatOffset, int floatStride, int triCount)
		{
			RasterizeTriangles(verts, floatOffset, floatStride, triCount, Area.Default);
		}

		/// <summary>
		/// Rasterizes several triangles at once.
		/// </summary>
		/// <param name="verts">An array of vertices.</param>
		/// <param name="floatOffset">An offset into the array.</param>
		/// <param name="floatStride">The number of array elements that make up a vertex. A value of 0 is interpreted as tightly-packed data (3 floats per vertex).</param>
		/// <param name="triCount">The number of triangles to rasterize.</param>
		/// <param name="area">The area flags for all of the triangles.</param>
		public void RasterizeTriangles(float[] verts, int floatOffset, int floatStride, int triCount, Area area)
		{
			int floatEnd = triCount * (floatStride * 3) + floatOffset;

			if (verts == null)
				throw new ArgumentNullException("verts");

			if (floatOffset < 0)
				throw new ArgumentOutOfRangeException("floatOffset", "floatOffset must be greater than or equal to 0.");

			if (triCount < 0)
				throw new ArgumentOutOfRangeException("triCount", "triCount must be greater than or equal to 0.");

			if (floatStride < 0)
				throw new ArgumentOutOfRangeException("floatStride", "floatStride must be a positive integer.");
			else if (floatStride == 0)
				floatStride = 3;

			if (floatEnd > verts.Length)
				throw new ArgumentOutOfRangeException("triCount", "The specified offset, count, and stride end outside the bounds of the provided array.");

			Vector3 a, b, c;

			Parallel.For(0, triCount, i =>
			{
				i = floatOffset + (i * floatStride * 3);
				int floatStride2 = floatStride * 2;

				a.X = verts[i];
				a.Y = verts[i + 1];
				a.Z = verts[i + 2];

				b.X = verts[i + floatStride];
				b.Y = verts[i + floatStride + 1];
				b.Z = verts[i + floatStride + 2];

				c.X = verts[i + floatStride2];
				c.Y = verts[i + floatStride2 + 1];
				c.Z = verts[i + floatStride2 + 2];

				RasterizeTriangle(ref a, ref b, ref c, area);
			});
		}

		/// <summary>
		/// Rasterizes a triangle using conservative voxelization.
		/// </summary>
		/// <param name="tri">The triangle as a <see cref="Triangle3"/> struct.</param>
		public void RasterizeTriangle(ref Triangle3 tri)
		{
			RasterizeTriangle(ref tri.A, ref tri.B, ref tri.C, Area.Default);
		}

		/// <summary>
		/// Rasterizes a triangle using conservative voxelization.
		/// </summary>
		/// <param name="tri">The triangle as a <see cref="Triangle3"/> struct.</param>
		/// <param name="area">The area flags for the triangle.</param>
		public void RasterizeTriangle(ref Triangle3 tri, Area area)
		{
			RasterizeTriangle(ref tri.A, ref tri.B, ref tri.C, area);
		}

		/// <summary>
		/// Rasterizes a triangle using conservative voxelization.
		/// </summary>
		/// <param name="ax">The X component of the first vertex of the triangle.</param>
		/// <param name="ay">The Y component of the first vertex of the triangle.</param>
		/// <param name="az">The Z component of the first vertex of the triangle.</param>
		/// <param name="bx">The X component of the second vertex of the triangle.</param>
		/// <param name="by">The Y component of the second vertex of the triangle.</param>
		/// <param name="bz">The Z component of the second vertex of the triangle.</param>
		/// <param name="cx">The X component of the third vertex of the triangle.</param>
		/// <param name="cy">The Y component of the third vertex of the triangle.</param>
		/// <param name="cz">The Z component of the third vertex of the triangle.</param>
		public void RasterizeTriangle(float ax, float ay, float az, float bx, float by, float bz, float cx, float cy, float cz)
		{
			RasterizeTriangle(ax, ay, az, bx, by, bz, cx, cy, cz, Area.Default);
		}

		/// <summary>
		/// Rasterizes a triangle using conservative voxelization.
		/// </summary>
		/// <param name="ax">The X component of the first vertex of the triangle.</param>
		/// <param name="ay">The Y component of the first vertex of the triangle.</param>
		/// <param name="az">The Z component of the first vertex of the triangle.</param>
		/// <param name="bx">The X component of the second vertex of the triangle.</param>
		/// <param name="by">The Y component of the second vertex of the triangle.</param>
		/// <param name="bz">The Z component of the second vertex of the triangle.</param>
		/// <param name="cx">The X component of the third vertex of the triangle.</param>
		/// <param name="cy">The Y component of the third vertex of the triangle.</param>
		/// <param name="cz">The Z component of the third vertex of the triangle.</param>
		/// <param name="area">The area flags for the triangle.</param>
		public void RasterizeTriangle(float ax, float ay, float az, float bx, float by, float bz, float cx, float cy, float cz, Area area)
		{
			Vector3 a, b, c;

			a.X = ax;
			a.Y = ay;
			a.Z = az;
			b.X = bx;
			b.Y = by;
			b.Z = bz;
			c.X = cx;
			c.Y = cy;
			c.Z = cz;

			RasterizeTriangle(ref a, ref b, ref c, area);
		}

		/// <summary>
		/// Rasterizes a triangle using conservative voxelization.
		/// </summary>
		/// <param name="a">The first vertex of the triangle.</param>
		/// <param name="b">The second vertex of the triangle.</param>
		/// <param name="c">The third vertex of the triangle.</param>
		public void RasterizeTriangle(ref Vector3 a, ref Vector3 b, ref Vector3 c)
		{
			RasterizeTriangle(ref a, ref b, ref c, Area.Default);
		}

		/// <summary>
		/// Rasterizes a triangle using conservative voxelization.
		/// </summary>
		/// <param name="a">The first vertex of the triangle.</param>
		/// <param name="b">The second vertex of the triangle.</param>
		/// <param name="c">The third vertex of the triangle.</param>
		/// <param name="area">The area flags for the triangle.</param>
		public void RasterizeTriangle(ref Vector3 a, ref Vector3 b, ref Vector3 c, Area area)
		{
			//distances buffer for ClipPolygonToBounds
			float[] distances = new float[12];

			float invCellSize = 1f / cellSize;
			float invCellHeight = 1f / cellHeight;
			float boundHeight = bounds.Max.Y - bounds.Min.Y;

			//calculate the triangle's bounding box
			BBox3 bbox;
			Triangle3.GetBoundingBox(ref a, ref b, ref c, out bbox);

			//make sure that the triangle is at least in one cell.
			if (!BBox3.Overlapping(ref bbox, ref bounds))
				return;

			//figure out which rows.
			int z0 = (int)((bbox.Min.Z - bounds.Min.Z) * invCellSize);
			int z1 = (int)((bbox.Max.Z - bounds.Min.Z) * invCellSize);

			//clamp to the field boundaries.
			MathHelper.Clamp(ref z0, 0, length - 1);
			MathHelper.Clamp(ref z1, 0, length - 1);

			Vector3[] inVerts = new Vector3[7], outVerts = new Vector3[7], inRowVerts = new Vector3[7];

			for (int z = z0; z <= z1; z++)
			{
				//copy the original vertices to the array.
				inVerts[0] = a;
				inVerts[1] = b;
				inVerts[2] = c;

				//clip the triangle to the row
				int nvrow = 3;
				float cz = bounds.Min.Z + z * cellSize;
				nvrow = MathHelper.ClipPolygonToPlane(inVerts, outVerts, distances, nvrow, 0, 1, -cz);
				if (nvrow < 3)
					continue;
				nvrow = MathHelper.ClipPolygonToPlane(outVerts, inRowVerts, distances, nvrow, 0, -1, cz + cellSize);
				if (nvrow < 3)
					continue;

				float minX = inRowVerts[0].X, maxX = minX;
				for (int i = 1; i < nvrow; i++)
				{
					float vx = inRowVerts[i].X;
					if (minX > vx)
						minX = vx;
					if (maxX < vx)
						maxX = vx;
				}

				int x0 = (int)((minX - bounds.Min.X) * invCellSize);
				int x1 = (int)((maxX - bounds.Min.X) * invCellSize);

				MathHelper.Clamp(ref x0, 0, width - 1);
				MathHelper.Clamp(ref x1, 0, width - 1);

				for (int x = x0; x <= x1; x++)
				{
					//clip the triangle to the column
					int nv = nvrow;
					float cx = bounds.Min.X + x * cellSize;
					nv = MathHelper.ClipPolygonToPlane(inRowVerts, outVerts, distances, nv, 1, 0, -cx);
					if (nv < 3)
						continue;
					nv = MathHelper.ClipPolygonToPlane(outVerts, inVerts, distances, nv, -1, 0, cx + cellSize);
					if (nv < 3)
						continue;

					//calculate the min/max of the polygon
					float polyMin = inVerts[0].Y, polyMax = polyMin;
					for (int i = 1; i < nv; i++)
					{
						float y = inVerts[i].Y;
						polyMin = Math.Min(polyMin, y);
						polyMax = Math.Max(polyMax, y);
					}

					//normalize span bounds to bottom of heightfield
					float boundMinY = bounds.Min.Y;
					polyMin -= boundMinY;
					polyMax -= boundMinY;

					//if the spans are outside the heightfield, skip.
					if (polyMax < 0f || polyMin > boundHeight)
						continue;

					//clamp the span to the heightfield.
					if (polyMin < 0)
						polyMin = 0;
					if (polyMax > boundHeight)
						polyMax = boundHeight;

					//snap to grid
					int spanMin = (int)(polyMin * invCellHeight);
					int spanMax = (int)Math.Ceiling(polyMax * invCellHeight);

					//add the span
					cells[z * width + x].AddSpan(new Span(spanMin, spanMax, area));
				}
			}
		}
	}
}
