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
	/// The PolyMeshDetail class is a combination of a PolyMesh and a CompactHeightfield merged together
	/// </summary>
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
		/// Initializes a new instance of the <see cref="PolyMeshDetail"/> class.
		/// </summary>
		/// <param name="mesh">The <see cref="PolyMesh"/>.</param>
		/// <param name="compactField">The <see cref="CompactHeightfield"/> used to add height detail.</param>
		/// <param name="settings">The settings to build with.</param>
		public PolyMeshDetail(PolyMesh mesh, CompactHeightfield compactField, NavMeshGenerationSettings settings)
			: this(mesh, compactField, settings.SampleDistance, settings.MaxSampleError)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="PolyMeshDetail"/> class.
		/// </summary>
		/// <remarks>
		/// <see cref="PolyMeshDetail"/> uses a <see cref="CompactHeightfield"/> to add in details to a
		/// <see cref="PolyMesh"/>. This detail is triangulated into a new mesh and can be used to approximate height in the walkable
		/// areas of a scene.
		/// </remarks>
		/// <param name="mesh">The <see cref="PolyMesh"/>.</param>
		/// <param name="compactField">The <see cref="CompactHeightfield"/> used to add height detail.</param>
		/// <param name="sampleDist">The sampling distance.</param>
		/// <param name="sampleMaxError">The maximum sampling error allowed.</param>
		public PolyMeshDetail(PolyMesh mesh, CompactHeightfield compactField, float sampleDist, float sampleMaxError)
		{
			if (mesh.VertCount == 0 || mesh.PolyCount == 0)
				return;

			Vector3 origin = mesh.Bounds.Min;

			int maxhw = 0, maxhh = 0;

			BBox2i[] bounds = new BBox2i[mesh.PolyCount];
			Vector3[] poly = new Vector3[mesh.NumVertsPerPoly];

			var storedVertices = new List<Vector3>();
			var storedTriangles = new List<TriangleData>();

			//find max size for polygon area
			for (int i = 0; i < mesh.PolyCount; i++)
			{
				var p = mesh.Polys[i];

				int xmin = compactField.Width;
				int xmax = 0;
				int zmin = compactField.Length;
				int zmax = 0;

				for (int j = 0; j < mesh.NumVertsPerPoly; j++)
				{
					var pj = p.Vertices[j];
					if (pj == PolyMesh.NullId)
						break;

					var v = mesh.Verts[pj];

					xmin = Math.Min(xmin, v.X);
					xmax = Math.Max(xmax, v.X);
					zmin = Math.Min(zmin, v.Z);
					zmax = Math.Max(zmax, v.Z);
				}

				xmin = Math.Max(0, xmin - 1);
				xmax = Math.Min(compactField.Width, xmax + 1);
				zmin = Math.Max(0, zmin - 1);
				zmax = Math.Min(compactField.Length, zmax + 1);

				if (xmin >= xmax || zmin >= zmax)
					continue;

				maxhw = Math.Max(maxhw, xmax - xmin);
				maxhh = Math.Max(maxhh, zmax - zmin);

				bounds[i] = new BBox2i(xmin, zmin, xmax, zmax);
			}

			HeightPatch hp = new HeightPatch(0, 0, maxhw, maxhh);

			this.meshes = new MeshData[mesh.PolyCount];

			for (int i = 0; i < mesh.PolyCount; i++)
			{
				var p = mesh.Polys[i];

				//store polygon vertices for processing
				int npoly = 0;
				for (int j = 0; j < mesh.NumVertsPerPoly; j++)
				{
					int pvi = p.Vertices[j];
					if (pvi == PolyMesh.NullId)
						break;

					PolyVertex pv = mesh.Verts[pvi];
					Vector3 v = new Vector3(pv.X, pv.Y, pv.Z);
					v.X *= mesh.CellSize;
					v.Y *= mesh.CellHeight;
					v.Z *= mesh.CellSize;
					poly[j] = v;
					npoly++;
				}

				//get height data from area of polygon
				BBox2i bound = bounds[i];
				hp.Resize(bound.Min.X, bound.Min.Y, bound.Max.X - bound.Min.X, bound.Max.Y - bound.Min.Y);
				GetHeightData(compactField, p, npoly, mesh.Verts, mesh.BorderSize, hp);

				List<Vector3> tempVerts = new List<Vector3>();
				List<TriangleData> tempTris = new List<TriangleData>(128);
				List<EdgeInfo> edges = new List<EdgeInfo>(16);
				List<SamplingData> samples = new List<SamplingData>(128);
				BuildPolyDetail(poly, npoly, sampleDist, sampleMaxError, compactField, hp, tempVerts, tempTris, edges, samples);

				//more detail verts
				for (int j = 0; j < tempVerts.Count; j++)
				{
					Vector3 tv = tempVerts[j];

					Vector3 v;
					v.X = tv.X + origin.X;
					v.Y = tv.Y + origin.Y + compactField.CellHeight;
					v.Z = tv.Z + origin.Z;

					tempVerts[j] = v;
				}

				for (int j = 0; j < npoly; j++)
				{
					Vector3 po = poly[j];

					po.X += origin.X;
					po.Y += origin.Y;
					po.Z += origin.Z;

					poly[j] = po;
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
					storedTriangles.Add(new TriangleData(tempTris[j], tempVerts, poly, npoly));
				}
			}

			this.verts = storedVertices.ToArray();
			this.tris = storedTriangles.ToArray();
		}

		/// <summary>
		/// Determines whether an edge has been created or not.
		/// </summary>
		private enum EdgeValues
		{
			/// <summary>
			/// Edge has not been initialized
			/// </summary>
			Undefined = -1,

			/// <summary>
			/// Edge is hull
			/// </summary>
			Hull = -2
		}
		
		/// <summary>
		/// Gets the number of meshes (MeshData)
		/// </summary>
		public int MeshCount
		{
			get
			{
				if (meshes == null)
					return 0;

				return meshes.Length;
			}
		}

		/// <summary>
		/// Gets the number of vertices
		/// </summary>
		public int VertCount
		{
			get
			{
				if (verts == null)
					return 0;

				return verts.Length;
			}
		}

		/// <summary>
		/// Gets the number of triangles
		/// </summary>
		public int TrisCount
		{
			get
			{
				if (tris == null)
					return 0;

				return tris.Length;
			}
		}

		/// <summary>
		/// Gets the mesh data		
		/// </summary>
		public MeshData[] Meshes
		{
			get
			{
				return meshes;
			}
		}

		/// <summary>
		/// Gets the vertex data
		/// </summary>
		public Vector3[] Verts
		{
			get
			{
				return verts;
			}
		}

		/// <summary>
		/// Gets the triangle data
		/// </summary>
		public TriangleData[] Tris
		{
			get
			{
				return tris;
			}
		}

		#region Black Magic

		/// <summary>
		/// Offset for the x-coordinate
		/// </summary>
		/// <param name="i">Starting number</param>
		/// <returns>A new offset</returns>
		private static float GetJitterX(int i)
		{
			return (((i * 0x8da6b343) & 0xffff) / 65535.0f * 2.0f) - 1.0f;
		}

		/// <summary>
		/// Offset for the y-coordinate
		/// </summary>
		/// <param name="i">Starting number</param>
		/// <returns>A new offset</returns>
		private static float GetJitterY(int i)
		{
			return (((i * 0xd8163841) & 0xffff) / 65535.0f * 2.0f) - 1.0f;
		}

		#endregion

		/// <summary>
		/// Determine whether an edge of the triangle is part of the polygon (1 if true, 0 if false)
		/// </summary>
		/// <param name="va">Triangle vertex A</param>
		/// <param name="vb">Triangle vertex B</param>
		/// <param name="vpoly">Polygon vertex data</param>
		/// <returns>1 if the vertices are close, 0 if otherwise</returns>
		private static int GetEdgeFlags(Vector3 va, Vector3 vb, Vector3[] vpoly, int npoly)
		{
			//true if edge is part of polygon
			float thrSqr = 0.001f * 0.001f;

			for (int i = 0, j = npoly - 1; i < npoly; j = i++)
			{
				Vector3 pt1 = va;
				Vector3 pt2 = vb;

				//the vertices pt1 (va) and pt2 (vb) are extremely close to the polygon edge
				if (Distance.PointToSegment2DSquared(ref pt1, ref vpoly[j], ref vpoly[i]) < thrSqr 
					&& Distance.PointToSegment2DSquared(ref pt2, ref vpoly[j], ref vpoly[i]) < thrSqr)
					return 1;
			}

			return 0;
		}

		private void GetHeightData(CompactHeightfield compactField, PolyMesh.Polygon poly, int polyCount, PolyVertex[] verts, int borderSize, HeightPatch hp)
		{
			var stack = new List<CompactSpanReference>();
			bool empty = true;
			hp.Clear();

			for (int y = 0; y < hp.Length; y++)
			{
				int hy = hp.Y + y + borderSize;
				for (int x = 0; x < hp.Width; x++)
				{
					int hx = hp.X + x + borderSize;
					var cells = compactField.Cells[hy * compactField.Width + hx];
					for (int i = cells.StartIndex, end = cells.StartIndex + cells.Count; i < end; i++)
					{
						var span = compactField.Spans[i];

						if (span.Region == poly.RegionId)
						{
							hp[x, y] = span.Minimum;
							empty = false;

							bool border = false;
							for (var dir = Direction.West; dir <= Direction.South; dir++)
							{
								if (span.IsConnected(dir))
								{
									int ax = hx + dir.GetHorizontalOffset();
									int ay = hy + dir.GetVerticalOffset();
									int ai = compactField.Cells[ay * compactField.Width + ax].StartIndex + CompactSpan.GetConnection(ref span, dir);

									if (compactField.Spans[ai].Region != poly.RegionId)
									{
										border = true;
										break;
									}
								}
							}

							if (border)
								stack.Add(new CompactSpanReference(hx, hy, i));

							break;
						}
					}
				}
			}

			if (empty)
				GetHeightDataSeedsFromVertices(compactField, poly, polyCount, verts, borderSize, hp, stack);

			const int RetractSize = 256;
			int head = 0;

			while (head < stack.Count)
			{
				var cell = stack[head++];
				var cs = compactField[cell];

				if (head >= RetractSize)
				{
					head = 0;
					if (stack.Count > RetractSize)
					{
						for (int i = 0; i < stack.Count - RetractSize; i++)
							stack[i] = stack[i + RetractSize];
					}

					int targetSize = stack.Count % RetractSize;
					while (stack.Count > targetSize)
						stack.RemoveAt(stack.Count - 1);
				}

				//loop in all four directions
				for (var dir = Direction.West; dir <= Direction.South; dir++)
				{
					//skip
					if (!cs.IsConnected(dir))
						continue;

					int ax = cell.X + dir.GetHorizontalOffset();
					int ay = cell.Y + dir.GetVerticalOffset();
					int hx = ax - hp.X - borderSize;
					int hy = ay - hp.Y - borderSize;

					if (hx < 0 || hx >= hp.Width || hy < 0 || hy >= hp.Length)
						continue;

					//only continue if height is unset
					if (hp.IsSet(hy * hp.Width + hx))
						continue;

					//get new span
					int ai = compactField.Cells[ay * compactField.Width + ax].StartIndex + CompactSpan.GetConnection(ref cs, dir);
					CompactSpan ds = compactField.Spans[ai];

					hp[hx, hy] = ds.Minimum;

					stack.Add(new CompactSpanReference(ax, ay, ai));
				}
			}
		}

		/// <summary>
		/// Floodfill heightfield to get 2D height data, starting at vertex locations
		/// </summary>
		/// <param name="compactField">Original heightfield data</param>
		/// <param name="poly">Polygon in PolyMesh</param>
		/// <param name="polyCount">Number of vertices per polygon</param>
		/// <param name="verts">PolyMesh Vertices</param>
		/// <param name="borderSize">Heightfield border size</param>
		/// <param name="hp">HeightPatch which extracts heightfield data</param>
		/// <param name="stack">Temporary stack of CompactSpanReferences</param>
		private void GetHeightDataSeedsFromVertices(CompactHeightfield compactField, PolyMesh.Polygon poly, int polyCount, PolyVertex[] verts, int borderSize, HeightPatch hp, List<CompactSpanReference> stack)
		{
			hp.SetAll(0);

			//use poly vertices as seed points
			for (int j = 0; j < polyCount; j++)
			{
				var csr = new CompactSpanReference(0, 0, -1);
				int dmin = int.MaxValue;

				var v = verts[poly.Vertices[j]];

				for (int k = 0; k < 9; k++)
				{
					//get vertices and offset x and z coordinates depending on current drection
					int ax = v.X + VertexOffset[k * 2 + 0];
					int ay = v.Y;
					int az = v.Z + VertexOffset[k * 2 + 1];

					//skip if out of bounds
					if (ax < hp.X || ax >= hp.X + hp.Width || az < hp.Y || az >= hp.Y + hp.Length)
						continue;

					//get new cell
					CompactCell c = compactField.Cells[(az + borderSize) * compactField.Width + (ax + borderSize)];
					
					//loop through all the spans
					for (int i = c.StartIndex, end = c.StartIndex + c.Count; i < end; i++)
					{
						CompactSpan s = compactField.Spans[i];
						
						//find minimum y-distance
						int d = Math.Abs(ay - s.Minimum);
						if (d < dmin)
						{
							csr = new CompactSpanReference(ax, az, i);
							dmin = d;
						}
					}
				}

				//only add if something new found
				if (csr.Index != -1)
				{
					stack.Add(csr);
				}
			}

			//find center of polygon using flood fill
			int pcx = 0, pcz = 0;
			for (int j = 0; j < polyCount; j++)
			{
				var v = verts[poly.Vertices[j]];
				pcx += v.X;
				pcz += v.Z;
			}

			pcx /= polyCount;
			pcz /= polyCount;

			//stack groups 3 elements as one part
			foreach (var cell in stack)
			{
				int idx = (cell.Y - hp.Y) * hp.Width + (cell.X - hp.X);
				hp[idx] = 1;
			}

			//process the entire stack
			while (stack.Count > 0)
			{
				var cell = stack[stack.Count - 1];
				stack.RemoveAt(stack.Count - 1);

				//check if close to center of polygon
				if (Math.Abs(cell.X - pcx) <= 1 && Math.Abs(cell.Y - pcz) <= 1)
				{
					//clear the stack and add a new group
					stack.Clear();

					stack.Add(cell);
					break;
				}

				CompactSpan cs = compactField[cell];

				//check all four directions
				for (var dir = Direction.West; dir <= Direction.South; dir++)
				{
					//skip if disconnected
					if (!cs.IsConnected(dir))
						continue;

					//get neighbor
					int ax = cell.X + dir.GetHorizontalOffset();
					int ay = cell.Y + dir.GetVerticalOffset();

					//skip if out of bounds
					if (ax < hp.X || ax >= (hp.X + hp.Width) || ay < hp.Y || ay >= (hp.Y + hp.Length))
						continue;

					if (hp[(ay - hp.Y) * hp.Width + (ax - hp.X)] != 0)
						continue;

					//get the new index
					int ai = compactField.Cells[(ay + borderSize) * compactField.Width + (ax + borderSize)].StartIndex + CompactSpan.GetConnection(ref cs, dir);

					//save data
					int idx = (ay - hp.Y) * hp.Width + (ax - hp.X);
					hp[idx] = 1;

					//push to stack
					stack.Add(new CompactSpanReference(ax, ay, ai));
				}
			}

			//clear the heightpatch
			hp.Clear();

			//mark start locations
			for (int i = 0; i < stack.Count; i++)
			{
				var c = stack[i];

				//set new heightpatch data
				int idx = (c.Y - hp.Y) * hp.Width + (c.X - hp.X);
				CompactSpan cs = compactField.Spans[c.Index];
				hp[idx] = cs.Minimum;

				stack[i] = new CompactSpanReference(c.X + borderSize, c.Y + borderSize, c.Index);
			}
		}

		private static float PolyMinExtent(Vector3[] verts)
		{
			float minDist = float.MaxValue;
			for (int i = 0; i < verts.Length; i++)
			{
				int ni = (i + 1) % verts.Length;
				Vector3 p0 = verts[i];
				Vector3 p1 = verts[ni];

				float maxEdgeDist = 0;
				for (int j = 0; j < verts.Length; j++)
				{
					if (j == i || j == ni)
						continue;

					float d = Distance.PointToSegment2DSquared(ref verts[j], ref p0, ref p1);
					maxEdgeDist = Math.Max(maxEdgeDist, d);
				}

				minDist = Math.Min(minDist, maxEdgeDist);
			}

			return (float)Math.Sqrt(minDist);
		}

		/// <summary>
		/// Generate the PolyMeshDetail using the PolyMesh and HeightPatch
		/// </summary>
		/// <param name="polyMeshVerts">PolyMesh Vertex data</param>
		/// <param name="numMeshVerts">Number of PolyMesh vertices</param>
		/// <param name="sampleDist">Sampling distance</param>
		/// <param name="sampleMaxError">Maximum sampling error</param>
		/// <param name="compactField">THe compactHeightfield</param>
		/// <param name="hp">The heightPatch</param>
		/// <param name="verts">Detail verts</param>
		/// <param name="tris">Detail triangles</param>
		/// <param name="edges">The edge array</param>
		/// <param name="samples">The samples array</param>
		private void BuildPolyDetail(Vector3[] polyMeshVerts, int numMeshVerts, float sampleDist, float sampleMaxError, CompactHeightfield compactField, HeightPatch hp, List<Vector3> verts, List<TriangleData> tris, List<EdgeInfo> edges, List<SamplingData> samples)
		{
			const int MAX_VERTS = 127;
			const int MAX_TRIS = 255;
			const int MAX_VERTS_PER_EDGE = 32;
			Vector3[] edge = new Vector3[MAX_VERTS_PER_EDGE + 1];
			List<int> hull = new List<int>(MAX_VERTS);

			//fill up vertex array
			for (int i = 0; i < numMeshVerts; ++i)
				verts.Add(polyMeshVerts[i]);

			float cs = compactField.CellSize;
			float ics = 1.0f / cs;

			float minExtent = PolyMinExtent(polyMeshVerts);

			//tessellate outlines
			if (sampleDist > 0)
			{
				for (int i = 0, j = verts.Count - 1; i < verts.Count; j = i++)
				{
					Vector3 vi = verts[i];
					Vector3 vj = verts[j];
					bool swapped = false;

					//make sure order is correct, otherwise swap data
					if (Math.Abs(vj.X - vi.X) < 1E-06f)
					{
						if (vj.Z > vi.Z)
						{
							Vector3 temp = vj;
							vj = vi;
							vi = temp;
							swapped = true;
						}
					}
					else if (vj.X > vi.X)
					{
						Vector3 temp = vj;
						vj = vi;
						vi = temp;
						swapped = true;
					}

					//create samples along the edge
					Vector3 dv;
					Vector3.Subtract(ref vi, ref vj, out dv);
					float d = (float)Math.Sqrt(dv.X * dv.X + dv.Z * dv.Z);
					int nn = 1 + (int)Math.Floor(d / sampleDist);

					if (nn >= MAX_VERTS_PER_EDGE)
						nn = MAX_VERTS_PER_EDGE - 1;

					if (verts.Count + nn >= MAX_VERTS)
						nn = MAX_VERTS - 1 - verts.Count;

					for (int k = 0; k <= nn; k++)
					{
						float u = (float)k / (float)nn;
						Vector3 pos;

						Vector3 tmp;
						Vector3.Multiply(ref dv, u, out tmp);
						Vector3.Add(ref vj, ref tmp, out pos);

						pos.Y = GetHeight(pos, ics, compactField.CellHeight, hp) * compactField.CellHeight;

						edge[k] = pos;
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
						Vector3 va = edge[a];
						Vector3 vb = edge[b];

						//find maximum deviation along segment
						float maxd = 0;
						int maxi = -1;
						for (int m = a + 1; m < b; m++)
						{
							float dev = Distance.PointToSegmentSquared(ref edge[m], ref va, ref vb);
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

			if (minExtent < sampleDist * 2)
			{
				TriangulateHull(verts, hull, tris);
				return;
			}

			TriangulateHull(verts, hull, tris);

			if (tris.Count == 0)
			{
				Console.WriteLine("Can't triangulate polygon, adding default data.");
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
						if (Distance.PointToPolygonSquared(pt, polyMeshVerts, numMeshVerts) > -sampleDist * 0.5f)
							continue;

						SamplingData sd = new SamplingData(x, GetHeight(pt, ics, compactField.CellHeight, hp), z, false);
						samples.Add(sd);
					}
				}

				//added samples
				for (int iter = 0; iter < samples.Count; iter++)
				{
					if (verts.Count >= MAX_VERTS)
						break;

					//find sample with most error
					Vector3 bestPt = Vector3.Zero;
					float bestDistance = 0;
					int bestIndex = -1;

					for (int i = 0; i < samples.Count; i++)
					{
						SamplingData sd = samples[i];
						if (sd.IsSampled)
							continue;

						//jitter sample location to remove effects of bad triangulation
						Vector3 pt;
						pt.X = sd.X * sampleDist + GetJitterX(i) * compactField.CellSize * 0.1f;
						pt.Y = sd.Y * compactField.CellHeight;
						pt.Z = sd.Z * sampleDist + GetJitterY(i) * compactField.CellSize * 0.1f;
						float d = DistanceToTriMesh(pt, verts, tris);

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

					SamplingData bsd = samples[bestIndex];
					bsd.IsSampled = true;
					samples[bestIndex] = bsd;

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
				//TODO we're using lists... let the user have super detailed meshes?
				//Perhaps just a warning saying there's a lot of tris?
				//tris.RemoveRange(MAX_TRIS + 1, tris.Count - MAX_TRIS);
				//Console.WriteLine("WARNING: shrinking number of triangles.");
			}
		}

		/// <summary>
		/// Use the HeightPatch data to obtain a height for a certain location.
		/// </summary>
		/// <param name="loc">The location</param>
		/// <param name="invCellSize">Reciprocal of cell size</param>
		/// <param name="cellHeight">Cell height</param>
		/// <param name="hp">Height patch</param>
		/// <returns>The height</returns>
		private int GetHeight(Vector3 loc, float invCellSize, float cellHeight, HeightPatch hp)
		{
			int ix = (int)Math.Floor(loc.X * invCellSize + 0.01f);
			int iz = (int)Math.Floor(loc.Z * invCellSize + 0.01f);
			ix = MathHelper.Clamp(ix - hp.X, 0, hp.Width - 1);
			iz = MathHelper.Clamp(iz - hp.Y, 0, hp.Length - 1);
			int h;

			if (!hp.TryGetHeight(ix, iz, out h))
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

					if (nx < 0 || nz < 0 || nx >= hp.Width || nz >= hp.Length)
						continue;

					int nh;
					if (!hp.TryGetHeight(nx, nz, out nh))
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

		private void TriangulateHull(List<Vector3> pts, List<int> hull, List<TriangleData> tris)
		{
			int start = 0, left = 1, right = hull.Count - 1;

			float dmin = 0;
			for (int i = 0; i < hull.Count; i++)
			{
				int pi = Prev(i, hull.Count);
				int ni = Next(i, hull.Count);
				Vector3 pv = pts[hull[pi]];
				Vector3 cv = pts[hull[i]];
				Vector3 nv = pts[hull[ni]];
				float d = 0;
				float dtmp;
				Vector3Extensions.Distance2D(ref pv, ref cv, out dtmp);
				d += dtmp;
				Vector3Extensions.Distance2D(ref cv, ref nv, out dtmp);
				d += dtmp;
				Vector3Extensions.Distance2D(ref nv, ref pv, out dtmp);
				d += dtmp;

				if (d < dmin)
				{
					start = i;
					left = ni;
					right = pi;
					dmin = d;
				}
			}

			tris.Add(new TriangleData(hull[start], hull[left], hull[right], 0));

			while (Next(left, hull.Count) != right)
			{
				int nleft = Next(left, hull.Count);
				int nright = Prev(right, hull.Count);

				Vector3 cvleft = pts[hull[left]];
				Vector3 nvleft = pts[hull[nleft]];
				Vector3 cvright = pts[hull[right]];
				Vector3 nvright = pts[hull[nright]];

				float dleft = 0, dright = 0;
				float dtmp;
				Vector3Extensions.Distance2D(ref cvleft, ref nvleft, out dtmp);
				dleft += dtmp;
				Vector3Extensions.Distance2D(ref nvleft, ref cvright, out dtmp);
				dleft += dtmp;

				Vector3Extensions.Distance2D(ref cvright, ref nvright, out dtmp);
				dright += dtmp;
				Vector3Extensions.Distance2D(ref cvleft, ref nvright, out dtmp);
				dright += dtmp;

				if (dleft < dright)
				{
					tris.Add(new TriangleData(hull[left], hull[nleft], hull[right], 0));
					left = nleft;
				}
				else
				{
					tris.Add(new TriangleData(hull[left], hull[nright], hull[right], 0));
					right = nright;
				}
			}
		}

		/// <summary>
		/// Delaunay triangulation is used to triangulate the polygon after adding detail to the edges. The result is a mesh.
		/// </summary>
		/// <param name="pts">Vertex data (each vertex has 3 elements x,y,z)</param>
		/// <param name="hull">The hull (purpose?)</param>
		/// <param name="tris">The triangles formed.</param>
		/// <param name="edges">The edge connections formed.</param>
		private void DelaunayHull(List<Vector3> pts, List<int> hull, List<TriangleData> tris, List<EdgeInfo> edges)
		{
			int nfaces = 0;
			edges.Clear();

			for (int i = 0, j = hull.Count - 1; i < hull.Count; j = i++)
				AddEdge(edges, hull[j], hull[i], (int)EdgeValues.Hull, (int)EdgeValues.Undefined);

			for (int i = 0; i < edges.Count; i++)
			{
				if (edges[i].LeftFace == (int)EdgeValues.Undefined)
					CompleteFacet(pts, edges, ref nfaces, i);
				
				if (edges[i].RightFace == (int)EdgeValues.Undefined)
					CompleteFacet(pts, edges, ref nfaces, i);
			}

			/*int currentEdge = 0;
			while (currentEdge < edges.Count)
			{
				if (edges[currentEdge].LeftFace == (int)EdgeValues.Undefined)
					CompleteFacet(pts, edges, ref nfaces, currentEdge);
				if (edges[currentEdge].RightFace == (int)EdgeValues.Undefined)
					CompleteFacet(pts, edges, ref nfaces, currentEdge);

				currentEdge++;
			}*/

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
					Console.WriteLine("WARNING: removing dangling face.");
					tris[i] = tris[tris.Count - 1];
					tris.RemoveAt(tris.Count - 1);
					i--;
				}
			}
		}

		/// <summary>
		/// If a face has missing edges, then fill in those edges
		/// </summary>
		/// <param name="pts">List of points</param>
		/// <param name="edges">List of edges</param>
		/// <param name="nfaces">The total number of faces</param>
		/// <param name="curEdge">The current index in the edge list</param>
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

				curEdge = AddEdge(edges, pt, s, nfaces, (int)EdgeValues.Undefined);
				if (curEdge != (int)EdgeValues.Undefined)
				{
					e = edges[curEdge];
					EdgeInfo.UpdateLeftFace(ref e, pt, s, nfaces);
					edges[curEdge] = e;
				}

				curEdge = AddEdge(edges, t, pt, nfaces, (int)EdgeValues.Undefined);
				if (curEdge != (int)EdgeValues.Undefined)
				{
					e = edges[curEdge];
					EdgeInfo.UpdateLeftFace(ref e, t, pt, nfaces);
					edges[curEdge] = e;
				}

				nfaces++;
			}
			else
			{
				e = edges[curEdge];
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
		/// <returns>The index of the edge (edge can already exist in the list).</returns>
		private int AddEdge(List<EdgeInfo> edges, int s, int t, int leftFace, int rightFace)
		{
			//add edge
			int e = FindEdge(edges, s, t);
			if (e == -1)
			{
				EdgeInfo edge = new EdgeInfo(s, t, rightFace, leftFace);
				edges.Add(edge);

				return edges.Count - 1;
			}
			else
				return e;
		}

		/// <summary>
		/// Search for an edge within the edge list
		/// </summary>
		/// <param name="edges">Edge list</param>
		/// <param name="s">Endpt 0</param>
		/// <param name="t">Endpt 1</param>
		/// <returns>If found, return the edge's index. Otherwise, return -1.</returns>
		private int FindEdge(List<EdgeInfo> edges, int s, int t)
		{
			for (int i = 0; i < edges.Count; i++)
			{
				EdgeInfo e = edges[i];
				if ((e.EndPt0 == s && e.EndPt1 == t) || (e.EndPt0 == t && e.EndPt1 == s))
					return i;
			}

			return -1;
		}

		/// <summary>
		/// Determine whether edges overlap with the points
		/// </summary>
		/// <param name="pts">Individual points</param>
		/// <param name="edges">Edge list</param>
		/// <param name="s1">An edge's endpt 0</param>
		/// <param name="t1">An edge's endpt1</param>
		/// <returns>True if there is overlap, false if not</returns>
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
				if (Intersection.SegmentSegment2D(ref ps0, ref pt0, ref ps1, ref pt1))
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
			const float EPS = 1e-6f;
			float cp;
			Vector3Extensions.Cross2D(ref p1, ref p2, ref p3, out cp);

			if (Math.Abs(cp) > EPS)
			{
				//find magnitude of each point
				float p1sq, p2sq, p3sq;

				Vector3Extensions.Dot2D(ref p1, ref p1, out p1sq);
				Vector3Extensions.Dot2D(ref p2, ref p2, out p2sq);
				Vector3Extensions.Dot2D(ref p3, ref p3, out p3sq);

				c.X = (p1sq * (p2.Z - p3.Z) + p2sq * (p3.Z - p1.Z) + p3sq * (p1.Z - p2.Z)) / (2 * cp);
				c.Z = (p1sq * (p3.X - p2.X) + p2sq * (p1.X - p3.X) + p3sq * (p2.X - p1.X)) / (2 * cp);

				float dx = p1.X - c.X;
				float dy = p1.Z - c.Z;
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
		private float DistanceToTriMesh(Vector3 p, List<Vector3> verts, List<TriangleData> tris)
		{
			float dmin = float.MaxValue;

			for (int i = 0; i < tris.Count; i++)
			{
				Vector3 va = verts[tris[i].VertexHash0];
				Vector3 vb = verts[tris[i].VertexHash1];
				Vector3 vc = verts[tris[i].VertexHash2];
				float d = Distance.PointToTriangle(p, va, vb, vc);
				if (d < dmin)
					dmin = d;
			}

			if (dmin == float.MaxValue)
				return -1;

			return dmin;
		}

		/// <summary>
		/// The MeshData struct contains information about vertex and triangle base and offset values for array indices
		/// </summary>
		public struct MeshData
		{
			public int VertexIndex;
			public int VertexCount;
			public int TriangleIndex;
			public int TriangleCount;
		}

		/// <summary>
		/// The triangle info contains three vertex hashes and a flag
		/// </summary>
		public struct TriangleData
		{
			public int VertexHash0;
			public int VertexHash1;
			public int VertexHash2;
			public int Flags; //indicates which 3 vertices are part of the polygon

			/// <summary>
			/// Initializes a new instance of the <see cref="TriangleData" /> struct.
			/// </summary>
			/// <param name="hash0">Vertex A</param>
			/// <param name="hash1">Vertex B</param>
			/// <param name="hash2">Vertex C</param>
			public TriangleData(int hash0, int hash1, int hash2)
			{
				VertexHash0 = hash0;
				VertexHash1 = hash1;
				VertexHash2 = hash2;
				Flags = 0;
			}

			/// <summary>
			/// Initializes a new instance of the <see cref="TriangleData" /> struct.
			/// </summary>
			/// <param name="hash0">Vertex A</param>
			/// <param name="hash1">Vertex B</param>
			/// <param name="hash2">Vertex C</param>
			/// <param name="flags">The triangle flags</param>
			public TriangleData(int hash0, int hash1, int hash2, int flags)
			{
				VertexHash0 = hash0;
				VertexHash1 = hash1;
				VertexHash2 = hash2;
				Flags = flags;
			}

			/// <summary>
			/// Initializes a new instance of the <see cref="TriangleData" /> struct.
			/// </summary>
			/// <param name="data">The triangle itself</param>
			/// <param name="verts">The list of all the vertices</param>
			/// <param name="vpoly">The list of the polygon's vertices</param>
			public TriangleData(TriangleData data, List<Vector3> verts, Vector3[] vpoly, int npoly)
			{
				VertexHash0 = data.VertexHash0;
				VertexHash1 = data.VertexHash1;
				VertexHash2 = data.VertexHash2;
				Flags = GetTriFlags(ref data, verts, vpoly, npoly);
			}

			/// <summary>
			/// Gets a triangle's particular vertex
			/// </summary>
			/// <param name="index">Vertex index</param>
			/// <returns>Triangle vertex hash</returns>
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
			/// <param name="t">A triangle.</param>
			/// <param name="verts">The vertex buffer that the triangle is referencing.</param>
			/// <param name="vpoly">Polygon vertex data.</param>
			/// <returns>The triangle's flags.</returns>
			public static int GetTriFlags(ref TriangleData t, List<Vector3> verts, Vector3[] vpoly, int npoly)
			{
				int flags = 0;

				//the triangle flags store five bits ?0?0? (like 10001, 10101, etc..)
				//each bit stores whether two vertices are close enough to a polygon edge 
				//since triangle has three vertices, there are three distinct pairs of vertices (va,vb), (vb,vc) and (vc,va)
				flags |= GetEdgeFlags(verts[t.VertexHash0], verts[t.VertexHash1], vpoly, npoly) << 0;
				flags |= GetEdgeFlags(verts[t.VertexHash1], verts[t.VertexHash2], vpoly, npoly) << 2;
				flags |= GetEdgeFlags(verts[t.VertexHash2], verts[t.VertexHash0], vpoly, npoly) << 4;

				return flags;
			}
		}

		/// <summary>
		/// The EdgeInfo struct contains two enpoints and the faces/polygons to the left and right of that edge.
		/// </summary>
		private struct EdgeInfo
		{
			public int EndPt0;
			public int EndPt1;
			public int RightFace;
			public int LeftFace;

			/// <summary>
			/// Initializes a new instance of the <see cref="EdgeInfo"/> struct.
			/// </summary>
			/// <param name="endPt0">Point A</param>
			/// <param name="endPt1">Point B</param>
			/// <param name="rightFace">The face to the left of the edge</param>
			/// <param name="leftFace">The face to the right of the edge</param>
			public EdgeInfo(int endPt0, int endPt1, int rightFace, int leftFace)
			{
				this.EndPt0 = endPt0;
				this.EndPt1 = endPt1;
				this.RightFace = rightFace;
				this.LeftFace = leftFace;
			}

			/// <summary>
			/// If the left face is undefined, assign it a value
			/// </summary>
			/// <param name="e">The current edge</param>
			/// <param name="s">Endpoint A</param>
			/// <param name="t">Endpoint B</param>
			/// <param name="f">The face value</param>
			public static void UpdateLeftFace(ref EdgeInfo e, int s, int t, int f)
			{
				if (e.EndPt0 == s && e.EndPt1 == t && e.LeftFace == (int)EdgeValues.Undefined)
					e.LeftFace = f;
				else if (e.EndPt1 == s && e.EndPt0 == t && e.RightFace == (int)EdgeValues.Undefined)
					e.RightFace = f;
			}
		}

		/// <summary>
		/// The SamplingData struct contains information about sampled vertices from the PolyMesh
		/// </summary>
		private struct SamplingData
		{
			public int X;
			public int Y;
			public int Z;
			public bool IsSampled;

			/// <summary>
			/// Initializes a new instance of the <see cref="SamplingData"/> struct.
			/// </summary>
			/// <param name="x">The x-coordinate</param>
			/// <param name="y">The y-coordinate</param>
			/// <param name="z">The z-coordinate</param>
			/// <param name="isSampled">Whether or not the vertex has been sampled</param>
			public SamplingData(int x, int y, int z, bool isSampled)
			{
				this.X = x;
				this.Y = y;
				this.Z = z;
				this.IsSampled = isSampled;
			}
		}
	}
}
