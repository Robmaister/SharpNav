#region License
/**
 * Copyright (c) 2013-2014 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
 * Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE
 */
#endregion

using System;
using System.Collections.Generic;

using SharpNav.Geometry;

#if MONOGAME || XNA
using Microsoft.Xna.Framework;
#elif OPENTK
using OpenTK;
#elif SHARPDX
using SharpDX;
#endif

namespace SharpNav
{
	public class PolyMesh
	{
		public const int NullId = -1;
		
		private const int DiagonalFlag = unchecked((int)0x80000000);
		private const int NeighborEdgeFlag = unchecked((int)0x80000000);

		private Vector3[] vertices; 
		private Polygon[] polygons;

		private int numVertsPerPoly;

		//copied data from CompactHeightfield
		private BBox3 bounds;
		private float cellSize;
		private float cellHeight;
		private int borderSize;

		public int VertCount { get { return vertices.Length; } }
		public int PolyCount { get { return polygons.Length; } }
		public int NumVertsPerPoly { get { return numVertsPerPoly; } }
		public Vector3[] Verts { get { return vertices; } }
		public Polygon[] Polys { get { return polygons; } }

		public BBox3 Bounds { get { return bounds; } }
		public float CellSize { get { return cellSize; } }
		public float CellHeight { get { return cellHeight; } }
		public int BorderSize { get { return borderSize; } }

		/// <summary>
		/// Create polygons out of a set of contours
		/// </summary>
		/// <param name="contSet">The ContourSet to use</param>
		/// <param name="numVertsPerPoly">Number vertices per polygon</param>
		public PolyMesh(ContourSet contSet, int numVertsPerPoly)
		{
			//copy contour data
			this.bounds = contSet.Bounds;
			this.cellSize = contSet.CellSize;
			this.cellHeight = contSet.CellHeight;
			this.borderSize = contSet.BorderSize;

			//get maximum limits
			//TODO move to ContourSet?
			int maxVertices = 0;
			int maxTris = 0;
			int maxVertsPerCont = 0;
			foreach (var cont in contSet)
			{
				int vertCount = cont.Vertices.Length;

				//skip null contours
				if (vertCount < 3) 
					continue;

				maxVertices += vertCount;
				maxTris += vertCount - 2;
				maxVertsPerCont = Math.Max(maxVertsPerCont, vertCount);
			}

			//initialize the mesh members
			List<Vector3> verts = new List<Vector3>(maxVertices);
			List<Polygon> polys = new List<Polygon>(maxTris);

			Queue<int> vertRemoveQueue = new Queue<int>(maxVertices);

			this.numVertsPerPoly = numVertsPerPoly;

			Dictionary<Vector3, int> vertDict = new Dictionary<Vector3, int>(new Vector3YRadiusEqualityComparer());

			int[] indices = new int[maxVertsPerCont]; //keep track of vertex hash codes
			Triangle[] tris = new Triangle[maxVertsPerCont];
			List<Polygon> contPolys = new List<Polygon>(maxVertsPerCont + 1);

			//extract contour data
			foreach (Contour cont in contSet)
			{
				//skip null contours
				if (cont.IsNull)
					continue;

				//triangulate contours
				for (int i = 0; i < cont.Vertices.Length; i++)
					indices[i] = i;

				//Form triangles inside the area bounded by the contours
				int ntris = Triangulate(cont.Vertices, indices, cont.Vertices.Length, tris);
				if (ntris <= 0) //TODO notify user when this happens. Logging?
					ntris = -ntris;

				//add and merge vertices
				for (int i = 0; i < cont.Vertices.Length; i++)
				{
					int v = i;
					ContourVertex cv = cont.Vertices[v];

					//save the hash code for each vertex
					indices[i] = AddVertex(vertDict, cv, verts);

					if (Region.IsBorderVertex(cv.RegionId))
					{
						//the vertex should be removed
						vertRemoveQueue.Enqueue(indices[i]);
					}
				}

				contPolys.Clear();

				//iterate through all the triangles
				for (int i = 0; i < ntris; i++)
				{
					//make sure there are three distinct vertices. anything less can't be a polygon.
					if (tris[i].Index0 != tris[i].Index1
						&& tris[i].Index0 != tris[i].Index2
						&& tris[i].Index1 != tris[i].Index2)
					{
						//each polygon has numVertsPerPoly
						//index 0, 1, 2 store triangle vertices
						//other polygon indexes (3 to numVertsPerPoly - 1) should be used for storing extra vertices when two polygons merge together
						Polygon p = new Polygon(numVertsPerPoly, AreaId.Null, 0, 0);
						p.Vertices[0] = indices[tris[i].Index0];
						p.Vertices[1] = indices[tris[i].Index1];
						p.Vertices[2] = indices[tris[i].Index2];
						contPolys.Add(p);
					}
				}
				
				//no polygons generated, so skip
				if (contPolys.Count == 0)
					continue;

				//merge polygons
				if (numVertsPerPoly > 3)
				{
					while (true)
					{
						//find best polygons
						int bestMergeVal = 0;
						int bestPolyA = 0, bestPolyB = 0, bestEdgeA = 0, bestEdgeB = 0;

						for (int i = 0; i < contPolys.Count - 1; i++)
						{
							int pj = i;

							for (int j = i + 1; j < contPolys.Count; j++)
							{
								int pk = j;
								int ea = 0, eb = 0;
								int v = GetPolyMergeValue(contPolys, pj, pk, verts, out ea, out eb);
								if (v > bestMergeVal)
								{
									bestMergeVal = v;
									bestPolyA = i;
									bestPolyB = j;
									bestEdgeA = ea;
									bestEdgeB = eb;
								}
							}
						}

						if (bestMergeVal > 0)
						{
							int pa = bestPolyA;
							int pb = bestPolyB;
							MergePolys(contPolys, pa, pb, bestEdgeA, bestEdgeB);
							contPolys[pb] = contPolys[contPolys.Count - 1];
							contPolys.RemoveAt(contPolys.Count - 1);
						}
						else
						{
							//no more merging
							break;
						}
					}
				}

				//store polygons
				for (int i = 0; i < contPolys.Count; i++)
				{
					Polygon p = contPolys[i];
					Polygon p2 = new Polygon(numVertsPerPoly, cont.Area, cont.RegionId, 0);

					Buffer.BlockCopy(p.Vertices, 0, p2.Vertices, 0, numVertsPerPoly * sizeof(int));

					polys.Add(p2);
				}
			}

			//remove edge vertices
			while (vertRemoveQueue.Count > 0)
			{
				int i = vertRemoveQueue.Dequeue();

				if (CanRemoveVertex(polys, i))
					RemoveVertex(verts, polys, i);
			}

			//calculate adjacency (edges)
			BuildMeshAdjacency(verts, polys, numVertsPerPoly);
			
			//find portal edges
			if (this.borderSize > 0)
			{
				//iterate through all the polygons
				for (int i = 0; i < polys.Count; i++)
				{
					Polygon p = polys[i];

					//iterate through all the vertices
					for (int j = 0; j < numVertsPerPoly; j++)
					{
						if (p.Vertices[j] == NullId)
							break;

						//skip connected edges
						if (p.NeighborEdges[j] != NullId)
							continue;

						int nj = j + 1;
						if (nj >= numVertsPerPoly || p.Vertices[nj] == NullId)
							nj = 0;

						//grab two consecutive vertices
						int va = p.Vertices[j];
						int vb = p.Vertices[nj];

						//set some flags
						if (verts[va].X == 0 && verts[vb].X == 0)
							p.NeighborEdges[j] = NeighborEdgeFlag | 0;
						else if (verts[va].Z == contSet.Height && verts[vb].Z == contSet.Height)
							p.NeighborEdges[j] = NeighborEdgeFlag | 1;
						else if (verts[va].X == contSet.Width && verts[vb].X == contSet.Width)
							p.NeighborEdges[j] = NeighborEdgeFlag | 2;
						else if (verts[va].Z == 0 && verts[vb].Z == 0)
							p.NeighborEdges[j] = NeighborEdgeFlag | 3;
					}
				}
			}

			this.vertices = verts.ToArray();
			this.polygons = polys.ToArray();
		}

		/// <summary>
		/// Walk the edges of a contour to determine whether a triangle can be formed.
		/// Form as many triangles as possible.
		/// </summary>
		/// <param name="verts">Vertices array</param>
		/// <param name="indices">Indices array</param>
		/// <param name="n">The number of vertices</param>
		/// <param name="tris">Triangles array</param>
		/// <returns></returns>
		private static int Triangulate(ContourVertex[] verts, int[] indices, int n, Triangle[] tris)
		{
			int ntris = 0;

			//last bit of index determines whether vertex can be removed
			for (int i = 0; i < n; i++)
			{
				int i1 = Next(i, n);
				int i2 = Next(i1, n);
				if (Diagonal(i, i2, n, verts, indices))
				{
					SetDiagonalFlag(ref indices[i1]);
				}
			}

			//need 3 verts minimum for a polygon 
			while (n > 3)
			{
				//find the minimum distance betwee two vertices. 
				//also, save their index
				int minLen = -1;
				int mini = -1;
				for (int i = 0; i < n; i++)
				{
					int i1 = Next(i, n);
					
					if (IsDiagonalFlagOn(indices[i1]))
					{
						int p0 = RemoveDiagonalFlag(indices[i]);
						int p2 = RemoveDiagonalFlag(indices[Next(i1, n)]);

						int dx = verts[p2].X - verts[p0].X;
						int dy = verts[p2].Z - verts[p0].Z;
						int len = dx * dx + dy * dy;

						if (minLen < 0 || len < minLen)
						{
							minLen = len;
							mini = i;
						}
					}
				}

				if (mini == -1)
				{
					return -ntris;
				}

				int mi = mini;
				int mi1 = Next(mi, n);
				int mi2 = Next(mi1, n);

				tris[ntris] = new Triangle();
				tris[ntris].Index0 = RemoveDiagonalFlag(indices[mi]);
				tris[ntris].Index1 = RemoveDiagonalFlag(indices[mi1]);
				tris[ntris].Index2 = RemoveDiagonalFlag(indices[mi2]);
				ntris++;

				//remove P[i1]
				n--;
				for (int k = mi1; k < n; k++)
					indices[k] = indices[k + 1];

				if (mi1 >= n) mi1 = 0;
				mi = Prev(mi1, n);

				//update diagonal flags
				if (Diagonal(Prev(mi, n), mi1, n, verts, indices))
				{
					SetDiagonalFlag(ref indices[mi]);
				}
				else
				{
					RemoveDiagonalFlag(ref indices[mi]);
				}

				if (Diagonal(mi, Next(mi1, n), n, verts, indices))
				{
					SetDiagonalFlag(ref indices[mi1]);
				}
				else
				{
					RemoveDiagonalFlag(ref indices[mi1]);
				}
			}

			//append remaining triangle
			tris[ntris] = new Triangle();
			tris[ntris].Index0 = RemoveDiagonalFlag(indices[0]);
			tris[ntris].Index1 = RemoveDiagonalFlag(indices[1]);
			tris[ntris].Index2 = RemoveDiagonalFlag(indices[2]);
			ntris++;

			return ntris;
		}

		/// <summary>
		/// Generate a new vertices with (x, y, z) coordiates and return the hash code index 
		/// </summary>
		/// <returns></returns>
		private static int AddVertex(Dictionary<Vector3, int> vertDict, ContourVertex cv, List<Vector3> verts)
		{
			Vector3 v = new Vector3(cv.X, cv.Y, cv.Z);
			int index;
			if (vertDict.TryGetValue(v, out index))
			{
				return index;
			}

			index = verts.Count;
			verts.Add(v);
			vertDict.Add(v, index);
			return index;
		}

		/// <summary>
		/// Try to merge two polygons. If possible, return the distance squared between two vertices.
		/// </summary>
		private static int GetPolyMergeValue(List<Polygon> polys, int polyA, int polyB, List<Vector3> verts, out int edgeA, out int edgeB)
		{
			int numVertsA = polys[polyA].VertexCount;
			int numVertsB = polys[polyB].VertexCount;

			//check if polygons share an edge
			edgeA = -1;
			edgeB = -1;

			//don't merge if result is too big
			if (numVertsA + numVertsB - 2 > polys[polyA].Vertices.Length)
				return -1;

			//iterate through all the vertices of polygonA
			for (int i = 0; i < numVertsA; i++)
			{
				//take two nearby vertices
				int va0 = polys[polyA].Vertices[i];
				int va1 = polys[polyA].Vertices[(i + 1) % numVertsA];
				
				//make sure va0 < va1
				if (va0 > va1)
				{
					int temp = va0;
					va0 = va1;
					va1 = temp;
				}

				//iterate through all the vertices of polygon B
				for (int j = 0; j < numVertsB; j++)
				{
					//take two nearby vertices
					int vb0 = polys[polyB].Vertices[j];
					int vb1 = polys[polyB].Vertices[(j + 1) % numVertsB];
					
					//make sure vb0 < vb1
					if (vb0 > vb1)
					{
						int temp = vb0;
						vb0 = vb1;
						vb1 = temp;
					}

					//edge shared, since vertices are equal
					if (va0 == vb0 && va1 == vb1)
					{
						edgeA = i;
						edgeB = j;
						break;
					}
				}
			}

			//no common edge
			if (edgeA == -1 || edgeB == -1)
				return -1;

			//check if merged polygon would be convex
			int vertA, vertB, vertC;

			vertA = polys[polyA].Vertices[(edgeA + numVertsA - 1) % numVertsA];
			vertB = polys[polyA].Vertices[edgeA];
			vertC = polys[polyB].Vertices[(edgeB + 2) % numVertsB];
			if (!ULeft(verts[vertA], verts[vertB], verts[vertC]))
				return -1;

			vertA = polys[polyB].Vertices[(edgeB + numVertsB - 1) % numVertsB];
			vertB = polys[polyB].Vertices[edgeB];
			vertC = polys[polyA].Vertices[(edgeA + 2) % numVertsA];
			if (!ULeft(verts[vertA], verts[vertB], verts[vertC]))
				return -1;

			vertA = polys[polyA].Vertices[edgeA];
			vertB = polys[polyA].Vertices[(edgeA + 1) % numVertsA];

			int dx = (int)(verts[vertA].X - verts[vertB].X);
			int dy = (int)(verts[vertA].Z - verts[vertB].Z);
			return dx * dx + dy * dy;
		}

		/// <summary>
		/// The two polygon arrrays are merged into a single array
		/// </summary>
		private void MergePolys(List<Polygon> polys, int polyA, int polyB, int edgeA, int edgeB)
		{
			int numA = polys[polyA].VertexCount;
			int numB = polys[polyB].VertexCount;
			int[] temp = new int[numA + numB];

			//merge
			for (int i = 0; i < numVertsPerPoly; i++)
				temp[i] = NullId;

			int n = 0;

			//add polygon A
			for (int i = 0; i < numA - 1; i++)
				temp[n++] = polys[polyA].Vertices[(edgeA + 1 + i) % numA];

			//add polygon B
			for (int i = 0; i < numB - 1; i++)
				temp[n++] = polys[polyB].Vertices[(edgeB + 1 + i) % numB];

			//save merged data to new polygon
			for (int i = 0; i < numVertsPerPoly; i++)
				polys[polyA].Vertices[i] = temp[i];
		}

		/// <summary>
		/// If vertex can't be removed, there is no need to spend time deleting it.
		/// </summary>
		private static bool CanRemoveVertex(List<Polygon> polys, int remove)
		{
			//count number of polygons to remove
			int numRemovedVerts = 0;
			int numTouchedVerts = 0;
			int numRemainingEdges = 0;

			for (int i = 0; i < polys.Count; i++)
			{
				Polygon p = polys[i];
				int nv = p.VertexCount;
				int numRemoved = 0;
				int numVerts = 0;

				for (int j = 0; j < nv; j++)
				{
					if (p.Vertices[j] == remove)
					{
						numTouchedVerts++;
						numRemoved++;
					}

					numVerts++;
				}

				if (numRemoved > 0)
				{
					numRemovedVerts += numRemoved;
					numRemainingEdges += numVerts - (numRemoved + 1);
				}
			}

			//don't remove a vertex from a triangle since you need at least three vertices to make a polygon
			if (numRemainingEdges <= 2)
				return false;

			//find edges which share removed vertex
			int maxEdges = numTouchedVerts * 2;
			int nedges = 0;
			int[] edges = new int[maxEdges * 3];

			for (int i = 0; i < polys.Count; i++)
			{
				Polygon p = polys[i];
				int nv = p.VertexCount;

				//collect edges which touch removed vertex
				for (int j = 0, k = nv - 1; j < nv; k = j++)
				{
					if (p.Vertices[j] == remove || p.Vertices[k] == remove)
					{
						//arrange edge so that a has the removed value
						int a = p.Vertices[j], b = p.Vertices[k];
						if (b == remove)
						{
							int temp = a;
							a = b;
							b = temp;
						}

						//check if edge exists
						bool exists = false;
						for (int m = 0; m < nedges; m++)
						{
							int e = m * 3;
							if (edges[e + 1] == b)
							{
								//increment vertex share count
								edges[e + 2]++;
								exists = true;
							}
						}

						//add new edge
						if (!exists)
						{
							int e = nedges * 3;
							edges[e + 0] = a;
							edges[e + 1] = b;
							edges[e + 2] = 1;
							nedges++;
						}
					}
				}
			}

			//make sure there can't be more than two open edges
			//since there could be two non-adjacent polygons which share the same vertex, which shouldn't be removed
			int numOpenEdges = 0;
			for (int i = 0; i < nedges; i++)
			{
				if (edges[i * 3 + 2] < 2)
					numOpenEdges++;
			}

			if (numOpenEdges > 2)
				return false;

			return true;
		}

		/// <summary>
		/// Removing vertices will leave holes that have to be triangulated again.
		/// </summary>
		/// <param name="vertex">Index in polygon array</param>
		/// <param name="maxTris">Maximum triangle count</param>
		private void RemoveVertex(List<Vector3> verts, List<Polygon> polys, int vertex)
		{
			int numVertsPerPoly = this.numVertsPerPoly;

			//count number of polygons to remove
			int numRemovedVerts = 0;
			for (int i = 0; i < polys.Count; i++)
			{
				Polygon p = polys[i];
				int nv = p.VertexCount;

				for (int j = 0; j < nv; j++)
				{
					if (p.Vertices[j] == vertex)
						numRemovedVerts++;
				}
			}

			List<Edge> edges = new List<Edge>(numRemovedVerts * numVertsPerPoly);
			List<int> hole = new List<int>(numRemovedVerts * numVertsPerPoly);
			List<RegionId> regions = new List<RegionId>(numRemovedVerts * numVertsPerPoly);
			List<AreaId> areas = new List<AreaId>(numRemovedVerts * numVertsPerPoly);

			//Iterate through all the polygons
			for (int i = 0; i < polys.Count; i++)
			{
				Polygon p = polys[i];
				
				if (p.ContainsVertex(vertex))
				{
					int nv = p.VertexCount;

					//collect edges which don't touch removed vertex
					for (int j = 0, k = nv - 1; j < nv; k = j++)
						if (p.Vertices[j] != vertex && p.Vertices[k] != vertex)
							edges.Add(new Edge(p.Vertices[k], p.Vertices[j], p.RegionId, p.Area));

					polys[i] = polys[polys.Count - 1];
					polys.RemoveAt(polys.Count - 1);
					i--;
				}
			}

			//remove vertex
			verts.RemoveAt(vertex);

			//adjust indices
			for (int i = 0; i < polys.Count; i++)
			{
				Polygon p = polys[i];
				int nv = p.VertexCount;

				for (int j = 0; j < nv; j++)
				{
					if (p.Vertices[j] > vertex)
						p.Vertices[j]--;
				}
			}

			for (int i = 0; i < edges.Count; i++)
			{
				Edge edge = edges[i];
				if (edge.Vert0 > vertex)
					edge.Vert0--;

				if (edge.Vert1 > vertex)
					edge.Vert1--;

				edges[i] = edge;
			}

			if (edges.Count == 0)
				return;

			//Find edges surrounding the holes
			hole.Add(edges[0].Vert0);
			regions.Add(edges[0].Region);
			areas.Add(edges[0].Area);

			while (edges.Count > 0)
			{
				bool match = false;

				for (int i = 0; i < edges.Count; i++)
				{
					Edge edge = edges[i];
					bool add = false;

					if (hole[0] == edge.Vert1)
					{
						//segment matches beginning of hole boundary
						hole.Insert(0, edge.Vert0);
						regions.Insert(0, edge.Region);
						areas.Insert(0, edge.Area);
						add = true;
					}
					else if (hole[hole.Count - 1] == edge.Vert0)
					{
						//segment matches end of hole boundary
						hole.Add(edge.Vert1);
						regions.Add(edge.Region);
						areas.Add(edge.Area);
						add = true;
					}

					if (add)
					{
						//edge segment was added so remove it
						edges[i] = edges[edges.Count - 1];
						edges.RemoveAt(edges.Count - 1);
						match = true;
						i--;
					}
				}

				if (!match)
					break;
			}

			Triangle[] tris = new Triangle[hole.Count];
			var tverts = new ContourVertex[hole.Count];
			int[] thole = new int[hole.Count];

			//generate temp vertex array for triangulation
			for (int i = 0; i < hole.Count; i++)
			{
				int pi = hole[i];
				tverts[i] = new ContourVertex(verts[pi], 0);
				thole[i] = i;
			}

			//triangulate the hole
			int ntris = Triangulate(tverts, thole, hole.Count, tris);
			if (ntris < 0)
				ntris = -ntris;

			//merge hole triangles back to polygons
			List<Polygon> mergePolys = new List<Polygon>(ntris + 1);

			for (int j = 0; j < ntris; j++)
			{
				Triangle t = tris[j];
				if (t.Index0 != t.Index1 && t.Index0 != t.Index2 && t.Index1 != t.Index2)
				{
					Polygon p = new Polygon(numVertsPerPoly, areas[t.Index0], regions[t.Index0], 0);
					p.Vertices[0] = hole[t.Index0];
					p.Vertices[1] = hole[t.Index1];
					p.Vertices[2] = hole[t.Index2];
					mergePolys.Add(p);
				}
			}

			if (mergePolys.Count == 0)
				return;

			//merge polygons
			if (numVertsPerPoly > 3)
			{
				while (true)
				{
					//find best polygons
					int bestMergeVal = 0;
					int bestPolyA = 0, bestPolyB = 0, bestEa = 0, bestEb = 0;

					for (int j = 0; j < mergePolys.Count - 1; j++)
					{
						int pj = j;
						for (int k = j + 1; k < mergePolys.Count; k++)
						{
							int pk = k;
							int edgeA, edgeB;
							int v = GetPolyMergeValue(mergePolys, pj, pk, verts, out edgeA, out edgeB);
							if (v > bestMergeVal)
							{
								bestMergeVal = v;
								bestPolyA = j;
								bestPolyB = k;
								bestEa = edgeA;
								bestEb = edgeB;
							}
						}
					}

					if (bestMergeVal > 0)
					{
						int polyA = bestPolyA;
						int polyB = bestPolyB;
						MergePolys(mergePolys, polyA, polyB, bestEa, bestEb);
						mergePolys[polyB] = mergePolys[mergePolys.Count - 1];
						mergePolys.RemoveAt(mergePolys.Count - 1);
					}
					else
					{
						//no more merging
						break;
					}
				}
			}

			//add merged polys back to the list.
			polys.AddRange(mergePolys);
		}

		/// <summary>
		/// Connect two adjacent vertices with edges.
		/// </summary>
		private static void BuildMeshAdjacency(List<Vector3> vertices, List<Polygon> polys, int numVertsPerPoly)
		{
			int maxEdgeCount = polys.Count * numVertsPerPoly;
			int[] firstEdge = new int[vertices.Count + maxEdgeCount];
			int nextEdge = vertices.Count;
			int edgeCount = 0;
			AdjacencyEdge[] edges = new AdjacencyEdge[maxEdgeCount];

			for (int i = 0; i < vertices.Count; i++)
				firstEdge[i] = NullId;

			//Iterate through all the polygons
			for (int i = 0; i < polys.Count; i++)
			{
				Polygon p = polys[i];
				//Iterate through all the vertices
				for (int j = 0; j < numVertsPerPoly; j++)
				{
					if (p.Vertices[j] == NullId)
						break;

					//get closest two verts
					int v0 = p.Vertices[j];
					int v1 = (j + 1 >= numVertsPerPoly || p.Vertices[j + 1] == NullId) ? p.Vertices[0] : p.Vertices[j + 1];

					if (v0 < v1)
					{
						AdjacencyEdge edge;

						//store vertices
						edge.Vert0 = v0;
						edge.Vert1 = v1;

						//poly array stores index of polygon
						//polyEdge stores the vertex
						edge.Poly0 = i;
						edge.PolyEdge0 = j;
						edge.Poly1 = i;
						edge.PolyEdge1 = 0;

						//insert edge
						firstEdge[nextEdge + edgeCount] = firstEdge[v0];
						firstEdge[v0] = edgeCount;

						edges[edgeCount] = edge;
						edgeCount++;
					}
				}
			}

			//Iterate through all the polygons again
			for (int i = 0; i < polys.Count; i++)
			{
				Polygon p = polys[i];
				for (int j = 0; j < numVertsPerPoly; j++)
				{
					if (p.Vertices[j] == NullId)
						break;

					//get adjacent vertices
					int v0 = p.Vertices[j];
					int v1 = (j + 1 >= numVertsPerPoly || p.Vertices[j + 1] == NullId) ? p.Vertices[0] : p.Vertices[j + 1];

					if (v0 > v1)
					{
						//Iterate through all the edges
						for (int e = firstEdge[v1]; e != NullId; e = firstEdge[nextEdge + e])
						{
							AdjacencyEdge edge = edges[e];
							if (edge.Vert1 == v0 && edge.Poly0 == edge.Poly1)
							{
								edge.Poly1 = i;
								edge.PolyEdge1 = j;
								edges[e] = edge;
								break;
							}
						}
					}
				}
			}

			//store adjacency
			for (int i = 0; i < edgeCount; i++)
			{
				AdjacencyEdge e = edges[i];

				//the endpoints belong to different polygons
				if (e.Poly0 != e.Poly1)
				{
					//store other polygon number as part of extra info
					polys[e.Poly0].NeighborEdges[e.PolyEdge0] = e.Poly1;
					polys[e.Poly1].NeighborEdges[e.PolyEdge1] = e.Poly0;
				}
			}
		}

		private static bool ULeft(Vector3 a, Vector3 b, Vector3 c)
		{
			return (b.X - a.X) * (c.Z - a.Z) -
				(c.X - a.X) * (b.Z - a.Z) < 0;
		}
		
		private static void SetDiagonalFlag(ref int index)
		{
			index |= DiagonalFlag;
		}

		private static int RemoveDiagonalFlag(int index)
		{
			return index & ~DiagonalFlag;
		}

		private static void RemoveDiagonalFlag(ref int index)
		{
			index &= ~DiagonalFlag;
		}

		public static bool IsDiagonalFlagOn(int index)
		{
			return (index & DiagonalFlag) == DiagonalFlag;
		}

		/// <summary>
		/// true if and only if (v[i], v[j]) is a proper internal diagonal of polygon
		/// </summary>
		public static bool Diagonal(int i, int j, int n, ContourVertex[] verts, int[] indices)
		{
			return InCone(i, j, n, verts, indices) && Diagonalie(i, j, n, verts, indices);
		}

		/// <summary>
		/// true if and only if diagonal (i, j) is strictly internal to polygon 
		/// in neighborhood of i endpoint
		/// </summary>
		public static bool InCone(int i, int j, int n, ContourVertex[] verts, int[] indices)
		{
			int pi = RemoveDiagonalFlag(indices[i]);
			int pj = RemoveDiagonalFlag(indices[j]);
			int pi1 = RemoveDiagonalFlag(indices[Next(i, n)]);
			int pin1 = RemoveDiagonalFlag(indices[Prev(i, n)]);

			//if P[i] is convex vertex (i + 1 left or on (i - 1, i))
			if (ContourVertex.IsLeftOn(ref verts[pin1], ref verts[pi], ref verts[pi1]))
				return ContourVertex.IsLeft(ref verts[pi], ref verts[pj], ref verts[pin1]) && ContourVertex.IsLeft(ref verts[pj], ref verts[pi], ref verts[pi1]);

			//assume (i - 1, i, i + 1) not collinear
			return !(ContourVertex.IsLeftOn(ref verts[pi], ref verts[pj], ref verts[pi1]) && ContourVertex.IsLeftOn(ref verts[pj], ref verts[pi], ref verts[pin1]));
		}

		/// <summary>
		/// true if and only if (v[i], v[j]) is internal or external diagonal
		/// ignoring edges incident to v[i] or v[j]
		/// </summary>
		public static bool Diagonalie(int i, int j, int n, ContourVertex[] verts, int[] indices)
		{
			int d0 = RemoveDiagonalFlag(indices[i]);
			int d1 = RemoveDiagonalFlag(indices[j]);

			//for each edge (k, k + 1)
			for (int k = 0; k < n; k++)
			{
				int k1 = Next(k, n);

				//skip edges incident to i or j
				if (!((k == i) || (k1 == i) || (k == j) || (k1 == j)))
				{
					int p0 = RemoveDiagonalFlag(indices[k]);
					int p1 = RemoveDiagonalFlag(indices[k1]);

					if (ContourVertex.Equal2D(ref verts[d0], ref verts[p0]) ||
						ContourVertex.Equal2D(ref verts[d1], ref verts[p0]) ||
						ContourVertex.Equal2D(ref verts[d0], ref verts[p1]) ||
						ContourVertex.Equal2D(ref verts[d1], ref verts[p1]))
						continue;

					if (ContourVertex.Intersect(ref verts[d0], ref verts[d1], ref verts[p0], ref verts[p1]))
						return false;
				}
			}

			return true;
		}

		private static int Prev(int i, int n) { return i - 1 >= 0 ? i - 1 : n - 1; }
		private static int Next(int i, int n) { return i + 1 < n ? i + 1 : 0; }
		public static bool IsBoundaryEdge(int flag) { return (flag & NeighborEdgeFlag) != 0; }
		public static bool IsInteriorEdge(int flag) { return (flag & NeighborEdgeFlag) == 0; }

		public class Polygon
		{
			private int[] vertices; //"numVertsPerPoly" elements
			private int[] neighborEdges; //"numVertsPerPoly" elements
			private AreaId area;
			private RegionId regionId;
			private int flags;

			public Polygon(int numVertsPerPoly, AreaId area, RegionId regionId, int flags)
			{
				vertices = new int[numVertsPerPoly];
				neighborEdges = new int[numVertsPerPoly];
				this.area = area;
				this.regionId = regionId;
				this.flags = flags;

				for (int i = 0; i < numVertsPerPoly; i++)
				{
					vertices[i] = NullId;
					neighborEdges[i] = NullId;
				}
			}

			public int[] Vertices { get { return vertices; } }
			public int[] NeighborEdges { get { return neighborEdges; } }
			public AreaId Area { get { return area; } set { area = value; } }
			public RegionId RegionId { get { return regionId; } set { regionId = value; } }
			public int Flags { get { return flags; } set { flags = value; } }

			public int VertexCount
			{
				get
				{
					for (int i = 0; i < vertices.Length; i++)
						if (vertices[i] == NullId)
							return i;

					return vertices.Length;
				}
			}

			public bool ContainsVertex(int vertex)
			{
				//iterate through all the vertices
				for (int i = 0; i < vertices.Length; i++)
				{
					//find the vertex, return false if at end of defined polygon.
					int v = vertices[i];
					if (v == vertex)
						return true;
					else if (v == NullId)
						return false;
				}

				return false;
			}
		}

		private struct Triangle
		{
			public int Index0;
			public int Index1;
			public int Index2;
		}

		private struct AdjacencyEdge
		{
			public int Vert0;
			public int Vert1;

			public int PolyEdge0;
			public int PolyEdge1;

			public int Poly0;
			public int Poly1;
		}

		private struct Edge
		{
			public int Vert0;
			public int Vert1;
			public RegionId Region;
			public AreaId Area;

			public Edge(int vert0, int vert1, RegionId region, AreaId area)
			{
				Vert0 = vert0;
				Vert1 = vert1;
				Region = region;
				Area = area;
			}
		}

		private class Vector3YRadiusEqualityComparer : IEqualityComparer<Vector3>
		{
			private const int hashConstX = unchecked((int)0x8da6b343);
			private const int hashConstY = unchecked((int)0xd8163841);
			private const int hashConstZ = unchecked((int)0xcb1ab31f);

			public bool Equals(Vector3 left, Vector3 right)
			{
				return left.X == right.X && (Math.Abs(left.Y - right.Y) <= 2) && left.Z == right.Z;
			}

			public int GetHashCode(Vector3 obj)
			{
				return (hashConstX * (int)obj.X + hashConstZ * (int)obj.Z);
			}
		}
	}
}
