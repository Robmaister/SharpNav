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
	public class NavMesh
	{
		private const int VERTEX_BUCKET_COUNT = 1 << 12; //2 ^ 12 vertices
		public const int MESH_NULL_IDX = -1;

		private int nverts;
		private int npolys;

		private Vector3[] verts; //each vertex contains (x, y, z)
		private Polygon[] polys;
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
		public Vector3[] Verts { get { return verts; } }
		public Polygon[] Polys { get { return polys; } }
		public AreaFlags[] Areas { get { return areas; } }

		public BBox3 Bounds { get { return bounds; } }
		public float CellSize { get { return cellSize; } }
		public float CellHeight { get { return cellHeight; } }
		public int BorderSize { get { return borderSize; } }

		public struct Polygon
		{
			public int[] Vertices; //"numVertsPerPoly" elements
			public int[] ExtraInfo; //"numVertsPerPoly" elements (contains flags, other polys)
		}

		private struct Tris
		{
			public int [] VertexHash; //make sure only 3 vertices
		}
		
		private struct Edge
		{
			public int[] vert;
			public int[] polyEdge;
			public int[] poly;
		}

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
				maxVertsPerCont = Math.Max(maxVertsPerCont, contSet.Contours[i].NumVerts);
			}

			//vertex flags
			int[] vFlags = new int[maxVertices];

			//initialize the mesh members
			this.verts = new Vector3[maxVertices]; 
			this.polys = new Polygon[maxTris];
			this.regionIds = new int[maxTris];
			this.areas = new AreaFlags[maxTris];

			this.nverts = 0;
			this.npolys = 0;
			this.numVertsPerPoly = numVertsPerPoly;
			this.maxPolys = maxTris;

			for (int i = 0; i < this.polys.Length; i++)
			{
				this.polys[i].Vertices = new int[numVertsPerPoly];
				this.polys[i].ExtraInfo = new int[numVertsPerPoly];

				for (int j = 0; j < numVertsPerPoly; j++)
				{
					this.polys[i].Vertices[j] = MESH_NULL_IDX;
					this.polys[i].ExtraInfo[j] = MESH_NULL_IDX;
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
			int[] polys = new int[(maxVertsPerCont + 1) * numVertsPerPoly];
			
			int tempPolyIndex = maxVertsPerCont * numVertsPerPoly;

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
				if (ntris <= 0) //TODO notify user when this happens. Logging?
					ntris = -ntris;

				//add and merge vertices
				for (int j = 0; j < cont.NumVerts; j++)
				{
					int v = j;

					//save the hash code for each vertex
					indices[j] = AddVertex(cont.Vertices[v].X, cont.Vertices[v].Y, cont.Vertices[v].Z, 
						this.verts, firstVert, nextVert, ref this.nverts);

					if ((cont.Vertices[v].RawVertexIndex & ContourSet.BORDER_VERTEX) != 0)
					{
						//the vertex should be removed
						vFlags[indices[j]] = 1;
					}
				}
				
				//builds initial polygons
				int npolys = 0;
				for (int j = 0; j < polys.Length; j++)
					polys[j] = MESH_NULL_IDX;

				//iterate through all the triangles
				for (int j = 0; j < ntris; j++)
				{
					//make sure there are three distinct vertices. anything less can't be a polygon.
					if (tris[j].VertexHash[0] != tris[j].VertexHash[1] 
						&& tris[j].VertexHash[0] != tris[j].VertexHash[2] 
						&& tris[j].VertexHash[1] != tris[j].VertexHash[2])
					{
						//each polygon has numVertsPerPoly
						//index 0, 1, 2 store triangle vertices
						//other polygon indexes (3 to numVertsPerPoly - 1) should be used for storing extra vertices when two polygons merge together
						polys[npolys * numVertsPerPoly + 0] = indices[tris[j].VertexHash[0]];
						polys[npolys * numVertsPerPoly + 1] = indices[tris[j].VertexHash[1]];
						polys[npolys * numVertsPerPoly + 2] = indices[tris[j].VertexHash[2]];
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
								int v = GetPolyMergeValue(polys, pj, pk, this.verts, ref ea, ref eb);
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
							MergePolys(polys, pa, pb, bestEdgeA, bestEdgeB);
							
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
					//NavMesh poly stores 2 * numVertsPerPoly elements
					int q = j * numVertsPerPoly;
					for (int k = 0; k < numVertsPerPoly; k++)
						this.polys[this.npolys].Vertices[k] = polys[q + k];

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
						if (this.polys[i].ExtraInfo[j] != MESH_NULL_IDX)
							continue;

						int nj = j + 1;
						if (nj >= numVertsPerPoly || this.polys[i].Vertices[nj] == MESH_NULL_IDX)
							nj = 0;

						//grab two consecutive vertices
						int va = this.polys[i].Vertices[j];
						int vb = this.polys[i].Vertices[nj];

						//set some flags
						if (this.verts[va].X == 0 && this.verts[vb].X == 0)
							this.polys[i].ExtraInfo[j] = 0x8000 | 0;
						else if (this.verts[va].Z == contSet.Height && this.verts[vb].Z == contSet.Height)
							this.polys[i].ExtraInfo[j] = 0x8000 | 1;
						else if (this.verts[va].X == contSet.Width && this.verts[vb].X == contSet.Width)
							this.polys[i].ExtraInfo[j] = 0x8000 | 2;
						else if (this.verts[va].Z == 0 && this.verts[vb].Z == 0)
							this.polys[i].ExtraInfo[j] = 0x8000 | 3;
					}
				}
			}

			//user fills this in?
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
		private int Triangulate(int n, ContourSet.SimplifiedVertex[] verts, int[] indices, Tris[] tris)
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
					uint temp = (uint)indices[i1];
					temp |= 0x80000000;
					indices[i1] |= (int)temp;
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
					
					if ((indices[i1] & 0x80000000) != 0)
					{
						int p0 = (indices[i] & 0x0fffffff);
						int p2 = (indices[Next(i1, n)] & 0x0fffffff);

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
				dst[ntris].VertexHash = new int[3];
				dst[ntris].VertexHash[0] = indices[mi] & 0x0fffffff; 
				dst[ntris].VertexHash[1] = indices[mi1] & 0x0fffffff; 
				dst[ntris].VertexHash[2] = indices[mi2] & 0x0fffffff; 
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
					uint temp = (uint)indices[mi1];
					temp |= 0x80000000;
					indices[mi1] = (int)temp;
				}
				else
				{
					indices[mi] &= 0x0fffffff;
				}

				if (Diagonal(mi, Next(mi1, n), n, verts, indices))
				{
					uint temp = (uint)indices[mi1];
					temp |= 0x80000000;
					indices[mi1] = (int)temp;
				}
				else
				{
					indices[mi1] &= 0x0fffffff;
				}
			}

			//append remaining triangle
			dst[ntris] = new Tris();
			dst[ntris].VertexHash = new int[3];
			dst[ntris].VertexHash[0] = indices[0] & 0x0fffffff; 
			dst[ntris].VertexHash[1] = indices[1] & 0x0fffffff;
			dst[ntris].VertexHash[2] = indices[2] & 0x0fffffff; 
			ntris++;

			//save triangle information
			tris = dst;
			return ntris;
		}

		/// <summary>
		/// Generate a new vertices with (x, y, z) coordiates and return the hash code index 
		/// </summary>
		/// <returns></returns>
		private int AddVertex(int x, int y, int z, Vector3[] verts, int[] firstVert, int[] nextVert, ref int nv)
		{
			//generate a unique index
			int bucket = ComputeVertexHash(x, 0, z);
			
			//access that vertex
			int i = firstVert[bucket];

			//default unintialized i value is -1.
			//if i isn't equal to -1, this vertex should exist somewhere
			while (i != -1)
			{
				//found existing vertex
				if (verts[i].X == x && (Math.Abs(verts[i].Y - y) <= 2) && verts[i].Z == z)
					return i;
				
				//next vertex. this stores the vertices linearly (similar to a linked list)
				i = nextVert[i];
			}

			//no existing vertex so make a new one
			i = nv;
			nv++;

			//save the data
			verts[i].X = x;
			verts[i].Y = y;
			verts[i].Z = z;

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
		private int GetPolyMergeValue(int[] polys, int polyA, int polyB, Vector3[] verts, ref int edgeA, ref int edgeB)
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
			if (!ULeft(verts, vertA, vertB, vertC))
				return -1;

			vertA = polys[polyB + (edgeB + numVertsB - 1) % numVertsB];
			vertB = polys[polyB + edgeB];
			vertC = polys[polyA + (edgeA + 2) % numVertsA];
			if (!ULeft(verts, vertA, vertB, vertC))
				return -1;

			vertA = polys[polyA + edgeA];
			vertB = polys[polyA + (edgeA + 1) % numVertsA];

			int dx = (int)(verts[vertA].X - verts[vertB].X);
			int dy = (int)(verts[vertA].Z - verts[vertB].Z);
			return dx * dx + dy * dy;
		}

		/// <summary>
		/// The two polygon arrrays are merged into a single array
		/// </summary>
		private void MergePolys(int[] polys, int polyA, int polyB, int edgeA, int edgeB)
		{
			int numA = CountPolyVerts(polys, polyA);
			int numB = CountPolyVerts(polys, polyB);
			int[] temp = new int[numA + numB];

			//merge
			for (int i = 0; i < temp.Length; i++)
				temp[i] = MESH_NULL_IDX;

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
							edges[e + 2] = this.regionIds[i];
							edges[e + 3] = (int)this.areas[i];
							nedges++;
						}
					}

					//remove polygon
					for (int j = 0; j < numVertsPerPoly; j++)
					{
						this.polys[i].Vertices[j] = this.polys[this.npolys - 1].Vertices[j];
						this.polys[i].ExtraInfo[j] = MESH_NULL_IDX;
					} 
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
			ContourSet.SimplifiedVertex[] tverts = new ContourSet.SimplifiedVertex[nhole];
			int[] thole = new int[nhole];

			//generate temp vertex array for triangulation
			for (int i = 0; i < nhole; i++)
			{
				int pi = hole[i];
				tverts[i].X = (int)this.verts[pi].X;
				tverts[i].Y = (int)this.verts[pi].Y;
				tverts[i].Z = (int)this.verts[pi].Z;
				tverts[i].RawVertexIndex = 0;
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
				polys[j] = MESH_NULL_IDX;

			for (int j = 0; j < ntris; j++)
			{
				if (tris[j].VertexHash[0] != tris[j].VertexHash[1] 
					&& tris[j].VertexHash[0] != tris[j].VertexHash[2] 
					&& tris[j].VertexHash[1] != tris[j].VertexHash[2])
				{
					polys[npolys * numVertsPerPoly + 0] = hole[tris[j].VertexHash[0]];
					polys[npolys * numVertsPerPoly + 1] = hole[tris[j].VertexHash[1]];
					polys[npolys * numVertsPerPoly + 2] = hole[tris[j].VertexHash[2]];
					pregs[npolys] = hreg[tris[j].VertexHash[0]];
					pareas[npolys] = harea[tris[j].VertexHash[0]];
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
						int polyA = bestPolyA * numVertsPerPoly;
						int polyB = bestPolyB * numVertsPerPoly;
						MergePolys(polys, polyA, polyB, bestEa, bestEb);
						
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

				for (int j = 0; j < numVertsPerPoly; j++)
				{
					this.polys[this.npolys].Vertices[j] = polys[i * numVertsPerPoly + j];
					this.polys[this.npolys].ExtraInfo[j] = MESH_NULL_IDX;
				}

				this.regionIds[this.npolys] = pregs[i];
				this.areas[this.npolys] = (AreaFlags)pareas[i];
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
						Edge edge = new Edge();
						edge.vert = new int[2];
						edge.polyEdge = new int[2];
						edge.poly = new int[2];

						//store vertices
						edge.vert[0] = v0;
						edge.vert[1] = v1;

						//poly array stores index of polygon
						//polyEdge stores the vertex
						edge.poly[0] = i;
						edge.polyEdge[0] = j;
						edge.poly[1] = i;
						edge.polyEdge[1] = 0;

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

				//the endpoints belong to different polygons
				if (e.poly[0] != e.poly[1])
				{
					//store other polygon number as part of extra info
					polys[e.poly[0]].ExtraInfo[e.polyEdge[0]] = e.poly[1];
					polys[e.poly[1]].ExtraInfo[e.polyEdge[1]] = e.poly[0];
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

		private int CountPolyVerts(int[] polys, int start)
		{
			for (int i = start; i < start + numVertsPerPoly; i++)
				if (polys[i] == MESH_NULL_IDX)
					return i;

			return numVertsPerPoly;
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
		
		private bool ULeft(Vector3[] verts, int a, int b, int c)
		{
			return (verts[b].X - verts[a].X) * (verts[c].Z - verts[a].Z) -
				(verts[c].X - verts[a].X) * (verts[b].Z - verts[a].Z) < 0;
		}

		private int Prev(int i, int n) { return i - 1 >= 0 ? i - 1 : n - 1; }
		private int Next(int i, int n) { return i + 1 < n ? i + 1 : 0; }
		
		///<summary>
		///true if and only if (v[i], v[j]) is a proper internal diagonal of polygon
		///</summary>
		private bool Diagonal(int i, int j, int n, ContourSet.SimplifiedVertex[] verts, int[] indices)
		{
			return InCone(i, j, n, verts, indices) && Diagonalie(i, j, n, verts, indices);
		}

		///<summary>
		///true if and only if diagonal (i, j) is strictly internal to polygon 
		///in neighborhood of i endpoint
		///</summary>
		private bool InCone(int i, int j, int n, ContourSet.SimplifiedVertex[] verts, int[] indices)
		{
			int pi = (indices[i] & 0x0fffffff);
			int pj = (indices[j] & 0x0fffffff);
			int pi1 = (indices[Next(i, n)] & 0x0fffffff);
			int pin1 = (indices[Prev(i, n)] & 0x0fffffff);

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
		private bool Diagonalie(int i, int j, int n, ContourSet.SimplifiedVertex[] verts, int[] indices)
		{
			int d0 = (indices[i] & 0x0fffffff);
			int d1 = (indices[j] & 0x0fffffff);

			//for each edge (k, k + 1)
			for (int k = 0; k < n; k++)
			{
				int k1 = Next(k, n);

				//skip edges incident to i or j
				if (!((k == i) || (k1 == i) || (k == j) || (k1 == j)))
				{
					int p0 = (indices[k] & 0x0fffffff);
					int p1 = (indices[k1] & 0x0fffffff);

					if (VEqual(verts, d0, p0) || VEqual(verts, d1, p0) || VEqual(verts, d0, p1) || VEqual(verts, d1, p1))
						continue;

					if (Intersect(verts, d0, d1, p0, p1))
						return false;
				}
			}

			return true;
		}

		private bool Left(ContourSet.SimplifiedVertex[] verts, int a, int b, int c)
		{
			return Area2(verts, a, b, c) < 0;
		}

		private bool LeftOn(ContourSet.SimplifiedVertex[] verts, int a, int b, int c)
		{
			return Area2(verts, a, b, c) <= 0;
		}

		private bool Collinear(ContourSet.SimplifiedVertex[] verts, int a, int b, int c)
		{
			return Area2(verts, a, b, c) == 0;
		}

		private int Area2(ContourSet.SimplifiedVertex[] verts, int a, int b, int c)
		{
			return (verts[b].X - verts[a].X) * (verts[c].Z - verts[a].Z) -
				(verts[c].X - verts[a].X) * (verts[b].Z - verts[a].Z);
		}

		private bool VEqual(ContourSet.SimplifiedVertex[] verts, int a, int b)
		{
			return verts[a].X == verts[b].X && verts[a].Z == verts[b].Z;
		}

		/// <summary>
		/// True if and only if segments AB and CD intersect, properly or improperyl
		/// </summary>
		private bool Intersect(ContourSet.SimplifiedVertex[] verts, int a, int b, int c, int d)
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
		private bool IntersectProp(ContourSet.SimplifiedVertex[] verts, int a, int b, int c, int d)
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
		private bool Between(ContourSet.SimplifiedVertex[] verts, int a, int b, int c)
		{
			if (!Collinear(verts, a, b, c))
				return false;

			if (verts[a].X != verts[b].X)
				return ((verts[a].X <= verts[c].X) && (verts[c].X <= verts[b].X)) ||
					((verts[a].X >= verts[c].X) && (verts[c].X >= verts[b].X));
			else
				return ((verts[a].Z <= verts[c].Z) && (verts[c].Z <= verts[b].Z)) ||
					((verts[a].Z >= verts[c].Z) && (verts[c].Z >= verts[b].Z));
		}
	}
}
