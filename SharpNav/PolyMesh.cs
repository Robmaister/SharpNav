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
		private const int VERTEX_BUCKET_COUNT = 1 << 12; //2 ^ 12 vertices
		public const int MESH_NULL_IDX = -1;
		
		private const int DiagonalFlag = unchecked((int)0x80000000);
		private const int NeighborEdgeFlag = 0x8000;

		private int nverts;
		private int npolys;

		private Vector3[] verts; //each vertex contains (x, y, z)
		private Polygon[] polys;
		
		private int maxPolys;
		private int numVertsPerPoly;

		//copied data from CompactHeightfield
		private BBox3 bounds;
		private float cellSize;
		private float cellHeight;
		private int borderSize;

		public int NVerts { get { return nverts; } }
		public int NPolys { get { return npolys; } }
		public int NumVertsPerPoly { get { return numVertsPerPoly; } }
		public Vector3[] Verts { get { return verts; } }
		public Polygon[] Polys { get { return polys; } }

		public BBox3 Bounds { get { return bounds; } }
		public float CellSize { get { return cellSize; } }
		public float CellHeight { get { return cellHeight; } }
		public int BorderSize { get { return borderSize; } }

		public static bool IsBoundaryEdge(int flag) { return (flag & NeighborEdgeFlag) != 0; }
		public static bool IsInteriorEdge(int flag) { return (flag & NeighborEdgeFlag) == 0; }

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

			//vertex flags
			int[] vFlags = new int[maxVertices];

			//initialize the mesh members
			this.verts = new Vector3[maxVertices]; 
			this.polys = new Polygon[maxTris];

			this.nverts = 0;
			this.npolys = 0;
			this.numVertsPerPoly = numVertsPerPoly;
			this.maxPolys = maxTris;

			for (int i = 0; i < this.polys.Length; i++)
			{
				this.polys[i].Vertices = new int[numVertsPerPoly];
				this.polys[i].NeighborEdges = new int[numVertsPerPoly];
				this.polys[i].Area = new AreaFlags();
				this.polys[i].RegionId = 0;

				for (int j = 0; j < numVertsPerPoly; j++)
				{
					this.polys[i].Vertices[j] = MESH_NULL_IDX;
					this.polys[i].NeighborEdges[j] = MESH_NULL_IDX;
				}
			}

			//temporary variables needed for calculations
			int[] nextVert = new int[maxVertices];

			//This stores a hash table. The x,y,z coordinates generate a specific number.
			int[] firstVert = new int[VERTEX_BUCKET_COUNT];
			for (int i = 0; i < firstVert.Length; i++)
				firstVert[i] = -1;

			int[] indices = new int[maxVertsPerCont]; //keep track of vertex hash codes
			Tris[] tris = new Tris[maxVertsPerCont];
			Polygon[] polys = new Polygon[maxVertsPerCont + 1];
			
			int tempPolyIndex = maxVertsPerCont * numVertsPerPoly;

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
				int ntris = Triangulate(cont.Vertices.Length, cont.Vertices, indices, tris);
				if (ntris <= 0) //TODO notify user when this happens. Logging?
					ntris = -ntris;

				//add and merge vertices
				for (int i = 0; i < cont.Vertices.Length; i++)
				{
					int v = i;

					//save the hash code for each vertex
					indices[i] = AddVertex(cont.Vertices[v], this.verts, firstVert, nextVert, ref this.nverts);

					if (Region.IsBorderVertex(cont.Vertices[v].RegionId))
					{
						//the vertex should be removed
						vFlags[indices[i]] = 1;
					}
				}
				
				//builds initial polygons
				int npolys = 0;
				for (int i = 0; i < polys.Length; i++)
				{
					polys[i].Vertices = new int[numVertsPerPoly];
					
					for (int j = 0; j < numVertsPerPoly; j++)
					{
						polys[i].Vertices[j] = MESH_NULL_IDX;
					}
				}

				//iterate through all the triangles
				for (int i = 0; i < ntris; i++)
				{
					//make sure there are three distinct vertices. anything less can't be a polygon.
					if (tris[i].VertexHash0 != tris[i].VertexHash1
						&& tris[i].VertexHash0 != tris[i].VertexHash2
						&& tris[i].VertexHash1 != tris[i].VertexHash2)
					{
						//each polygon has numVertsPerPoly
						//index 0, 1, 2 store triangle vertices
						//other polygon indexes (3 to numVertsPerPoly - 1) should be used for storing extra vertices when two polygons merge together
						polys[npolys].Vertices[0] = indices[tris[i].VertexHash0];
						polys[npolys].Vertices[1] = indices[tris[i].VertexHash1];
						polys[npolys].Vertices[2] = indices[tris[i].VertexHash2];
						npolys++;
					}
				}
				
				//no polygons generated, so skip
				if (npolys == 0)
					continue;

				//merge polygons
				if (numVertsPerPoly > 3)
				{
					for (;;)
					{
						//find best polygons
						int bestMergeVal = 0;
						int bestPolyA = 0, bestPolyB = 0, bestEdgeA = 0, bestEdgeB = 0;

						for (int i = 0; i < npolys - 1; i++)
						{
							int pj = i;

							for (int j = i + 1; j < npolys; j++)
							{
								int pk = j;
								int ea = 0, eb = 0;
								int v = GetPolyMergeValue(polys, pj, pk, this.verts, ref ea, ref eb);
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
							MergePolys(polys, pa, pb, bestEdgeA, bestEdgeB);
							
							//overwrite second polygon since it has already part of another polygon
							int lastPoly = npolys - 1;

							bool polygonsEqual = true;
							for (int j = 0; j < numVertsPerPoly; j++)
							{
								if (polys[pb].Vertices[j] != polys[lastPoly].Vertices[j])
								{
									polygonsEqual = false;
									break;
								}
							}

							if (polygonsEqual == false)
							{
								for (int j = 0; j < numVertsPerPoly; j++)
									polys[pb].Vertices[j] = polys[lastPoly].Vertices[j];
							}

							npolys--;
						}
						else
						{
							//no more merging
							break;
						}
					}
				}

				//store polygons
				for (int i = 0; i < npolys; i++)
				{
					for (int j = 0; j < numVertsPerPoly; j++)
					{
						this.polys[this.npolys].Vertices[j] = polys[i].Vertices[j];
					}
					this.polys[this.npolys].RegionId = cont.RegionId;
					this.polys[this.npolys].Area = cont.Area;

					this.npolys++;
				}
			}

			//remove edge vertices
			for (int i = 0; i < this.nverts; i++)
			{
				if (vFlags[i] != 0)
				{
					if (!CanRemoveVertex(i))
						continue;
					
					RemoveVertex(i, maxTris);

					//change flags
					for (int j = i; j < this.nverts; j++)
						vFlags[j] = vFlags[j + 1];

					--i;
				}
			}

			//calculate adjacency (edges)
			BuildMeshAdjacency();
			
			//find portal edges
			if (this.borderSize > 0)
			{
				//iterate through all the polygons
				for (int i = 0; i < this.npolys; i++)
				{
					//iterate through all the vertices
					for (int j = 0; j < numVertsPerPoly; j++)
					{
						if (this.polys[i].Vertices[j] == MESH_NULL_IDX)
							break;

						//skip connected edges
						if (this.polys[i].NeighborEdges[j] != MESH_NULL_IDX)
							continue;

						int nj = j + 1;
						if (nj >= numVertsPerPoly || this.polys[i].Vertices[nj] == MESH_NULL_IDX)
							nj = 0;

						//grab two consecutive vertices
						int va = this.polys[i].Vertices[j];
						int vb = this.polys[i].Vertices[nj];

						//set some flags
						if (this.verts[va].X == 0 && this.verts[vb].X == 0)
							this.polys[i].NeighborEdges[j] = NeighborEdgeFlag | 0;
						else if (this.verts[va].Z == contSet.Height && this.verts[vb].Z == contSet.Height)
							this.polys[i].NeighborEdges[j] = NeighborEdgeFlag | 1;
						else if (this.verts[va].X == contSet.Width && this.verts[vb].X == contSet.Width)
							this.polys[i].NeighborEdges[j] = NeighborEdgeFlag | 2;
						else if (this.verts[va].Z == 0 && this.verts[vb].Z == 0)
							this.polys[i].NeighborEdges[j] = NeighborEdgeFlag | 3;
					}
				}
			}

			//user fills this in?
			for (int i = 0; i < this.polys.Length; i++)
			{
				this.polys[i].Flags = 0;
			}
		}

		/// <summary>
		/// Walk the edges of a contour to determine whether a triangle can be formed.
		/// Form as many triangles as possible.
		/// </summary>
		/// <param name="n">The number of vertices</param>
		/// <param name="verts">Vertices array</param>
		/// <param name="indices">Indices array</param>
		/// <param name="tris">Triangles array</param>
		/// <returns></returns>
		private int Triangulate(int n, ContourVertex[] verts, int[] indices, Tris[] tris)
		{
			int ntris = 0;
			Tris[] dst = tris;

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

				dst[ntris] = new Tris();
				dst[ntris].VertexHash0 = RemoveDiagonalFlag(indices[mi]);
				dst[ntris].VertexHash1 = RemoveDiagonalFlag(indices[mi1]);
				dst[ntris].VertexHash2 = RemoveDiagonalFlag(indices[mi2]);
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
			dst[ntris] = new Tris();
			dst[ntris].VertexHash0 = RemoveDiagonalFlag(indices[0]);
			dst[ntris].VertexHash1 = RemoveDiagonalFlag(indices[1]);
			dst[ntris].VertexHash2 = RemoveDiagonalFlag(indices[2]);
			ntris++;

			//save triangle information
			tris = dst;
			return ntris;
		}

		/// <summary>
		/// Generate a new vertices with (x, y, z) coordiates and return the hash code index 
		/// </summary>
		/// <returns></returns>
		private int AddVertex(ContourVertex v, Vector3[] verts, int[] firstVert, int[] nextVert, ref int nv)
		{
			//generate a unique index
			int bucket = ComputeVertexHash(v.X, 0, v.Z);
			
			//access that vertex
			int i = firstVert[bucket];

			//default unintialized i value is -1.
			//if i isn't equal to -1, this vertex should exist somewhere
			while (i != -1)
			{
				//found existing vertex
				if (verts[i].X == v.X && (Math.Abs(verts[i].Y - v.Y) <= 2) && verts[i].Z == v.Z)
					return i;
				
				//next vertex. this stores the vertices linearly (similar to a linked list)
				i = nextVert[i];
			}

			//no existing vertex so make a new one
			i = nv;
			nv++;

			//save the data
			verts[i].X = v.X;
			verts[i].Y = v.Y;
			verts[i].Z = v.Z;

			//add this current vertex to the chain
			nextVert[i] = firstVert[bucket];

			//update so this vertex index isn't reused again
			firstVert[bucket] = i;
			
			return i;
		}

		/// <summary>
		/// Compute a hash code based off of the (x, y, z) coordinates
		/// </summary>
		/// <param name="x"></param>
		/// <param name="y"></param>
		/// <param name="z"></param>
		/// <returns></returns>
		private int ComputeVertexHash(int x, int y, int z)
		{
			//choose large multiplicative constants, which are primes
			uint h1 = 0x8da6b343;
			uint h2 = 0xd8163841;
			uint h3 = 0xcb1ab31f;

			uint n = (uint)(h1 * x + h2 * y + h3 * z);
			return (int)(n & (VERTEX_BUCKET_COUNT - 1));
		}

		/// <summary>
		/// Try to merge two polygons. If possible, return the distance squared between two vertices.
		/// </summary>
		private int GetPolyMergeValue(Polygon[] polys, int polyA, int polyB, Vector3[] verts, ref int edgeA, ref int edgeB)
		{
			int numVertsA = CountPolyVerts(polys, polyA);
			int numVertsB = CountPolyVerts(polys, polyB);

			//don't merge if result is too big
			if (numVertsA + numVertsB - 2 > numVertsPerPoly)
				return -1;

			//check if polygons share an edge
			edgeA = -1;
			edgeB = -1;

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
			if (!ULeft(ref verts[vertA], ref verts[vertB], ref verts[vertC]))
				return -1;

			vertA = polys[polyB].Vertices[(edgeB + numVertsB - 1) % numVertsB];
			vertB = polys[polyB].Vertices[edgeB];
			vertC = polys[polyA].Vertices[(edgeA + 2) % numVertsA];
			if (!ULeft(ref verts[vertA], ref verts[vertB], ref verts[vertC]))
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
		private void MergePolys(Polygon[] polys, int polyA, int polyB, int edgeA, int edgeB)
		{
			int numA = CountPolyVerts(polys, polyA);
			int numB = CountPolyVerts(polys, polyB);
			int[] temp = new int[numA + numB];

			//merge
			for (int i = 0; i < numVertsPerPoly; i++)
				temp[i] = MESH_NULL_IDX;

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
		private bool CanRemoveVertex(int remove)
		{
			int numVertsPerPoly = this.numVertsPerPoly;

			//count number of polygons to remove
			int numRemovedVerts = 0;
			int numTouchedVerts = 0;
			int numRemainingEdges = 0;

			for (int i = 0; i < this.npolys; i++)
			{
				int nv = CountPolyVerts(this.polys, i);
				int numRemoved = 0;
				int numVerts = 0;

				for (int j = 0; j < nv; j++)
				{
					if (this.polys[i].Vertices[j] == remove)
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

			for (int i = 0; i < this.npolys; i++)
			{
				int nv = CountPolyVerts(this.polys, i);

				//collect edges which touch removed vertex
				for (int j = 0, k = nv - 1; j < nv; k = j++)
				{
					if (this.polys[i].Vertices[j] == remove || this.polys[i].Vertices[k] == remove)
					{
						//arrange edge so that a has the removed value
						int a = polys[i].Vertices[j], b = polys[i].Vertices[k];
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
		/// <param name="remove">Index in polygon array</param>
		/// <param name="maxTris">Maximum triangle count</param>
		private void RemoveVertex(int remove, int maxTris)
		{
			int numVertsPerPoly = this.numVertsPerPoly;

			//count number of polygons to remove
			int numRemovedVerts = 0;
			for (int i = 0; i < this.npolys; i++)
			{
				int nv = CountPolyVerts(this.polys, i);

				for (int j = 0; j < nv; j++)
				{
					if (this.polys[i].Vertices[j] == remove)
						numRemovedVerts++;
				}
			}

			int nedges = 0;
			int[] edges = new int[numRemovedVerts * numVertsPerPoly * 4];
			
			int nhole = 0;
			int[] hole = new int[numRemovedVerts * numVertsPerPoly];

			int nhreg = 0;
			int[] hreg = new int[numRemovedVerts * numVertsPerPoly];

			int nharea = 0;
			int[] harea = new int[numRemovedVerts * numVertsPerPoly];

			//Iterate through all the polygons
			for (int i = 0; i < this.npolys; i++)
			{
				int nv = CountPolyVerts(this.polys, i);
				
				//determine if any vertices need to be removed
				bool hasRemove = false;
				for (int j = 0; j < nv; j++)
				{
					if (this.polys[i].Vertices[j] == remove)
						hasRemove = true;
				}

				if (hasRemove)
				{
					//collect edges which don't touch removed vertex
					for (int j = 0, k = nv - 1; j < nv; k = j++)
					{
						if (this.polys[i].Vertices[j] != remove && this.polys[i].Vertices[k] != remove)
						{
							int e = nedges * 4;
							edges[e + 0] = this.polys[i].Vertices[k];
							edges[e + 1] = this.polys[i].Vertices[j];
							edges[e + 2] = this.polys[i].RegionId;
							edges[e + 3] = (int)this.polys[i].Area;
							nedges++;
						}
					}

					//remove polygon
					for (int j = 0; j < numVertsPerPoly; j++)
					{
						this.polys[i].Vertices[j] = this.polys[this.npolys - 1].Vertices[j];
						this.polys[i].NeighborEdges[j] = MESH_NULL_IDX;
					}

					this.polys[i].RegionId = this.polys[this.npolys - 1].RegionId;
					this.polys[i].Area = this.polys[this.npolys - 1].Area;
					this.npolys--;
					--i;
				}
			}

			//remove vertex
			for (int i = remove; i < this.nverts; i++)
			{
				this.verts[i] = this.verts[i + 1];
			}

			this.nverts--;

			//adjust indices
			for (int i = 0; i < this.npolys; i++)
			{
				int nv = CountPolyVerts(this.polys, i);

				for (int j = 0; j < nv; j++)
				{
					if (this.polys[i].Vertices[j] > remove)
						this.polys[i].Vertices[j]--;
				}
			}

			for (int i = 0; i < nedges; i++)
			{
				if (edges[i * 4 + 0] > remove)
					edges[i * 4 + 0]--;

				if (edges[i * 4 + 1] > remove)
					edges[i * 4 + 1]--;
			}

			if (nedges == 0)
				return;

			//Find edges surrounding the holes
			PushBack(edges[0], hole, ref nhole);
			PushBack(edges[1], hreg, ref nhreg);
			PushBack(edges[2], harea, ref nharea);

			while (nedges > 0)
			{
				bool match = false;

				for (int i = 0; i < nedges; i++)
				{
					int edgeA = i * 4 + 0;
					int edgeB = i * 4 + 1;
					int reg = i * 4 + 2;
					int area = i * 4 + 3;
					bool add = false;

					if (hole[0] == edgeB)
					{
						//segment matches beginning of hole boundary
						PushFront(edgeA, hole, ref nhole);
						PushFront(reg, hreg, ref nhreg);
						PushFront(area, harea, ref nharea);
						add = true;
					}
					else if (hole[nhole - 1] == edgeA)
					{
						//segment matches end of hole boundary
						PushBack(edgeB, hole, ref nhole);
						PushBack(reg, hreg, ref nhreg);
						PushBack(area, harea, ref nharea);
						add = true;
					}

					if (add)
					{
						//edge segment was added so remove it
						edges[i * 4 + 0] = edges[(nedges - 1) * 4 + 0];
						edges[i * 4 + 1] = edges[(nedges - 1) * 4 + 1];
						edges[i * 4 + 2] = edges[(nedges - 1) * 4 + 2];
						edges[i * 4 + 3] = edges[(nedges - 1) * 4 + 3];
						--nedges;
						match = true;
						--i;
					}
				}

				if (!match)
					break;
			}

			Tris[] tris = new Tris[nhole];
			var tverts = new ContourVertex[nhole];
			int[] thole = new int[nhole];

			//generate temp vertex array for triangulation
			for (int i = 0; i < nhole; i++)
			{
				int pi = hole[i];
				tverts[i].X = (int)this.verts[pi].X;
				tverts[i].Y = (int)this.verts[pi].Y;
				tverts[i].Z = (int)this.verts[pi].Z;
				tverts[i].RegionId = 0;
				thole[i] = i;
			}

			//triangulate the hole
			int ntris = Triangulate(nhole, tverts, thole, tris);
			if (ntris < 0)
				ntris = -ntris;

			//merge hole triangles back to polygons
			Polygon[] polys = new Polygon[ntris + 1];
			int[] pregs = new int[ntris];
			int[] pareas = new int[ntris];

			//builds initial polygons
			int npolys = 0;
			for (int j = 0; j < polys.Length; j++)
			{
				polys[j].Vertices = new int[numVertsPerPoly];

				for (int k = 0; k < numVertsPerPoly; k++)
				{
					polys[j].Vertices[k] = MESH_NULL_IDX;
				}
			}

			for (int j = 0; j < ntris; j++)
			{
				if (tris[j].VertexHash0 != tris[j].VertexHash1
					&& tris[j].VertexHash0 != tris[j].VertexHash2
					&& tris[j].VertexHash1 != tris[j].VertexHash2)
				{
					polys[npolys].Vertices[0] = hole[tris[j].VertexHash0];
					polys[npolys].Vertices[1] = hole[tris[j].VertexHash1];
					polys[npolys].Vertices[2] = hole[tris[j].VertexHash2];
					pregs[npolys] = hreg[tris[j].VertexHash0];
					pareas[npolys] = harea[tris[j].VertexHash0];
					npolys++;
				}
			}

			if (npolys == 0)
				return;

			//merge polygons
			if (numVertsPerPoly > 3)
			{
				for (;;)
				{
					//find best polygons
					int bestMergeVal = 0;
					int bestPolyA = 0, bestPolyB = 0, bestEa = 0, bestEb = 0;

					for (int j = 0; j < npolys - 1; j++)
					{
						int pj = j;
						for (int k = j + 1; k < npolys; k++)
						{
							int pk = k;
							int edgeA = 0, edgeB = 0;
							int v = GetPolyMergeValue(polys, pj, pk, this.verts, ref edgeA, ref edgeB);
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
						MergePolys(polys, polyA, polyB, bestEa, bestEb);
						
						//save space by overwriting second polygon with last polygon 
						//since second polygon is no longer need
						int lastPoly = npolys - 1;
						
						for (int k = 0; k < numVertsPerPoly; k++)
							polys[polyB].Vertices[k] = polys[lastPoly].Vertices[k];
						
						pregs[bestPolyB] = pregs[npolys - 1];
						pareas[bestPolyB] = pareas[npolys - 1];
						npolys--;
					}
					else
					{
						//no more merging
						break;
					}
				}
			}

			//store polygons
			for (int i = 0; i < npolys; i++)
			{
				//polygon count exceeds limit
				if (this.npolys >= maxTris) 
					break;

				for (int j = 0; j < numVertsPerPoly; j++)
				{
					this.polys[this.npolys].Vertices[j] = polys[i].Vertices[j];
					this.polys[this.npolys].NeighborEdges[j] = MESH_NULL_IDX;
				}

				this.polys[this.npolys].RegionId = pregs[i];
				this.polys[this.npolys].Area = (AreaFlags)pareas[i];
				this.npolys++;
			}
		}

		/// <summary>
		/// Connect two adjacent vertices with edges.
		/// </summary>
		private void BuildMeshAdjacency()
		{
			int maxEdgeCount = npolys * numVertsPerPoly;
			int[] firstEdge = new int[nverts + maxEdgeCount];
			int nextEdge = nverts;
			int edgeCount = 0;
			Edge[] edges = new Edge[maxEdgeCount];

			for (int i = 0; i < nverts; i++)
				firstEdge[i] = MESH_NULL_IDX;

			//Iterate through all the polygons
			for (int i = 0; i < npolys; i++)
			{
				//Iterate through all the vertices
				for (int j = 0; j < numVertsPerPoly; j++)
				{
					if (polys[i].Vertices[j] == MESH_NULL_IDX)
						break;

					//get closest two verts
					int v0 = polys[i].Vertices[j];
					int v1 = (j + 1 >= numVertsPerPoly || polys[i].Vertices[j + 1] == MESH_NULL_IDX) ? polys[i].Vertices[0] : polys[i].Vertices[j + 1];

					if (v0 < v1)
					{
						Edge edge;

						//store vertices
						edge.VertX = v0;
						edge.VertY = v1;

						//poly array stores index of polygon
						//polyEdge stores the vertex
						edge.PolyX = i;
						edge.PolyEdgeX = j;
						edge.PolyY = i;
						edge.PolyEdgeY = 0;

						//insert edge
						firstEdge[nextEdge + edgeCount] = firstEdge[v0];
						firstEdge[v0] = edgeCount;

						edges[edgeCount] = edge;
						edgeCount++;
					}
				}
			}

			//Iterate through all the polygons again
			for (int i = 0; i < npolys; i++)
			{
				for (int j = 0; j < numVertsPerPoly; j++)
				{
					if (polys[i].Vertices[j] == MESH_NULL_IDX)
						break;

					//get adjacent vertices
					int v0 = polys[i].Vertices[j];
					int v1 = (j + 1 >= numVertsPerPoly || polys[i].Vertices[j + 1] == MESH_NULL_IDX) ? polys[i].Vertices[0] : polys[i].Vertices[j + 1];

					if (v0 > v1)
					{
						//Iterate through all the edges
						for (int e = firstEdge[v1]; e != MESH_NULL_IDX; e = firstEdge[nextEdge + e])
						{
							Edge edge = edges[e];
							if (edge.VertY == v0 && edge.PolyX == edge.PolyY)
							{
								edge.PolyY = i;
								edge.PolyEdgeY = j;
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
				Edge e = edges[i];

				//the endpoints belong to different polygons
				if (e.PolyX != e.PolyY)
				{
					//store other polygon number as part of extra info
					polys[e.PolyX].NeighborEdges[e.PolyEdgeX] = e.PolyY;
					polys[e.PolyY].NeighborEdges[e.PolyEdgeY] = e.PolyX;
				}
			}
		}

		/// <summary>
		/// Count the number of vertices per polygon
		/// </summary>
		private int CountPolyVerts(Polygon[] polys, int polyIndex)
		{
			for (int i = 0; i < numVertsPerPoly; i++)
				if (polys[polyIndex].Vertices[i] == MESH_NULL_IDX)
					return i;

			return numVertsPerPoly;
		}

		/// <summary>
		/// Shift all existing elements to the right and insert new element at index 0
		/// </summary>
		/// <param name="v">Value of new element</param>
		/// <param name="array">Array of elements</param>
		/// <param name="an">Number of elements</param>
		private void PushFront(int v, int[] array, ref int an)
		{
			an++;
			for (int i = an - 1; i > 0; i--)
				array[i] = array[i - 1];
			array[0] = v;
		}

		/// <summary>
		/// Append new element to the end of the list
		/// </summary>
		/// <param name="v">Value of new element</param>
		/// <param name="array">Array of elements</param>
		/// <param name="an">Number of elements</param>
		private void PushBack(int v, int[] array, ref int an)
		{
			array[an] = v;
			an++;
		}
		
		private bool ULeft(ref Vector3 a, ref Vector3 b, ref Vector3 c)
		{
			return (b.X - a.X) * (c.Z - a.Z) -
				(c.X - a.X) * (b.Z - a.Z) < 0;
		}
		
		private void SetDiagonalFlag(ref int index)
		{
			index |= DiagonalFlag;
		}

		private void RemoveDiagonalFlag(ref int index)
		{
			index &= ~DiagonalFlag;
		}

		public bool IsDiagonalFlagOn(int index)
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

		//HACK this is also in Contour, find a good place to move.
		private static int RemoveDiagonalFlag(int index) { return index & ~DiagonalFlag; }
		private static int Prev(int i, int n) { return i - 1 >= 0 ? i - 1 : n - 1; }
		private static int Next(int i, int n) { return i + 1 < n ? i + 1 : 0; }

		//TODO arrays in mutable structs 
		public struct Polygon
		{
			public int[] Vertices; //"numVertsPerPoly" elements
			public int[] NeighborEdges; //"numVertsPerPoly" elements
			public AreaFlags Area;
			public int RegionId; 
			public int Flags; 
		}

		private struct Tris
		{
			public int VertexHash0;
			public int VertexHash1;
			public int VertexHash2;
		}

		private struct Edge
		{
			public int VertX;
			public int VertY;

			public int PolyEdgeX;
			public int PolyEdgeY;

			public int PolyX;
			public int PolyY;
		}
	}
}
