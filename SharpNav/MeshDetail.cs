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

				//TODO: Build Detail Mesh
				//...
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
