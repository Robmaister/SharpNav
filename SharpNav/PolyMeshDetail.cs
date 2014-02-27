#region License
/**
 * Copyright (c) 2013-2014 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
 * Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE
 */
#endregion

using System;
using System.Collections.Generic;

using SharpNav.Collections.Generic;
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
	public class PolyMeshDetail
	{
		//9 x 2
		private static readonly int[] VertexOffset =
		{
			0, 0,
			-1, -1,
			0, -1,
			1, -1,
			1, 0,
			1, 1,
			0, 1,
			-1, 1,
			-1, 0
		};
		
		private MeshData[] meshes;
		private Vector3[] verts;
		private TriangleData[] tris;

		/// <summary>
		/// Use the CompactHeightfield data to add the height detail to the mesh. 
		/// Triangulate the added detail to form a complete navigation mesh.
		/// </summary>
		/// <param name="mesh">Basic mesh</param>
		/// <param name="openField">Compact heightfield data</param>
		/// <param name="sampleDist">Sampling distance</param>
		/// <param name="sampleMaxError">Maximum sampling error allowed</param>
		public PolyMeshDetail(PolyMesh mesh, CompactHeightfield openField, float sampleDist, float sampleMaxError)
		{
			if (mesh.VertCount == 0 || mesh.PolyCount == 0)
				return;

			Vector3 origin = mesh.Bounds.Min;

			int maxhw = 0, maxhh = 0;

			BBox3[] bounds = new BBox3[mesh.PolyCount];
			Vector3[] poly = new Vector3[mesh.NumVertsPerPoly];

			List<Vector3> storedVertices = new List<Vector3>();
			List<TriangleData> storedTriangles = new List<TriangleData>();

			//find max size for polygon area
			for (int i = 0; i < mesh.PolyCount; i++)
			{
				float xmin, xmax, zmin, zmax;

				xmin = bounds[i].Min.X = openField.Width; 
				xmax = bounds[i].Max.X = 0;
				zmin = bounds[i].Min.Z = openField.Length;
				zmax = bounds[i].Max.Z = 0;

				for (int j = 0; j < mesh.NumVertsPerPoly; j++)
				{
					if (mesh.Polys[i].Vertices[j] == PolyMesh.NullId)
						break;

					int v = mesh.Polys[i].Vertices[j];

					xmin = bounds[i].Min.X = Math.Min(xmin, mesh.Verts[v].X);
					xmax = bounds[i].Max.X = Math.Max(xmax, mesh.Verts[v].X);
					zmin = bounds[i].Min.Z = Math.Min(zmin, mesh.Verts[v].Z);
					zmax = bounds[i].Max.Z = Math.Max(zmax, mesh.Verts[v].Z);
				}

				xmin = bounds[i].Min.X = Math.Max(0, xmin - 1);
				xmax = bounds[i].Max.X = Math.Min(openField.Width, xmax + 1);
				zmin = bounds[i].Min.Z = Math.Max(0, zmin - 1);
				zmax = bounds[i].Max.Z = Math.Min(openField.Length, zmax + 1);

				if (xmin >= xmax || zmin >= zmax)
					continue;

				maxhw = (int)Math.Max(maxhw, xmax - xmin);
				maxhh = (int)Math.Max(maxhh, zmax - zmin);
			}

			HeightPatch hp = new HeightPatch(0, 0, maxhw, maxhh);

			this.meshes = new MeshData[mesh.PolyCount];

			for (int i = 0; i < mesh.PolyCount; i++)
			{
				//store polygon vertices for processing
				int npoly = 0;
				for (int j = 0; j < mesh.NumVertsPerPoly; j++)
				{
					if (mesh.Polys[i].Vertices[j] == PolyMesh.NullId)
						break;

					int v = mesh.Polys[i].Vertices[j];
					poly[j].X = mesh.Verts[v].X * mesh.CellSize;
					poly[j].Y = mesh.Verts[v].Y * mesh.CellHeight;
					poly[j].Z = mesh.Verts[v].Z * mesh.CellSize;
					npoly++;
				}

				//get height data from area of polygon
				hp.Resize((int)bounds[i].Min.X, (int)bounds[i].Min.Z, (int)(bounds[i].Max.X - bounds[i].Min.X), (int)(bounds[i].Max.Z - bounds[i].Min.Z));
				GetHeightData(openField, mesh.Polys[i], npoly, mesh.Verts, mesh.BorderSize, hp);

				List<Vector3> tempVerts = new List<Vector3>();
				List<TriangleData> tempTris = new List<TriangleData>(128);
				List<EdgeInfo> edges = new List<EdgeInfo>(16);
				List<SamplingData> samples = new List<SamplingData>(128);
				BuildPolyDetail(poly, npoly, sampleDist, sampleMaxError, openField, hp, tempVerts, tempTris, edges, samples);

				//more detail verts
				for (int j = 0; j < tempVerts.Count; j++)
				{
					Vector3 tempVert = new Vector3();
					tempVert.X = tempVerts[j].X + origin.X;
					tempVert.Y = tempVerts[j].Y + origin.Y + openField.CellHeight;
					tempVert.Z = tempVerts[j].Z + origin.Z;
					tempVerts[j] = tempVert;
				}

				for (int j = 0; j < npoly; j++)
				{
					poly[j].X += origin.X;
					poly[j].Y += origin.Y;
					poly[j].Z += origin.Z;
				}

				//save data
				this.meshes[i].VertexIndex = storedVertices.Count;
				this.meshes[i].VertexCount = tempVerts.Count;
				this.meshes[i].TriangleIndex = storedTriangles.Count;
				this.meshes[i].TriangleCount = tempTris.Count;

				//store vertices
				storedVertices.AddRange(tempVerts);
				
				//store triangles
				for (int j = 0; j < tempTris.Count; j++)
				{
					storedTriangles.Add(new TriangleData(tempTris[j], tempVerts, poly));
				}
			}

			this.verts = storedVertices.ToArray();
			this.tris = storedTriangles.ToArray();
		}

		public int MeshCount
		{
			get
			{
				return meshes.Length;
			}
		}

		public int VertCount
		{
			get
			{
				return verts.Length;
			}
		}

		public int TrisCount
		{
			get
			{
				return tris.Length;
			}
		}

		public MeshData[] Meshes
		{
			get
			{
				return meshes;
			}
		}

		public Vector3[] Verts
		{
			get
			{
				return verts;
			}
		}

		public TriangleData[] Tris
		{
			get
			{
				return tris;
			}
		}

		/// <summary>
		/// Determine whether an edge of the triangle is part of the polygon (1 if true, 0 if false)
		/// </summary>
		/// <param name="verts">Vertices containing triangles</param>
		/// <param name="va">Triangle vertex A</param>
		/// <param name="vb">Triangle vertex B</param>
		/// <param name="vpoly">Polygon vertex data</param>
		/// <param name="npoly">Number of polygons</param>
		/// <returns></returns>
		private static int GetEdgeFlags(Vector3 va, Vector3 vb, Vector3[] vpoly)
		{
			//true if edge is part of polygon
			float thrSqr = 0.001f * 0.001f;

			for (int i = 0, j = vpoly.Length - 1; i < vpoly.Length; j = i++)
			{
				Vector3 pt1 = va;
				Vector3 pt2 = vb;

				//the vertices pt1 (va) and pt2 (vb) are extremely close to the polygon edge
				if (MathHelper.Distance.PointToSegment2DSquared(ref pt1, ref vpoly[j], ref vpoly[i]) < thrSqr 
					&& MathHelper.Distance.PointToSegment2DSquared(ref pt2, ref vpoly[j], ref vpoly[i]) < thrSqr)
					return 1;
			}

			return 0;
		}

		/// <summary>
		/// Floodfill heightfield to get 2D height data, starting at vertex locations
		/// </summary>
		/// <param name="compactField">Original heightfield data</param>
		/// <param name="poly">Polygon in PolyMesh</param>
		/// <param name="numVertsPerPoly">Number of vertices per polygon</param>
		/// <param name="verts">PolyMesh Vertices</param>
		/// <param name="borderSize">Heightfield border size</param>
		/// <param name="hp">HeightPatch which extracts heightfield data</param>
		private void GetHeightData(CompactHeightfield compactField, PolyMesh.Polygon poly, int numVertsPerPoly, Vector3[] verts, int borderSize, HeightPatch hp)
		{
			var stack = new Stack<CompactSpanReference>();

			//use poly vertices as seed points
			for (int j = 0; j < numVertsPerPoly; j++)
			{
				int cx = 0, cz = 0, ci = -1;
				int dmin = int.MaxValue;

				for (int k = 0; k < 9; k++)
				{
					//get vertices and offset x and z coordinates depending on current drection
					int ax = (int)verts[poly.Vertices[j]].X + VertexOffset[k * 2 + 0];
					int ay = (int)verts[poly.Vertices[j]].Y;
					int az = (int)verts[poly.Vertices[j]].Z + VertexOffset[k * 2 + 1];

					//skip if out of bounds
					if (ax < hp.X || ax >= hp.X + hp.Width ||
						az < hp.Y || az >= hp.Y + hp.Height)
						continue;

					//get new cell
					CompactCell c = compactField.Cells[(ax + borderSize) + (az + borderSize) * compactField.Width];
					
					//loop through all the spans
					for (int i = c.StartIndex, end = c.StartIndex + c.Count; i < end; i++)
					{
						CompactSpan s = compactField.Spans[i];
						
						//find minimum y-distance
						int d = Math.Abs(ay - s.Minimum);
						if (d < dmin)
						{
							cx = ax;
							cz = az;
							ci = i;

							dmin = d;
						}
					}
				}

				//only add if something new found
				if (ci != -1)
				{
					stack.Push(new CompactSpanReference(cx, cz, ci));
				}
			}

			//find center of polygon using flood fill
			int pcx = 0, pcz = 0;
			for (int j = 0; j < numVertsPerPoly; j++)
			{
				pcx += (int)verts[poly.Vertices[j]].X;
				pcz += (int)verts[poly.Vertices[j]].Z;
			}

			pcx /= numVertsPerPoly;
			pcz /= numVertsPerPoly;

			//stack groups 3 elements as one part
			foreach (var cell in stack)
			{
				int idx = cell.X - hp.X + (cell.Y - hp.Y) * hp.Width;
				hp[idx] = 1;
			}

			//process the entire stack
			while (stack.Count > 0)
			{
				//since we add cx, cy, ci to stack, cx is at bottom and ci is at top
				//so the order we remove items is the opposite of the order we insert items
				var cell = stack.Pop();
				int ci = cell.Index;
				int cy = cell.Y;
				int cx = cell.X;

				//check if close to center of polygon
				if (Math.Abs(cx - pcx) <= 1 && Math.Abs(cy - pcz) <= 1)
				{
					//clear the stack and add a new group
					stack.Clear();

					stack.Push(new CompactSpanReference(cx, cy, ci));
					break;
				}

				CompactSpan cs = compactField.Spans[ci];

				//check all four directions
				for (var dir = Direction.West; dir <= Direction.South; dir++)
				{
					//skip if disconnected
					if (!cs.IsConnected(dir))
						continue;

					//get neighbor
					int ax = cx + dir.GetHorizontalOffset();
					int ay = cy + dir.GetVerticalOffset();

					//skip if out of bounds
					if (ax < hp.X || ax >= (hp.X + hp.Width) ||
						ay < hp.Y || ay >= (hp.Y + hp.Height))
						continue;

					if (hp[ax - hp.X + (ay - hp.Y) * hp.Width] != 0)
						continue;

					//get the new index
					int ai = compactField.Cells[(ax + borderSize) + (ay + borderSize) * compactField.Width].StartIndex +
						CompactSpan.GetConnection(ref cs, dir);

					//save data
					int idx = ax - hp.X + (ay - hp.Y) * hp.Width;
					hp[idx] = 1;

					//push to stack
					stack.Push(new CompactSpanReference(ax, ay, ai));
				}
			}

			//initialize to some default value 
			hp.Clear();

			//mark start locations
			foreach (var cell in stack)
			{
				//set new heightpatch data
				int idx = cell.X - hp.X + (cell.Y - hp.Y) * hp.Width;
				CompactSpan cs = compactField.Spans[cell.Index];
				hp[idx] = cs.Minimum;
			}

			BufferedStack<CompactSpanReference> bufferedStack = new BufferedStack<CompactSpanReference>(256, stack);
			int head = 0;

			while (head < bufferedStack.Count)
			{
				var cell = bufferedStack[head];
				int cx = cell.X;
				int cy = cell.Y;
				int ci = cell.Index;
				head++;

				//examine span
				CompactSpan cs = compactField.Spans[ci];
				
				//loop in all four directions
				for (var dir = Direction.West; dir <= Direction.South; dir++)
				{
					//skip
					if (!cs.IsConnected(dir))
						continue;

					int ax = cx + dir.GetHorizontalOffset();
					int ay = cy + dir.GetVerticalOffset();

					if (ax < hp.X || ax >= (hp.X + hp.Width) ||
						ay < hp.Y || ay >= (hp.Y + hp.Height))
						continue;

					//only continue if height is unset
					if (hp.IsSet(ax - hp.X + (ay - hp.Y) * hp.Width))
						continue;

					//get new span index
					int ai = compactField.Cells[(ax + borderSize) + (ay + borderSize) * compactField.Width].StartIndex +
						CompactSpan.GetConnection(ref cs, dir);

					//get new span
					CompactSpan ds = compactField.Spans[ai];
					
					//save
					int idx = ax - hp.X + (ay - hp.Y) * hp.Width;
					hp[idx] = ds.Minimum;

					//add grouping to stack, adjust head if buffer resets
					if (bufferedStack.Push(new CompactSpanReference(ax, ay, ai)))
						head = 0;
				}
			}
		}

		/// <summary>
		/// Generate the PolyMeshDetail using the PolyMesh and HeightPatch
		/// </summary>
		/// <param name="polyMeshVerts">PolyMesh Vertex data</param>
		/// <param name="numMeshVerts">Number of PolyMesh vertices</param>
		/// <param name="sampleDist">Sampling distance</param>
		/// <param name="sampleMaxError">Maximum sampling error</param>
		/// <param name="openField">CompactHeightfield</param>
		/// <param name="hp">HeightPatch</param>
		/// <param name="verts">Detail verts</param>
		/// <param name="tris">Detail triangles</param>
		/// <param name="edges">Edges</param>
		/// <param name="samples">Samples</param>
		private void BuildPolyDetail(Vector3[] polyMeshVerts, int numMeshVerts, float sampleDist, float sampleMaxError, CompactHeightfield openField, HeightPatch hp, List<Vector3> verts, List<TriangleData> tris, List<EdgeInfo> edges, List<SamplingData> samples)
		{
			const int MAX_VERTS = 127;
			const int MAX_TRIS = 255;
			const int MAX_VERTS_PER_EDGE = 32;
			Vector3[] edge = new Vector3[MAX_VERTS_PER_EDGE + 1];
			List<int> hull = new List<int>(MAX_VERTS);

			//fill up vertex array
			for (int i = 0; i < numMeshVerts; ++i)
			{
				verts.Add(polyMeshVerts[i]);
			}

			float cs = openField.CellSize;
			float ics = 1.0f / cs;

			//tessellate outlines
			if (sampleDist > 0)
			{
				for (int i = 0, j = numMeshVerts - 1; i < numMeshVerts; j = i++)
				{
					int vj = j;
					int vi = i;
					bool swapped = false;

					//make sure order is correct, otherwise swap data
					if (Math.Abs(polyMeshVerts[vj].X - polyMeshVerts[vi].X) < 1E-06f)
					{
						if (polyMeshVerts[vj].Z > polyMeshVerts[vi].Z)
						{
							float temp = polyMeshVerts[vj].Z;
							polyMeshVerts[vj].Z = polyMeshVerts[vi].Z;
							polyMeshVerts[vi].Z = temp;
							swapped = true;
						}
					}
					else if (polyMeshVerts[vj].X > polyMeshVerts[vi].X)
					{
						float temp = polyMeshVerts[vj].X;
						polyMeshVerts[vj].X = polyMeshVerts[vi].X;
						polyMeshVerts[vi].X = temp;
						swapped = true;
					}

					//create samples along the edge
					float dx = polyMeshVerts[vi].X - polyMeshVerts[vj].X;
					float dy = polyMeshVerts[vi].Y - polyMeshVerts[vj].Y;
					float dz = polyMeshVerts[vi].Z - polyMeshVerts[vj].Z;
					float d = (float)Math.Sqrt(dx * dx + dz * dz);
					int nn = 1 + (int)Math.Floor(d / sampleDist);
					if (verts.Count + nn >= MAX_VERTS)
						nn = MAX_VERTS - 1 - verts.Count;

					for (int k = 0; k <= nn; k++)
					{
						float u = (float)k / (float)nn;
						int pos = k;
						
						//edge seems to store vertex data
						edge[pos].X = polyMeshVerts[vj].X + dx * u;
						edge[pos].Y = polyMeshVerts[vj].Y + dy * u;
						edge[pos].Z = polyMeshVerts[vj].Z + dz * u;

						edge[pos].Y = GetHeight(edge[pos], ics, openField.CellHeight, hp) * openField.CellHeight;
					}

					//simplify samples
					int[] idx = new int[MAX_VERTS_PER_EDGE];
					idx[0] = 0;
					idx[1] = nn;
					int nidx = 2;

					for (int k = 0; k < nidx - 1;)
					{
						int a = idx[k];
						int b = idx[k + 1];
						int va = a;
						int vb = b;

						//find maximum deviation along segment
						float maxd = 0;
						int maxi = 0;
						for (int m = a + 1; m < b; m++)
						{
							float dev = MathHelper.Distance.PointToSegmentSquared(ref edge[m], ref edge[va], ref edge[vb]);
							if (dev > maxd)
							{
								maxd = dev;
								maxi = m;
							}
						}

						if (maxi != -1 && maxd > (sampleMaxError * sampleMaxError))
						{
							//shift data to the right
							for (int m = nidx; m > k; m--)
								idx[m] = idx[m - 1];

							//set new value
							idx[k + 1] = maxi;
							nidx++;
						}
						else
						{
							k++;
						}
					}

					hull.Add(j);

					//add new vertices
					if (swapped)
					{
						for (int k = nidx - 2; k > 0; k--)
						{
							hull.Add(verts.Count);
							verts.Add(edge[idx[k]]);
						}
					}
					else
					{
						for (int k = 1; k < nidx - 1; k++)
						{
							hull.Add(verts.Count);
							verts.Add(edge[idx[k]]);
						}
					}
				}
			}

			//tesselate base mesh
			edges.Clear();
			tris.Clear();
			
			DelaunayHull(verts, hull, tris, edges);

			if (tris.Count == 0)
			{
				Console.WriteLine("Can't triangulate polygon, adding default data.");
				//add default data
				for (int i = 2; i < verts.Count; i++)
					tris.Add(new TriangleData(0, i - 1, i));

				return;
			}

			if (sampleDist > 0)
			{
				//create sample locations
				BBox3 bounds = new BBox3();
				bounds.Min = polyMeshVerts[0];
				bounds.Max = polyMeshVerts[0];

				for (int i = 1; i < numMeshVerts; i++)
				{
					Vector3Extensions.ComponentMin(ref bounds.Min, ref polyMeshVerts[i], out bounds.Min);
					Vector3Extensions.ComponentMax(ref bounds.Max, ref polyMeshVerts[i], out bounds.Max); 
				}

				int x0 = (int)Math.Floor(bounds.Min.X / sampleDist);
				int x1 = (int)Math.Ceiling(bounds.Max.X / sampleDist);
				int z0 = (int)Math.Floor(bounds.Min.Z / sampleDist);
				int z1 = (int)Math.Ceiling(bounds.Max.Z / sampleDist);

				samples.Clear();

				for (int z = z0; z < z1; z++)
				{
					for (int x = x0; x < x1; x++)
					{
						Vector3 pt = new Vector3(x * sampleDist, (bounds.Max.Y + bounds.Min.Y) * 0.5f, z * sampleDist);

						//make sure samples aren't too close to edge
						if (MathHelper.Distance.PointToPolygonEdgeSquared(pt, polyMeshVerts, numMeshVerts) > -sampleDist / 2)
							continue;

						SamplingData sd = new SamplingData(x, GetHeight(pt, ics, openField.CellHeight, hp), z, false);
						samples.Add(sd);
					}
				}

				//added samples
				for (int iter = 0; iter < samples.Count; iter++)
				{
					if (verts.Count >= MAX_VERTS)
						break;

					//find sample with most error
					Vector3 bestPt = new Vector3();
					float bestDistance = 0;
					int bestIndex = -1;

					for (int i = 0; i < samples.Count; i++)
					{
						if (samples[i].IsSampled)
							continue;

						Vector3 pt = new Vector3();

						//jitter sample location to remove effects of bad triangulation
						pt.X = samples[i].X * sampleDist + GetJitterX(i) * openField.CellSize * 0.1f;
						pt.Y = samples[i].Y * openField.CellHeight;
						pt.Z = samples[i].Z * sampleDist + GetJitterY(i) * openField.CellSize * 0.1f;
						float d = DistanceToTriMesh(pt, verts.ToArray(), tris);

						if (d < 0)
							continue;

						if (d > bestDistance)
						{
							bestDistance = d;
							bestIndex = i;
							bestPt = pt;
						}
					}

					if (bestDistance <= sampleMaxError || bestIndex == -1)
						break;

					SamplingData sd = samples[bestIndex];
					sd.IsSampled = true;
					samples[bestIndex] = sd;

					verts.Add(bestPt);

					//create new triangulation
					edges.Clear();
					tris.Clear();
					DelaunayHull(verts, hull, tris, edges);
				}
			}

			int ntris = tris.Count;
			if (ntris > MAX_TRIS)
			{
				tris.RemoveRange(MAX_TRIS + 1, tris.Count - MAX_TRIS);
			}

			return;
		}

		#region Black Magic

		private float GetJitterX(int i)
		{
			return (((i * 0x8da6b343) & 0xffff) / 65535.0f * 2.0f) - 1.0f;
		}

		private float GetJitterY(int i)
		{
			return (((i * 0xd8163841) & 0xffff) / 65535.0f * 2.0f) - 1.0f;
		}

		#endregion

		/// <summary>
		/// Use the HeightPatch data to obtain a height for a certain location.
		/// </summary>
		/// <param name="loc">Location</param>
		/// <param name="invCellSize">Reciprocal of cell size</param>
		/// <param name="cellHeight">Cell height</param>
		/// <param name="hp">Height patch</param>
		private int GetHeight(Vector3 loc, float invCellSize, float cellHeight, HeightPatch hp)
		{
			int ix = (int)Math.Floor(loc.X * invCellSize + 0.01f);
			int iz = (int)Math.Floor(loc.Z * invCellSize + 0.01f);
			ix = MathHelper.Clamp(ix - hp.X, 0, hp.Width - 1);
			iz = MathHelper.Clamp(iz - hp.Y, 0, hp.Height - 1);
			int h = hp[ix, iz];

			if (h == HeightPatch.UnsetHeight)
			{
				//go in counterclockwise direction starting from west, ending in northwest
				int[] off =
				{
					-1,  0,
					-1, -1,
					 0, -1,
					 1, -1,
					 1,  0,
					 1,  1,
					 0,  1,
					-1,  1
				};

				float dmin = float.MaxValue;

				for (int i = 0; i < 8; i++)
				{
					int nx = ix + off[i * 2 + 0];
					int nz = iz + off[i * 2 + 1];

					if (nx < 0 || nz < 0 || nx >= hp.Width || nz >= hp.Height)
						continue;

					int nh = hp[nx, nz];
					if (nh == HeightPatch.UnsetHeight)
						continue;

					float d = Math.Abs(nh * cellHeight - loc.Y);
					if (d < dmin)
					{
						h = nh;
						dmin = d;
					}
				}
			}

			return h;
		}

		/// <summary>
		/// Delaunay triangulation is used to triangulate the polygon after adding detail to the edges. The result is a mesh. 
		/// 
		/// The definition of Delaunay traingulation:
		/// "For a set S of points in the Euclidean plane, the unique triangulation DT(S) of S such that no point in S 
		/// is inside the circumcircle of any triangle in DT(S)." (Dictionary.com)
		/// </summary>
		/// <param name="pts">Vertex data (each vertex has 3 elements x,y,z)</param>
		/// <param name="hull">?</param>
		/// <param name="tris">The triangles formed.</param>
		/// <param name="edges">The edge connections formed.</param>
		private void DelaunayHull(List<Vector3> pts, List<int> hull, List<TriangleData> tris, List<EdgeInfo> edges)
		{
			int nfaces = 0;
			edges.Clear();

			for (int i = 0, j = hull.Count - 1; i < hull.Count; j = i++)
				AddEdge(edges, hull[j], hull[i], (int)EdgeValues.Hull, (int)EdgeValues.Undefined);

			int currentEdge = 0;
			while (currentEdge < edges.Count)
			{
				if (edges[currentEdge].LeftFace == (int)EdgeValues.Undefined)
					CompleteFacet(pts, edges, ref nfaces, currentEdge);
				
				if (edges[currentEdge].RightFace == (int)EdgeValues.Undefined)
					CompleteFacet(pts, edges, ref nfaces, currentEdge);
				
				currentEdge++;
			}

			//create triangles
			tris.Clear();
			for (int i = 0; i < nfaces; i++)
				tris.Add(new TriangleData(-1, -1, -1, -1));

			for (int i = 0; i < edges.Count; i++)
			{
				EdgeInfo e = edges[i];

				if (e.RightFace >= 0)
				{
					//left face
					var tri = tris[e.RightFace];
					
					if (tri.VertexHash0 == -1)
					{
						tri.VertexHash0 = e.EndPt0;
						tri.VertexHash1 = e.EndPt1;
					}
					else if (tri.VertexHash0 == e.EndPt1)
					{
						tri.VertexHash2 = e.EndPt0;
					}
					else if (tri.VertexHash1 == e.EndPt0)
					{
						tri.VertexHash2 = e.EndPt1;
					}

					tris[e.RightFace] = tri;
				}

				if (e.LeftFace >= 0)
				{
					//right
					var tri = tris[e.LeftFace];
					
					if (tri.VertexHash0 == -1)
					{
						tri.VertexHash0 = e.EndPt1;
						tri.VertexHash1 = e.EndPt0;
					}
					else if (tri.VertexHash0 == e.EndPt0)
					{
						tri.VertexHash2 = e.EndPt1;
					}
					else if (tri.VertexHash1 == e.EndPt1)
					{
						tri.VertexHash2 = e.EndPt0;
					}

					tris[e.LeftFace] = tri;
				}
			}

			for (int i = 0; i < tris.Count; i++)
			{
				var t = tris[i];
				if (t.VertexHash0 == -1 || t.VertexHash1 == -1 || t.VertexHash2 == -1)
				{
					//remove dangling face
					tris[i] = tris[tris.Count - 1];
					tris.RemoveAt(tris.Count - 1);
					--i;
				}
			}
		}

		private void CompleteFacet(List<Vector3> pts, List<EdgeInfo> edges, ref int nfaces, int curEdge)
		{
			const float EPS = 1e-5f;

			EdgeInfo e = edges[curEdge];

			//cache s and t
			int s, t;
			if (e.LeftFace == (int)EdgeValues.Undefined)
			{
				s = e.EndPt0;
				t = e.EndPt1;
			}
			else if (e.RightFace == (int)EdgeValues.Undefined)
			{
				s = e.EndPt1;
				t = e.EndPt0;
			}
			else
			{
				//edge already completed
				return;
			}

			//find best point on left edge
			int pt = pts.Count;
			Vector3 c = Vector3.Zero;
			float r = -1;
			float cross;
			for (int u = 0; u < pts.Count; u++)
			{
				if (u == s || u == t)
					continue;

				cross = Vector3Extensions.Cross2D(pts[s], pts[t], pts[u]);
				if (cross > EPS)
				{
					if (r < 0)
					{
						//update circle now
						pt = u;
						CircumCircle(pts[s], pts[t], pts[u], ref c, out r);
						continue;
					}

					float d = Vector3Extensions.Distance2D(c, pts[u]);
					float tol = 0.001f;

					if (d > r * (1 + tol))
					{
						//outside circumcircle
						continue;
					}
					else if (d < r * (1 - tol))
					{
						//inside circumcircle, update
						pt = u;
						CircumCircle(pts[s], pts[t], pts[u], ref c, out r);
					}
					else
					{
						//inside epsilon circumcircle
						if (OverlapEdges(pts, edges, s, u))
							continue;

						if (OverlapEdges(pts, edges, t, u))
							continue;

						//edge is valid
						pt = u;
						CircumCircle(pts[s], pts[t], pts[u], ref c, out r);
					}
				}
			}

			//add new triangle or update edge if s-t on hull
			if (pt < pts.Count)
			{
				EdgeInfo.UpdateLeftFace(ref e, s, t, nfaces);
				edges[curEdge] = e;

				curEdge = FindEdge(edges, pt, s);
				if (curEdge == (int)EdgeValues.Undefined)
					AddEdge(edges, pt, s, nfaces, (int)EdgeValues.Undefined);
				else
				{
					e = edges[curEdge];
					EdgeInfo.UpdateLeftFace(ref e, pt, s, nfaces);
					edges[curEdge] = e;
				}

				curEdge = FindEdge(edges, t, pt);
				if (curEdge == (int)EdgeValues.Undefined)
					AddEdge(edges, t, pt, nfaces, (int)EdgeValues.Undefined);
				else
				{
					e = edges[curEdge];
					EdgeInfo.UpdateLeftFace(ref e, t, pt, nfaces);
					edges[curEdge] = e;
				}

				nfaces++;
			}
			else
			{
				EdgeInfo.UpdateLeftFace(ref e, s, t, (int)EdgeValues.Hull);
				edges[curEdge] = e;
			}
		}

		/// <summary>
		/// Add an edge to the edge list if it hasn't been done so already
		/// </summary>
		/// <param name="edges">Edge list</param>
		/// <param name="s">Endpt 0</param>
		/// <param name="t">Endpt 1</param>
		/// <param name="leftFace">Left face value</param>
		/// <param name="rightFace">Right face value</param>
		/// <returns>If new edge added, return indec. Otherwise, return undefined.</returns>
		private int AddEdge(List<EdgeInfo> edges, int s, int t, int leftFace, int rightFace)
		{
			//TODO can grow larger since this is a list now.
			if (edges.Count >= edges.Capacity)
			{
				return (int)EdgeValues.Undefined;
			}

			//add edge
			int e = FindEdge(edges, s, t);
			if (e == (int)EdgeValues.Undefined)
			{
				EdgeInfo edge = new EdgeInfo(s, t, leftFace, rightFace);
				edges.Add(edge);

				//list stores count, necessary to return?
				return edges.Count - 1;
			}
			else
			{
				return (int)EdgeValues.Undefined;
			}
		}

		/// <summary>
		/// Search for an edge within the edge list
		/// </summary>
		/// <param name="edges">Edge list</param>
		/// <param name="s">Endpt 0</param>
		/// <param name="t">Endpt 1</param>
		/// <returns>If found, return the edge's index. Otherwise, return undefined.</returns>
		private int FindEdge(List<EdgeInfo> edges, int s, int t)
		{
			for (int i = 0; i < edges.Count; i++)
			{
				if ((edges[i].EndPt0 == s && edges[i].EndPt1 == t) || (edges[i].EndPt0 == t && edges[i].EndPt1 == s))
					return i;
			}

			return (int)EdgeValues.Undefined;
		}

		/// <summary>
		/// Determine whether edges overlap with the points
		/// </summary>
		/// <param name="pts">Individual points</param>
		/// <param name="edges">Edge list</param>
		/// <param name="s1">An edge's endpt 0</param>
		/// <param name="t1">An edge's endpt1</param>
		/// <returns></returns>
		private bool OverlapEdges(List<Vector3> pts, List<EdgeInfo> edges, int s1, int t1)
		{
			Vector3 ps1 = pts[s1], pt1 = pts[t1];

			for (int i = 0; i < edges.Count; i++)
			{
				int s0 = edges[i].EndPt0;
				int t0 = edges[i].EndPt1;

				//same or connected edges do not overlap
				if (s0 == s1 || s0 == t1 || t0 == s1 || t0 == t1)
					continue;

				Vector3 ps0 = pts[s0], pt0 = pts[t0];
				if (MathHelper.Intersection.SegmentSegment2D(ref ps0, ref pt0, ref ps1, ref pt1))
					return true;
			}

			return false;
		}

		/// <summary>
		/// Form a triangle ABC out of the three vectors and calculate the center and radius 
		/// of the resulting circumcircle
		/// </summary>
		/// <param name="p1">Point A</param>
		/// <param name="p2">Point B</param>
		/// <param name="p3">point C</param>
		/// <param name="c">Circumcirlce center</param>
		/// <param name="r">Circumcircle radius</param>
		/// <returns>True, if a circumcirle can be found. False, if otherwise.</returns>
		private bool CircumCircle(Vector3 p1, Vector3 p2, Vector3 p3, ref Vector3 c, out float r)
		{
			float EPS = 1e-6f;
			float cp;
			Vector3Extensions.Cross2D(ref p1, ref p2, ref p3, out cp);

			if (Math.Abs(cp) > EPS)
			{
				//find magnitude of each point
				float p1Sq, p2Sq, p3Sq;

				Vector3Extensions.Dot2D(ref p1, ref p1, out p1Sq);
				Vector3Extensions.Dot2D(ref p2, ref p2, out p2Sq);
				Vector3Extensions.Dot2D(ref p3, ref p3, out p3Sq);

				c.X = (p1Sq * (p2.Z - p3.Z) + p2Sq * (p3.Z - p1.Z) + p3Sq * (p1.Z - p2.Z)) / (2 * cp);
				c.Z = (p1Sq * (p2.X - p3.X) + p2Sq * (p3.X - p1.X) + p3Sq * (p1.X - p2.X)) / (2 * cp);

				float dx = c.X - p1.X;
				float dy = c.Z - p1.Z;
				r = (float)Math.Sqrt(dx * dx + dy * dy);
				return true;
			}

			c.X = p1.X;
			c.Z = p1.Z;
			r = 0;
			return false;
		}

		/// <summary>
		/// Find the distance from a point to a triangle mesh.
		/// </summary>
		/// <param name="p">Individual point</param>
		/// <param name="verts">Vertex array</param>
		/// <param name="tris">Triange list</param>
		/// <returns>The distance</returns>
		private float DistanceToTriMesh(Vector3 p, Vector3[] verts, List<TriangleData> tris)
		{
			float dmin = float.MaxValue;

			for (int i = 0; i < tris.Count; i++)
			{
				int va = tris[i].VertexHash0;
				int vb = tris[i].VertexHash1;
				int vc = tris[i].VertexHash2;
				float d = MathHelper.Distance.PointToTriangle(p, verts[va], verts[vb], verts[vc]);
				if (d < dmin)
					dmin = d;
			}

			if (dmin == float.MaxValue)
				return -1;

			return dmin;
		}

		public struct MeshData
		{
			public int VertexIndex;
			public int VertexCount;
			public int TriangleIndex;
			public int TriangleCount;
		}

		//triangle info contains three vertex hashes and a flag
		public struct TriangleData
		{
			public int VertexHash0;
			public int VertexHash1;
			public int VertexHash2;
			public int Flags; //indicates which 3 vertices are part of the polygon

			public TriangleData(int hash0, int hash1, int hash2)
			{
				VertexHash0 = hash0;
				VertexHash1 = hash1;
				VertexHash2 = hash2;
				Flags = 0;
			}

			public TriangleData(int hash0, int hash1, int hash2, int flags)
			{
				VertexHash0 = hash0;
				VertexHash1 = hash1;
				VertexHash2 = hash2;
				Flags = flags;
			}

			public TriangleData(TriangleData data, List<Vector3> verts, Vector3[] vpoly)
			{
				VertexHash0 = data.VertexHash0;
				VertexHash1 = data.VertexHash1;
				VertexHash2 = data.VertexHash2;
				GetTriFlags(ref data, verts, vpoly, out Flags);
			}

			public int this[int index]
			{
				get
				{
					switch (index)
					{
						case 0:
							return VertexHash0;
						case 1:
							return VertexHash1;
						case 2:
						default:
							return VertexHash2;
					}
				}
			}

			/// <summary>
			/// Determine which edges of the triangle are part of the polygon
			/// </summary>
			/// <param name="verts">Vertices containing triangles</param>
			/// <param name="va">Triangle vertex A</param>
			/// <param name="vb">Triangle vertex B</param>
			/// <param name="vc">Triangle vertex C</param>
			/// <param name="vpoly">Polygon vertex data</param>
			/// <param name="npoly">Number of polygons</param>
			/// <returns></returns>
			public static void GetTriFlags(ref TriangleData t, List<Vector3> verts, Vector3[] vpoly, out int flags)
			{
				flags = 0;

				//the triangle flags store five bits ?0?0? (like 10001, 10101, etc..)
				//each bit stores whether two vertices are close enough to a polygon edge 
				//since triangle has three vertices, there are three distinct pairs of vertices (va,vb), (vb,vc) and (vc,va)
				flags |= GetEdgeFlags(verts[t.VertexHash0], verts[t.VertexHash1], vpoly) << 0;
				flags |= GetEdgeFlags(verts[t.VertexHash1], verts[t.VertexHash2], vpoly) << 2;
				flags |= GetEdgeFlags(verts[t.VertexHash2], verts[t.VertexHash0], vpoly) << 4;
			}
		}

		private struct EdgeInfo
		{
			public int EndPt0;
			public int EndPt1;
			public int LeftFace;
			public int RightFace;

			public EdgeInfo(int endPt0, int endPt1, int leftFace, int rightFace)
			{
				this.EndPt0 = endPt0;
				this.EndPt1 = endPt1;
				this.LeftFace = leftFace;
				this.RightFace = rightFace;
			}

			public static void UpdateLeftFace(ref EdgeInfo e, int s, int t, int f)
			{
				if (e.EndPt0 == s && e.EndPt1 == t && e.LeftFace == (int)EdgeValues.Undefined)
					e.LeftFace = f;
				else if (e.EndPt1 == s && e.EndPt0 == t && e.RightFace == (int)EdgeValues.Undefined)
					e.RightFace = f;
			}
		}

		private struct SamplingData
		{
			public int X;
			public int Y;
			public int Z;
			public bool IsSampled;

			public SamplingData(int x, int y, int z, bool isSampled)
			{
				this.X = x;
				this.Y = y;
				this.Z = z;
				this.IsSampled = isSampled;
			}
		}

		/// <summary>
		/// Determines whether an edge has been created or not
		/// </summary>
		private enum EdgeValues
		{
			Undefined = -1,
			Hull = -2
		}
	}
}
