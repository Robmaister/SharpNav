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
	public class PolyMeshDetail
	{
		public const int UNSET_HEIGHT = unchecked((int)0xffffffff);

		private int nmeshes;
		private int nverts;
		private int ntris;

		//mesh info contains number of vertices and triangles
		public struct MeshData
		{
			public int VertexIndex;
			public int VertexCount;
			public int TriangleIndex;
			public int TriangleCount;
		}

		private MeshData[] meshes;
		
		//each vertex is basically a vector3 (has x, y, z coordinates)
		private Vector3[] verts;

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
		}

		private TriangleData[] tris;

		private class EdgeInfo
		{
			public int[] EndPts;
			public int LeftFace;
			public int RightFace;
		}

		/// <summary>
		/// Use the CompactHeightfield data to add the height detail to the mesh. 
		/// Triangulate the added detail to form a complete navigation mesh.
		/// </summary>
		/// <param name="mesh">Basic mesh</param>
		/// <param name="openField">Compact heightfield data</param>
		/// <param name="sampleDist"></param>
		/// <param name="sampleMaxError"></param>
		public PolyMeshDetail(PolyMesh mesh, CompactHeightfield openField, float sampleDist, float sampleMaxError)
		{
			if (mesh.NVerts == 0 || mesh.NPolys == 0)
				return;

			Vector3 origin = mesh.Bounds.Min;

			List<EdgeInfo> edges = new List<EdgeInfo>(16);
			List<TriangleData> tris = new List<TriangleData>(128);
			List<int> samples = new List<int>(512);
			HeightPatch hp = new HeightPatch();
			Vector3[] verts = new Vector3[256];
			int nPolyVerts = 0;
			int maxhw = 0, maxhh = 0;

			int[] bounds = new int[mesh.NPolys * 4];
			Vector3[] poly = new Vector3[mesh.NumVertsPerPoly]; 

			//find max size for polygon area
			for (int i = 0; i < mesh.NPolys; i++)
			{
				int xmin, xmax, ymin, ymax;

				xmin = bounds[i * 4 + 0] = openField.Width;
				xmax = bounds[i * 4 + 1] = 0;
				ymin = bounds[i * 4 + 2] = openField.Height;
				ymax = bounds[i * 4 + 3] = 0;

				for (int j = 0; j < mesh.NumVertsPerPoly; j++)
				{
					if (mesh.Polys[i].Vertices[j] == PolyMesh.MESH_NULL_IDX)
						break;

					int v = mesh.Polys[i].Vertices[j];

					xmin = bounds[i * 4 + 0] = (int)Math.Min(xmin, mesh.Verts[v].X);
					xmax = bounds[i * 4 + 1] = (int)Math.Max(xmax, mesh.Verts[v].X);
					ymin = bounds[i * 4 + 2] = (int)Math.Min(ymin, mesh.Verts[v].Z);
					ymax = bounds[i * 4 + 3] = (int)Math.Max(ymax, mesh.Verts[v].Z);

					nPolyVerts++;
				}

				xmin = bounds[i * 4 + 0] = Math.Max(0, xmin - 1);
				xmax = bounds[i * 4 + 1] = Math.Min(openField.Width, xmax + 1);
				ymin = bounds[i * 4 + 2] = Math.Max(0, ymin - 1);
				ymax = bounds[i * 4 + 3] = Math.Min(openField.Height, ymax + 1);

				if (xmin >= xmax || ymin >= ymax)
					continue;

				maxhw = Math.Max(maxhw, xmax - xmin);
				maxhh = Math.Max(maxhh, ymax - ymin);
			}

			hp.Data = new int[maxhw * maxhh];

			this.nmeshes = mesh.NPolys;
			this.meshes = new MeshData[this.nmeshes];

			int vcap = nPolyVerts + nPolyVerts / 2;
			int tcap = vcap * 2;

			this.nverts = 0;
			this.verts = new Vector3[vcap];

			this.ntris = 0;
			this.tris = new TriangleData[tcap];

			for (int i = 0; i < mesh.NPolys; i++)
			{
				//store polygon vertices for processing
				int npoly = 0;
				for (int j = 0; j < mesh.NumVertsPerPoly; j++)
				{
					if (mesh.Polys[i].Vertices[j] == PolyMesh.MESH_NULL_IDX)
						break;

					int v = mesh.Polys[i].Vertices[j];
					poly[j].X = mesh.Verts[v].X * mesh.CellSize;
					poly[j].Y = mesh.Verts[v].Y * mesh.CellHeight;
					poly[j].Z = mesh.Verts[v].Z * mesh.CellSize;
					npoly++;
				}

				//get height data from area of polygon
				hp.xmin = bounds[i * 4 + 0];
				hp.ymin = bounds[i * 4 + 2];
				hp.width = bounds[i * 4 + 1] - bounds[i * 4 + 0];
				hp.height = bounds[i * 4 + 3] - bounds[i * 4 + 2];
				GetHeightData(openField, mesh.Polys, i, npoly, mesh.Verts, mesh.BorderSize, ref hp);

				int nverts = 0;
				BuildPolyDetail(poly, npoly, sampleDist, sampleMaxError, openField, hp, verts, ref nverts, tris, edges, samples);

				//more detail verts
				for (int j = 0; j < nverts; j++)
				{
					verts[j].X += origin.X;
					verts[j].Y += origin.Y + openField.CellHeight;
					verts[j].Z += origin.Z;
				}

				for (int j = 0; j < npoly; j++)
				{
					poly[j].X += origin.X;
					poly[j].Y += origin.Y;
					poly[j].Z += origin.Z;
				}

				//save data
				int ntris = tris.Count;

				this.meshes[i].VertexIndex = this.nverts;
				this.meshes[i].VertexCount = nverts;
				this.meshes[i].TriangleIndex = this.ntris;
				this.meshes[i].TriangleCount = ntris;

				//exapnd vertex array
				if (this.nverts + nverts > vcap)
				{
					//make sure vertex cap is large enough
					while (this.nverts + nverts > vcap)
						vcap += 256;

					//copy old elements to new array
					Vector3[] newv = new Vector3[vcap];
					if (this.nverts > 0)
					{
						for (int j = 0; j < this.verts.Length; j++)
							newv[j] = this.verts[j];
					}

					this.verts = newv;
				}

				//save new vertices
				for (int j = 0; j < nverts; j++)
				{
					this.verts[this.nverts] = verts[j];
					this.nverts++;
				}

				//expand triangle array
				if (this.ntris + ntris > tcap)
				{
					while (this.ntris + ntris > tcap)
						tcap += 256;

					TriangleData[] newt = new TriangleData[tcap];

					if (this.ntris > 0)
					{
						for (int j = 0; j < this.tris.Length; j++)
							newt[j] = this.tris[j];
					}

					this.tris = newt;
				}

				//store triangles
				for (int j = 0; j < ntris; j++)
				{
					int t = j;
					TriangleData ti;
					ti.VertexHash0 = tris[t].VertexHash0;
					ti.VertexHash1 = tris[t].VertexHash1;
					ti.VertexHash2 = tris[t].VertexHash2;
					ti.Flags = GetTriFlags(verts, tris[t].VertexHash0, tris[t].VertexHash1, tris[t].VertexHash2, poly, npoly);
					this.ntris++;
				}
			}
		}

		public int NMeshes { get { return nmeshes; } }

		public int NVerts { get { return nverts; } }

		public int NTris { get { return ntris; } }

		public MeshData[] Meshes { get { return meshes; } }

		public Vector3[] Verts { get { return verts; } }

		public TriangleData[] Tris { get { return tris; } }

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
		private int GetTriFlags(Vector3[] verts, int va, int vb, int vc, Vector3[] vpoly, int npoly)
		{
			int flags = 0;

			//the triangle flags store five bits ?0?0? (like 10001, 10101, etc..)
			//each bit stores whether two vertices are close enough to a polygon edge 
			//since triangle has three vertices, there are three distinct pairs of vertices (va,vb), (vb,vc) and (vc,va)
			flags |= GetEdgeFlags(verts[va], verts[vb], vpoly, npoly) << 0;
			flags |= GetEdgeFlags(verts[vb], verts[vc], vpoly, npoly) << 2;
			flags |= GetEdgeFlags(verts[vc], verts[va], vpoly, npoly) << 4;
			
			return flags;
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
		private int GetEdgeFlags(Vector3 va, Vector3 vb, Vector3[] vpoly, int npoly)
		{
			//true if edge is part of polygon
			float thrSqr = 0.001f * 0.001f;

			for (int i = 0, j = npoly - 1; i < npoly; j = i++)
			{
				Vector3 pt1 = va;
				Vector3 pt2 = vb;

				//the vertices pt1 (va) and pt2 (vb) are extremely close to the polygon edge
				if (MathHelper.DistanceFromPointToSegment2D(ref pt1, ref vpoly[j], ref vpoly[i]) < thrSqr && MathHelper.DistanceFromPointToSegment2D(ref pt2, ref vpoly[j], ref vpoly[i]) < thrSqr)
					return 1;
			}

			return 0;
		}

		/// <summary>
		/// Floodfill heightfield to get 2D height data, starting at vertex locations
		/// </summary>
		/// <param name="compactField">Original heightfield data</param>
		/// <param name="poly">Polygon vertices</param>
		/// <param name="npoly">Number of polygons</param>
		/// <param name="verts"></param>
		/// <param name="borderSize"></param>
		/// <param name="hp">Heightpatch which extracts heightfield data</param>
		private void GetHeightData(CompactHeightfield compactField, PolyMesh.Polygon[] poly, int polyStartIndex, int numVertsPerPoly, Vector3[] verts, int borderSize, ref HeightPatch hp)
		{
			for (int i = 0; i < hp.Data.Length; i++)
				hp.Data[i] = 0;

			List<int> stack = new List<int>();

			//9 x 2
			int[] offset = { 0,0, -1,-1, 0,-1, 
								1,-1, 1,0, 1,1, 
								0,1, -1,1, -1,0 };

			//use poly vertices as seed points
			for (int j = 0; j < numVertsPerPoly; j++)
			{
				int cx = 0, cz = 0, ci = -1;
				int dmin = UNSET_HEIGHT;

				for (int k = 0; k < 9; k++)
				{
					//get vertices and offset x and z coordinates depending on current drection
					int ax = (int)verts[poly[polyStartIndex].Vertices[j]].X + offset[k * 2 + 0];
					int ay = (int)verts[poly[polyStartIndex].Vertices[j]].Y;
					int az = (int)verts[poly[polyStartIndex].Vertices[j]].Z + offset[k * 2 + 1];

					//skip if out of bounds
					if (ax < hp.xmin || ax >= hp.xmin + hp.width ||
						az < hp.ymin || az >= hp.ymin + hp.height)
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
					stack.Add(cx);
					stack.Add(cz);
					stack.Add(ci);
				}
			}

			//find center of polygon using flood fill
			int pcx = 0, pcz = 0;
			for (int j = 0; j < numVertsPerPoly; j++)
			{
				pcx += (int)verts[poly[polyStartIndex].Vertices[j]].X;
				pcz += (int)verts[poly[polyStartIndex].Vertices[j]].Z;
			}

			pcx /= numVertsPerPoly;
			pcz /= numVertsPerPoly;

			//stack groups 3 elements as one part
			for (int i = 0; i < stack.Count; i += 3)
			{
				int cx = stack[i + 0];
				int cy = stack[i + 1];
				int idx = cx - hp.xmin + (cy - hp.ymin) * hp.width;
				hp.Data[idx] = 1;
			}

			//process the entire stack
			while (stack.Count > 0)
			{
				//since we add cx, cy, ci to stack, cx is at bottom and ci is at top
				//so the order we remove items is the opposite of the order we insert items
				int ci = stack[stack.Count - 1];
				stack.RemoveAt(stack.Count - 1);
				
				int cy = stack[stack.Count - 1];
				stack.RemoveAt(stack.Count - 1);
				
				int cx = stack[stack.Count - 1];
				stack.RemoveAt(stack.Count - 1);

				//check if close to center of polygon
				if (Math.Abs(cx - pcx) <= 1 && Math.Abs(cy - pcz) <= 1)
				{
					//clear the stack and add a new group
					stack.Clear();
					
					stack.Add(cx);
					stack.Add(cy);
					stack.Add(ci);
					break;
				}

				CompactSpan cs = compactField.Spans[ci];

				//check all four directions
				for (int d = 0; d < 4; d++)
				{
					Direction dir = (Direction)d;

					//skip if disconnected
					if (!cs.IsConnected(dir))
						continue;

					//get neighbor
					int ax = cx + dir.HorizontalOffset();
					int ay = cy + dir.VerticalOffset();

					//skip if out of bounds
					if (ax < hp.xmin || ax >= (hp.xmin + hp.width) ||
						ay < hp.ymin || ay >= (hp.ymin + hp.height))
						continue;

					if (hp.Data[ax - hp.xmin + (ay - hp.ymin) * hp.width] != 0)
						continue;

					//get the new index
					int ai = compactField.Cells[(ax + borderSize) + (ay + borderSize) * compactField.Width].StartIndex +
						CompactSpan.GetConnection(ref cs, dir);

					//save data
					int idx = ax - hp.xmin + (ay - hp.ymin) * hp.width;
					hp.Data[idx] = 1;

					//push to stack
					stack.Add(ax);
					stack.Add(ay);
					stack.Add(ai);
				}
			}

			//initialize to some default value 
			for (int i = 0; i < hp.Data.Length; i++)
				hp.Data[i] = 0xff;

			//mark start locations
			for (int i = 0; i < stack.Count; i += 3)
			{
				//get stack information
				int cx = stack[i + 0];
				int cy = stack[i + 1];
				int ci = stack[i + 2];

				//set new heightpatch data
				int idx = cx - hp.xmin + (cy - hp.ymin) * hp.width;
				CompactSpan cs = compactField.Spans[ci];
				hp.Data[idx] = cs.Minimum;
			}

			const int RETRACT_SIZE = 256;
			int head = 0;

			while (head * 3 < stack.Count)
			{
				int cx = stack[head * 3 + 0];
				int cy = stack[head * 3 + 1];
				int ci = stack[head * 3 + 2];
				head++;

				//stack is greater than the maximum size
				if (head >= RETRACT_SIZE)
				{
					head = 0;

					if (stack.Count > RETRACT_SIZE * 3)
					{
						//copy elements at the end of the stack to the beginning
						for (int i = 0; i < stack.Count - RETRACT_SIZE * 3; i++)
							stack[i] = stack[RETRACT_SIZE * 3 + i];

						//shrink stack
						stack.RemoveRange(RETRACT_SIZE * 3, stack.Count - RETRACT_SIZE * 3);
					}
				}

				//examine span
				CompactSpan cs = compactField.Spans[ci];
				
				//loop in all four directions
				for (int d = 0; d < 4; d++)
				{
					Direction dir = (Direction)d;

					//skip
					if (!cs.IsConnected(dir))
						continue;

					int ax = cx + dir.HorizontalOffset();
					int ay = cy + dir.VerticalOffset();

					if (ax < hp.xmin || ax >= (hp.xmin + hp.width) ||
						ay < hp.ymin || ay >= (hp.ymin + hp.height))
						continue;

					//only continue if height is unset
					if (hp.Data[ax - hp.xmin + (ay - hp.ymin) * hp.width] != UNSET_HEIGHT)
						continue;

					//get new span index
					int ai = compactField.Cells[(ax + borderSize) + (ay + borderSize) * compactField.Width].StartIndex +
						CompactSpan.GetConnection(ref cs, dir);

					//get new span
					CompactSpan ds = compactField.Spans[ai];
					
					//save
					int idx = ax - hp.xmin + (ay - hp.ymin) * hp.width;
					hp.Data[idx] = ds.Minimum;

					//add grouping to stack
					stack.Add(ax);
					stack.Add(ay);
					stack.Add(ai);
				}
			}
		}

		private void BuildPolyDetail(Vector3[] in_, int nin_, float sampleDist, float sampleMaxError, CompactHeightfield openField, HeightPatch hp,
			Vector3[] verts, ref int nverts, List<TriangleData> tris, List<EdgeInfo> edges, List<int> samples)
		{
			const int MAX_VERTS = 127;
			const int MAX_TRIS = 255;
			const int MAX_VERTS_PER_EDGE = 32;
			Vector3[] edge = new Vector3[MAX_VERTS_PER_EDGE + 1];
			float[] hull = new float[MAX_VERTS];
			int nhull = 0;

			nverts = 0;

			//fill up vertex array
			for (int i = 0; i < nin_; ++i)
			{
				verts[i] = in_[i];
			}

			nverts = nin_;

			float cs = openField.CellSize;
			float ics = 1.0f / cs;

			//tessellate outlines
			if (sampleDist > 0)
			{
				for (int i = 0, j = nin_ - 1; i < nin_; j = i++)
				{
					int vj = j;
					int vi = i;
					bool swapped = false;

					//make sure order is correct, otherwise swap data
					if (Math.Abs(in_[vj].X - in_[vi].X) < 1E-06f)
					{
						if (in_[vj].Z > in_[vi].Z)
						{
							float temp = in_[vj].Z;
							in_[vj].Z = in_[vi].Z;
							in_[vi].Z = temp;
							swapped = true;
						}
					}
					else
					{
						if (in_[vj].X > in_[vi].X)
						{
							float temp = in_[vj].X;
							in_[vj].X = in_[vi].X;
							in_[vi].X = temp;
							swapped = true;
						}
					}

					//create samples along the edge
					float dx = in_[vi].X - in_[vj].X;
					float dy = in_[vi].Y - in_[vj].Y;
					float dz = in_[vi].Z - in_[vj].Z;
					float d = (float)Math.Sqrt(dx * dx + dz * dz);
					int nn = 1 + (int)Math.Floor(d / sampleDist);
					if (nverts + nn >= MAX_VERTS)
						nn = MAX_VERTS - 1 - nverts;

					for (int k = 0; k <= nn; k++)
					{
						float u = (float)k / (float)nn;
						int pos = k;
						
						//edge seems to store vertex data
						edge[pos].X = in_[vj].X + dx * u;
						edge[pos].Y = in_[vj].Y + dy * u;
						edge[pos].Z = in_[vj].Z + dz * u;

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
							float dev = MathHelper.DistanceFromPointToSegment(ref edge[m], ref edge[va], ref edge[vb]);
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

					hull[nhull++] = j;

					//add new vertices
					if (swapped)
					{
						for (int k = nidx - 2; k > 0; k--)
						{
							verts[nverts] = edge[idx[k]];
							hull[nhull++] = nverts;
							nverts++;
						}
					}
					else
					{
						for (int k = 1; k < nidx - 1; k++)
						{
							verts[nverts] = edge[idx[k]];
							hull[nhull++] = nverts;
							nverts++;
						}
					}
				}
			}

			//tesselate base mesh
			edges.Clear();
			tris.Clear();
			
			DelaunayHull(nverts, verts, nhull, hull, tris, edges);

			if (tris.Count == 0)
			{
				//add default data
				for (int i = 2; i < nverts; i++)
					tris.Add(new TriangleData(0, i - 1, i));

				return;
			}

			if (sampleDist > 0)
			{
				//create sample locations
				BBox3 bounds = new BBox3();
				bounds.Min = in_[0];
				bounds.Max = in_[0];

				for (int i = 1; i < nin_; i++)
				{
					bounds.Min = Vector3.Min(bounds.Min, in_[i]);
					bounds.Max = Vector3.Max(bounds.Max, in_[i]); 
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
						Vector3 pt = new Vector3();
						pt.X = x * sampleDist;
						pt.Y = (bounds.Max.Y + bounds.Min.Y) * 0.5f;
						pt.Z = z * sampleDist;

						//make sure samples aren't too close to edge
						if (DistanceToPoly(nin_, in_, pt) > -sampleDist / 2)
							continue;

						samples.Add(x);
						samples.Add((int)GetHeight(pt, ics, openField.CellHeight, hp));
						samples.Add(z);
						samples.Add(0); //not added
					}
				}

				//added samples
				int nsamples = samples.Count / 4;
				for (int iter = 0; iter < nsamples; iter++)
				{
					if (nverts >= MAX_VERTS)
						break;

					//find sample with most error
					Vector3 bestpt = new Vector3();
					float bestd = 0;
					int besti = -1;

					for (int i = 0; i < nsamples; i++)
					{
						int s = i * 4;
						if (samples[s + 3] != 0)
							continue;

						Vector3 pt = new Vector3();

						//jitter sample location to remove effects of bad triangulation
						pt.X = samples[s + 0] * sampleDist + GetJitterX(i) * openField.CellSize * 0.1f;
						pt.Y = samples[s + 1] * openField.CellHeight;
						pt.Z = samples[s + 2] * sampleDist + GetJitterY(i) * openField.CellSize * 0.1f;
						float d = DistanceToTriMesh(pt, verts, tris);

						if (d < 0)
							continue;

						if (d > bestd)
						{
							bestd = d;
							besti = i;
							bestpt = pt;
						}
					}

					if (bestd <= sampleMaxError || besti == -1)
						break;

					samples[besti * 4 + 3] = 1;

					verts[nverts] = bestpt;
					nverts++;

					//create new triangulation
					edges.Clear();
					tris.Clear();
					DelaunayHull(nverts, verts, nhull, hull, tris, edges);
				}
			}

			int ntris = tris.Count;
			if (ntris > MAX_TRIS)
			{
				tris.RemoveRange(MAX_TRIS + 1, tris.Count - MAX_TRIS);
			}

			return;
		}

		private float GetJitterX(int i)
		{
			return (((i * 0x8da6b343) & 0xffff) / 65535.0f * 2.0f) - 1.0f;
		}

		private float GetJitterY(int i)
		{
			return (((i * 0xd8163841) & 0xffff) / 65535.0f * 2.0f) - 1.0f;
		}

		/// <summary>
		/// Use the HeightPatch data to obtain a height for a certain location.
		/// </summary>
		/// <param name="loc">Location</param>
		/// <param name="invCellSize">Reciprocal of cell size</param>
		/// <param name="cellHeight">Cell height</param>
		/// <param name="hp">Height patch</param>
		private float GetHeight(Vector3 loc, float invCellSize, float cellHeight, HeightPatch hp)
		{
			int ix = (int)Math.Floor(loc.X * invCellSize + 0.01f);
			int iz = (int)Math.Floor(loc.Z * invCellSize + 0.01f);
			ix = MathHelper.Clamp(ix - hp.xmin, 0, hp.width - 1);
			iz = MathHelper.Clamp(iz - hp.ymin, 0, hp.height - 1);
			int h = hp.Data[ix + iz * hp.width];

			if (h == UNSET_HEIGHT)
			{
				//go in counterclockwise direction starting from west, ending in northwest
				int[] off = { -1, 0,	-1, -1,		0, -1, 
							   1, -1,	 1, 0,		1, 1, 
							   0, 1,    -1, 1 };

				float dmin = float.MaxValue;

				for (int i = 0; i < 8; i++)
				{
					int nx = ix + off[i * 2 + 0];
					int nz = iz + off[i * 2 + 1];

					if (nx < 0 || nz < 0 || nx >= hp.width || nz >= hp.height)
						continue;

					int nh = hp.Data[nx + nz * hp.width];
					if (nh == UNSET_HEIGHT)
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
		/// <param name="npts">Number of vertices</param>
		/// <param name="pts">Vertex data (each vertex has 3 elements x,y,z)</param>
		/// <param name="nhull">?</param>
		/// <param name="hull">?</param>
		/// <param name="tris">The triangles formed.</param>
		/// <param name="edges">The edge connections formed.</param>
		private void DelaunayHull(int npts, Vector3[] pts, int nhull, float[] hull, List<TriangleData> tris, List<EdgeInfo> edges)
		{
			int nfaces = 0;
			int nedges = 0;
			int maxEdges = npts * 10;
			edges = new List<EdgeInfo>(maxEdges);

			//HACK should be an array, or algorithm should add edges as it goes
			for (int i = 0; i < maxEdges; i++)
			{
				EdgeInfo e = new EdgeInfo();
				e.EndPts = new int[2];
				e.EndPts[0] = 0;
				e.EndPts[1] = 0;
				e.LeftFace = 0;
				e.RightFace = 0;
				edges.Add(e);
			}

			for (int i = 0, j = nhull - 1; i < nhull; j = i++)
				AddEdge(edges, ref nedges, maxEdges, (int)hull[j], (int)hull[i], (int)EdgeValues.HULL, (int)EdgeValues.UNDEF);

			int currentEdge = 0;
			while (currentEdge < nedges)
			{
				if (edges[currentEdge].LeftFace == (int)EdgeValues.UNDEF)
					CompleteFacet(pts, npts, edges, ref nedges, maxEdges, ref nfaces, currentEdge);
				
				if (edges[currentEdge].RightFace == (int)EdgeValues.UNDEF)
					CompleteFacet(pts, npts, edges, ref nedges, maxEdges, ref nfaces, currentEdge);
				
				currentEdge++;
			}

			//create triangles
			tris = new List<TriangleData>();
			for (int i = 0; i < nfaces; i++)
				tris.Add(new TriangleData(-1, -1, -1, -1));

			for (int i = 0; i < nedges; i++)
			{
				if (edges[i].RightFace >= 0)
				{
					//left face
					int t = edges[i].RightFace;
					var tri = tris[t];
					
					if (tri.VertexHash0 == -1)
					{
						tri.VertexHash0 = edges[i].EndPts[0];
						tri.VertexHash1 = edges[i].EndPts[1];
					}
					else if (tri.VertexHash0 == edges[i].EndPts[1])
					{
						tri.VertexHash2 = edges[i].EndPts[0];
					}
					else if (tri.VertexHash1 == edges[i].EndPts[0])
					{
						tri.VertexHash2 = edges[i].EndPts[1];
					}

					tris[t] = tri;
				}

				if (edges[i].LeftFace >= 0)
				{
					//right
					int t = edges[i].LeftFace;
					var tri = tris[t];
					
					if (tri.VertexHash0 == -1)
					{
						tri.VertexHash0 = edges[i].EndPts[1];
						tri.VertexHash1 = edges[i].EndPts[0];
					}
					else if (tri.VertexHash0 == edges[i].EndPts[0])
					{
						tri.VertexHash2 = edges[i].EndPts[1];
					}
					else if (tri.VertexHash1 == edges[i].EndPts[1])
					{
						tri.VertexHash2 = edges[i].EndPts[0];
					}

					tris[t] = tri;
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

		private void CompleteFacet(Vector3[] pts, int npts, List<EdgeInfo> edges, ref int nedges, int maxEdges, ref int nfaces, int e)
		{
			const float EPS = 1e-5f;

			int edgePos = e; 

			//cache s and t
			int s, t;
			if (edges[edgePos].LeftFace == (int)EdgeValues.UNDEF)
			{
				s = edges[edgePos].EndPts[0];
				t = edges[edgePos].EndPts[1];
			}
			else if (edges[edgePos].RightFace == (int)EdgeValues.UNDEF)
			{
				s = edges[edgePos].EndPts[1];
				t = edges[edgePos].EndPts[0];
			}
			else
			{
				//edge already completed
				return;
			}

			//find best point on left edge
			int pt = npts;
			Vector3 c = new Vector3();
			float r = -1;
			float cross;
			for (int u = 0; u < npts; u++)
			{
				if (u == s || u == t)
					continue;

				Vector3Extensions.Cross2D(ref pts[s], ref pts[t], ref pts[u], out cross);
				if (cross > EPS)
				{
					if (r < 0)
					{
						//update circle now
						pt = u;
						CircumCircle(pts[s], pts[t], pts[u], c, ref r);
						continue;
					}

					float dx = c.X - pts[u].X;
					float dy = c.Z - pts[u].Z;
					float d = (float)Math.Sqrt(dx * dx + dy * dy);
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
						CircumCircle(pts[s], pts[t], pts[u], c, ref r);
					}
					else
					{
						//inside epsilon circumcircle
						if (OverlapEdges(pts, edges, nedges, s, u))
							continue;

						if (OverlapEdges(pts, edges, nedges, t, u))
							continue;

						//edge is valid
						pt = u;
						CircumCircle(pts[s], pts[t], pts[u], c, ref r);
					}
				}
			}

			//add new triangle or update edge if s-t on hull
			if (pt < npts)
			{
				UpdateLeftFace(edges, e, s, t, nfaces);

				e = FindEdge(edges, nedges, pt, s);
				if (e == (int)EdgeValues.UNDEF)
					AddEdge(edges, ref nedges, maxEdges, pt, s, nfaces, (int)EdgeValues.UNDEF);
				else
					UpdateLeftFace(edges, e, pt, s, nfaces);

				e = FindEdge(edges, nedges, t, pt);
				if (e == (int)EdgeValues.UNDEF)
					AddEdge(edges, ref nedges, maxEdges, t, pt, nfaces, (int)EdgeValues.UNDEF);
				else
					UpdateLeftFace(edges, e, t, pt, nfaces);

				nfaces++;
			}
			else
			{
				UpdateLeftFace(edges, e, s, t, (int)EdgeValues.HULL);
			}
		}

		private int AddEdge(List<EdgeInfo> edges, ref int nedges, int maxEdges, int s, int t, int l, int r)
		{
			if (nedges >= maxEdges)
			{
				return (int)EdgeValues.UNDEF;
			}

			//add edge
			int e = FindEdge(edges, nedges, s, t);
			if (e == (int)EdgeValues.UNDEF)
			{
				int edge = nedges;
				edges[edge].EndPts[0] = s;
				edges[edge].EndPts[1] = t;
				edges[edge].LeftFace = l;
				edges[edge].RightFace = r;
				return nedges++;
			}
			else
			{
				return (int)EdgeValues.UNDEF;
			}
		}

		private int FindEdge(List<EdgeInfo> edges, int nedges, int s, int t)
		{
			for (int i = 0; i < nedges; i++)
			{
				if ((edges[i].EndPts[0] == s && edges[i].EndPts[1] == t) || (edges[i].EndPts[0] == t && edges[i].EndPts[1] == s))
					return i;
			}

			return (int)EdgeValues.UNDEF;
		}

		private bool OverlapEdges(Vector3[] pts, List<EdgeInfo> edges, int nedges, int s1, int t1)
		{
			for (int i = 0; i < nedges; i++)
			{
				int s0 = edges[i].EndPts[0];
				int t0 = edges[i].EndPts[1];

				//same or connected edges do not overlap
				if (s0 == s1 || s0 == t1 || t0 == s1 || t0 == t1)
					continue;

				if (OverlapSegSeg2d(ref pts[s0], ref pts[t0], ref pts[s1], ref pts[t1]))
					return true;
			}

			return false;
		}

		private void UpdateLeftFace(List<EdgeInfo> edges, int edgePos, int s, int t, int f)
		{
			if (edges[edgePos].EndPts[0] == s && edges[edgePos].EndPts[1] == t && edges[edgePos].LeftFace == (int)EdgeValues.UNDEF)
				edges[edgePos].LeftFace = f;
			else if (edges[edgePos].EndPts[1] == s && edges[edgePos].EndPts[0] == t && edges[edgePos].LeftFace == (int)EdgeValues.UNDEF)
				edges[edgePos].RightFace = f;
		}

		private bool CircumCircle(Vector3 p1, Vector3 p2, Vector3 p3, Vector3 c, ref float r)
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

		private float DistanceToTriMesh(Vector3 p, Vector3[] verts, List<TriangleData> tris)
		{
			float dmin = float.MaxValue;

			for (int i = 0; i < tris.Count; i++)
			{
				int va = tris[i].VertexHash0;
				int vb = tris[i].VertexHash1;
				int vc = tris[i].VertexHash2;
				float d = DistancePointTri(p, verts[va], verts[vb], verts[vc]);
				if (d < dmin)
					dmin = d;
			}

			if (dmin == float.MaxValue)
				return -1;

			return dmin;
		}

		private float DistancePointTri(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
		{
			Vector3 v0 = c - a;
			Vector3 v1 = b - a;
			Vector3 v2 = p - a;

			float dot00, dot01, dot02, dot11, dot12;

			Vector3Extensions.Dot2D(ref v0, ref v0, out dot00);
			Vector3Extensions.Dot2D(ref v0, ref v1, out dot01);
			Vector3Extensions.Dot2D(ref v0, ref v2, out dot02);
			Vector3Extensions.Dot2D(ref v1, ref v1, out dot11);
			Vector3Extensions.Dot2D(ref v1, ref v2, out dot12);

			//compute barycentric coordinates
			float invDenom = 1.0f / (dot00 * dot11 - dot01 * dot01);
			float u = (dot11 * dot02 - dot01 * dot12) * invDenom;
			float v = (dot00 * dot12 - dot01 * dot02) * invDenom;

			//if point lies inside triangle, return interpolated y-coordinate
			float EPS = 1E-4f;
			if (u >= -EPS && v >= -EPS && (u + v) <= 1 + EPS)
			{
				float y = a.Y + v0.Y * u + v1.Y * v;
				return Math.Abs(y - p.Y);
			}

			return float.MaxValue;
		}

		private float DistanceToPoly(int nvert, Vector3[] verts, Vector3 p)
		{
			float dmin = float.MaxValue;
			bool c = false;

			for (int i = 0, j = nvert - 1; i < nvert; j = i++)
			{
				int vi = i;
				int vj = j;

				if (((verts[vi].Z > p.Z) != (verts[vj].Z > p.Z)) &&
					(p.X < (verts[vj].X - verts[vi].X) * (p.Z - verts[vi].Z) / (verts[vj].Z - verts[vi].Z) + verts[vi].X))
				{
					c = !c;
				}

				dmin = Math.Min(dmin, MathHelper.DistanceFromPointToSegment2D(ref p, ref verts[vj], ref verts[vi]));
			}

			return c ? -dmin : dmin;
		}
	
		private bool OverlapSegSeg2d(ref Vector3 a, ref Vector3 b, ref Vector3 c, ref Vector3 d)
		{
			float a1, a2, a3;

			Vector3Extensions.Cross2D(ref a, ref b, ref d, out a1);
			Vector3Extensions.Cross2D(ref a, ref b, ref c, out a2);

			if (a1 * a2 < 0.0f)
			{
				Vector3Extensions.Cross2D(ref c, ref d, ref a, out a3);
				float a4 = a3 + a2 - a1;
				
				if (a3 * a4 < 0.0f)
					return true;
			}

			return false;
		}

		/// <summary>
		/// Determines whether an edge has been created or not
		/// </summary>
		private enum EdgeValues : int
		{
			UNDEF = -1,
			HULL = -2
		}

		/// <summary>
		/// Store height data, which will later be merged with the NavMesh
		/// </summary>
		private class HeightPatch
		{
			public HeightPatch()
			{
				xmin = 0;
				ymin = 0;
				width = 0;
				height = 0;
			}

			public int[] Data;
			public int xmin, ymin, width, height;
		}
	}
}
