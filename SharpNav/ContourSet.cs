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
	class ContourSet
	{
		private List<Contour> contours;
		private BBox3 bounds;
		private float cellSize;
		private float cellHeight;
		private int width;
		private int height;
		private int borderSize;

		//flags used in the build process
		public const int BORDER_VERTEX = 0x10000;
		public const int AREA_BORDER = 0x20000;
		public const int CONTOUR_TESS_WALL_EDGES = 0x01;
		public const int CONTOUR_TESS_AREA_EDGES = 0x02;

		//applied to region id field of contour vertices in order to extract region id
		public const int CONTOUR_REG_MASK = 0xffff;

		public ContourSet(OpenHeightfield openField, float maxError, int maxEdgeLen, int buildFlags)
		{
			this.bounds = openField.Bounds;
			if (openField.BorderSize > 0)
			{
				//remove offset
				float pad = openField.BorderSize * openField.CellSize;
				this.bounds.Min.X += pad;
				this.bounds.Min.Z += pad;
				this.bounds.Max.X -= pad;
				this.bounds.Max.Z -= pad;
			}
			this.cellSize = openField.CellSize;
			this.cellHeight = openField.CellHeight;
			this.width = openField.Width - openField.BorderSize * 2;
			this.height = openField.Height - openField.BorderSize * 2;
			this.borderSize = openField.BorderSize;

			int maxContours = Math.Max((int)openField.MaxRegions, 8);
			contours = new List<Contour>(maxContours);

			byte[] flags = new byte[openField.Spans.Length];

			//mark boundaries
			for (int y = 0; y < openField.Length; y++)
			{
				for (int x = 0; x < openField.Width; x++)
				{
					OpenHeightfield.Cell c = openField.Cells[x + y * openField.Width];
					for (int i = c.StartIndex, end = c.StartIndex + c.Count; i < end; i++)
					{
						byte res = 0;
						OpenHeightfield.Span s = openField.Spans[i];

						if (openField.Spans[i].Region == 0 || (openField.Spans[i].Region & OpenHeightfield.NotConnected) != 0)
						{
							flags[i] = 0;
							continue;
						}

						for (int dir = 0; dir < 4; dir++)
						{
							int r = 0;
							if (OpenHeightfield.Span.GetConnection(dir, ref s) != OpenHeightfield.NotConnected)
							{
								int dx = x + MathHelper.GetDirOffsetX(dir);
								int dy = y + MathHelper.GetDirOffsetY(dir);
								int di = openField.Cells[dx + dy * openField.Width].StartIndex + OpenHeightfield.Span.GetConnection(dir, ref s);
								r = openField.Spans[di].Region;
							}
							if (r == openField.Spans[i].Region)
								res |= (byte)(1 << dir); //?
						}
						flags[i] = (byte)(res ^ 0xf); //inverse, mark non connected edges
					}
				}
			}

			List<int> verts = new List<int>();
			List<int> simplified = new List<int>();

			int numContours = 0;
			for (int y = 0; y < openField.Length; y++)
			{
				for (int x = 0; x < openField.Width; x++)
				{
					OpenHeightfield.Cell c = openField.Cells[x + y * openField.Width];
					for (int i = c.StartIndex, end = c.StartIndex + c.Count; i < end; i++)
					{
						if (flags[i] == 0 || flags[i] == 0xf)
						{
							flags[i] = 0;
							continue;
						}
						ushort reg = (ushort)(openField.Spans[i].Region);
						if (reg == 0 || (reg & OpenHeightfield.BORDER_REG) != 0)
							continue;
						AreaFlags area = openField.Areas[i];

						verts.Clear();
						simplified.Clear();

						WalkContour(x, y, i, openField, flags, verts);
						SimplifyContour(verts, simplified, maxError, maxEdgeLen, buildFlags);
						RemoveDegenerateSegments(simplified);

						if (simplified.Count / 4 >= 3)
						{
							if (numContours >= maxContours)
							{
								maxContours *= 2;
								List<Contour> newContours = new List<Contour>(maxContours);
								for (int j = 0; j < contours.Count; j++)
									newContours[j] = contours[j];
								this.contours = newContours;
							}

							Contour cont = contours[numContours++];

							cont.vertices = simplified;
							if (borderSize > 0)
							{
								//remove offset
								for (int j = 0; j < cont.vertices.Count / 4; j++)
								{
									cont.vertices[j * 4 + 0] -= borderSize;
									cont.vertices[j * 4 + 2] -= borderSize;
								}
							}

							cont.rawVertices = verts;
							if (borderSize > 0)
							{
								//remove offset
								for (int j = 0; j < cont.rawVertices.Count / 4; j++)
								{
									cont.rawVertices[j * 4 + 0] -= borderSize;
									cont.rawVertices[j * 4 + 2] -= borderSize;
								}
							}

							cont.regionId = reg;
							cont.area = area;

						}
					}
				}
			}

			//check and merge droppings
			for (int i = 0; i < numContours; i++)
			{
				Contour cont = contours[i];
				//check if contour is backwards
				if (CalcAreaOfPolygon2D(cont.vertices) < 0)
				{
					//find another contour with same region id
					int mergeIdx = -1;
					for (int j = 0; j < numContours; j++)
					{
						if (i == j) continue;
						if (contours[j].vertices.Count / 4 != 0 && contours[j].regionId == cont.regionId)
						{
							//make sure polygon is correctly oriented
							if (CalcAreaOfPolygon2D(contours[j].vertices) != 0)
							{
								mergeIdx = j;
								break;
							}
						}
					}

					if (mergeIdx != -1)
					{
						Contour mcont = contours[mergeIdx];

						//merge by closest points
						int ia = 0, ib = 0;
						//getClosestIndices()
						if (ia == -1 || ib == -1)
							continue;

						mcont.mergeWithOtherContour(cont, ia, ib);
					}
				}
			}
		}

		public int GetCornerHeight(int x, int y, int i, int dir, OpenHeightfield openField, ref bool isBorderVertex)
		{
			OpenHeightfield.Span s = openField.Spans[i];
			int cornerHeight = s.Minimum;
			int dirp = (dir + 1) % 4;

			uint[] regs = { 0, 0, 0, 0 };

			//combine region and area codes in order to prevent border vertices, which are in between two areas, to be removed 
			regs[0] = (uint)(openField.Spans[i].Region | ((byte)(openField.Areas[i]) << 16));

			if (OpenHeightfield.Span.GetConnection(dir, ref s) != OpenHeightfield.NotConnected)
			{
				int dx = x + MathHelper.GetDirOffsetX(dir);
				int dy = y + MathHelper.GetDirOffsetY(dir);
				int di = openField.Cells[dx + dy * openField.Width].StartIndex + OpenHeightfield.Span.GetConnection(dir, ref s);
				OpenHeightfield.Span ds = openField.Spans[di];
				cornerHeight = Math.Max(cornerHeight, ds.Minimum);
				regs[1] = (uint)(openField.Spans[i].Region | ((byte)(openField.Areas[di]) << 16));
				if (OpenHeightfield.Span.GetConnection(dirp, ref ds) != OpenHeightfield.NotConnected)
				{
					int dx2 = dx + MathHelper.GetDirOffsetX(dirp);
					int dy2 = dy + MathHelper.GetDirOffsetY(dirp);
					int di2 = openField.Cells[dx2 + dy2 * openField.Width].StartIndex + OpenHeightfield.Span.GetConnection(dirp, ref ds);
					OpenHeightfield.Span ds2 = openField.Spans[di2];
					cornerHeight = Math.Max(cornerHeight, ds2.Minimum);
					regs[2] = (uint)(openField.Spans[i].Region | ((byte)(openField.Areas[di2]) << 16));
				}
			}

			if (OpenHeightfield.Span.GetConnection(dirp, ref s) != OpenHeightfield.NotConnected)
			{
				int dx = x + MathHelper.GetDirOffsetX(dirp);
				int dy = y + MathHelper.GetDirOffsetY(dirp);
				int di = openField.Cells[dx + dy * openField.Width].StartIndex + OpenHeightfield.Span.GetConnection(dirp, ref s);
				OpenHeightfield.Span ds = openField.Spans[di];
				cornerHeight = Math.Max(cornerHeight, ds.Minimum);
				regs[3] = (uint)(openField.Spans[i].Region | ((byte)(openField.Areas[di]) << 16));
				if (OpenHeightfield.Span.GetConnection(dir, ref ds) != OpenHeightfield.NotConnected)
				{
					int dx2 = dx + MathHelper.GetDirOffsetX(dir);
					int dy2 = dy + MathHelper.GetDirOffsetY(dir);
					int di2 = openField.Cells[dx2 + dy2 * openField.Width].StartIndex + OpenHeightfield.Span.GetConnection(dir, ref ds);
					OpenHeightfield.Span ds2 = openField.Spans[di2];
					cornerHeight = Math.Max(cornerHeight, ds2.Minimum);
					regs[2] = (uint)(openField.Spans[i].Region | ((byte)(openField.Areas[di2]) << 16));
				}
			}

			//check if vertex is special edge vertex
			//if so, these vertices will be removed later
			for (int j = 0; j < 4; j++)
			{
				int a = j;
				int b = (j + 1) % 4;
				int c = (j + 2) % 4;
				int d = (j + 3) % 4;

				//the vertex is a border vertex if:
				//two same exterior cells in a row followed by two interior cells and none of the regions are out of bounds
				bool twoSameExteriors = (regs[a] & regs[b] & OpenHeightfield.BORDER_REG) != 0 && regs[a] == regs[b];
				bool twoSameInteriors = ((regs[c] | regs[d]) & OpenHeightfield.BORDER_REG) == 0;
				bool intsSameArea = (regs[c] >> 16) == (regs[d] >> 16);
				bool noZeros = regs[a] != 0 && regs[b] != 0 && regs[c] != 0 && regs[d] != 0;
				if (twoSameExteriors && twoSameInteriors && intsSameArea && noZeros)
				{
					isBorderVertex = true;
					break;
				}
			}

			return cornerHeight;
		}

		/// <summary>
		/// Initial generation of the contours
		/// </summary>
		/// <param name="x"></param>
		/// <param name="y"></param>
		/// <param name="i"></param>
		/// <param name="openField"></param>
		/// <param name="flags"></param>
		/// <param name="points"></param>
		public void WalkContour(int x, int y, int i, OpenHeightfield openField, byte[] flags, List<int> points)
		{
			//choose the first non-connected edge
			int dir = 0;
			while ((flags[i] & (1 << dir)) == 0)
				dir++;

			int startDir = dir;
			int starti = i;

			AreaFlags area = openField.Areas[i];

			int iter = 0;
			while (++iter < 40000)
			{
				if ((flags[i] & (1 << dir)) != 0)
				{
					//choose the edge corner
					bool isBorderVertex = false;
					bool isAreaBorder = false;
					int px = x;
					int py = GetCornerHeight(x, y, i, dir, openField, ref isBorderVertex);
					int pz = y;
					switch (dir)
					{
						case 0: pz++; break;
						case 1: px++; pz++; break;
						case 2: px++; break;
					}
					int r = 0;
					OpenHeightfield.Span s = openField.Spans[i];
					if (OpenHeightfield.Span.GetConnection(dir, ref s) != OpenHeightfield.NotConnected)
					{
						int dx = x + MathHelper.GetDirOffsetX(dir);
						int dy = y + MathHelper.GetDirOffsetY(dir);
						int di = openField.Cells[dx + dy * openField.Width].StartIndex + OpenHeightfield.Span.GetConnection(dir, ref s);
						r = openField.Spans[di].Region;
						if (area != openField.Areas[di])
							isAreaBorder = true;
					}
					if (isBorderVertex)
						r |= BORDER_VERTEX;
					if (isAreaBorder)
						r |= AREA_BORDER;
					points.Add(px);
					points.Add(py);
					points.Add(pz);
					points.Add(r);

					flags[i] &= (byte)(~(1 << dir)); // remove visited edges
					dir = (dir + 1) % 4; //rotate clockwise
				}
				else
				{
					int di = -1;
					int dx = x + MathHelper.GetDirOffsetX(dir);
					int dy = y + MathHelper.GetDirOffsetY(dir);
					OpenHeightfield.Span s = openField.Spans[i];
					if (OpenHeightfield.Span.GetConnection(dir, ref s) != OpenHeightfield.NotConnected)
					{
						OpenHeightfield.Cell dc = openField.Cells[dx + dy * openField.Width];
						di = dc.StartIndex + OpenHeightfield.Span.GetConnection(dir, ref s);
					}
					if (di == -1)
					{
						//shouldn't happen
						return;
					}
					x = dx;
					y = dy;
					i = di;
					dir = (dir + 3) % 4; //rotate counterclockwise
				}

				if (starti == i && startDir == dir)
				{
					break;
				}
			}
		}

		/// <summary>
		/// Point is (x, z)
		/// Segment made out of two points P(px, pz) and Q(qx, qz).
		/// </summary>
		/// <param name="x"></param>
		/// <param name="z"></param>
		/// <param name="px"></param>
		/// <param name="pz"></param>
		/// <param name="qx"></param>
		/// <param name="qz"></param>
		/// <returns></returns>
		public float DistancePointSegment(int x, int z, int px, int pz, int qx, int qz)
		{
			float segmentDeltaX = qx - px;
			float segmentDeltaZ = qz - pz;
			float dx = x - px;
			float dz = z - pz;
			float segmentMagnitudeSquared = segmentDeltaX * segmentDeltaX + segmentDeltaZ * segmentDeltaZ;
			float t = segmentDeltaX * dx + segmentDeltaZ * dz;

			if (segmentMagnitudeSquared > 0)
				t /= segmentMagnitudeSquared;

			if (t < 0)
				t = 0;
			else if (t > 1)
				t = 1;

			dx = px + t * segmentDeltaX - x;
			dz = pz + t * segmentDeltaZ - z;

			return dx * dx + dz * dz;
		}

		/// <summary>
		/// Simplify the contours by reducing the number of edges
		/// </summary>
		/// <param name="points"></param>
		/// <param name="simplified"></param>
		public void SimplifyContour(List<int> points, List<int> simplified, float maxError, int maxEdgeLen, int buildFlags)
		{
			//add initial points
			bool hasConnections = false;
			for (int i = 0; i < points.Count; i += 4)
			{
				if ((points[i + 3] & CONTOUR_REG_MASK) != 0)
				{
					hasConnections = true;
					break;
				}
			}

			if (hasConnections)
			{
				//contour has some portals to other regions
				//add new point to every location where region changes
				for (int i = 0, end = points.Count / 4; i < end; i++)
				{
					int ii = (i + 1) % end;
					bool differentRegions = (points[i * 4 + 3] & CONTOUR_REG_MASK) != (points[ii * 4 + 3] & CONTOUR_REG_MASK);
					bool areaBorders = (points[i * 4 + 3] & AREA_BORDER) != (points[ii * 4 + 3] & AREA_BORDER);
					if (differentRegions || areaBorders)
					{
						simplified.Add(points[i * 4 + 0]);
						simplified.Add(points[i * 4 + 1]);
						simplified.Add(points[i * 4 + 2]);
						simplified.Add(i);
					}
				}
			}

			if (simplified.Count == 0)
			{
				//find lower-left and upper-right vertices of contour
				int lowerLeftX = points[0];
				int lowerLeftY = points[1];
				int lowerLeftZ = points[2];
				int lowerLeftI = 0;
				
				int upperRightX = points[0];
				int upperRightY = points[1];
				int upperRightZ = points[2];
				int upperRightI = 0;
				
				for (int i = 0; i < points.Count; i += 4)
				{
					int x = points[i + 0];
					int y = points[i + 1];
					int z = points[i + 2];
					
					if (x < lowerLeftX || (x == lowerLeftX && z < lowerLeftZ))
					{
						lowerLeftX = x;
						lowerLeftY = y;
						lowerLeftZ = z;
						lowerLeftI = i / 4;
					}
					
					if (x > upperRightX || (x == upperRightX && z > upperRightZ))
					{
						upperRightX = x;
						upperRightY = y;
						upperRightZ = z;
						upperRightI = i / 4;
					}
				}
				
				simplified.Add(lowerLeftX);
				simplified.Add(lowerLeftY);
				simplified.Add(lowerLeftZ);
				simplified.Add(lowerLeftI);

				simplified.Add(upperRightX);
				simplified.Add(upperRightY);
				simplified.Add(upperRightZ);
				simplified.Add(upperRightI);
			}

			//add points until all points are within erorr tolerance of simplified slope
			int numPoints = points.Count / 4;
			for (int i = 0; i < simplified.Count / 4; )
			{
				int ii = (i + 1) % (simplified.Count / 4);

				int ax = simplified[i * 4 + 0];
				int az = simplified[i * 4 + 2];
				int ai = simplified[i * 4 + 3];

				int bx = simplified[ii * 4 + 0];
				int bz = simplified[ii * 4 + 2];
				int bi = simplified[ii * 4 + 3];

				float maxDeviation = 0;
				int maxi = -1;
				int ci, cinc, endi;

				//traverse segment in lexilogical order
				if (bx > ax || (bx == ax && bz > az))
				{
					cinc = 1;
					ci = (ai + cinc) % numPoints;
					endi = bi;
				}
				else
				{
					cinc = numPoints - 1;
					ci = (bi + cinc) % numPoints;
					endi = ai;
				}

				//tessellate only outer edges or edges between areas
				if ((points[ci * 4 + 3] & CONTOUR_REG_MASK) == 0 || (points[ci * 4 + 3] & AREA_BORDER) != 0)
				{
					while (ci != endi)
					{
						float deviation = DistancePointSegment(points[ci * 4 + 0], points[ci * 4 + 2], ax, az, bx, bz);
						if (deviation > maxDeviation)
						{
							maxDeviation = deviation;
							maxi = ci;
						}
						ci = (ci + cinc) % numPoints;
					}
				}

				if (maxi != -1 && maxDeviation > (maxError * maxError))
				{
					for (int j = 0; j < 4; j++)
						simplified.Add(new int());

					//make space for new point
					for (int j = simplified.Count / 4 - 1; j > i; j--)
					{
						simplified[j * 4 + 0] = simplified[(j - 1) * 4 + 0];
						simplified[j * 4 + 1] = simplified[(j - 1) * 4 + 1];
						simplified[j * 4 + 2] = simplified[(j - 1) * 4 + 2];
						simplified[j * 4 + 3] = simplified[(j - 1) * 4 + 3];
					}

					//add point
					simplified[(i + 1) * 4 + 0] = points[maxi * 4 + 0];
					simplified[(i + 1) * 4 + 1] = points[maxi * 4 + 1];
					simplified[(i + 1) * 4 + 2] = points[maxi * 4 + 2];
					simplified[(i + 1) * 4 + 3] = maxi;
				}
				else
				{
					i++;
				}
			}

			//split too long edges
			if (maxEdgeLen > 0 && (buildFlags & (CONTOUR_TESS_WALL_EDGES | CONTOUR_TESS_AREA_EDGES)) != 0)
			{
				for (int i = 0; i < simplified.Count / 4; )
				{
					int ii = (i + 1) % (simplified.Count / 4);

					int ax = simplified[i * 4 + 0];
					int az = simplified[i * 4 + 2];
					int ai = simplified[i * 4 + 3];

					int bx = simplified[ii * 4 + 0];
					int bz = simplified[ii * 4 + 2];
					int bi = simplified[ii * 4 + 3];

					//find maximum deviation from segment
					int maxi = -1;
					int ci = (ai + 1) % numPoints;

					//tessellate only outer edges or edges between areas
					bool tess = false;
					//wall edges
					if ((buildFlags & CONTOUR_TESS_WALL_EDGES) != 0 && (points[ci * 4 + 3] & CONTOUR_REG_MASK) == 0)
						tess = true;
					//edges between areas
					if ((buildFlags & CONTOUR_TESS_AREA_EDGES) != 0 && (points[ci * 4 + 3] & AREA_BORDER) != 0)
						tess = true;

					if (tess)
					{
						int dx = bx - ax;
						int dz = bz - az;
						if (dx * dx + dz * dz > maxEdgeLen * maxEdgeLen)
						{
							//round based on lexilogical direction
							int n = bi < ai ? (bi + numPoints - ai) : (bi - ai);
							if (n > 1)
							{
								if (bx > ax || (bx == ax && bz > az))
									maxi = (ai + n / 2) % numPoints;
								else
									maxi = (ai + (n + 1) / 2) % numPoints;
							}
						}
					}

					if (maxi != -1)
					{
						for (int j = 0; j < 4; j++)
							simplified.Add(new int());

						//make space for new point
						for (int j = simplified.Count / 4 - 1; j > i; j--)
						{
							simplified[j * 4 + 0] = simplified[(j - 1) * 4 + 0];
							simplified[j * 4 + 1] = simplified[(j - 1) * 4 + 1];
							simplified[j * 4 + 2] = simplified[(j - 1) * 4 + 2];
							simplified[j * 4 + 3] = simplified[(j - 1) * 4 + 3];
						}

						//add point
						simplified[(i + 1) * 4 + 0] = points[maxi * 4 + 0];
						simplified[(i + 1) * 4 + 1] = points[maxi * 4 + 1];
						simplified[(i + 1) * 4 + 2] = points[maxi * 4 + 2];
						simplified[(i + 1) * 4 + 3] = maxi;
					}
					else
					{
						i++;
					}
				}
			}

			for (int i = 0; i < simplified.Count / 4; i++)
			{
				//take edge vertex flag from current raw point and neighbor region from next raw point
				int ai = (simplified[i * 4 + 3] + 1) % numPoints;
				int bi = simplified[i * 4 + 3];
				simplified[i * 4 + 3] = (points[ai * 4 + 3] & (CONTOUR_REG_MASK | AREA_BORDER)) | (points[bi * 4 + 3] & BORDER_VERTEX);
			}
		}

		/// <summary>
		/// Clean up the simplified segments
		/// </summary>
		/// <param name="simplified"></param>
		public void RemoveDegenerateSegments(List<int> simplified)
		{
			//remove adjacent vertices which are equal on the xz-plane
			for (int i = 0; i < simplified.Count / 4; i++)
			{
				int ni = i + 1;
				if (ni >= simplified.Count / 4)
					ni = 0;

				if (simplified[i * 4 + 0] == simplified[ni * 4 + 0] &&
					simplified[i * 4 + 2] == simplified[ni * 4 + 2])
				{
					//remove degenerate segment
					for (int j = i; j < simplified.Count / 4 - 1; j++)
					{
						simplified[j * 4 + 0] = simplified[(j + 1) * 4 + 0];
						simplified[j * 4 + 1] = simplified[(j + 1) * 4 + 1];
						simplified[j * 4 + 2] = simplified[(j + 1) * 4 + 2];
						simplified[j * 4 + 3] = simplified[(j + 1) * 4 + 3];
					}
					simplified.RemoveRange(simplified.Count - 4, 4);
				}
			}
		}

		public int CalcAreaOfPolygon2D(List<int> verts)
		{
			int area = 0;
			int numVertices = verts.Count / 4;
			for (int i = 0, j = numVertices - 1; i < numVertices; j = i++)
			{
				area += verts[i * 4 + 0] * verts[j * 4 + 2] - verts[j * 4 + 0] * verts[i * 4 + 2];
			}
			return (area + 1) / 2; 
		}

		class Contour
		{
			public List<int> vertices;
			public List<int> rawVertices;
			public ushort regionId;
			public AreaFlags area;

			public void mergeWithOtherContour(Contour contB, int ia, int ib)
			{
				int numVertsA = this.vertices.Count / 4;
				int numVertsB = contB.vertices.Count / 4;
				int maxVerts = numVertsA + numVertsB + 2;
				List<int> newVerts = new List<int>(maxVerts);

				//copy contour B
				for (int i = 0; i <= numVertsB; i++)
				{
					int src = ((ib + i) % numVertsB) * 4;
					this.vertices.Add(contB.vertices[src + 0]);
					this.vertices.Add(contB.vertices[src + 0]);
					this.vertices.Add(contB.vertices[src + 0]);
					this.vertices.Add(contB.vertices[src + 0]);
				}
			}
		}
	}
}
