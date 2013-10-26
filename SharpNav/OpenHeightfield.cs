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

			const int NotConnected = 0xff; //HACK figure out a better way to do this

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

		/// <summary>
		/// A distance field estimates how far each span is from its nearest border span. This data is needed for region generation.
		/// </summary>
		/// <param name="src">Array of values, each corresponding to an individual span</param>
		/// <param name="maxDist">The maximum value of the src array</param>
		public void CalculateDistanceField(ushort[] src, ref ushort maxDist)
		{
			//initialize distance and points
			for (int i = 0; i < spans.Length; i++)
				src[i] = 0xffff;

			const int NotConnected = 0xff; //HACK figure out a better way to do this

			//mark boundary cells
			for (int y = 0; y < length; y++)
			{
				for (int x = 0; x < width; x++)
				{
					Cell c = cells[y * width + x];
					for (int i = c.StartIndex, end = c.StartIndex + c.Count; i < end; i++)
					{
						Span s = spans[i];
						AreaFlags area = spans[i].Area;

						int numConnections = 0;
						for (int dir = 0; dir < 4; dir++)
						{
							if (Span.GetConnection(dir, ref s) != NotConnected)
							{
								int dx = x + MathHelper.GetDirOffsetX(dir);
								int dy = y + MathHelper.GetDirOffsetY(dir);
								int di = cells[dx + dy * width].StartIndex + Span.GetConnection(dir, ref s);
								if (area == spans[di].Area)
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
							if (Span.GetConnection(3, ref s) != NotConnected)
							{
								int ddx = x + MathHelper.GetDirOffsetX(3);
								int ddy = y + MathHelper.GetDirOffsetY(3);
								int ddi = cells[dx + dy * width].StartIndex + Span.GetConnection(3, ref s);
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
							if (Span.GetConnection(2, ref s) != NotConnected)
							{
								int ddx = x + MathHelper.GetDirOffsetX(2);
								int ddy = y + MathHelper.GetDirOffsetY(2);
								int ddi = cells[dx + dy * width].StartIndex + Span.GetConnection(2, ref s);
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
							if (Span.GetConnection(1, ref s) != NotConnected)
							{
								int ddx = x + MathHelper.GetDirOffsetX(1);
								int ddy = y + MathHelper.GetDirOffsetY(1);
								int ddi = cells[dx + dy * width].StartIndex + Span.GetConnection(1, ref s);
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
							if (Span.GetConnection(0, ref s) != NotConnected)
							{
								int ddx = x + MathHelper.GetDirOffsetX(0);
								int ddy = y + MathHelper.GetDirOffsetY(0);
								int ddi = cells[dx + dy * width].StartIndex + Span.GetConnection(0, ref s);
								if (src[ddi] + 3 < src[i])
									src[i] = (ushort)(src[ddi] + 3);
							}
						}
					}
				}
			}

			maxDist = 0;
			for (int i = 0; i < spans.Length; i++)
				maxDist = Math.Max(src[i], maxDist);
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
			public AreaFlags Area;

			public Span(int minimum, int height)
			{
				this.Minimum = minimum;
				this.Height = height;
				this.Connections = 0;
				Area = AreaFlags.Null;
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
				span.Connections = 0;
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
