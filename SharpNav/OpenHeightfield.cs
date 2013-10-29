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
	public class OpenHeightfield
	{
		private BBox3 bounds;

		private int width, height, length;
		private float cellSize, cellHeight;

		private Cell[] cells;
		private Span[] spans;
		private AreaFlags[] areas;

		const int NotConnected = 0xff; //HACK figure out a better way to do this
		
		//variables that are part of region generation
		private ushort maxDist;
		private ushort [] dist;

		public OpenHeightfield(Heightfield field, int walkableHeight, int walkableClimb)
		{
			this.bounds = field.Bounds;
			this.width = field.Width;
			this.height = field.Height;
			this.length = field.Length;
			this.cellSize = field.CellSizeXZ;
			this.cellHeight = field.CellHeight;

			cells = new Cell[width * length];
			spans = new Span[field.SpanCount];
			areas = new AreaFlags[field.SpanCount];

			//iterate over the Heightfield's cells
			int spanIndex = 0;
			for (int i = 0; i < cells.Length; i++)
			{
				//get the heightfield span list, skip if empty
				var fs = field[i].Spans;
				if (fs.Count == 0)
					continue;

				Cell c = new Cell(spanIndex, 0);

				//convert the closed spans to open spans
				int lastInd = fs.Count - 1;
				for (int j = 0; j < lastInd; j++)
				{
					var s = fs[j];
					if (s.Area != AreaFlags.Null)
					{
						Span.FromMinMax(s.Maximum, fs[j + 1].Minimum, out spans[spanIndex]);
						spanIndex++;
						c.Count++;
					}
				}

				//the last closed span that has an "infinite" height
				var lastS = fs[lastInd];
				if (lastS.Area != AreaFlags.Null)
				{
					spans[spanIndex] = new Span(fs[lastInd].Maximum, int.MaxValue);
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
					Cell c = cells[y * width + x];
					for (int i = c.StartIndex, end = c.StartIndex + c.Count; i < end; i++)
					{
						Span s = spans[i];

						for (int dir = 0; dir < 4; dir++)
						{
							Span.SetConnection(dir, NotConnected, ref spans[i]);

							int dx = x + MathHelper.GetDirOffsetX(dir);
							int dy = y + MathHelper.GetDirOffsetY(dir);

							if (dx < 0 || dy < 0 || dx >= width || dy >= length)
								continue;

							Cell dc = cells[dy * width + dx];
							for (int j = dc.StartIndex, jEnd = dc.StartIndex + dc.Count; j < jEnd; j++)
							{
								Span ds = spans[j];

								int overlapBottom = Math.Max(s.Minimum, ds.Minimum);
								int overlapTop = Math.Min(s.Minimum + s.Height, ds.Minimum + ds.Height);

								if (!s.HasUpperBound && !ds.HasUpperBound)
									overlapTop = int.MaxValue;

								if ((overlapTop - overlapBottom) >= walkableHeight && Math.Abs(ds.Minimum - s.Minimum) <= walkableClimb)
								{
									int con = j - dc.StartIndex;
									if (con < 0 || con >= 0xff)
										throw new InvalidOperationException("The neighbor index is too high to store. Reduce the number of cells in the Y direction.");

									Span.SetConnection(dir, con, ref spans[i]);
									break;
								}
							}
						}
					}
				}
			}
		}

		public ushort[] Distances { get { return dist; } }
		public ushort MaxDist { get { return maxDist; } }

		public Cell[] Cells { get { return cells; } }
		public Span[] Spans { get { return spans; } }

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
					Cell c = cells[y * width + x];
					for (int i = c.StartIndex, end = c.StartIndex + c.Count; i < end; i++)
					{
						Span s = spans[i];
						AreaFlags area = areas[i];

						int numConnections = 0;
						for (int dir = 0; dir < 4; dir++)
						{
							if (Span.GetConnection(dir, ref s) != NotConnected)
							{
								int dx = x + MathHelper.GetDirOffsetX(dir);
								int dy = y + MathHelper.GetDirOffsetY(dir);
								int di = cells[dx + dy * width].StartIndex + Span.GetConnection(dir, ref s);
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
					Cell c = cells[y * width + x];
					for (int i = c.StartIndex, end = c.StartIndex + c.Count; i < end; i++)
					{
						Span s = spans[i];

						if (Span.GetConnection(0, ref s) != NotConnected)
						{
							//(-1, 0)
							int dx = x + MathHelper.GetDirOffsetX(0);
							int dy = y + MathHelper.GetDirOffsetY(0);
							int di = cells[dx + dy * width].StartIndex + Span.GetConnection(0, ref s);
							Span ds = spans[di];
							if (src[di] + 2 < src[i])
								src[i] = (ushort)(src[di] + 2);

							//(-1, -1)
							if (Span.GetConnection(3, ref ds) != NotConnected)
							{
								int ddx = dx + MathHelper.GetDirOffsetX(3);
								int ddy = dy + MathHelper.GetDirOffsetY(3);
								int ddi = cells[ddx + ddy * width].StartIndex + Span.GetConnection(3, ref ds);
								if (src[ddi] + 3 < src[i])
									src[i] = (ushort)(src[ddi] + 3);
							}
						}

						if (Span.GetConnection(3, ref s) != NotConnected)
						{
							//(0, -1)
							int dx = x + MathHelper.GetDirOffsetX(3);
							int dy = y + MathHelper.GetDirOffsetY(3);
							int di = cells[dx + dy * width].StartIndex + Span.GetConnection(3, ref s);
							Span ds = spans[di];
							if (src[di] + 2 < src[i])
								src[i] = (ushort)(src[di] + 2);

							//(1, -1)
							if (Span.GetConnection(2, ref ds) != NotConnected)
							{
								int ddx = dx + MathHelper.GetDirOffsetX(2);
								int ddy = dy + MathHelper.GetDirOffsetY(2);
								int ddi = cells[ddx + ddy * width].StartIndex + Span.GetConnection(2, ref ds);
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
					Cell c = cells[y * width + x];
					for (int i = c.StartIndex, end = c.StartIndex + c.Count; i < end; i++)
					{
						Span s = spans[i];

						if (Span.GetConnection(2, ref s) != NotConnected)
						{
							//(1, 0)
							int dx = x + MathHelper.GetDirOffsetX(2);
							int dy = y + MathHelper.GetDirOffsetY(2);
							int di = cells[dx + dy * width].StartIndex + Span.GetConnection(2, ref s);
							Span ds = spans[di];
							if (src[di] + 2 < src[i])
								src[i] = (ushort)(src[di] + 2);

							//(1, 1)
							if (Span.GetConnection(1, ref ds) != NotConnected)
							{
								int ddx = dx + MathHelper.GetDirOffsetX(1);
								int ddy = dy + MathHelper.GetDirOffsetY(1);
								int ddi = cells[ddx + ddy * width].StartIndex + Span.GetConnection(1, ref ds);
								if (src[ddi] + 3 < src[i])
									src[i] = (ushort)(src[ddi] + 3);
							}
						}

						if (Span.GetConnection(1, ref s) != NotConnected)
						{
							//(0, 1)
							int dx = x + MathHelper.GetDirOffsetX(1);
							int dy = y + MathHelper.GetDirOffsetY(1);
							int di = cells[dx + dy * width].StartIndex + Span.GetConnection(1, ref s);
							Span ds = spans[di];
							if (src[di] + 2 < src[i])
								src[i] = (ushort)(src[di] + 2);

							//(-1, 1)
							if (Span.GetConnection(0, ref ds) != NotConnected)
							{
								int ddx = dx + MathHelper.GetDirOffsetX(0);
								int ddy = dy + MathHelper.GetDirOffsetY(0);
								int ddi = cells[ddx + ddy * width].StartIndex + Span.GetConnection(0, ref ds);
								if (src[ddi] + 3 < src[i])
									src[i] = (ushort)(src[ddi] + 3);
							}
						}
					}
				}
			}

			this.maxDist = 0;
			for (int i = 0; i < spans.Length; i++)
				this.maxDist = Math.Max(src[i], maxDist);
		}

		/// <summary>
		/// Part of building the distance field. It may or may not return an array equal to src.
		/// </summary>
		/// <param name="thr">Threshold?</param>
		/// <param name="src">The source that contains all distance values</param>
		/// <param name="dst">Write all values to the destination</param>
		/// <returns></returns>
		public ushort[] BoxBlur(int thr, ushort[] src, ushort[] dst)
		{
			thr *= 2;

			for (int y = 0; y < length; y++)
			{
				for (int x = 0; x < width; x++)
				{
					Cell c = cells[y * width + x];
					for (int i = c.StartIndex, end = c.StartIndex + c.Count; i < end; i++)
					{
						Span s = spans[i];
						ushort cd = src[i];

						//in BuildDistanceField, thr = 1.
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
							if (Span.GetConnection(dir, ref s) != NotConnected)
							{
								int dx = x + MathHelper.GetDirOffsetX(dir);
								int dy = y + MathHelper.GetDirOffsetY(dir);
								int di = cells[dx + dy * width].StartIndex + Span.GetConnection(dir, ref s);
								d += src[di];

								//check spans in next direction
								Span ds = spans[di];
								int dir2 = (dir + 1) % 4;
								if (Span.GetConnection(dir2, ref ds) != NotConnected)
								{
									int dx2 = dx + MathHelper.GetDirOffsetX(dir2);
									int dy2 = dy + MathHelper.GetDirOffsetY(dir2);
									int di2 = cells[dx2 + dy2 * width].StartIndex + Span.GetConnection(dir2, ref ds);
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

		/// <summary>
		/// A required step in creating a heightfield.
		/// </summary>
		/// <returns></returns>
		public bool BuildDistanceField()
		{
			ushort[] src = new ushort[spans.Length];
			ushort[] dst = new ushort[spans.Length];

			//fill up all the values in src and get maxDist
			CalculateDistanceField(src);

			//blur 
			if (BoxBlur(1, src, dst) != src)
			{
				//swap src and dst
				ushort[] temp = src;
				src = dst;
				dst = temp;
			}

			//store distances
			this.dist = src;

			return true;
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
			Span s = spans[i];
			ushort r = 0;

			if (Span.GetConnection(dir, ref s) != NotConnected)
			{
				int dx = x + MathHelper.GetDirOffsetX(dir);
				int dy = y + MathHelper.GetDirOffsetY(dir);
				int di = cells[dx + dy * width].StartIndex + Span.GetConnection(dir, ref s);
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

			Span ss = spans[i];
			ushort curReg = 0;

			if (Span.GetConnection(dir, ref ss) != NotConnected)
			{
				int dx = x + MathHelper.GetDirOffsetX(dir);
				int dy = y + MathHelper.GetDirOffsetY(dir);
				int di = cells[dx + dy * width].StartIndex + Span.GetConnection(dir, ref ss);
				curReg = srcReg[di];
			}
			cont.Add(curReg);

			int iter = 0;
			while (++iter < 40000)
			{
				Span s = spans[i];

				if (IsSolidEdge(srcReg, x, y, i, dir))
				{
					//choose the edge corner
					ushort r = 0;
					if (Span.GetConnection(dir, ref s) != NotConnected)
					{
						int dx = x + MathHelper.GetDirOffsetX(dir);
						int dy = y + MathHelper.GetDirOffsetY(dir);
						int di = cells[dx + dy * width].StartIndex + Span.GetConnection(dir, ref s);
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
					if (Span.GetConnection(dir, ref s) != NotConnected)
					{
						Cell dc = cells[dx + dy * width];
						di = dc.StartIndex + Span.GetConnection(dir, ref s);
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
		/// <param name="maxRegionId"></param>
		/// <param name="srcReg"></param>
		/// <returns></returns>
		public bool FilterSmallRegions(int minRegionArea, int mergeRegionSize, ref ushort maxRegionId, ushort[] srcReg)
		{
			//HACK: Heightfield border flag. Unwalkable
			const ushort BORDER_REG = 0x8000;

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
					Cell c = cells[x + y * width];
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
							reg.addUniqueFloorRegion(floorId);
						}

						//have found contour
						if (reg.getConnections().Count > 0)
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
							WalkContour(srcReg, x, y, i, ndir, reg.getConnections());
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

					for (int j = 0; j < creg.getConnections().Count; j++)
					{
						if ((creg.getConnections()[j] & BORDER_REG) != 0)
						{
							connectsToBorder = true;
							continue;
						}
						
						Region neiReg = regions[creg.getConnections()[j]];
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
					if (reg.SpanCount > mergeRegionSize && reg.isRegionConnectedToBorder())
						continue;
					
					//small region with more than one connection or region which is not connected to border at all
					//find smallest neighbor that connects to this one
					int smallest = 0xfffffff; //HACK
					ushort mergeId = reg.Id;
					for (int j = 0; j < reg.getConnections().Count; j++)
					{
						if ((reg.getConnections()[i] & BORDER_REG) != 0) continue;
						Region mreg = regions[reg.getConnections()[j]];
						if (mreg.Id == 0 || (mreg.Id & BORDER_REG) != 0) continue;
						if (mreg.SpanCount < smallest && reg.canMergeWithRegion(mreg) && mreg.canMergeWithRegion(reg))
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
						if (target.mergeWithRegion(reg))
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
								regions[j].replaceNeighbour(oldId, mergeId);
							}
							mergeCount++;
						}
					}

				}

			} while (mergeCount > 0);

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

		public int Width { get { return width; } }
		public int Height { get { return height; } }
		public int Length { get { return length; } }

		/// <summary>
		/// Gets the <see cref="Heightfield.Cell"/> at the specified coordinate.
		/// </summary>
		/// <param name="x">The x coordinate.</param>
		/// <param name="y">The y coordinate.</param>
		public IEnumerable<Span> this[int x, int y]
		{
			get
			{
				if (x < 0 || x >= width || y < 0 || y >= length)
					throw new IndexOutOfRangeException();

				Cell c = cells[y * width + x];

				int end = c.StartIndex + c.Count;
				for (int i = c.StartIndex; i < end; i++)
					yield return spans[i];
			}
		}

		/// <summary>
		/// Gets the <see cref="Heightfield.Cell"/> at the specified index.
		/// </summary>
		/// <param name="i">The index.</param>
		public IEnumerable<Span> this[int i]
		{
			get
			{
				Cell c = cells[i];

				int end = c.StartIndex + c.Count;
				for (int j = c.StartIndex; j < end; j++)
					yield return spans[j];
			}
		}

		public struct Cell
		{
			public int StartIndex;
			public int Count;

			public Cell(int start, int count)
			{
				StartIndex = start;
				Count = count;
			}
		}

		public struct Span
		{
			public int Minimum;
			public int Height;
			public int Connections;
			public int Region;

			public Span(int minimum, int height)
			{
				this.Minimum = minimum;
				this.Height = height;
				this.Connections = ~0;
				this.Region = 0;
			}

			public bool HasUpperBound { get { return Height != int.MaxValue; } }
			public int Maximum { get { return Minimum + Height; } }

			public static Span FromMinMax(int min, int max)
			{
				Span s;
				FromMinMax(min, max, out s);
				return s;
			}

			public static void FromMinMax(int min, int max, out Span span)
			{
				span.Minimum = min;
				span.Height = max - min;
				span.Connections = ~0;
				span.Region = 0;
			}

			public static void SetConnection(int dir, int i, ref Span s)
			{
				//split the int up into 4 parts, 8 bits each
				int shift = dir * 8;
				s.Connections = (s.Connections & ~(0xff << shift)) | ((i & 0xff) << shift);
			}

			public static int GetConnection(int dir, Span s)
			{
				return GetConnection(dir, ref s);
			}

			public static int GetConnection(int dir, ref Span s)
			{
				return (s.Connections >> (dir * 8)) & 0xff;
			}

			/*public static void Overlap(ref Span a, ref Span b, out Span r)
			{
				int max = Math.Min(a.Minimum + a.Height, b.Minimum + b.Height);
				r.Minimum = a.Minimum > b.Minimum ? a.Minimum : b.Minimum;
				r.Height = max - r.Minimum;
				r.Connections = 0;
			}*/
		}
	}
}
