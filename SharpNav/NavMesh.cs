#region License
/**
 * Copyright (c) 2013 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
 * Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE
 */
#endregion

using System;
using System.Collections.Generic;
using SharpNav.Geometry;

namespace SharpNav
{
	public class Edge
	{
		public int [] vert;
		public int [] polyEdge;
		public int [] poly;
	}

	public class NavMesh
	{
		private const int VERTEX_BUCKET_COUNT = 1 << 12; //2 ^ 12 vertices
		public const int MESH_NULL_IDX = 0xffff;

		private int nverts;
		private int npolys;

		private int[] verts; //each vertex contains (x, y, z)
		private int[] polys;
		private int[] regionIds; //contains region id for each triangle
		private int[] flags; //flags for a polygon
		private AreaFlags[] areas; //contains area flags for each triangle
		
		private int maxPolys;
		private int numVertsPerPoly;

		//copied data from OpenHeightfield
		private BBox3 bounds;
		private float cellSize;
		private float cellHeight;
		private int borderSize;

		public int NVerts { get { return nverts; } }
		public int NPolys { get { return npolys; } }
		public int NumVertsPerPoly { get { return numVertsPerPoly; } }
		public int[] Verts { get { return verts; } }
		public int[] Polys { get { return polys; } }

		public BBox3 Bounds { get { return bounds; } }
		public float CellSize { get { return cellSize; } }
		public float CellHeight { get { return CellHeight; } }
		public int BorderSize { get { return borderSize; } }

		/// <summary>
		/// Create polygons out of a set of contours
		/// </summary>
		/// <param name="contSet">The ContourSet to use</param>
		/// <param name="numVertsPerPoly">Number vertices per polygon</param>
		public NavMesh(ContourSet contSet, int numVertsPerPoly)
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
			for (int i = 0; i < contSet.Contours.Count; i++)
			{
				//skip null contours
				if (contSet.Contours[i].NumVerts < 3) 
					continue;
				
				maxVertices += contSet.Contours[i].NumVerts;
				maxTris += contSet.Contours[i].NumVerts - 2;
				maxVertsPerCont += Math.Max(maxVertsPerCont, contSet.Contours[i].NumVerts);
			}

			//vertex flags
			int[] vFlags = new int[maxVertices];
			for (int i = 0; i < vFlags.Length; i++)
				vFlags[i] = 0;

			//initialize the mesh members
			this.verts = new int[maxVertices * 3]; 
			this.polys = new int[maxTris * numVertsPerPoly * 2];
			this.regionIds = new int[maxTris];
			this.areas = new AreaFlags[maxTris];

			this.nverts = 0;
			this.npolys = 0;
			this.numVertsPerPoly = numVertsPerPoly;
			this.maxPolys = maxTris;

			for (int i = 0; i < this.verts.Length; i++)
				this.verts[i] = 0;
			for (int i = 0; i < this.polys.Length; i++)
				this.polys[i] = 0xff;
			for (int i = 0; i < this.regionIds.Length; i++)
				this.regionIds[i] = 0;
			for (int i = 0; i < this.areas.Length; i++)
				this.areas[i] = AreaFlags.Null;

			//temporary variables needed for calculations
			int[] nextVert = new int[maxVertices];
			for (int i = 0; i < nextVert.Length; i++)
				nextVert[i] = 0;

			//This stores a hash table. The x,y,z coordinates generate a specific number.
			int[] firstVert = new int[VERTEX_BUCKET_COUNT];
			for (int i = 0; i < firstVert.Length; i++)
				firstVert[i] = -1;

			int[] indices = new int[maxVertsPerCont]; //keep track of vertex hash codes
			int[] tris = new int[maxVertsPerCont * 3]; //store three vertex hash codes
			int[] polys = new int[(maxVertsPerCont + 1) * numVertsPerPoly]; 

			//extract contour data
			for (int i = 0; i < contSet.Contours.Count; i++)
			{
				ContourSet.Contour cont = contSet.Contours[i];

				//skip null contours
				if (cont.NumVerts < 3)
					continue;

				//triangulate contours
				for (int j = 0; j < cont.NumVerts; j++)
					indices[j] = j;

				//Form triangles inside the area bounded by the contours
				int ntris = Triangulate(cont.NumVerts, cont.Vertices, indices, tris);
				if (ntris <= 0) 
					ntris = -ntris;

				//add and merge vertices
				for (int j = 0; j < cont.NumVerts; j++)
				{
					int v = j * 4;

					//save the hash code for each vertex
					indices[j] = AddVertex(cont.Vertices[v + 0], cont.Vertices[v + 1], cont.Vertices[v + 2],
						this.verts, firstVert, nextVert, ref this.nverts);

					if ((cont.Vertices[v + 3] & ContourSet.BORDER_VERTEX) != 0)
					{
						//the vertex should be removed
						vFlags[indices[j]] = 1;
					}
				}
				
				//builds initial polygons
				int npolys = 0;
				for (int j = 0; j < polys.Length; j++)
					polys[j] = 0xff;

				//iterate through all the triangles
				for (int j = 0; j < ntris; j++)
				{
					int t = j * 3;

					//make sure there are three distinct vertices. anything less can't be a polygon.
					if (tris[t + 0] != tris[t + 1] && tris[t + 0] != tris[t + 2] && tris[t + 1] != tris[t + 2])
					{
						//each polygon has numVertsPerPoly
						//index 0, 1, 2 store triangle vertices
						//other polygon indexes (3 to numVertsPerPoly - 1) should be used for storing extra vertices when two polygons merge together
						polys[npolys * numVertsPerPoly + 0] = indices[tris[t + 0]];
						polys[npolys * numVertsPerPoly + 1] = indices[tris[t + 1]];
						polys[npolys * numVertsPerPoly + 2] = indices[tris[t + 2]];
						npolys++;
					}
				}
				
				//no polygons generated, so skip
				if (npolys == 0)
					continue;

				//merge polygons
				if (numVertsPerPoly > 3)
				{
					for ( ; ; )
					{
						//find best polygons
						int bestMergeVal = 0;
						int bestPolyA = 0, bestPolyB = 0, bestEdgeA = 0, bestEdgeB = 0;

						for (int j = 0; j < npolys - 1; j++)
						{
							int pj = j * numVertsPerPoly;

							for (int k = j + 1; k < npolys; k++)
							{
								int pk = k * numVertsPerPoly;
								int ea = 0, eb = 0;
								int v = GetPolyMergeValue(polys, pj, pk, this.verts, ref ea, ref eb, numVertsPerPoly);
								if (v > bestMergeVal)
								{
									bestMergeVal = v;
									bestPolyA = j;
									bestPolyB = k;
									bestEdgeA = ea;
									bestEdgeB = eb;
								}
							}
						}

						if (bestMergeVal > 0)
						{
							int pa = bestPolyA * numVertsPerPoly;
							int pb = bestPolyB * numVertsPerPoly;
							MergePolys(polys, pa, pb, bestEdgeA, bestEdgeB, numVertsPerPoly);
							
							//overwrite second polygon since it has already part of another polygon
							int lastPoly = (npolys - 1) * numVertsPerPoly;
							if (polys[pb] != polys[lastPoly])
							{
								for (int k = 0; k < numVertsPerPoly; k++)
									polys[pb + k] = polys[lastPoly + k];
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
				for (int j = 0; j < npolys; j++)
				{
					int p = this.npolys * numVertsPerPoly * 2;
					int q = j * numVertsPerPoly;
					for (int k = 0; k < numVertsPerPoly; k++)
						this.polys[p + k] = polys[q + k];

					this.regionIds[this.npolys] = cont.RegionId;
					this.areas[this.npolys] = cont.Area;
					this.npolys++;
				}
			}

			//remove edge vertices
			for (int i = 0; i < this.nverts; i++)
			{
				if (vFlags[i] != 0)
				{
					if(!CanRemoveVertex(i))
						continue;
					
					RemoveVertex(i, maxTris);

					//change flags
					for (int j = i; j < this.nverts; j++)
						vFlags[j] = vFlags[j + 1];

					--i;
				}
			}

			BuildMeshAdjacency();
			
			//find portal edges
			if (this.borderSize > 0)
			{
				for (int i = 0; i < this.npolys; i++)
				{
					int p = i * 2 * numVertsPerPoly;
					
					for (int j = 0; j < numVertsPerPoly; j++)
					{
						if (this.polys[p + j] == MESH_NULL_IDX)
							break;

						//skip connected edges
						if (this.polys[p + numVertsPerPoly + j] != MESH_NULL_IDX)
							continue;

						int nj = j + 1;
						if (nj >= numVertsPerPoly || this.polys[p + nj] == MESH_NULL_IDX)
							nj = 0;

						int va = this.polys[(p + j) * 3];
						int vb = this.polys[(p + nj) * 3];

						if (this.verts[va + 0] == 0 && this.verts[vb + 0] == 0)
							this.polys[p + numVertsPerPoly + j] = 0x8000 | 0;
						else if (this.verts[va + 2] == contSet.Height && this.verts[vb + 2] == contSet.Height)
							this.polys[p + numVertsPerPoly + j] = 0x8000 | 1;
						else if (this.verts[va + 0] == contSet.Width && this.verts[vb + 0] == contSet.Width)
							this.polys[p + numVertsPerPoly + j] = 0x8000 | 2;
						else if (this.verts[va + 2] == 0 && this.verts[vb + 2] == 0)
							this.polys[p + numVertsPerPoly + j] = 0x8000 | 3;
					}
				}
			}

			this.flags = new int[this.npolys];
			for (int i = 0; i < this.flags.Length; i++)
				this.flags[i] = 0;
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
		private int Triangulate(int n, int[] verts, int[] indices, int[] tris)
		{
			int ntris = 0;
			int[] dst = tris;
			int dstIndex = 0;

			int i, i1;

			//last bit of index determines whether vertex can be removed
			for (i = 0; i < n; i++)
			{
				i1 = Next(i, n);
				int i2 = Next(i1, n);
				if (Diagonal(i, i2, n, verts, indices))
				{
					uint temp = (uint)indices[i1];
					temp |= 0x80000000;
					indices[i1] = (int)temp;
				}
			}

			//need 3 verts minimum for a polygon 
			while (n > 3)
			{
				//find the minimum distance betwee two vertices. 
				//also, save their index
				int minLen = -1;
				int mini = -1;
				for (i = 0; i < n; i++)
				{
					i1 = Next(i, n);
					
					if ((indices[i1] & 0x80000000) != 0)
					{
						int p0 = (indices[i] & 0x0fffffff) * 4;
						int p2 = (indices[Next(i1, n)] & 0x0fffffff) * 4;

						int dx = verts[p2 + 0] - verts[p0 + 0];
						int dy = verts[p2 + 2] - verts[p0 + 2];
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

				i = mini;
				i1 = Next(i, n);
				int i2 = Next(i1, n);

				dst[dstIndex] = indices[i] & 0x0fffffff; dstIndex++;
				dst[dstIndex] = indices[i1] & 0x0fffffff; dstIndex++;
				dst[dstIndex] = indices[i2] & 0x0fffffff; dstIndex++;
				ntris++;

				//remove P[i1]
				n--;
				for (int k = i1; k < n; k++)
					indices[k] = indices[k + 1];

				if (i1 >= n) i1 = 0;
				i = Prev(i1, n);

				//update diagonal flags
				if (Diagonal(Prev(i, n), i1, n, verts, indices))
				{
					uint temp = (uint)indices[i1];
					temp |= 0x80000000;
					indices[i1] = (int)temp;
				}
				else
				{
					indices[i] &= 0x0fffffff;
				}

				if (Diagonal(i, Next(i1, n), n, verts, indices))
				{
					uint temp = (uint)indices[i1];
					temp |= 0x80000000;
					indices[i1] = (int)temp;
				}
				else
				{
					indices[i1] &= 0x0fffffff;
				}
			}

			//append remaining triangle
			dst[dstIndex] = indices[0] & 0x0fffffff; dstIndex++;
			dst[dstIndex] = indices[1] & 0x0fffffff; dstIndex++;
			dst[dstIndex] = indices[2] & 0x0fffffff; dstIndex++;
			ntris++;

			//save triangle information
			tris = dst;
			return ntris;
		}

		/// <summary>
		/// Generate a new vertices with (x, y, z) coordiates and return the hash code index 
		/// </summary>
		/// <returns></returns>
		private int AddVertex(int x, int y, int z, int[] verts, int[] firstVert, int[] nextVert, ref int nv)
		{
			//generate a unique index
			int bucket = ComputeVertexHash(x, 0, z);
			
			//access that vertex
			int i = firstVert[bucket];
			int v;

			//default unintialized i value is -1.
			//if i isn't equal to -1, this vertex should exist somewhere
			while (i != -1)
			{
				//vertex only has three elements (x,y,z)
				v = i * 3;

				//found existing vertex
				if (verts[v + 0] == x && (Math.Abs(verts[v + 1] - y) <= 2) && verts[v + 2] == z)
					return i;
				
				//next vertex. this stores the vertices linearly (similar to a linked list)
				i = nextVert[i];
			}

			//no existing vertex so make a new one
			i = nv;
			nv++;

			//save the data
			v = i * 3;
			verts[v + 0] = x;
			verts[v + 1] = y;
			verts[v + 2] = z;

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
		private int GetPolyMergeValue(int[] polys, int polyA, int polyB, int[] verts, ref int edgeA, ref int edgeB, int nvp)
		{
			int numVertsA = CountPolyVerts(polys, polyA, nvp);
			int numVertsB = CountPolyVerts(polys, polyB, nvp);

			//don't merge if result is too big
			if (numVertsA + numVertsB - 2 > nvp)
				return -1;

			//check if polygons share an edge
			edgeA = -1;
			edgeB = -1;

			//iterate through all the vertices of polygonA
			for (int i = 0; i < numVertsA; i++)
			{
				//take two nearby vertices
				int va0 = polys[polyA + i];
				int va1 = polys[polyA + (i + 1) % numVertsA];
				
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
					int vb0 = polys[polyB + j];
					int vb1 = polys[polyB + (j + 1) % numVertsB];
					
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

			vertA = polys[polyA + (edgeA + numVertsA - 1) % numVertsA];
			vertB = polys[polyA + edgeA];
			vertC = polys[polyB + (edgeB + 2) % numVertsB];
			if (!ULeft(verts, vertA * 3, vertB * 3, vertC * 3))
				return -1;

			vertA = polys[polyB + (edgeB + numVertsB - 1) % numVertsB];
			vertB = polys[polyB + edgeB];
			vertC = polys[polyA + (edgeA + 2) % numVertsA];
			if (!ULeft(verts, vertA * 3, vertB * 3, vertC * 3))
				return -1;

			vertA = polys[polyA + edgeA];
			vertB = polys[polyA + (edgeA + 1) % numVertsA];

			int dx = verts[vertA * 3 + 0] - verts[vertB * 3 + 0];
			int dy = verts[vertA * 3 + 2] - verts[vertB * 3 + 2];
			return dx * dx + dy * dy;
		}

		/// <summary>
		/// The two polygon arrrays are merged into a single array
		/// </summary>
		private void MergePolys(int[] polys, int polyA, int polyB, int edgeA, int edgeB, int numVertsPerPolygon)
		{
			int numA = CountPolyVerts(polys, polyA, numVertsPerPolygon);
			int numB = CountPolyVerts(polys, polyB, numVertsPerPolygon);
			int[] temp = new int[numA + numB];

			//merge
			for (int i = 0; i < temp.Length; i++)
				temp[i] = 0xff;

			int n = 0;

			//add polygon A
			for (int i = 0; i < numA - 1; i++)
				temp[n++] = polys[polyA + (edgeA + 1 + i) % numA];

			//add polygon B
			for (int i = 0; i < numB - 1; i++)
				temp[n++] = polys[polyB + (edgeB + 1 + i) % numB];

			//save merged data to new polygon
			polys = temp;
		}

		///<summary>
		/// If vertex can't be removed, there is no need to spend time deleting it.
		///</summary>
		private bool CanRemoveVertex(int remove)
		{
			int numVertsPerPoly = this.numVertsPerPoly;

			//count number of polygons to remove
			int numRemovedVerts = 0;
			int numTouchedVerts = 0;
			int numRemainingEdges = 0;

			for (int i = 0; i < this.npolys; i++)
			{
				int p = i * numVertsPerPoly * 2;
				int nv = CountPolyVerts(this.polys, p, numVertsPerPoly);
				int numRemoved = 0;
				int numVerts = 0;

				for (int j = 0; j < nv; j++)
				{
					if (this.polys[p + j] == remove)
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
				int p = i * numVertsPerPoly * 2;
				int nv = CountPolyVerts(this.polys, p, numVertsPerPoly);

				//collect edges which touch removed vertex
				for (int j = 0, k = nv - 1; j < nv; k = j++)
				{
					if (this.polys[p + j] == remove || this.polys[p + k] == remove)
					{
						//arrange edge so that a has the removed value
						int a = polys[p + j], b = polys[p + k];
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
				int p = i * numVertsPerPoly * 2;
				int nv = CountPolyVerts(this.polys, p, numVertsPerPoly);

				for (int j = 0; j < nv; j++)
				{
					if (this.polys[p + j] == remove)
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
				int p = i * numVertsPerPoly * 2;
				int nv = CountPolyVerts(this.polys, p, numVertsPerPoly);
				
				//determine if any vertices need to be removed
				bool hasRemove = false;
				for (int j = 0; j < nv; j++)
				{
					if (this.polys[p + j] == remove)
						hasRemove = true;
				}

				if (hasRemove)
				{
					//collect edges which don't touch removed vertex
					for (int j = 0, k = nv - 1; j < nv; k = j++)
					{
						if (this.polys[p + j] != remove && this.polys[p + k] != remove)
						{
							int e = nedges * 4;
							edges[e + 0] = this.polys[p + k];
							edges[e + 1] = this.polys[p + j];
							edges[e + 2] = this.regionIds[i];
							edges[e + 3] = (int)this.areas[i];
							nedges++;
						}
					}

					//remove polygon
					int p2 = (this.npolys - 1) * numVertsPerPoly * 2;
					this.polys[p] = this.polys[p2];
					this.polys[p + numVertsPerPoly] = 0xff;
					this.regionIds[i] = this.regionIds[this.npolys - 1];
					this.areas[i] = this.areas[this.npolys - 1];
					this.npolys--;
					--i;
				}
			}

			//remove vertex
			for (int i = remove; i < this.nverts; i++)
			{
				this.verts[i * 3 + 0] = this.verts[(i + 1) * 3 + 0];
				this.verts[i * 3 + 1] = this.verts[(i + 1) * 3 + 1];
				this.verts[i * 3 + 2] = this.verts[(i + 1) * 3 + 2];
			}
			this.nverts--;

			//adjust indices
			for (int i = 0; i < this.npolys; i++)
			{
				int p = i * numVertsPerPoly * 2;
				int nv = CountPolyVerts(this.polys, p, numVertsPerPoly);

				for (int j = 0; j < nv; j++)
				{
					if (this.polys[p + j] > remove)
						this.polys[p + j]--;
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

			int[] tris = new int[nhole * 3];
			int[] tverts = new int[nhole * 4];
			int[] thole = new int[nhole];

			//generate temp vertex array for triangulation
			for (int i = 0; i < nhole; i++)
			{
				int pi = hole[i];
				tverts[i * 4 + 0] = this.verts[pi * 3 + 0];
				tverts[i * 4 + 1] = this.verts[pi * 3 + 1];
				tverts[i * 4 + 2] = this.verts[pi * 3 + 2];
				tverts[i * 4 + 3] = 0;
				thole[i] = i;
			}

			//triangulate the hole
			int ntris = Triangulate(nhole, tverts, thole, tris);
			if (ntris < 0)
				ntris = -ntris;

			//merge hole triangles back to polygons
			int[] polys = new int[(ntris + 1) * numVertsPerPoly];
			int[] pregs = new int[ntris];
			int[] pareas = new int[ntris];

			//builds initial polygons
			int npolys = 0;
			for (int j = 0; j < polys.Length; j++)
				polys[j] = 0xff;

			for (int j = 0; j < ntris; j++)
			{
				int t = j * 3;
				if (tris[t + 0] != tris[t + 1] && tris[t + 0] != tris[t + 2] && tris[t + 1] != tris[t + 2])
				{
					polys[npolys * numVertsPerPoly + 0] = hole[tris[t + 0]];
					polys[npolys * numVertsPerPoly + 1] = hole[tris[t + 1]];
					polys[npolys * numVertsPerPoly + 2] = hole[tris[t + 2]];
					pregs[npolys] = hreg[tris[t + 0]];
					pareas[npolys] = harea[tris[t + 0]];
					npolys++;
				}
			}
			if (npolys == 0)
				return;

			//merge polygons
			if (numVertsPerPoly > 3)
			{
				for (; ; )
				{
					//find best polygons
					int bestMergeVal = 0;
					int bestPolyA = 0, bestPolyB = 0, bestEa = 0, bestEb = 0;

					for (int j = 0; j < npolys - 1; j++)
					{
						int pj = j * numVertsPerPoly;
						for (int k = j + 1; k < npolys; k++)
						{
							int pk = k * numVertsPerPoly;
							int edgeA = 0, edgeB = 0;
							int v = GetPolyMergeValue(polys, pj, pk, this.verts, ref edgeA, ref edgeB, numVertsPerPoly);
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
						int polyA = bestPolyA * numVertsPerPoly;
						int polyB = bestPolyB * numVertsPerPoly;
						MergePolys(polys, polyA, polyB, bestEa, bestEb, numVertsPerPoly);
						
						//save space by overwriting second polygon with last polygon 
						//since second polygon is no longer need
						int lastPoly = (npolys - 1) * numVertsPerPoly;
						
						for (int k = 0; k < numVertsPerPoly; k++)
							polys[polyB + k] = polys[lastPoly + k];
						
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

				int p = this.npolys * numVertsPerPoly * 2;
				for (int j = 0; j < numVertsPerPoly; j++)
					this.polys[p + j] = polys[i * numVertsPerPoly + j];

				this.regionIds[this.npolys] = pregs[i];
				this.areas[this.npolys] = (AreaFlags)pareas[i];
				this.npolys++;
			}
		}

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
				int t = i * numVertsPerPoly * 2;
				for (int j = 0; j < numVertsPerPoly; j++)
				{
					if (polys[t + j] == MESH_NULL_IDX)
						break;

					//get closest two verts
					int v0 = polys[t + j];
					int v1 = (j + 1 >= numVertsPerPoly || polys[t + j + 1] == MESH_NULL_IDX) ? polys[t + 0] : polys[t + j + 1];

					if (v0 < v1)
					{
						Edge edge = edges[edgeCount];
						edge.vert = new int[2];
						edge.polyEdge = new int[2];
						edge.poly = new int[2];

						edge.vert[0] = v0;
						edge.vert[1] = v1;
						edge.poly[0] = i;
						edge.polyEdge[0] = j;
						edge.poly[1] = i;
						edge.polyEdge[1] = 0;

						//insert edge
						firstEdge[nextEdge + edgeCount] = firstEdge[v0];
						firstEdge[v0] = edgeCount;
						edgeCount++;
					}
				}
			}

			//Iterate through all the polygons again
			for (int i = 0; i < npolys; i++)
			{
				int t = i * numVertsPerPoly * 2;
				for (int j = 0; j < numVertsPerPoly; j++)
				{
					if (polys[t + j] == MESH_NULL_IDX)
						break;

					int v0 = polys[t + j];
					int v1 = (j + 1 >= numVertsPerPoly || polys[t + j + 1] == MESH_NULL_IDX) ? polys[t + 0] : polys[t + j + 1];

					if (v0 > v1)
					{
						//Iterate through al the edges
						for (int e = firstEdge[v1]; e != MESH_NULL_IDX; e = firstEdge[nextEdge + e])
						{
							Edge edge = edges[e];
							if (edge.vert[1] == v0 && edge.poly[0] == edge.poly[1])
							{
								edge.poly[1] = i;
								edge.polyEdge[1] = j;
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
				if (e.poly[0] != e.poly[1])
				{
					int p0 = e.poly[0] * numVertsPerPoly * 2;
					int p1 = e.poly[1] * numVertsPerPoly * 2;
					polys[p0 + numVertsPerPoly + e.polyEdge[0]] = e.poly[1];
					polys[p1 + numVertsPerPoly + e.polyEdge[1]] = e.poly[0];
				}
			}
		}


		/// <summary>
		/// Count the number of vertices per polygon
		/// </summary>
		private int CountPolyVerts(int[] polys, int start, int numVertsPerPolygon)
		{
			for (int i = start; i < start + numVertsPerPolygon; i++)
				if (polys[i] == MESH_NULL_IDX)
					return i;

			return numVertsPerPolygon;
		}

		/// <summary>
		/// Shift all existing elements to the right and insert new element at index 0
		/// </summary>
		/// <param name="v"></param>
		/// <param name="array"></param>
		/// <param name="an"></param>
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
		/// <param name="v"></param>
		/// <param name="array"></param>
		/// <param name="an"></param>
		private void PushBack(int v, int[] array, ref int an)
		{
			array[an] = v;
			an++;
		}
		
		// (Bx - Ax) (Cx - Ax)
		// (Bz - Az) (Cz - Az) 
		//
		//(Bx - Ax) * (Cz - Az) - (Cx - Ax) * (Bz - Az)
		private bool ULeft(int[] verts, int a, int b, int c)
		{
			return (verts[b + 0] - verts[a + 0]) * (verts[c + 2] - verts[a + 2]) -
				(verts[c + 0] - verts[a + 0]) * (verts[b + 2] - verts[a + 2]) < 0;
		}

		private int Prev(int i, int n) { return i - 1 >= 0 ? i - 1 : n - 1; }
		private int Next(int i, int n) { return i + 1 < n ? i + 1 : 0; }
		
		///<summary>
		///true if and only if (v[i], v[j]) is a proper internal diagonal of polygon
		///</summary>
		private bool Diagonal(int i, int j, int n, int[] verts, int[] indices)
		{
			return InCone(i, j, n, verts, indices) && Diagonalie(i, j, n, verts, indices);
		}

		///<summary>
		///true if and only if diagonal (i, j) is strictly internal to polygon 
		///in neighborhood of i endpoint
		///</summary>
		private bool InCone(int i, int j, int n, int[] verts, int[] indices)
		{
			int pi = (indices[i] & 0x0fffffff) * 4;
			int pj = (indices[j] & 0x0fffffff) * 4;
			int pi1 = (indices[Next(i, n)] & 0x0fffffff) * 4;
			int pin1 = (indices[Prev(i, n)] & 0x0fffffff) * 4;

			//if P[i] is convex vertex (i + 1 left or on (i - 1, i))
			if (LeftOn(verts, pin1, pi, pi1))
				return Left(verts, pi, pj, pin1) && Left(verts, pj, pi, pi1);

			//assume (i - 1, i, i + 1) not collinear
			return !(LeftOn(verts, pi, pj, pi1) && LeftOn(verts, pj, pi, pin1));
		}

		///<summary>
		///true if and only if (v[i], v[j]) is internal or external diagonal
		///ignoring edges incident to v[i] or v[j]
		///</summary>
		private bool Diagonalie(int i, int j, int n, int[] verts, int[] indices)
		{
			int d0 = (indices[i] & 0x0fffffff) * 4;
			int d1 = (indices[j] & 0x0fffffff) * 4;

			//for each edge (k, k + 1)
			for (int k = 0; k < n; k++)
			{
				int k1 = Next(k, n);

				//skip edges incident to i or j
				if (!((k == i) || (k1 == i) || (k == j) || (k1 == j)))
				{
					int p0 = (indices[k] & 0x0fffffff) * 4;
					int p1 = (indices[k1] & 0x0fffffff) * 4;

					if (VEqual(verts, d0, p0) || VEqual(verts, d1, p0) || VEqual(verts, d0, p1) || VEqual(verts, d1, p1))
						continue;

					if (Intersect(verts, d0, d1, p0, p1))
						return false;
				}
			}

			return true;
		}

		private bool Left(int[] verts, int a, int b, int c)
		{
			return Area2(verts, a, b, c) < 0;
		}

		private bool LeftOn(int[] verts, int a, int b, int c)
		{
			return Area2(verts, a, b, c) <= 0;
		}

		private bool Collinear(int[] verts, int a, int b, int c)
		{
			return Area2(verts, a, b, c) == 0;
		}

		private int Area2(int[] verts, int a, int b, int c)
		{
			return (verts[b + 0] - verts[a + 0]) * (verts[c + 2] - verts[a + 2]) -
				(verts[c + 0] - verts[a + 0]) * (verts[b + 2] - verts[a + 2]);
		}

		private bool VEqual(int[] verts, int a, int b)
		{
			return verts[a + 0] == verts[b + 0] && verts[a + 2] == verts[b + 2];
		}

		/// <summary>
		/// True if and only if segments AB and CD intersect, properly or improperyl
		/// </summary>
		private bool Intersect(int[] verts, int a, int b, int c, int d)
		{
			if (IntersectProp(verts, a, b, c, d))
				return true;
			else if (Between(verts, a, b, c) || Between(verts, a, b, d) ||
				Between(verts, c, d, a) || Between(verts, c, d, b))
				return true;
			else
				return false;
		}

		/// <summary>
		/// Intersect properly: share a point interior to both segments. properness determined by strict leftness
		/// </summary>
		private bool IntersectProp(int[] verts, int a, int b, int c, int d)
		{
			//eliminate improper cases
			if (Collinear(verts, a, b, c) || Collinear(verts, a, b, d) ||
				Collinear(verts, c, d, a) || Collinear(verts, c, d, b))
				return false;

			return xorb(Left(verts, a, b, c), Left(verts, a, b, d)) && xorb(Left(verts, c, d, a), Left(verts, c, d, b));
		}

		/// <summary>
		/// Exclusive OR
		/// True if and only if exactly one argument is true
		/// </summary>
		/// <param name="x"></param>
		/// <param name="y"></param>
		/// <returns></returns>
		private bool xorb(bool x, bool y)
		{
			return !x ^ !y;
		}

		/// <summary>
		/// True if and only if (A, B, C) are collinear and point C lies on closed segment AB
		/// </summary>
		private bool Between(int[] verts, int a, int b, int c)
		{
			if (!Collinear(verts, a, b, c))
				return false;

			if (verts[a + 0] != verts[b + 0])
				return ((verts[a + 0] <= verts[c + 0]) && (verts[c + 0] <= verts[b + 0])) ||
					((verts[a + 0] >= verts[c + 0]) && (verts[c + 0] >= verts[b + 0]));
			else
				return ((verts[a + 2] <= verts[c + 2]) && (verts[c + 2] <= verts[b + 2])) ||
					((verts[a + 2] >= verts[c + 2]) && (verts[c + 2] >= verts[b + 2]));
		}
	}
}
