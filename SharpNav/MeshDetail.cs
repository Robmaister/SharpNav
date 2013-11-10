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
	class MeshDetail
	{
		public const int UNSET_HEIGHT = 0xffff;

		private int nmeshes;
		private int nverts;
		private int ntris;
		private int[] meshes;
		private int[] verts;
		private int[] tris;

		public MeshDetail(Mesh mesh, CompactHeightfield openField, float sampleDist, float sampleMaxError)
		{
			if (mesh.NVerts == 0 || mesh.NPolys == 0)
				return;

			List<int> edges = new List<int>(64);
			List<int> tris = new List<int>(512);
			List<int> samples = new List<int>(512);
			HeightPatch hp = new HeightPatch();
			int nPolyVerts = 0;
			int maxhw = 0, maxhh = 0;

			int[] bounds = new int[mesh.NPolys * 4];
			float[] poly = new float[mesh.NumVertsPerPoly * 3]; 

			//find max size for polygon area
			for (int i = 0; i < mesh.NPolys; i++)
			{
				int p = i * mesh.NumVertsPerPoly * 2;
				int xmin, xmax, ymin, ymax;

				xmin = bounds[i * 4 + 0] = openField.Width;
				xmax = bounds[i * 4 + 1] = 0;
				ymin = bounds[i * 4 + 2] = openField.Height;
				ymax = bounds[i * 4 + 3] = 0;

				for (int j = 0; j < mesh.NumVertsPerPoly; j++)
				{
					if (mesh.Polys[p + j] == Mesh.MESH_NULL_IDX)
						break;

					int v = mesh.Polys[p + j] * 3;

					xmin = bounds[i * 4 + 0] = Math.Min(xmin, mesh.Verts[v + 0]);
					xmax = bounds[i * 4 + 1] = Math.Max(xmax, mesh.Verts[v + 0]);
					ymin = bounds[i * 4 + 2] = Math.Min(ymin, mesh.Verts[v + 2]);
					ymax = bounds[i * 4 + 3] = Math.Max(ymax, mesh.Verts[v + 2]);

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

			hp.data = new int[maxhw * maxhh];

			this.nmeshes = mesh.NPolys;
			this.meshes = new int[this.nmeshes * 4];

			int vcap = nPolyVerts + nPolyVerts / 2;
			int tcap = vcap * 2;

			this.nverts = 0;
			this.verts = new int[vcap * 3];

			this.ntris = 0;
			this.tris = new int[tcap * 4];

			for (int i = 0; i < mesh.NPolys; i++)
			{
				int p = i * mesh.NumVertsPerPoly * 2;

				//store polygon vertices for processing
				int npoly = 0;
				for (int j = 0; j < mesh.NumVertsPerPoly; j++)
				{
					if (mesh.Polys[p + j] == Mesh.MESH_NULL_IDX)
						break;

					int v = mesh.Polys[p + j] * 3;
					poly[j * 3 + 0] = mesh.Verts[v + 0] * mesh.CellSize;
					poly[j * 3 + 1] = mesh.Verts[v + 1] * mesh.CellHeight;
					poly[j * 3 + 2] = mesh.Verts[v + 2] * mesh.CellSize;
					npoly++;
				}

				//get height data from area of polygon
				hp.xmin = bounds[i * 4 + 0];
				hp.ymin = bounds[i * 4 + 2];
				hp.width = bounds[i * 4 + 1] - bounds[i * 4 + 0];
				hp.height = bounds[i * 4 + 3] - bounds[i * 4 + 2];
				GetHeightData(openField, poly, i * mesh.NumVertsPerPoly * 2, npoly, mesh.Verts, mesh.BorderSize, ref hp);

				int verts = 0;
				BuildPolyDetail(poly, npoly, sampleDist, sampleMaxError, openField, hp, verts, nverts, tris, edges, samples);
			}
		}

		/// <summary>
		/// Floodfill heightfield to get 2D height data, starting at vertex locations
		/// </summary>
		/// <param name="openField"></param>
		/// <param name="poly"></param>
		/// <param name="npoly"></param>
		/// <param name="verts"></param>
		/// <param name="borderSize"></param>
		/// <param name="hp"></param>
		public void GetHeightData(CompactHeightfield openField, float[] poly, int polyStartIndex, int npoly, int[] verts, int borderSize, ref HeightPatch hp)
		{
			for (int i = 0; i < hp.data.Length; i++)
				hp.data[i] = 0;

			List<int> stack = new List<int>();

			//9 x 2
			int [] offset = { 0,0, -1,-1, 0,-1, 
								1,-1, 1,0, 1,1, 
								0,1 -1,1, -1,0};

			//use poly vertices as seed points
			for (int j = 0; j < npoly; j++)
			{
				int cx = 0, cz = 0, ci = -1;
				int dmin = UNSET_HEIGHT;

				for (int k = 0; k < 9; k++)
				{
					int ax = verts[(int)poly[polyStartIndex + j] * 3 + 0] + offset[k * 2 + 0];
					int ay = verts[(int)poly[polyStartIndex + j] * 3 + 1];
					int az = verts[(int)poly[polyStartIndex + j] * 3 + 2] + offset[k * 2 + 1];

					if (ax < hp.xmin || ax >= hp.xmin + hp.width ||
						az < hp.ymin || az >= hp.ymin + hp.height)
						continue;

					CompactCell c = openField.Cells[(ax + borderSize) + (az + borderSize) * openField.Width];
					for (int i = c.StartIndex, end = c.StartIndex + c.Count; i < end; i++)
					{
						CompactSpan s = openField.Spans[i];
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

				if (ci != -1)
				{
					stack.Add(cx);
					stack.Add(cz);
					stack.Add(ci);
				}
			}

			//find center of polygon using flood fill
			int pcx = 0, pcz = 0;
			for (int j = 0; j < npoly; j++)
			{
				pcx += verts[(int)poly[polyStartIndex + j] * 3 + 0];
				pcz += verts[(int)poly[polyStartIndex + j] * 3 + 2];
			}
			pcx /= npoly;
			pcz /= npoly;

			for (int i = 0; i < stack.Count; i += 3)
			{
				int cx = stack[i + 0];
				int cy = stack[i + 1];
				int idx = cx - hp.xmin + (cy - hp.ymin) * hp.width;
				hp.data[idx] = 1;
			}

			while (stack.Count > 0)
			{
				int ci = stack[stack.Count - 1];
				stack.RemoveAt(stack.Count - 1);
				int cy = stack[stack.Count - 1];
				stack.RemoveAt(stack.Count - 1);
				int cx = stack[stack.Count - 1];
				stack.RemoveAt(stack.Count - 1);

				//check if close to center of polygon
				if (Math.Abs(cx - pcx) <= 1 && Math.Abs(cy - pcz) <= 1)
				{
					stack = new List<int>();
					stack.Add(cx);
					stack.Add(cy);
					stack.Add(ci);
					break;
				}

				CompactSpan cs = openField.Spans[ci];

				for (int dir = 0; dir < 4; dir++)
				{
					if (CompactSpan.GetConnection(dir, ref cs) == CompactHeightfield.NotConnected)
						continue;

					int ax = cx + MathHelper.GetDirOffsetX(dir);
					int ay = cy + MathHelper.GetDirOffsetY(dir);

					if (ax < hp.xmin || ax >= (hp.xmin + hp.width) ||
						ay < hp.ymin || ay >= (hp.ymin + hp.height))
						continue;

					if (hp.data[ax - hp.xmin + (ay - hp.ymin) * hp.width] != 0)
						continue;

					int ai = openField.Cells[(ax + borderSize) + (ay + borderSize) * openField.Width].StartIndex +
						CompactSpan.GetConnection(dir, ref cs);

					int idx = ax - hp.xmin + (ay - hp.ymin) * hp.width;
					hp.data[idx] = 1;

					stack.Add(ax);
					stack.Add(ay);
					stack.Add(ai);
				}
			}

			for (int i = 0; i < hp.data.Length; i++)
				hp.data[i] = 0xff;

			//mark start locations
			for (int i = 0; i < stack.Count; i += 3)
			{
				int cx = stack[i + 0];
				int cy = stack[i + 1];
				int ci = stack[i + 2];
				int idx = cx - hp.xmin + (cy - hp.ymin) * hp.width;
				CompactSpan cs = openField.Spans[ci];
				hp.data[idx] = cs.Minimum;
			}

			const int RETRACT_SIZE = 256;
			int head = 0;

			while (head * 3 < stack.Count)
			{
				int cx = stack[head * 3 + 0];
				int cy = stack[head * 3 + 1];
				int ci = stack[head * 3 + 2];
				head++;
				if (head >= RETRACT_SIZE)
				{
					head = 0;
					if (stack.Count > RETRACT_SIZE * 3)
					{
						stack.RemoveRange(0, RETRACT_SIZE * 3 - 1);
					}
				}

				CompactSpan cs = openField.Spans[ci];
				for (int dir = 0; dir < 4; dir++)
				{
					if (CompactSpan.GetConnection(dir, ref cs) == CompactHeightfield.NotConnected)
						continue;

					int ax = cx + MathHelper.GetDirOffsetX(dir);
					int ay = cy + MathHelper.GetDirOffsetY(dir);

					if (ax < hp.xmin || ax >= (hp.xmin + hp.width) ||
						ay < hp.ymin || ay >= (hp.ymin + hp.height))
						continue;

					if (hp.data[ax - hp.xmin + (ay - hp.ymin) * hp.width] != UNSET_HEIGHT)
						continue;

					int ai = openField.Cells[(ax + borderSize) + (ay + borderSize) * openField.Width].StartIndex +
						CompactSpan.GetConnection(dir, ref cs);

					CompactSpan ds = openField.Spans[ai];
					int idx = ax - hp.xmin + (ay - hp.ymin) * hp.width;
					hp.data[idx] = ds.Minimum;

					stack.Add(ax);
					stack.Add(ay);
					stack.Add(ai);
				}
			}

		}

		public void BuildPolyDetail(float[] in_, int nin_, float sampleDist, float sampleMaxError, CompactHeightfield openField, HeightPatch hp,
			float[] verts, ref int nverts, List<int> tris, List<int> edges, List<int> samples)
		{
			const int MAX_VERTS = 127;
			const int MAX_TRIS = 255;
			const int MAX_VERTS_PER_EDGE = 32;
			float[] edge = new float[(MAX_VERTS_PER_EDGE + 1) * 3];
			int[] hull = new int [MAX_VERTS];
			int nhull = 0;

			nverts = 0;

			for (int i = 0; i < nin_; ++i)
			{
				verts[i * 3 + 0] = in_[i * 3 + 0];
				verts[i * 3 + 1] = in_[i * 3 + 1];
				verts[i * 3 + 2] = in_[i * 3 + 2];
			}
			nverts = nin_;

			float cs = openField.CellSize;
			float ics = 1.0f / cs;

			//tessellate outlines
			if (sampleDist > 0)
			{
				for (int i = 0, j = nin_ - 1; i < nin_; j = i++)
				{
					int vj = j * 3;
					int vi = i * 3;
					bool swapped = false;

					//make sure order is correct
					if (Math.Abs(in_[vj + 0] - in_[vi + 0]) < 1E-06f)
					{
						if (in_[vj + 2] > in_[vi + 2])
						{
							float temp = in_[vj + 2];
							in_[vj + 2] = in_[vi + 2];
							in_[vi + 2] = temp;
							swapped = true;
						}
					}
					else
					{
						if (in_[vj + 0] > in_[vi + 0])
						{
							float temp = in_[vj + 0];
							in_[vj + 0] = in_[vi + 0];
							in_[vi + 0] = temp;
							swapped = true;
						}
					}

					//create samples along the edge
					float dx = in_[vi + 0] - in_[vj + 0];
					float dy = in_[vi + 1] - in_[vj + 1];
					float dz = in_[vi + 2] - in_[vj + 2];
					float d = (float)Math.Sqrt(dx * dx + dz * dz);
					int nn = 1 + (int)Math.Floor(d / sampleDist);
					if (nverts + nn >= MAX_VERTS)
						nn = MAX_VERTS - 1 - nverts;

					for (int k = 0; k <= nn; k++)
					{
						float u = (float)k / (float)nn;
						int pos = k * 3;
						edge[pos + 0] = in_[vj + 0] + dx * u;
						edge[pos + 1] = in_[vj + 1] + dy * u;
						edge[pos + 2] = in_[vj + 2] + dz * u;
						edge[pos + 1] = GetHeight(edge[pos + 0], edge[pos + 1], edge[pos + 2], ics, openField.CellHeight, hp) * openField.CellHeight;
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
						int va = a * 3;
						int vb = b * 3;

						//find maximum deviation along segment
						float maxd = 0;
						int maxi = 0;
						for (int m = a + 1; m < b; m++)
						{
							float dev = DistancePointSegment(edge, m * 3, va, vb);
							if (dev > maxd)
							{
								maxd = dev;
								maxi = m;
							}
						}

						if (maxi != -1 && maxd > (sampleMaxError * sampleMaxError))
						{
							for (int m = nidx; m > k; m--)
								idx[m] = idx[m - 1];

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
							verts[nverts * 3 + 0] = edge[idx[k] * 3 + 0];
							verts[nverts * 3 + 1] = edge[idx[k] * 3 + 1];
							verts[nverts * 3 + 2] = edge[idx[k] * 3 + 2];
							hull[nhull++] = nverts;
							nverts++;
						}
					}
					else
					{
						for (int k = 1; k < nidx - 1; k++)
						{
							verts[nverts * 3 + 0] = edge[idx[k] * 3 + 0];
							verts[nverts * 3 + 1] = edge[idx[k] * 3 + 1];
							verts[nverts * 3 + 2] = edge[idx[k] * 3 + 2];
							hull[nhull++] = nverts;
							nverts++;
						}
					}
				}
			}

			//TODO: tesselate base mesh
			edges.Clear();
			tris.Clear();
		}

		/// <summary>
		/// Use the HeightPatch data to obtain a height for a certain location.
		/// </summary>
		/// <param name="fx"></param>
		/// <param name="fy"></param>
		/// <param name="fz"></param>
		/// <param name="invCellSize"></param>
		/// <param name="cellHeight"></param>
		/// <param name="hp"></param>
		public float GetHeight(float fx, float fy, float fz, float invCellSize, float cellHeight, HeightPatch hp)
		{
			int ix = (int)Math.Floor(fx * invCellSize + 0.01f);
			int iz = (int)Math.Floor(fz * invCellSize + 0.01f);
			ix = MathHelper.Clamp(ix - hp.xmin, 0, hp.width - 1);
			iz = MathHelper.Clamp(iz - hp.ymin, 0, hp.height - 1);
			int h = hp.data[ix + iz * hp.width];

			if (h == UNSET_HEIGHT)
			{
				int[] off = { -1,0, -1,-1, 0,-1, 1,-1,
								 1,0, 1,1, 0,1, -1,1 };
				float dmin = float.MaxValue;

				for (int i = 0; i < 8; i++)
				{
					int nx = ix + off[i * 2 + 0];
					int nz = iz + off[i * 2 + 1];

					if (nx < 0 || nz < 0 || nx >= hp.width || nz >= hp.height)
						continue;

					int nh = hp.data[nx + nz * hp.width];
					if (nh == UNSET_HEIGHT)
						continue;

					float d = Math.Abs(nh * cellHeight - fy);
					if (d < dmin)
					{
						h = nh;
						dmin = d;
					}
				}
			}

			return h;
		}

		public float DistancePointSegment(float[] edge, int pt, int p, int q)
		{
			float pqx = edge[q + 0] - edge[p + 0];
			float pqy = edge[q + 1] - edge[p + 1];
			float pqz = edge[q + 2] - edge[p + 2];
			float dx = edge[pt + 0] - edge[p + 0];
			float dy = edge[pt + 1] - edge[p + 1];
			float dz = edge[pt + 2] - edge[p + 2];
			float d = pqx * pqx + pqy * pqy + pqz * pqz;
			float t = pqx * dx + pqy * dy + pqz * dz;

			if (d > 0)
				t /= d;

			//keep t between 0 and 1
			if (t < 0)
				t = 0;
			else if (t > 1)
				t = 1;

			dx = edge[p + 0] + t * pqx - edge[pt + 0];
			dy = edge[p + 1] + t * pqy - edge[pt + 1];
			dz = edge[p + 2] + t * pqz - edge[pt + 2];
			return dx * dx + dy * dy + dz * dz;
		}

		public class HeightPatch
		{
			public HeightPatch()
			{
				xmin = 0;
				ymin = 0;
				width = 0;
				height = 0;
			}

			public int[] data;
			public int xmin, ymin, width, height;
		}
	}
}
