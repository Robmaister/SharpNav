// Copyright (c) 2013-2015 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
// Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE

using System;
using System.Collections.Generic;

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
	/// The class of Poly mesh.
	/// </summary>
	public class PolyMesh
	{
		public const int NullId = -1;
		
		private const int DiagonalFlag = unchecked((int)0x80000000);
		private const int NeighborEdgeFlag = unchecked((int)0x80000000);

		private PolyVertex[] vertices;
		private Polygon[] polygons;

		private int numVertsPerPoly;

		//copied data from CompactHeightfield
		private BBox3 bounds;
		private float cellSize;
		private float cellHeight;
		private int borderSize;

		//HACK borderSize is 0 here. Fix with borderSize.

		/// <summary>
		/// Initializes a new instance of the <see cref="PolyMesh"/> class.
		/// </summary>
		/// <param name="contSet">The <see cref="ContourSet"/> to generate polygons from.</param>
		/// <param name="settings">The settings to build with.</param>
		public PolyMesh(ContourSet contSet, NavMeshGenerationSettings settings)
			: this(contSet, settings.CellSize, settings.CellHeight, 0, settings.VertsPerPoly)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="PolyMesh"/> class by creating polygons from contours.
		/// </summary>
		/// <param name="contSet">The <see cref="ContourSet"/> to generate polygons from.</param>
		/// <param name="cellSize">The size of one voxel/cell.</param>
		/// <param name="cellHeight">The height of one voxel/cell.</param>
		/// <param name="borderSize">The size of the border around the mesh.</param>
		/// <param name="numVertsPerPoly">The maximum number of vertices per polygon.</param>
		public PolyMesh(ContourSet contSet, float cellSize, float cellHeight, int borderSize, int numVertsPerPoly)
		{
			//copy contour data
			this.bounds = contSet.Bounds;
			this.cellSize = cellSize;
			this.cellHeight = cellHeight;
			this.borderSize = borderSize;

			//get maximum limits
			int maxVertices, maxTris, maxVertsPerCont;
			contSet.GetVertexLimits(out maxVertices, out maxTris, out maxVertsPerCont);

			//initialize the mesh members
			var verts = new List<PolyVertex>(maxVertices);
			var polys = new List<Polygon>(maxTris);

			Queue<int> vertRemoveQueue = new Queue<int>(maxVertices);

			this.numVertsPerPoly = numVertsPerPoly;
			int[] mergeTemp = new int[numVertsPerPoly];

			var vertDict = new Dictionary<PolyVertex, int>(new PolyVertex.RoughYEqualityComparer(2));

			int[] indices = new int[maxVertsPerCont]; //keep track of vertex hash codes
			Triangle[] tris = new Triangle[maxVertsPerCont];
			List<Polygon> contPolys = new List<Polygon>(maxVertsPerCont + 1);

			//extract contour data
			foreach (Contour cont in contSet)
			{
				//skip null contours
				if (cont.IsNull)
					continue;

				PolyVertex[] vertices = new PolyVertex[cont.Vertices.Length];

				//triangulate contours
				for (int i = 0; i < cont.Vertices.Length; i++)
				{
					var cv = cont.Vertices[i];
					vertices[i] = new PolyVertex(cv.X, cv.Y, cv.Z);
					indices[i] = i;
				}

				//Form triangles inside the area bounded by the contours
				int ntris = Triangulate(cont.Vertices.Length, vertices, indices, tris);
				if (ntris <= 0) //TODO notify user when this happens. Logging?
				{
					Console.WriteLine("ntris <= 0");
					ntris = -ntris;
				}

				//add and merge vertices
				for (int i = 0; i < cont.Vertices.Length; i++)
				{
					var cv = cont.Vertices[i];
					var pv = vertices[i];

					//save the hash code for each vertex
					indices[i] = AddVertex(vertDict, pv, verts);

					if (RegionId.HasFlags(cv.RegionId, RegionFlags.VertexBorder))
					{
						//the vertex should be removed
						vertRemoveQueue.Enqueue(indices[i]);
					}
				}

				contPolys.Clear();

				//iterate through all the triangles
				for (int i = 0; i < ntris; i++)
				{
					Triangle ti = tris[i];

					//make sure there are three distinct vertices. anything less can't be a polygon.
					if (ti.Index0 == ti.Index1
						|| ti.Index0 == ti.Index2
						|| ti.Index1 == ti.Index2)
						continue;

					//each polygon has numVertsPerPoly
					//index 0, 1, 2 store triangle vertices
					//other polygon indexes (3 to numVertsPerPoly - 1) should be used for storing extra vertices when two polygons merge together
					Polygon p = new Polygon(numVertsPerPoly, Area.Null, RegionId.Null);
					p.Vertices[0] = RemoveDiagonalFlag(indices[ti.Index0]);
					p.Vertices[1] = RemoveDiagonalFlag(indices[ti.Index1]);
					p.Vertices[2] = RemoveDiagonalFlag(indices[ti.Index2]);
					contPolys.Add(p);
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

						if (bestMergeVal <= 0)
							break;

						Polygon pa = contPolys[bestPolyA];
						Polygon pb = contPolys[bestPolyB];
						pa.MergeWith(pb, bestEdgeA, bestEdgeB, mergeTemp);
						contPolys[bestPolyB] = contPolys[contPolys.Count - 1];
						contPolys.RemoveAt(contPolys.Count - 1);

					}
				}

				//store polygons
				for (int i = 0; i < contPolys.Count; i++)
				{
					Polygon p = contPolys[i];
					Polygon p2 = new Polygon(numVertsPerPoly, cont.Area, cont.RegionId);

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
		/// Gets the number of vertices
		/// </summary>
		public int VertCount
		{
			get
			{
				return vertices.Length;
			}
		}

		/// <summary>
		/// Gets the number of polygons
		/// </summary>
		public int PolyCount
		{
			get
			{
				return polygons.Length;
			}
		}

		/// <summary>
		/// Gets the number of vertices per polygon
		/// </summary>
		public int NumVertsPerPoly
		{
			get
			{
				return numVertsPerPoly;
			}
		}

		/// <summary>
		/// Gets the vertex data
		/// </summary>
		public PolyVertex[] Verts
		{
			get
			{
				return vertices;
			}
		}

		/// <summary>
		/// Gets the polygon data
		/// </summary>
		public Polygon[] Polys
		{
			get
			{
				return polygons;
			}
		}

		/// <summary>
		/// Gets the bounds.
		/// </summary>
		/// <value>The bounds.</value>
		public BBox3 Bounds
		{
			get
			{
				return bounds;
			}
		}

		/// <summary>
		/// Gets the cell size
		/// </summary>
		public float CellSize
		{
			get
			{
				return cellSize;
			}
		}

		/// <summary>
		/// Gets the cell height
		/// </summary>
		public float CellHeight
		{
			get
			{
				return cellHeight;
			}
		}

		/// <summary>
		/// Gets the border size
		/// </summary>
		public int BorderSize
		{
			get
			{
				return borderSize;
			}
		}

		/// <summary>
		/// Determines if it is a boundary edge with the specified flag.
		/// </summary>
		/// <returns><c>true</c> if is boundary edge the specified flag; otherwise, <c>false</c>.</returns>
		/// <param name="flag">The flag.</param>
		public static bool IsBoundaryEdge(int flag)
		{
			return (flag & NeighborEdgeFlag) != 0;
		}

		/// <summary>
		/// Determines if it is an interior edge with the specified flag.
		/// </summary>
		/// <returns><c>true</c> if is interior edge the specified flag; otherwise, <c>false</c>.</returns>
		/// <param name="flag">The flag.</param>
		public static bool IsInteriorEdge(int flag)
		{
			return (flag & NeighborEdgeFlag) == 0;
		}

		/// <summary>
		/// Determines if it is a diagonal flag on the specified index.
		/// </summary>
		/// <param name="index">The index</param>
		/// <returns><c>true</c> if it is a diagonal flag on the specified index; otherwise, <c>false</c>.</returns>
		public static bool HasDiagonalFlag(int index)
		{
			return (index & DiagonalFlag) != 0;
		}

		/// <summary>
		/// True if and only if (v[i], v[j]) is a proper internal diagonal of polygon.
		/// </summary>
		/// <param name="i">Vertex index i</param>
		/// <param name="j">Vertex index j</param>
		/// <param name="verts">Contour vertices</param>
		/// <param name="indices">PolyMesh indices</param>
		/// <returns>True, if internal diagonal. False, if otherwise.</returns>
		public static bool Diagonal(int i, int j, PolyVertex[] verts, int[] indices)
		{
			return InCone(i, j, verts, indices) && Diagonalie(i, j, verts, indices);
		}

		/// <summary>
		/// True if and only if diagonal (i, j) is strictly internal to polygon 
		/// in neighborhood of i endpoint.
		/// </summary>
		/// <param name="i">Vertex index i</param>
		/// <param name="j">Vertex index j</param>
		/// <param name="verts">Contour vertices</param>
		/// <param name="indices">PolyMesh indices</param>
		/// <returns>True, if internal. False, if otherwise.</returns>
		public static bool InCone(int i, int j, PolyVertex[] verts, int[] indices)
		{
			int pi = RemoveDiagonalFlag(indices[i]);
			int pj = RemoveDiagonalFlag(indices[j]);
			int pi1 = RemoveDiagonalFlag(indices[Next(i, verts.Length)]);
			int pin1 = RemoveDiagonalFlag(indices[Prev(i, verts.Length)]);

			//if P[i] is convex vertex (i + 1 left or on (i - 1, i))
			if (PolyVertex.IsLeftOn(ref verts[pin1], ref verts[pi], ref verts[pi1]))
				return PolyVertex.IsLeft(ref verts[pi], ref verts[pj], ref verts[pin1]) && PolyVertex.IsLeft(ref verts[pj], ref verts[pi], ref verts[pi1]);

			//assume (i - 1, i, i + 1) not collinear
			return !(PolyVertex.IsLeftOn(ref verts[pi], ref verts[pj], ref verts[pi1]) && PolyVertex.IsLeftOn(ref verts[pj], ref verts[pi], ref verts[pin1]));
		}

		/// <summary>
		/// True if and only if (v[i], v[j]) is internal or external diagonal
		/// ignoring edges incident to v[i] or v[j].
		/// </summary>
		/// <param name="i">Vertex index i</param>
		/// <param name="j">Vertex index j</param>
		/// <param name="verts">Contour vertices</param>
		/// <param name="indices">PolyMesh indices</param>
		/// <returns>True, if internal or external diagonal. False, if otherwise.</returns>
		public static bool Diagonalie(int i, int j, PolyVertex[] verts, int[] indices)
		{
			int d0 = RemoveDiagonalFlag(indices[i]);
			int d1 = RemoveDiagonalFlag(indices[j]);

			//for each edge (k, k + 1)
			for (int k = 0; k < verts.Length; k++)
			{
				int k1 = Next(k, verts.Length);

				//skip edges incident to i or j
				if (!((k == i) || (k1 == i) || (k == j) || (k1 == j)))
				{
					int p0 = RemoveDiagonalFlag(indices[k]);
					int p1 = RemoveDiagonalFlag(indices[k1]);

					if (PolyVertex.Equal2D(ref verts[d0], ref verts[p0]) ||
						PolyVertex.Equal2D(ref verts[d1], ref verts[p0]) ||
						PolyVertex.Equal2D(ref verts[d0], ref verts[p1]) ||
						PolyVertex.Equal2D(ref verts[d1], ref verts[p1]))
						continue;

					if (PolyVertex.Intersect(ref verts[d0], ref verts[d1], ref verts[p0], ref verts[p1]))
						return false;
				}
			}

			return true;
		}

		/// <summary>
		/// Gets the previous vertex index
		/// </summary>
		/// <param name="i">The current index</param>
		/// <param name="n">The max number of vertices</param>
		/// <returns>The previous index</returns>
		private static int Prev(int i, int n)
		{
			return i - 1 >= 0 ? i - 1 : n - 1;
		}

		/// <summary>
		/// Gets the next vertex index
		/// </summary>
		/// <param name="i">The current index</param>
		/// <param name="n">The max number of vertices</param>
		/// <returns>The next index</returns>
		private static int Next(int i, int n)
		{
			return i + 1 < n ? i + 1 : 0;
		}

		/// <summary>
		/// Determines whether the vertices follow a certain order
		/// </summary>
		/// <param name="a">Vertex A</param>
		/// <param name="b">Vertex B</param>
		/// <param name="c">Vertex C</param>
		/// <returns>True if conditions met, false if not</returns>
		private static bool ULeft(PolyVertex a, PolyVertex b, PolyVertex c)
		{
			return (b.X - a.X) * (c.Z - a.Z) -
				(c.X - a.X) * (b.Z - a.Z) < 0;
		}

		/// <summary>
		/// Sets the diagonal flag for a vertex
		/// </summary>
		/// <param name="index">The vertex index</param>
		private static void SetDiagonalFlag(ref int index)
		{
			index |= DiagonalFlag;
		}

		/// <summary>
		/// Remove the diagonal flag for a vertex
		/// </summary>
		/// <param name="index">The vertex index</param>
		/// <returns>The new index</returns>
		private static int RemoveDiagonalFlag(int index)
		{
			return index & ~DiagonalFlag;
		}

		/// <summary>
		/// Remove the diagonal flag for a vertex
		/// </summary>
		/// <param name="index">The vertex index</param>
		private static void RemoveDiagonalFlag(ref int index)
		{
			index &= ~DiagonalFlag;
		}

		/// <summary>
		/// Walk the edges of a contour to determine whether a triangle can be formed.
		/// Form as many triangles as possible.
		/// </summary>
		/// <param name="verts">Vertices array</param>
		/// <param name="indices">Indices array</param>
		/// <param name="tris">Triangles array</param>
		/// <returns>The number of triangles.</returns>
		private static int Triangulate(int n, PolyVertex[] verts, int[] indices, Triangle[] tris)
		{
			int ntris = 0;

			//last bit of index determines whether vertex can be removed
			for (int i = 0; i < n; i++)
			{
				int i1 = Next(i, n);
				int i2 = Next(i1, n);
				if (Diagonal(i, i2, verts, indices))
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
				int minIndex = -1;
				for (int i = 0; i < n; i++)
				{
					int i1 = Next(i, n);
					
					if (HasDiagonalFlag(indices[i1]))
					{
						int p0 = RemoveDiagonalFlag(indices[i]);
						int p2 = RemoveDiagonalFlag(indices[Next(i1, n)]);

						int dx = verts[p2].X - verts[p0].X;
						int dy = verts[p2].Z - verts[p0].Z;
						int len = dx * dx + dy * dy;

						if (minLen < 0 || len < minLen)
						{
							minLen = len;
							minIndex = i;
						}
					}
				}

				if (minIndex == -1)
				{
					minLen = -1;
					minIndex = -1;
					for (int i = 0; i < n; i++)
					{
						int i1 = Next(i, n);
						int i2 = Next(i1, n);
						if (DiagonalLoose(i, i2, verts, indices))
						{
							int p0 = RemoveDiagonalFlag(indices[i]);
							int p2 = RemoveDiagonalFlag(indices[Next(i2, n)]);

							int dx = verts[p2].X - verts[p0].X;
							int dy = verts[p2].Z - verts[p0].Z;
							int len = dx * dx + dy * dy;
							if (minLen < 0 || len < minLen)
							{
								minLen = len;
								minIndex = i;
							}
						}
					}

					//really messed up
					if (minIndex == -1)
						return -ntris;
				}

				int mi = minIndex;
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
				if (Diagonal(Prev(mi, n), mi1, verts, indices))
				{
					SetDiagonalFlag(ref indices[mi]);
				}
				else
				{
					RemoveDiagonalFlag(ref indices[mi]);
				}

				if (Diagonal(mi, Next(mi1, n), verts, indices))
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
		/// <param name="vertDict">Vertex dictionary that maps coordinates to index</param>
		/// <param name="v">A vertex.</param>
		/// <param name="verts">The list of vertices</param>
		/// <returns>The vertex index</returns>
		private static int AddVertex(Dictionary<PolyVertex, int> vertDict, PolyVertex v, List<PolyVertex> verts)
		{
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
		/// <param name="polys">Polygon list</param>
		/// <param name="polyA">Polygon A</param>
		/// <param name="polyB">Polygon B</param>
		/// <param name="verts">Vertex list</param>
		/// <param name="edgeA">Shared edge's endpoint A</param>
		/// <param name="edgeB">Shared edge's endpoint B</param>
		/// <returns>The distance between two vertices</returns>
		private static int GetPolyMergeValue(List<Polygon> polys, int polyA, int polyB, List<PolyVertex> verts, out int edgeA, out int edgeB)
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
		/// If vertex can't be removed, there is no need to spend time deleting it.
		/// </summary>
		/// <param name="polys">The polygon list</param>
		/// <param name="remove">The vertex index</param>
		/// <returns>True, if vertex can be removed. False, if otherwise.</returns>
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
		/// Connect two adjacent vertices with edges.
		/// </summary>
		/// <param name="vertices">The vertex list</param>
		/// <param name="polys">The polygon list</param>
		/// <param name="numVertsPerPoly">Number of vertices per polygon</param>
		private static void BuildMeshAdjacency(List<PolyVertex> vertices, List<Polygon> polys, int numVertsPerPoly)
		{
			int maxEdgeCount = polys.Count * numVertsPerPoly;
			int[] firstEdge = new int[vertices.Count + maxEdgeCount];
			int nextEdge = vertices.Count;
			List<AdjacencyEdge> edges = new List<AdjacencyEdge>(maxEdgeCount);

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
						firstEdge[nextEdge + edges.Count] = firstEdge[v0];
						firstEdge[v0] = edges.Count;

						edges.Add(edge);
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
			for (int i = 0; i < edges.Count; i++)
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

		/// <summary>
		/// Removing vertices will leave holes that have to be triangulated again.
		/// </summary>
		/// <param name="verts">A list of vertices</param>
		/// <param name="polys">A list of polygons</param>
		/// <param name="vertex">The vertex to remove</param>
		private void RemoveVertex(List<PolyVertex> verts, List<Polygon> polys, int vertex)
		{
			int[] mergeTemp = new int[numVertsPerPoly];

			//count number of polygons to remove
			int numRemovedVerts = 0;
			for (int i = 0; i < polys.Count; i++)
			{
				Polygon p = polys[i];

				for (int j = 0; j < p.VertexCount; j++)
				{
					if (p.Vertices[j] == vertex)
						numRemovedVerts++;
				}
			}

			List<Edge> edges = new List<Edge>(numRemovedVerts * numVertsPerPoly);
			List<int> hole = new List<int>(numRemovedVerts * numVertsPerPoly);
			List<RegionId> regions = new List<RegionId>(numRemovedVerts * numVertsPerPoly);
			List<Area> areas = new List<Area>(numRemovedVerts * numVertsPerPoly);

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

				for (int j = 0; j < p.VertexCount; j++)
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

			var tris = new Triangle[hole.Count];
			var tverts = new PolyVertex[hole.Count];
			var thole = new int[hole.Count];

			//generate temp vertex array for triangulation
			for (int i = 0; i < hole.Count; i++)
			{
				int polyIndex = hole[i];
				tverts[i] = verts[polyIndex];
				thole[i] = i;
			}

			//triangulate the hole
			int ntris = Triangulate(hole.Count, tverts, thole, tris);
			if (ntris < 0)
				ntris = -ntris;

			//merge hole triangles back to polygons
			List<Polygon> mergePolys = new List<Polygon>(ntris + 1);

			for (int j = 0; j < ntris; j++)
			{
				Triangle t = tris[j];
				if (t.Index0 != t.Index1 && t.Index0 != t.Index2 && t.Index1 != t.Index2)
				{
					Polygon p = new Polygon(numVertsPerPoly, areas[t.Index0], regions[t.Index0]);
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

					if (bestMergeVal <= 0)
						break;

					Polygon pa = mergePolys[bestPolyA];
					Polygon pb = mergePolys[bestPolyB];
					pa.MergeWith(pb, bestEa, bestEb, mergeTemp);
					mergePolys[bestPolyB] = mergePolys[mergePolys.Count - 1];
					mergePolys.RemoveAt(mergePolys.Count - 1);
				}
			}

			//add merged polys back to the list.
			polys.AddRange(mergePolys);
		}

		private static bool DiagonalLoose(int i, int j, PolyVertex[] verts, int[] indices)
		{
			return InConeLoose(i, j, verts, indices) && DiagonalieLoose(i, j, verts, indices);
		}

		private static bool InConeLoose(int i, int j, PolyVertex[] verts, int[] indices)
		{
			int pi = RemoveDiagonalFlag(indices[i]);
			int pj = RemoveDiagonalFlag(indices[j]);
			int pi1 = RemoveDiagonalFlag(indices[Next(i, verts.Length)]);
			int pin1 = RemoveDiagonalFlag(indices[Prev(i, verts.Length)]);

			if (PolyVertex.IsLeftOn(ref verts[pin1], ref verts[pi], ref verts[pi1]))
				return PolyVertex.IsLeftOn(ref verts[pi], ref verts[pj], ref verts[pin1])
					&& PolyVertex.IsLeftOn(ref verts[pj], ref verts[pi], ref verts[pi1]);

			return !(PolyVertex.IsLeftOn(ref verts[pi], ref verts[pj], ref verts[pi1])
				&& PolyVertex.IsLeftOn(ref verts[pj], ref verts[pi], ref verts[pin1]));
		}

		private static bool DiagonalieLoose(int i, int j, PolyVertex[] verts, int[] indices)
		{
			int d0 = RemoveDiagonalFlag(indices[i]);
			int d1 = RemoveDiagonalFlag(indices[j]);

			for (int k = 0; k < verts.Length; k++)
			{
				int k1 = Next(k, verts.Length);
				if (!((k == i) || (k1 == i) || (k == j) || (k1 == j)))
				{
					int p0 = RemoveDiagonalFlag(indices[k]);
					int p1 = RemoveDiagonalFlag(indices[k1]);

					if (PolyVertex.Equal2D(ref verts[d0], ref verts[p0]) ||
						PolyVertex.Equal2D(ref verts[d1], ref verts[p0]) ||
						PolyVertex.Equal2D(ref verts[d0], ref verts[p1]) ||
						PolyVertex.Equal2D(ref verts[d1], ref verts[p1]))
						continue;

					if (PolyVertex.IntersectProp(ref verts[d0], ref verts[d1], ref verts[p0], ref verts[p1]))
						return false;
				}
			}

			return true;
		}

		/// <summary>
		/// A triangle contains three indices.
		/// </summary>
		private struct Triangle
		{
			public int Index0;
			public int Index1;
			public int Index2;
		}

		/// <summary>
		/// Two adjacent vertices form an edge.
		/// </summary>
		private struct AdjacencyEdge
		{
			public int Vert0;
			public int Vert1;

			public int PolyEdge0;
			public int PolyEdge1;

			public int Poly0;
			public int Poly1;
		}

		/// <summary>
		/// Another edge structure, but this one contains the RegionId and AreaId.
		/// </summary>
		private struct Edge
		{
			public int Vert0;
			public int Vert1;
			public RegionId Region;
			public Area Area;

			/// <summary>
			/// Initializes a new instance of the <see cref="Edge"/> struct.
			/// </summary>
			/// <param name="vert0">Vertex A</param>
			/// <param name="vert1">Vertex B</param>
			/// <param name="region">Region id</param>
			/// <param name="area">Area id</param>
			public Edge(int vert0, int vert1, RegionId region, Area area)
			{
				Vert0 = vert0;
				Vert1 = vert1;
				Region = region;
				Area = area;
			}
		}
			
		/// <summary>
		/// Each polygon is a collection of vertices. It is the basic unit of the PolyMesh
		/// </summary>
		public class Polygon
		{
			private int[] vertices; //"numVertsPerPoly" elements
			private int[] neighborEdges; //"numVertsPerPoly" elements
			private Area area;
			private RegionId regionId;

			/// <summary>
			/// Initializes a new instance of the <see cref="Polygon" /> class.
			/// </summary>
			/// <param name="numVertsPerPoly">The number of vertices per polygon.</param>
			/// <param name="area">The AreaId</param>
			/// <param name="regionId">The RegionId</param>
			public Polygon(int numVertsPerPoly, Area area, RegionId regionId)
			{
				vertices = new int[numVertsPerPoly];
				neighborEdges = new int[numVertsPerPoly];
				this.area = area;
				this.regionId = regionId;

				for (int i = 0; i < numVertsPerPoly; i++)
				{
					vertices[i] = NullId;
					neighborEdges[i] = NullId;
				}
			}

			/// <summary>
			/// Gets the indices for the vertices.
			/// </summary>
			/// <value>The vertices.</value>
			public int[] Vertices
			{
				get
				{
					return vertices;
				}
			}

			/// <summary>
			/// Gets the neighbor edges.
			/// </summary>
			/// <value>The neighbor edges.</value>
			public int[] NeighborEdges
			{
				get
				{
					return neighborEdges;
				}
			}

			/// <summary>
			/// Gets or sets the area id
			/// </summary>
			public Area Area
			{
				get
				{
					return area;
				}

				set
				{
					area = value;
				}
			}

			/// <summary>
			/// Gets or sets the region identifier.
			/// </summary>
			/// <value>The region identifier.</value>
			public RegionId RegionId
			{
				get
				{
					return regionId;
				}

				set
				{
					regionId = value;
				}
			}

			/// <summary>
			/// Gets or sets a tag for this instance.
			/// </summary>
			/// <value>Any object to tag this instance with.</value>
			public object Tag { get; set; }

			/// <summary>
			/// Gets the the number of vertex.
			/// </summary>
			/// <value>The vertex count.</value>
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

			/// <summary>
			/// Determine if the vertex is in polygon.
			/// </summary>
			/// <returns><c>true</c>, if vertex was containsed, <c>false</c> otherwise.</returns>
			/// <param name="vertex">The Vertex.</param>
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

			/// <summary>
			/// Merges another polygon with this one.
			/// </summary>
			/// <param name="other">The other polygon to merge into this one.</param>
			/// <param name="startEdge">This starting edge for this polygon.</param>
			/// <param name="otherStartEdge">The starting edge for the other polygon.</param>
			/// <param name="temp">A temporary vertex buffer. Must be at least <c>numVertsPerPoly</c> long.</param>
			public void MergeWith(Polygon other, int startEdge, int otherStartEdge, int[] temp)
			{
				if (temp.Length < vertices.Length)
					throw new ArgumentException("Buffer not large enough. Must be at least numVertsPerPoly (" + vertices.Length + ")", "temp");

				int thisCount = this.VertexCount;
				int otherCount = other.VertexCount;

				int n = 0;

				for (int t = 0; t < temp.Length; t++)
					temp[t] = NullId;

				//add self, starting at best edge
				for (int i = 0; i < thisCount - 1; i++)
					temp[n++] = this.Vertices[(startEdge + 1 + i) % thisCount];

				//add other polygon
				for (int i = 0; i < otherCount - 1; i++)
					temp[n++] = other.Vertices[(otherStartEdge + 1 + i) % otherCount];

				//save merged data back
				Array.Copy(temp, this.vertices, this.vertices.Length);
			}
		}
	}
}
