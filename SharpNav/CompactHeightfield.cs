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
	public class CompactHeightfield
	{
		public const int NotConnected = 0xff; //HACK figure out a better way to do this

		public const ushort BORDER_REG = 0x8000; //HACK: Heightfield border flag. Unwalkable

		private BBox3 bounds;

		private int width, height, length;
		private float cellSize, cellHeight;

		private CompactCell[] cells;
		private CompactSpan[] spans;
		private AreaFlags[] areas;

		//distance field
		private ushort[] distances;
		private ushort maxDistance;

		//region
		private ushort maxRegions;
		private int borderSize;
		
		public CompactHeightfield(Heightfield field, int walkableHeight, int walkableClimb)
		{
			this.bounds = field.Bounds;
			this.width = field.Width;
			this.height = field.Height;
			this.length = field.Length;
			this.cellSize = field.CellSizeXZ;
			this.cellHeight = field.CellHeight;

			cells = new CompactCell[width * length];
			spans = new CompactSpan[field.SpanCount];
			areas = new AreaFlags[field.SpanCount];

			//iterate over the Heightfield's cells
			int spanIndex = 0;
			for (int i = 0; i < cells.Length; i++)
			{
				//get the heightfield span list, skip if empty
				var fs = field[i].Spans;
				if (fs.Count == 0)
					continue;

				CompactCell c = new CompactCell(spanIndex, 0);

				//convert the closed spans to open spans
				int lastInd = fs.Count - 1;
				for (int j = 0; j < lastInd; j++)
				{
					var s = fs[j];
					if (s.Area != AreaFlags.Null)
					{
						CompactSpan.FromMinMax(s.Maximum, fs[j + 1].Minimum, out spans[spanIndex]);
						spanIndex++;
						c.Count++;
					}
				}

				//the last closed span that has an "infinite" height
				var lastS = fs[lastInd];
				if (lastS.Area != AreaFlags.Null)
				{
					spans[spanIndex] = new CompactSpan(fs[lastInd].Maximum, int.MaxValue);
					spanIndex++;
					c.Count++;
				}

				cells[i] = c;
			}

			//set neighbor connections
			for (int y = 0; y < length; y++)
			{
				for (int x = 0; x < width; x++)
				{
					CompactCell c = cells[y * width + x];
					for (int i = c.StartIndex, end = c.StartIndex + c.Count; i < end; i++)
					{
						CompactSpan s = spans[i];

						for (int dir = 0; dir < 4; dir++)
						{
							CompactSpan.SetConnection(dir, NotConnected, ref spans[i]);

							int dx = x + MathHelper.GetDirOffsetX(dir);
							int dy = y + MathHelper.GetDirOffsetY(dir);

							if (dx < 0 || dy < 0 || dx >= width || dy >= length)
								continue;

							CompactCell dc = cells[dy * width + dx];
							for (int j = dc.StartIndex, jEnd = dc.StartIndex + dc.Count; j < jEnd; j++)
							{
								CompactSpan ds = spans[j];

								int overlapBottom = Math.Max(s.Minimum, ds.Minimum);
								int overlapTop = Math.Min(s.Minimum + s.Height, ds.Minimum + ds.Height);

								if (!s.HasUpperBound && !ds.HasUpperBound)
									overlapTop = int.MaxValue;

								if ((overlapTop - overlapBottom) >= walkableHeight && Math.Abs(ds.Minimum - s.Minimum) <= walkableClimb)
								{
									int con = j - dc.StartIndex;
									if (con < 0 || con >= 0xff)
										throw new InvalidOperationException("The neighbor index is too high to store. Reduce the number of cells in the Y direction.");

									CompactSpan.SetConnection(dir, con, ref spans[i]);
									break;
								}
							}
						}
					}
				}
			}
		}

		public int Width { get { return width; } }
		public int Height { get { return height; } }
		public int Length { get { return length; } }

		public BBox3 Bounds { get { return bounds; } }
		public float CellSize { get { return cellSize; } }
		public float CellHeight { get { return cellHeight; } }

		public ushort MaxDistance { get { return maxDistance; } }
		public ushort[] Distances { get { return distances; } }
		public int BorderSize { get { return borderSize; } }
		public int MaxRegions { get { return maxRegions; } }

		public CompactCell[] Cells { get { return cells; } }
		public CompactSpan[] Spans { get { return spans; } }
		public AreaFlags[] Areas { get { return areas; } }

		/// <summary>
		/// Gets the <see cref="Heightfield.Cell"/> at the specified coordinate.
		/// </summary>
		/// <param name="x">The x coordinate.</param>
		/// <param name="y">The y coordinate.</param>
		public IEnumerable<CompactSpan> this[int x, int y]
		{
			get
			{
				if (x < 0 || x >= width || y < 0 || y >= length)
					throw new IndexOutOfRangeException();

				CompactCell c = cells[y * width + x];

				int end = c.StartIndex + c.Count;
				for (int i = c.StartIndex; i < end; i++)
					yield return spans[i];
			}
		}

		/// <summary>
		/// Gets the <see cref="Heightfield.Cell"/> at the specified index.
		/// </summary>
		/// <param name="i">The index.</param>
		public IEnumerable<CompactSpan> this[int i]
		{
			get
			{
				CompactCell c = cells[i];

				int end = c.StartIndex + c.Count;
				for (int j = c.StartIndex; j < end; j++)
					yield return spans[j];
			}
		}

		/// <summary>
		/// A distance field estimates how far each span is from its nearest border span. This data is needed for region generation.
		/// </summary>
		/// <param name="src">Array of values, each corresponding to an individual span</param>
		/// <param name="maxDist">The maximum value of the src array</param>
		public void CalculateDistanceField(ushort[] src)
		{
			//initialize distance and points
			for (int i = 0; i < spans.Length; i++)
				src[i] = 0xffff;

			//mark boundary cells
			for (int y = 0; y < length; y++)
			{
				for (int x = 0; x < width; x++)
				{
					CompactCell c = cells[y * width + x];
					for (int i = c.StartIndex, end = c.StartIndex + c.Count; i < end; i++)
					{
						CompactSpan s = spans[i];
						AreaFlags area = areas[i];

						int numConnections = 0;
						for (int dir = 0; dir < 4; dir++)
						{
							if (CompactSpan.GetConnection(dir, ref s) != NotConnected)
							{
								int dx = x + MathHelper.GetDirOffsetX(dir);
								int dy = y + MathHelper.GetDirOffsetY(dir);
								int di = cells[dx + dy * width].StartIndex + CompactSpan.GetConnection(dir, ref s);
								if (area == areas[di])
									numConnections++;
							}
						}
						if (numConnections != 4)
							src[i] = 0;
					}
				}
			}

			//pass 1
			for (int y = 0; y < length; y++)
			{
				for (int x = 0; x < width; x++)
				{
					CompactCell c = cells[y * width + x];
					for (int i = c.StartIndex, end = c.StartIndex + c.Count; i < end; i++)
					{
						CompactSpan s = spans[i];

						if (CompactSpan.GetConnection(0, ref s) != NotConnected)
						{
							//(-1, 0)
							int dx = x + MathHelper.GetDirOffsetX(0);
							int dy = y + MathHelper.GetDirOffsetY(0);
							int di = cells[dx + dy * width].StartIndex + CompactSpan.GetConnection(0, ref s);
							CompactSpan ds = spans[di];
							if (src[di] + 2 < src[i])
								src[i] = (ushort)(src[di] + 2);

							//(-1, -1)
							if (CompactSpan.GetConnection(3, ref ds) != NotConnected)
							{
								int ddx = dx + MathHelper.GetDirOffsetX(3);
								int ddy = dy + MathHelper.GetDirOffsetY(3);
								int ddi = cells[ddx + ddy * width].StartIndex + CompactSpan.GetConnection(3, ref ds);
								if (src[ddi] + 3 < src[i])
									src[i] = (ushort)(src[ddi] + 3);
							}
						}

						if (CompactSpan.GetConnection(3, ref s) != NotConnected)
						{
							//(0, -1)
							int dx = x + MathHelper.GetDirOffsetX(3);
							int dy = y + MathHelper.GetDirOffsetY(3);
							int di = cells[dx + dy * width].StartIndex + CompactSpan.GetConnection(3, ref s);
							CompactSpan ds = spans[di];
							if (src[di] + 2 < src[i])
								src[i] = (ushort)(src[di] + 2);

							//(1, -1)
							if (CompactSpan.GetConnection(2, ref ds) != NotConnected)
							{
								int ddx = dx + MathHelper.GetDirOffsetX(2);
								int ddy = dy + MathHelper.GetDirOffsetY(2);
								int ddi = cells[ddx + ddy * width].StartIndex + CompactSpan.GetConnection(2, ref ds);
								if (src[ddi] + 3 < src[i])
									src[i] = (ushort)(src[ddi] + 3);
							}
						}
					}
				}
			}

			//pass 2
			for (int y = length - 1; y >= 0; y--)
			{
				for (int x = width - 1; x >= 0; x--)
				{
					CompactCell c = cells[y * width + x];
					for (int i = c.StartIndex, end = c.StartIndex + c.Count; i < end; i++)
					{
						CompactSpan s = spans[i];

						if (CompactSpan.GetConnection(2, ref s) != NotConnected)
						{
							//(1, 0)
							int dx = x + MathHelper.GetDirOffsetX(2);
							int dy = y + MathHelper.GetDirOffsetY(2);
							int di = cells[dx + dy * width].StartIndex + CompactSpan.GetConnection(2, ref s);
							CompactSpan ds = spans[di];
							if (src[di] + 2 < src[i])
								src[i] = (ushort)(src[di] + 2);

							//(1, 1)
							if (CompactSpan.GetConnection(1, ref ds) != NotConnected)
							{
								int ddx = dx + MathHelper.GetDirOffsetX(1);
								int ddy = dy + MathHelper.GetDirOffsetY(1);
								int ddi = cells[ddx + ddy * width].StartIndex + CompactSpan.GetConnection(1, ref ds);
								if (src[ddi] + 3 < src[i])
									src[i] = (ushort)(src[ddi] + 3);
							}
						}

						if (CompactSpan.GetConnection(1, ref s) != NotConnected)
						{
							//(0, 1)
							int dx = x + MathHelper.GetDirOffsetX(1);
							int dy = y + MathHelper.GetDirOffsetY(1);
							int di = cells[dx + dy * width].StartIndex + CompactSpan.GetConnection(1, ref s);
							CompactSpan ds = spans[di];
							if (src[di] + 2 < src[i])
								src[i] = (ushort)(src[di] + 2);

							//(-1, 1)
							if (CompactSpan.GetConnection(0, ref ds) != NotConnected)
							{
								int ddx = dx + MathHelper.GetDirOffsetX(0);
								int ddy = dy + MathHelper.GetDirOffsetY(0);
								int ddi = cells[ddx + ddy * width].StartIndex + CompactSpan.GetConnection(0, ref ds);
								if (src[ddi] + 3 < src[i])
									src[i] = (ushort)(src[ddi] + 3);
							}
						}
					}
				}
			}
		}

		/// <summary>
		/// Part of building the distance field. It may or may not return an array equal to src.
		/// </summary>
		/// <param name="openField">The OpenHeightfield</param>
		/// <param name="thr">Threshold?</param>
		/// <returns></returns>
		public ushort[] BoxBlur(int thr, ushort[] src, ushort[] dst)
		{
			thr *= 2;

			for (int y = 0; y < length; y++)
			{
				for (int x = 0; x < width; x++)
				{
					CompactCell c = cells[y * width + x];
					for (int i = c.StartIndex, end = c.StartIndex + c.Count; i < end; i++)
					{
						CompactSpan s = spans[i];
						ushort cd = src[i];

						//in constructor, thr = 1.
						//in this method, thr *= 2, so thr = 2
						//cd is either 0, 1, or 2 so set that to destination
						if (cd <= thr)
						{
							dst[i] = cd;
							continue;
						}

						//cd must be greater than 2
						int d = cd;
						for (int dir = 0; dir < 4; dir++)
						{
							//check neighbor span
							if (CompactSpan.GetConnection(dir, ref s) != NotConnected)
							{
								int dx = x + MathHelper.GetDirOffsetX(dir);
								int dy = y + MathHelper.GetDirOffsetY(dir);
								int di = cells[dx + dy * width].StartIndex + CompactSpan.GetConnection(dir, ref s);
								d += src[di];

								//check next span in next clockwise direction
								CompactSpan ds = spans[di];
								int dir2 = (dir + 1) % 4;
								if (CompactSpan.GetConnection(dir2, ref ds) != NotConnected)
								{
									int dx2 = dx + MathHelper.GetDirOffsetX(dir2);
									int dy2 = dy + MathHelper.GetDirOffsetY(dir2);
									int di2 = cells[dx2 + dy2 * width].StartIndex + CompactSpan.GetConnection(dir2, ref ds);
									d += src[di2];
								}
								else
								{
									d += cd;
								}
							}
							else
							{
								d += cd * 2;
							}
						}
						//save new value to destination
						dst[i] = (ushort)((d + 5) / 9);
					}
				}
			}

			return dst;
		}

		public void BuildDistanceField()
		{
			ushort[] src = new ushort[spans.Length];
			ushort[] dst = new ushort[spans.Length];

			//fill up all the values in src
			CalculateDistanceField(src);

			//find the maximum distance
			this.maxDistance = 0;
			for (int i = 0; i < spans.Length; i++)
				this.maxDistance = Math.Max(src[i], this.maxDistance);

			//blur 
			if (BoxBlur(1, src, dst) != src)
			{
				src = dst;
			}

			//store distances
			this.distances = src;
		}

		/// <summary>
		/// Locate spans below the water level and try to add them to existing regions or create new regions
		/// </summary>
		/// <param name="maxIter">max iterations to go through</param>
		/// <param name="level">current levels</param>
		/// <param name="srcReg">source regions</param>
		/// <param name="srcDist">source distances</param>
		/// <param name="dstReg">destination region</param>
		/// <param name="dstDist">destination distances</param>
		/// <returns></returns>
		public ushort[] ExpandRegions(int maxIter, ushort level, ushort[] srcReg, ushort[] srcDist, ushort[] dstReg, ushort[] dstDist)
		{
			//find cells revealed by the raised level
			List<int> stack = new List<int>();
			for (int y = 0; y < length; y++)
			{
				for (int x = 0; x < width; x++)
				{
					CompactCell c = cells[x + y * width];
					for (int i = c.StartIndex, end = c.StartIndex + c.Count; i < end; i++)
					{
						if (distances[i] >= level && srcReg[i] == 0 && areas[i] != AreaFlags.Null)
						{
							stack.Add(x);
							stack.Add(y);
							stack.Add(i);
						}
					}
				}
			}

			int iter = 0;
			while (stack.Count > 0)
			{
				int failed = 0;

				dstReg = srcReg;
				dstDist = srcDist;

				for (int j = 0; j < stack.Count; j += 3)
				{
					int x = stack[j + 0];
					int y = stack[j + 1];
					int i = stack[j + 2];
					if (i < 0)
					{
						failed++;
						continue;
					}

					ushort r = srcReg[i];
					ushort d2 = 0xffff;
					AreaFlags area = areas[i];
					CompactSpan s = spans[i];
					for (int dir = 0; dir < 4; dir++)
					{
						if (CompactSpan.GetConnection(dir, ref s) == NotConnected) continue;
						int dx = x + MathHelper.GetDirOffsetX(dir);
						int dy = y + MathHelper.GetDirOffsetY(dir);
						int di = cells[dx + dy * width].StartIndex + CompactSpan.GetConnection(dir, ref s);
						if (areas[di] != area) continue;
						if (srcReg[di] > 0 && (srcReg[di] & BORDER_REG) == 0)
						{
							if (srcDist[di] + 2 < d2)
							{
								r = srcReg[di];
								d2 = (ushort)(srcDist[di] + 2);
							}
						}
					}
					if (r != 0)
					{
						stack[j + 2] = -1; //mark as used
						dstReg[i] = r;
						dstDist[i] = d2;
					}
					else
					{
						failed++;
					}
				}

				//swap source and dest
				ushort[] temp = srcReg;
				srcReg = dstReg;
				dstReg = temp;

				temp = srcDist;
				srcDist = dstDist;
				dstDist = temp;

				if (failed * 3 == stack.Count)
					break;

				if (level > 0)
				{
					++iter;
					if (iter >= maxIter)
						break;
				}
			}

			return srcReg;
		}

		/// <summary>
		/// Floods the regions at a certain level
		/// </summary>
		/// <param name="x">starting x</param>
		/// <param name="y">starting y</param>
		/// <param name="i">span index</param>
		/// <param name="level">current level</param>
		/// <param name="r">region id</param>
		/// <param name="srcReg">source region</param>
		/// <param name="srcDist">source distances</param>
		/// <returns></returns>
		public bool FloodRegion(int x, int y, int i, ushort level, ushort r, ushort[] srcReg, ushort[] srcDist)
		{
			AreaFlags area = areas[i];

			//flood fill mark region
			List<int> stack = new List<int>();
			stack.Add(x);
			stack.Add(y);
			stack.Add(i);
			srcReg[i] = r;
			srcDist[i] = 0;

			ushort lev = (ushort)(level >= 2 ? level - 2 : 0);
			int count = 0;

			while (stack.Count > 0)
			{
				int ci = stack[stack.Count - 1];
				stack.RemoveAt(stack.Count - 1);
				int cy = stack[stack.Count - 1];
				stack.RemoveAt(stack.Count - 1);
				int cx = stack[stack.Count - 1];
				stack.RemoveAt(stack.Count - 1);

				CompactSpan cs = spans[ci];

				//check if any of the neighbors already have a valid reigon set
				ushort ar = 0;
				for (int dir = 0; dir < 4; dir++)
				{
					//8 connected
					if (CompactSpan.GetConnection(dir, ref cs) != NotConnected)
					{
						int dx = cx + MathHelper.GetDirOffsetX(dir);
						int dy = cy + MathHelper.GetDirOffsetY(dir);
						int di = cells[dx + dy * width].StartIndex + CompactSpan.GetConnection(dir, ref cs);

						if (areas[di] != area)
							continue;

						ushort nr = srcReg[di];
						if ((nr & BORDER_REG) != 0) //skip borders
							continue;
						if (nr != 0 && nr != r)
							ar = nr;

						CompactSpan ds = spans[di];
						int dir2 = (dir + 1) % 4;
						if (CompactSpan.GetConnection(dir2, ref ds) != NotConnected)
						{
							int dx2 = dx + MathHelper.GetDirOffsetX(dir2);
							int dy2 = dy + MathHelper.GetDirOffsetY(dir2);
							int di2 = cells[dx2 + dy2 * width].StartIndex + CompactSpan.GetConnection(dir2, ref ds);

							if (areas[di2] != area)
								continue;

							ushort nr2 = srcReg[di2];
							if (nr2 != 0 && nr2 != r)
								ar = nr2;
						}

					}
				}

				if (ar != 0)
				{
					srcReg[ci] = 0;
					continue;
				}

				count++;

				//expand neighbors
				for (int dir = 0; dir < 4; dir++)
				{
					if (CompactSpan.GetConnection(dir, ref cs) != NotConnected)
					{
						int dx = cx + MathHelper.GetDirOffsetX(dir);
						int dy = cy + MathHelper.GetDirOffsetY(dir);
						int di = cells[dx + dy * width].StartIndex + CompactSpan.GetConnection(dir, ref cs);

						if (areas[di] != area)
							continue;

						if (distances[di] >= lev && srcReg[di] == 0)
						{
							srcReg[di] = r;
							srcDist[di] = 0;
							stack.Add(dx);
							stack.Add(dy);
							stack.Add(di);
						}
					}
				}
			}

			return count > 0;
		}

		/// <summary>
		/// A helper method for WalkContour
		/// </summary>
		/// <param name="srcReg">an array of ushort values</param>
		/// <param name="x">cell x</param>
		/// <param name="y">cell y</param>
		/// <param name="i">index of span</param>
		/// <param name="dir">direction</param>
		/// <returns></returns>
		public bool IsSolidEdge(ushort[] srcReg, int x, int y, int i, int dir)
		{
			CompactSpan s = spans[i];
			ushort r = 0;

			if (CompactSpan.GetConnection(dir, ref s) != NotConnected)
			{
				int dx = x + MathHelper.GetDirOffsetX(dir);
				int dy = y + MathHelper.GetDirOffsetY(dir);
				int di = cells[dx + dy * width].StartIndex + CompactSpan.GetConnection(dir, ref s);
				r = srcReg[di];
			}

			if (r == srcReg[i])
				return false;

			return true;
		}

		/// <summary>
		/// Try to visit all the spans. May be needed in filtering small regions. 
		/// </summary>
		/// <param name="srcReg">an array of ushort values</param>
		/// <param name="x">cell x-coordinate</param>
		/// <param name="y">cell y-coordinate</param>
		/// <param name="i">index of span</param>
		/// <param name="dir">direction</param>
		/// <param name="cont">list of ints</param>
		public void WalkContour(ushort[] srcReg, int x, int y, int i, int dir, List<int> cont)
		{
			int startDir = dir;
			int starti = i;

			CompactSpan ss = spans[i];
			ushort curReg = 0;

			if (CompactSpan.GetConnection(dir, ref ss) != NotConnected)
			{
				int dx = x + MathHelper.GetDirOffsetX(dir);
				int dy = y + MathHelper.GetDirOffsetY(dir);
				int di = cells[dx + dy * width].StartIndex + CompactSpan.GetConnection(dir, ref ss);
				curReg = srcReg[di];
			}
			cont.Add(curReg);

			int iter = 0;
			while (++iter < 40000)
			{
				CompactSpan s = spans[i];

				if (IsSolidEdge(srcReg, x, y, i, dir))
				{
					//choose the edge corner
					ushort r = 0;
					if (CompactSpan.GetConnection(dir, ref s) != NotConnected)
					{
						int dx = x + MathHelper.GetDirOffsetX(dir);
						int dy = y + MathHelper.GetDirOffsetY(dir);
						int di = cells[dx + dy * width].StartIndex + CompactSpan.GetConnection(dir, ref s);
						r = srcReg[di];
					}

					if (r != curReg)
					{
						curReg = r;
						cont.Add(curReg);
					}

					dir = (dir + 1) % 4; //rotate clockwise
				}
				else
				{
					int di = -1;
					int dx = x + MathHelper.GetDirOffsetX(dir);
					int dy = y + MathHelper.GetDirOffsetY(dir);

					if (CompactSpan.GetConnection(dir, ref s) != NotConnected)
					{
						CompactCell dc = cells[dx + dy * width];
						di = dc.StartIndex + CompactSpan.GetConnection(dir, ref s);
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
					break;
			}

			//remove adjacent duplicates
			if (cont.Count > 1)
			{
				for (int j = 0; j < cont.Count; )
				{
					//next element
					int nj = (j + 1) % cont.Count;

					//adjacent duplicate found
					if (cont[j] == cont[nj]) 
						cont.RemoveAt(j);
					else
						j++; 
				}
			}
		}

		/// <summary>
		/// Discards regions that are too small. 
		/// </summary>
		/// <param name="minRegionArea"></param>
		/// <param name="mergeRegionSize"></param>
		/// <param name="maxRegionId">determines the number of regions available</param>
		/// <param name="srcReg">region data</param>
		/// <returns></returns>
		public bool FilterSmallRegions(int minRegionArea, int mergeRegionSize, ref ushort maxRegionId, ushort[] srcReg)
		{
			int numRegions = maxRegionId + 1;
			Region[] regions = new Region[numRegions];

			//construct regions
			for (int i = 0; i < numRegions; i++)
				regions[i] = new Region((ushort)i);

			//find edge of a region and find connections around a contour
			for (int y = 0; y < length; y++)
			{
				for (int x = 0; x < width; x++)
				{
					CompactCell c = cells[x + y * width];
					for (int i = c.StartIndex, end = c.StartIndex + c.Count; i < end; i++)
					{
						ushort r = srcReg[i];
						if (r == 0 || r >= numRegions)
							continue;

						Region reg = regions[r];
						reg.SpanCount++;
						
						//update floors
						for (int j = c.StartIndex; j < end; j++)
						{
							if (i == j) continue;
							ushort floorId = srcReg[j];
							if (floorId == 0 || floorId >= numRegions)
								continue;
							reg.AddUniqueFloorRegion(floorId);
						}

						//have found contour
						if (reg.Connections.Count > 0)
							continue;

						reg.AreaType = areas[i];

						//check if this cell is next to a border
						int ndir = -1;
						for (int dir = 0; dir < 4; dir++)
						{
							if (IsSolidEdge(srcReg, x, y, i, dir))
							{
								ndir = dir;
								break;
							}
						}

						if (ndir != -1)
						{
							//The cell is at a border. 
							//Walk around contour to find all neighbors
							WalkContour(srcReg, x, y, i, ndir, reg.Connections);
						}
					}
				}
			}

			//Remove too small regions
			List<int> stack = new List<int>();
			List<int> trace = new List<int>();
			for (int i = 0; i < numRegions; i++)
			{
				Region reg = regions[i];
				if (reg.Id == 0 || (reg.Id & BORDER_REG) != 0)
					continue;
				if (reg.SpanCount == 0)
					continue;
				if (reg.Visited)
					continue;

				//count the total size of all connected regions
				//also keep track of the regions connections to a tile border
				bool connectsToBorder = false;
				int spanCount = 0;
				stack.Clear();
				trace.Clear();

				reg.Visited = true;
				stack.Add(i);

				while (stack.Count != 0)
				{
					//pop
					int ri = stack[stack.Count - 1];
					stack.RemoveAt(stack.Count - 1);

					Region creg = regions[ri];

					spanCount += creg.SpanCount;
					trace.Add(ri);

					for (int j = 0; j < creg.Connections.Count; j++)
					{
						if ((creg.Connections[j] & BORDER_REG) != 0)
						{
							connectsToBorder = true;
							continue;
						}
						
						Region neiReg = regions[creg.Connections[j]];
						if (neiReg.Visited)
							continue;
						if (neiReg.Id == 0 || (neiReg.Id & BORDER_REG) != 0)
							continue;
						
						//visit
						stack.Add(neiReg.Id);
						neiReg.Visited = true;
					}
				}

				//if the accumulated region size is too small, remove it
				//do not remove areas which connect to tile borders as their size can't be estimated correctly
				//and removing them can potentially remove necessary areas
				if (spanCount < minRegionArea && !connectsToBorder)
				{
					//kill all visited regions
					for (int j = 0; j < trace.Count; j++)
					{
						regions[trace[j]].SpanCount = 0;
						regions[trace[j]].Id = 0;
					}
				}

			}

			//Merge too small regions to neighbor regions
			int mergeCount = 0;
			do
			{
				mergeCount = 0;
				for (int i = 0; i < numRegions; i++)
				{
					Region reg = regions[i];
					if (reg.Id == 0 || (reg.Id & BORDER_REG) != 0)
						continue;
					if (reg.SpanCount == 0)
						continue;

					//check to see if region should be merged
					if (reg.SpanCount > mergeRegionSize && reg.IsRegionConnectedToBorder())
						continue;
					
					//small region with more than one connection or region which is not connected to border at all
					//find smallest neighbor that connects to this one
					int smallest = 0xfffffff; 
					ushort mergeId = reg.Id;
					for (int j = 0; j < reg.Connections.Count; j++)
					{
						if ((reg.Connections[i] & BORDER_REG) != 0) continue;
						Region mreg = regions[reg.Connections[j]];
						if (mreg.Id == 0 || (mreg.Id & BORDER_REG) != 0) continue;
						if (mreg.SpanCount < smallest && reg.CanMergeWithRegion(mreg) && mreg.CanMergeWithRegion(reg))
						{
							smallest = mreg.SpanCount;
							mergeId = mreg.Id;
						}
					}

					//found new id
					if (mergeId != reg.Id)
					{
						ushort oldId = reg.Id;
						Region target = regions[mergeId];

						//merge regions
						if (target.MergeWithRegion(reg))
						{
							//fix regions pointing to current region
							for (int j = 0; j < numRegions; j++)
							{
								if (regions[j].Id == 0 || (regions[j].Id & BORDER_REG) != 0) continue;
								
								//if another regions was already merged into current region
								//change the nid of the previous region too
								if (regions[j].Id == oldId)
									regions[j].Id = mergeId;
								
								//replace current region with new one if current region is neighbor
								regions[j].ReplaceNeighbour(oldId, mergeId);
							}
							mergeCount++;
						}
					}

				}

			}
			while (mergeCount > 0);

			//Compress region ids
			for (int i = 0; i < numRegions; i++)
			{
				regions[i].Remap = false;
				if (regions[i].Id == 0) continue; //skip nil regions
				if ((regions[i].Id & BORDER_REG) != 0) continue; //skip external regions
				regions[i].Remap = true;
			}

			ushort regIdGen = 0;
			for (int i = 0; i < numRegions; i++)
			{
				if (!regions[i].Remap)
					continue;

				ushort oldId = regions[i].Id;
				ushort newId = ++regIdGen;
				for (int j = i; j < numRegions; j++)
				{
					if (regions[j].Id == oldId)
					{
						regions[j].Id = newId;
						regions[j].Remap = false;
					}
				}
			}

			maxRegionId = regIdGen;

			//Remap regions
			for (int i = 0; i < spans.Length; i++)
			{
				if ((srcReg[i] & BORDER_REG) == 0)
					srcReg[i] = regions[srcReg[i]].Id;
			}

			return true;
		}

		/// <summary>
		/// Fill in a rectangular region with a region id.
		/// </summary>
		/// <param name="minX">minimum x</param>
		/// <param name="maxX">maximum x</param>
		/// <param name="minY">minimum y</param>
		/// <param name="maxY">maximum y</param>
		/// <param name="regionId">value to fill with</param>
		/// <param name="srcReg">array to store the values</param>
		public void PaintRectRegion(int minX, int maxX, int minY, int maxY, ushort regionId, ushort[] srcReg)
		{
			for (int y = minY; y < maxY; y++)
			{
				for (int x = minX; x < maxX; x++)
				{
					CompactCell c = cells[x + y * width];
					for (int i = c.StartIndex, end = c.StartIndex + c.Count; i < end; i++)
					{
						if (areas[i] != AreaFlags.Null)
							srcReg[i] = regionId;
					}
				}
			}
		}

		/// <summary>
		/// The central method for building regions, which consists of connected, non-overlapping walkable spans.
		/// </summary>
		/// <param name="borderSize"></param>
		/// <param name="minRegionArea">If smaller than this value, region will be null</param>
		/// <param name="mergeRegionArea">Reduce unneccesarily small regions</param>
		/// <returns></returns>
		public bool BuildRegions(int borderSize, int minRegionArea, int mergeRegionArea)
		{
			ushort[] srcReg = new ushort[spans.Length];
			ushort[] srcDist = new ushort[spans.Length];
			ushort[] dstReg = new ushort[spans.Length];
			ushort[] dstDist = new ushort[spans.Length];

			BuildDistanceField();

			ushort regionId = 1;
			ushort level = (ushort)((maxDistance + 1) & ~1); //find a better way to compute this

			const int ExpandIters = 8;

			if (borderSize > 0)
			{
				//make sure border doesn't overflow
				int borderWidth = Math.Min(width, borderSize);
				int borderHeight = Math.Min(height, borderSize);

				//paint regions
				PaintRectRegion(0, borderWidth, 0, height, (ushort)(regionId | BORDER_REG), srcReg);
				regionId++;
				PaintRectRegion(width - borderWidth, width, 0, height, (ushort)(regionId | BORDER_REG), srcReg);
				regionId++;
				PaintRectRegion(0, width, 0, borderHeight, (ushort)(regionId | BORDER_REG), srcReg);
				regionId++;
				PaintRectRegion(0, width, height - borderHeight, height, (ushort)(regionId | BORDER_REG), srcReg);
				regionId++;

				this.borderSize = borderSize;
			}

			while (level > 0)
			{
				level = (ushort)(level >= 2 ? level - 2 : 0);

				//expand current regions until no new empty connected cells found
				if (ExpandRegions(ExpandIters, level, srcReg, srcDist, dstReg, dstDist) != srcReg)
				{
					ushort[] temp = srcReg;
					srcReg = dstReg;
					dstReg = temp;

					temp = srcDist;
					srcDist = dstDist;
					dstDist = temp;
				}

				//mark new regions with ids
				for (int y = 0; y < length; y++)
				{
					for (int x = 0; x < width; x++)
					{
						CompactCell c = cells[x + y * width];
						for (int i = c.StartIndex, end = c.StartIndex + c.Count; i < end; i++)
						{
							if (distances[i] < level || srcReg[i] != 0 || areas[i] == AreaFlags.Null)
								continue;

							if (FloodRegion(x, y, i, level, regionId, srcReg, srcDist))
								regionId++;
						}
					}
				}
			}

			//expand current regions until no new empty connected cells found
			if (ExpandRegions(ExpandIters * 8, 0, srcReg, srcDist, dstReg, dstDist) != srcReg)
			{
				ushort[] temp = srcReg;
				srcReg = dstReg;
				dstReg = temp;

				temp = srcDist;
				srcDist = dstDist;
				dstDist = temp;
			}

			//filter out small regions
			this.maxRegions = regionId;
			if (!FilterSmallRegions(minRegionArea, mergeRegionArea, ref this.maxRegions, srcReg))
				return false;

			//write the result out
			for (int i = 0; i < spans.Length; i++)
				spans[i].Region = srcReg[i];

			return true;
		}
	}
}
