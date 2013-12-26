#region License
/**
 * Copyright (c) 2013 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
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
	public class NavMeshDetail
	{
		public const int UNSET_HEIGHT = 0xffff;

		private int nmeshes;
		private int nverts;
		private int ntris;

		//mesh info contains number of vertices and triangles
		public struct MeshInfo
		{
			public int OldNumVerts;
			public int NewNumVerts;
			public int OldNumTris;
			public int NewNumTris;
		}
		private MeshInfo[] meshes;
		
		//each vertex is basically a vector3 (has x, y, z coordinates)
		private Vector3[] verts;

		//triangle info contains three vertex hashes and a flag
		public struct TrisInfo
		{
			public int [] VertexHash;
			public int Flag; //indicates which 3 vertices are part of the polygon
		}
		private TrisInfo[] tris;

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
		public NavMeshDetail(NavMesh mesh, CompactHeightfield openField, float sampleDist, float sampleMaxError)
		{
			if (mesh.NVerts == 0 || mesh.NPolys == 0)
				return;

			Vector3 origin = mesh.Bounds.Min;

			List<EdgeInfo> edges = new List<EdgeInfo>(16);
			List<TrisInfo> tris = new List<TrisInfo>(128);
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
					if (mesh.Polys[i].Vertices[j] == NavMesh.MESH_NULL_IDX)
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
			this.meshes = new MeshInfo[this.nmeshes];

			int vcap = nPolyVerts + nPolyVerts / 2;
			int tcap = vcap * 2;

			this.nverts = 0;
			this.verts = new Vector3[vcap];

			this.ntris = 0;
			this.tris = new TrisInfo[tcap];

			for (int i = 0; i < mesh.NPolys; i++)
			{
				//store polygon vertices for processing
				int npoly = 0;
				for (int j = 0; j < mesh.NumVertsPerPoly; j++)
				{
					if (mesh.Polys[i].Vertices[j] == NavMesh.MESH_NULL_IDX)
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

				this.meshes[i].OldNumVerts = this.nverts;
				this.meshes[i].NewNumVerts = nverts;
				this.meshes[i].OldNumTris = this.ntris;
				this.meshes[i].NewNumTris = ntris;

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

					TrisInfo[] newt = new TrisInfo[tcap];

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
					this.tris[this.ntris].VertexHash = new int[3];
					this.tris[this.ntris].VertexHash = tris[t].VertexHash;
					this.tris[this.ntris].Flag = GetTriFlags(verts, tris[t].VertexHash[0], tris[t].VertexHash[1], tris[t].VertexHash[2], poly, npoly);
					this.ntris++;
				}
			}
		}

		public int NMeshes { get { return nmeshes; } }

		public int NVerts { get { return nverts; } }

		public int NTris { get { return ntris; } }

		public MeshInfo[] Meshes { get { return meshes; } }

		public Vector3[] Verts { get { return verts; } }

		public TrisInfo[] Tris { get { return tris; } }


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
			flags |= GetEdgeFlags(verts, va, vb, vpoly, npoly) << 0;
			flags |= GetEdgeFlags(verts, vb, vc, vpoly, npoly) << 2;
			flags |= GetEdgeFlags(verts, vc, va, vpoly, npoly) << 4;
			
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
		private int GetEdgeFlags(Vector3[] verts, int va, int vb, Vector3[] vpoly, int npoly)
		{
			//true if edge is part of polygon
			float thrSqr = 0.001f * 0.001f;

			for (int i = 0, j = npoly - 1; i < npoly; j = i++)
			{
				Vector3 pt1 = verts[va];
				Vector3 pt2 = verts[vb];

				//the vertices pt1 (va) and pt2 (vb) are extremely close to the polygon edge
				if (DistancePointSegment2d(pt1 , vpoly, j, i) < thrSqr && DistancePointSegment2d(pt2, vpoly, j, i) < thrSqr)
					return 1;
			}

			return 0;
		}

		/// <summary>
		/// Floodfill heightfield to get 2D height data, starting at vertex locations
		/// </summary>
		/// <param name="openField">Original heightfield data</param>
		/// <param name="poly">Polygon vertices</param>
		/// <param name="npoly">Number of polygons</param>
		/// <param name="verts"></param>
		/// <param name="borderSize"></param>
		/// <param name="hp">Heightpatch which extracts heightfield data</param>
		private void GetHeightData(CompactHeightfield openField, NavMesh.Polygon[] poly, int polyStartIndex, int numVertsPerPoly, Vector3[] verts, int borderSize, ref HeightPatch hp)
		{
			for (int i = 0; i < hp.Data.Length; i++)
				hp.Data[i] = 0;

			List<int> stack = new List<int>();

			//9 x 2
			int [] offset = { 0,0, -1,-1, 0,-1, 
								1,-1, 1,0, 1,1, 
								0,1, -1,1, -1,0};

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
					CompactCell c = openField.Cells[(ax + borderSize) + (az + borderSize) * openField.Width];
					
					//loop through all the spans
					for (int i = c.StartIndex, end = c.StartIndex + c.Count; i < end; i++)
					{
						CompactSpan s = openField.Spans[i];
						
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

				CompactSpan cs = openField.Spans[ci];

				//check all four directions
				for (int dir = 0; dir < 4; dir++)
				{
					//skip if disconnected
					if (CompactSpan.GetConnection(dir, ref cs) == CompactSpan.NotConnected)
						continue;

					//get neighbor
					int ax = cx + MathHelper.GetDirOffsetX(dir);
					int ay = cy + MathHelper.GetDirOffsetY(dir);

					//skip if out of bounds
					if (ax < hp.xmin || ax >= (hp.xmin + hp.width) ||
						ay < hp.ymin || ay >= (hp.ymin + hp.height))
						continue;

					if (hp.Data[ax - hp.xmin + (ay - hp.ymin) * hp.width] != 0)
						continue;

					//get the new index
					int ai = openField.Cells[(ax + borderSize) + (ay + borderSize) * openField.Width].StartIndex +
						CompactSpan.GetConnection(dir, ref cs);

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
				CompactSpan cs = openField.Spans[ci];
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
				CompactSpan cs = openField.Spans[ci];
				
				//loop in all four directions
				for (int dir = 0; dir < 4; dir++)
				{
					//skip
					if (CompactSpan.GetConnection(dir, ref cs) == CompactSpan.NotConnected)
						continue;

					int ax = cx + MathHelper.GetDirOffsetX(dir);
					int ay = cy + MathHelper.GetDirOffsetY(dir);

					if (ax < hp.xmin || ax >= (hp.xmin + hp.width) ||
						ay < hp.ymin || ay >= (hp.ymin + hp.height))
						continue;

					//only continue if height is unset
					if (hp.Data[ax - hp.xmin + (ay - hp.ymin) * hp.width] != UNSET_HEIGHT)
						continue;

					//get new span index
					int ai = openField.Cells[(ax + borderSize) + (ay + borderSize) * openField.Width].StartIndex +
						CompactSpan.GetConnection(dir, ref cs);

					//get new span
					CompactSpan ds = openField.Spans[ai];
					
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
			Vector3[] verts, ref int nverts, List<TrisInfo> tris, List<EdgeInfo> edges, List<int> samples)
		{
			const int MAX_VERTS = 127;
			const int MAX_TRIS = 255;
			const int MAX_VERTS_PER_EDGE = 32;
			Vector3[] edge = new Vector3[MAX_VERTS_PER_EDGE + 1];
			float[] hull = new float [MAX_VERTS];
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

					for (int k = 0; k < nidx - 1; )
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
							float dev = DistancePointSegment(edge, m, va, vb);
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
				{
					TrisInfo newTris = new TrisInfo();
					newTris.VertexHash = new int[3];
					newTris.VertexHash[0] = 0;
					newTris.VertexHash[1] = i - 1;
					newTris.VertexHash[2] = i;
					newTris.Flag = 0;
					tris.Add(newTris);
				}

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
		private void DelaunayHull(int npts, Vector3[] pts, int nhull, float[] hull, List<TrisInfo> tris, List<EdgeInfo> edges)
		{
			int nfaces = 0;
			int nedges = 0;
			int maxEdges = npts * 10;
			edges = new List<EdgeInfo>(maxEdges);
			for (int i = 0; i < maxEdges; i++) //HACK should be an array, or algorithm should add edges as it goes
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
			tris = new List<TrisInfo>();
			for (int i = 0; i < nfaces; i++)
			{
				TrisInfo newTris = new TrisInfo();
				newTris.VertexHash = new int[3];
				newTris.VertexHash[0] = -1;
				newTris.VertexHash[1] = -1;
				newTris.VertexHash[2] = -1;
				newTris.Flag = -1;
				tris.Add(newTris);
			}

			for (int i = 0; i < nedges; i++)
			{
				if (edges[i].RightFace >= 0)
				{
					//left face
					int t = edges[i].RightFace;
					
					if (tris[t].VertexHash[0] == -1)
					{
						tris[t].VertexHash[0] = edges[i].EndPts[0];
						tris[t].VertexHash[1] = edges[i].EndPts[1];
					}
					else if (tris[t].VertexHash[0] == edges[i].EndPts[1])
					{
						tris[t].VertexHash[2] = edges[i].EndPts[0];
					}
					else if (tris[t].VertexHash[1] == edges[i].EndPts[0])
					{
						tris[t].VertexHash[2] = edges[i].EndPts[1];
					}
				}

				if (edges[i].LeftFace >= 0)
				{
					//right
					int t = edges[i].LeftFace;
					
					if (tris[t].VertexHash[0] == -1)
					{
						tris[t].VertexHash[0] = edges[i].EndPts[1];
						tris[t].VertexHash[1] = edges[i].EndPts[0];
					}
					else if (tris[t].VertexHash[0] == edges[i].EndPts[0])
					{
						tris[t].VertexHash[2] = edges[i].EndPts[1];
					}
					else if (tris[t].VertexHash[1] == edges[i].EndPts[1])
					{
						tris[t].VertexHash[2] = edges[i].EndPts[0];
					}
				}
			}

			for (int i = 0; i < tris.Count; i++)
			{
				int t = i;
				if (tris[t].VertexHash[0] == -1 || tris[t].VertexHash[1] == -1 || tris[t].VertexHash[2] == -1)
				{
					//remove dangling face
					tris[t] = tris[tris.Count - 1];
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
			for (int u = 0; u < npts; u++)
			{
				if (u == s || u == t)
					continue;

				if (VCross2(pts, s, t, u) > EPS)
				{
					if (r < 0)
					{
						//update circle now
						pt = u;
						CircumCircle(pts, s, t, u, c, ref r);
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
						CircumCircle(pts, s, t, u, c, ref r);
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
						CircumCircle(pts, s, t, u, c, ref r);
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

				if (OverlapSegSeg2d(pts, s0, t0, s1, t1) == true)
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

		private bool CircumCircle(Vector3[] pts, int p1, int p2, int p3, Vector3 c, ref float r)
		{
			float EPS = 1e-6f;
			float cp = VCross2(pts, p1, p2, p3);

			if (Math.Abs(cp) > EPS)
			{
				//find magnitude of each point
				float p1Sq = VDot2(pts[p1], pts[p1]);
				float p2Sq = VDot2(pts[p2], pts[p2]);
				float p3Sq = VDot2(pts[p3], pts[p3]);

				c.X = (p1Sq * (pts[p2].Z - pts[p3].Z) + p2Sq * (pts[p3].Z - pts[p1].Z) + p3Sq * (pts[p1].Z - pts[p2].Z)) / (2 * cp);
				c.Z = (p1Sq * (pts[p2].X - pts[p3].X) + p2Sq * (pts[p3].X - pts[p1].X) + p3Sq * (pts[p1].X - pts[p2].X)) / (2 * cp);

				float dx = c.X - pts[p1].X;
				float dy = c.Z - pts[p1].Z;
				r = (float)Math.Sqrt(dx * dx + dy * dy);
				return true;
			}

			c.X = pts[p1].X;
			c.Z = pts[p1].Z;
			r = 0;
			return false;
		}

		private float DistanceToTriMesh(Vector3 p, Vector3[] verts, List<TrisInfo> tris)
		{
			float dmin = float.MaxValue;

			for (int i = 0; i < tris.Count; i++)
			{
				int va = tris[i].VertexHash[0];
				int vb = tris[i].VertexHash[1];
				int vc = tris[i].VertexHash[2];
				float d = DistancePointTri(p, verts, va, vb, vc);
				if (d < dmin)
					dmin = d;
			}

			if (dmin == float.MaxValue)
				return -1;

			return dmin;
		}

		private float DistancePointTri(Vector3 p, Vector3[] verts, int a, int b, int c)
		{
			Vector3 v0 = new Vector3();
			Vector3 v1 = new Vector3();
			Vector3 v2 = new Vector3();

			v0 = verts[c] - verts[a];
			v1 = verts[b] - verts[a];
			v2 = p - verts[a];

			float dot00 = VDot2(v0, v0);
			float dot01 = VDot2(v0, v1);
			float dot02 = VDot2(v0, v2);
			float dot11 = VDot2(v1, v1);
			float dot12 = VDot2(v1, v2);

			//compute barycentric coordinates
			float invDenom = 1.0f / (dot00 * dot11 - dot01 * dot01);
			float u = (dot11 * dot02 - dot01 * dot12) * invDenom;
			float v = (dot00 * dot12 - dot01 * dot02) * invDenom;

			//if point lies inside triangle, return interpolated y-coordinate
			float EPS = 1E-4f;
			if (u >= -EPS && v >= -EPS && (u + v) <= 1 + EPS)
			{
				float y = verts[a].Y + v0.Y * u + v1.Y * v;
				return Math.Abs(y - p[1]);
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

				dmin = Math.Min(dmin, DistancePointSegment2d(p, verts, vj, vi));
			}

			return c ? -dmin : dmin;
		}

		/// <summary>
		/// Finds the shortest distance between a point and a segment in the 3d plane.
		/// </summary>
		/// <param name="verts"></param>
		/// <param name="pt">Individual point</param>
		/// <param name="p">One end of a segment</param>
		/// <param name="q">Other end of segment</param>
		/// <returns></returns>
		private float DistancePointSegment(Vector3[] verts, int pt, int p, int q)
		{
			//distance from P to Q
			Vector3 pq = verts[q] - verts[p];

			//disance from P to the lone point
			float dx = verts[pt].X - verts[p].X;
			float dy = verts[pt].Y - verts[p].Y;
			float dz = verts[pt].Z - verts[p].Z;
		
			float segmentMagnitude = pq.LengthSquared();
			float t = pq.X * dx + pq.Y * dy + pq.Z * dz;

			if (segmentMagnitude > 0)
				t /= segmentMagnitude;

			//keep t between 0 and 1
			if (t < 0)
				t = 0;
			else if (t > 1)
				t = 1;

			dx = verts[p].X + t * pq.X - verts[pt].X;
			dy = verts[p].Y + t * pq.Y - verts[pt].Y;
			dz = verts[p].Z + t * pq.Z - verts[pt].Z;

			return dx * dx + dy * dy + dz * dz;
		}

		/// <summary>
		/// Find the shortest distance between a point and a segment in the 2D xz-plane.
		/// </summary>
		/// <param name="pt">Lone point</param>
		/// <param name="verts">Vertices that store P and Q</param>
		/// <param name="p">First vertex</param>
		/// <param name="q">Second vertex</param>
		/// <returns></returns>
		private float DistancePointSegment2d(Vector3 pt, Vector3[] verts, int p, int q)
		{
			//distance from P to Q in the xz plane
			float pqx = verts[q].X - verts[p].X;
			float pqz = verts[q].Z - verts[p].Z;

			//distance from P to lone point in xz plane
			float dx = pt.X - verts[p].X;
			float dz = pt.Z - verts[p].Z;

			float segmentMagnitude = pqx * pqx + pqz * pqz;
			float t = pqx * dx + pqz * dz;

			if (segmentMagnitude > 0)
				t /= segmentMagnitude;

			//keep t between 0 and 1
			if (t < 0)
				t = 0;
			else if (t > 1)
				t = 1;

			dx = verts[p].X + t * pqx - pt.X;
			dz = verts[p].Z + t * pqz - pt.Z;

			return dx * dx + dz * dz;
		}

		private bool OverlapSegSeg2d(Vector3[] pts, int a, int b, int c, int d)
		{
			float a1 = VCross2(pts, a, b, d);
			float a2 = VCross2(pts, a, b, c);

			if (a1 * a2 < 0.0f)
			{
				float a3 = VCross2(pts, c, d, a);
				float a4 = a3 + a2 - a1;
				
				if (a3 * a4 < 0.0f)
					return true;
			}

			return false;
		}

		private float VCross2(Vector3[] pts, int p1, int p2, int p3)
		{
			float u1 = pts[p2].X - pts[p1].X;
			float v1 = pts[p2].Z - pts[p1].Z;
			float u2 = pts[p3].X - pts[p1].X;
			float v2 = pts[p3].Z - pts[p1].Z;

			return u1 * v2 - v1 * u2;
		}

		private float VDot2(Vector3 v1, Vector3 v2)
		{
			//dot product of (x1, z1) and (x2, z2) is x1 * x2 + z1 * z2 
			return v1.X * v2.X + v1.Z * v2.Z;
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
