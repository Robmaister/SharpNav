#region License
/**
 * Copyright (c) 2013 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
 * Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE
 */
#endregion

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using SharpNav.Geometry;

namespace SharpNav
{
	class DistanceField
	{
		//temp variables
		private ushort[] src;
		private ushort[] dst;

		//necessary variables needed later
		private ushort[] distances;
		private ushort maxDistance;

		public ushort[] Distances { get { return distances; } }
		public ushort MaxDistance { get { return maxDistance; } }

		public DistanceField(OpenHeightfield openField)
		{
			ushort[] src = new ushort[openField.Spans.Length];
			ushort[] dst = new ushort[openField.Spans.Length];

			//fill up all the values in src
			CalculateDistanceField(openField);

			//find the maximum distance
			this.maxDistance = 0;
			for (int i = 0; i < openField.Spans.Length; i++)
				this.maxDistance = Math.Max(src[i], this.maxDistance);

			//blur 
			if (BoxBlur(openField, 1) != src)
			{
				src = dst;
			}

			//store distances
			this.distances = src;
		}

		/// <summary>
		/// A distance field estimates how far each span is from its nearest border span. This data is needed for region generation.
		/// </summary>
		/// <param name="src">Array of values, each corresponding to an individual span</param>
		/// <param name="maxDist">The maximum value of the src array</param>
		public void CalculateDistanceField(OpenHeightfield openField)
		{
			//initialize distance and points
			for (int i = 0; i < openField.Spans.Length; i++)
				src[i] = 0xffff;

			//mark boundary cells
			for (int y = 0; y < openField.Length; y++)
			{
				for (int x = 0; x < openField.Width; x++)
				{
					OpenHeightfield.Cell c = openField.Cells[y * openField.Width + x];
					for (int i = c.StartIndex, end = c.StartIndex + c.Count; i < end; i++)
					{
						OpenHeightfield.Span s = openField.Spans[i];
						AreaFlags area = openField.Areas[i];

						int numConnections = 0;
						for (int dir = 0; dir < 4; dir++)
						{
							if (OpenHeightfield.Span.GetConnection(dir, ref s) != OpenHeightfield.NotConnected)
							{
								int dx = x + MathHelper.GetDirOffsetX(dir);
								int dy = y + MathHelper.GetDirOffsetY(dir);
								int di = openField.Cells[dx + dy * openField.Width].StartIndex + OpenHeightfield.Span.GetConnection(dir, ref s);
								if (area == openField.Areas[di])
									numConnections++;
							}
						}
						if (numConnections != 4)
							src[i] = 0;
					}
				}
			}

			//pass 1
			for (int y = 0; y < openField.Length; y++)
			{
				for (int x = 0; x < openField.Width; x++)
				{
					OpenHeightfield.Cell c = openField.Cells[y * openField.Width + x];
					for (int i = c.StartIndex, end = c.StartIndex + c.Count; i < end; i++)
					{
						OpenHeightfield.Span s = openField.Spans[i];

						if (OpenHeightfield.Span.GetConnection(0, ref s) != OpenHeightfield.NotConnected)
						{
							//(-1, 0)
							int dx = x + MathHelper.GetDirOffsetX(0);
							int dy = y + MathHelper.GetDirOffsetY(0);
							int di = openField.Cells[dx + dy * openField.Width].StartIndex + OpenHeightfield.Span.GetConnection(0, ref s);
							OpenHeightfield.Span ds = openField.Spans[di];
							if (src[di] + 2 < src[i])
								src[i] = (ushort)(src[di] + 2);

							//(-1, -1)
							if (OpenHeightfield.Span.GetConnection(3, ref ds) != OpenHeightfield.NotConnected)
							{
								int ddx = dx + MathHelper.GetDirOffsetX(3);
								int ddy = dy + MathHelper.GetDirOffsetY(3);
								int ddi = openField.Cells[ddx + ddy * openField.Width].StartIndex + OpenHeightfield.Span.GetConnection(3, ref ds);
								if (src[ddi] + 3 < src[i])
									src[i] = (ushort)(src[ddi] + 3);
							}
						}

						if (OpenHeightfield.Span.GetConnection(3, ref s) != OpenHeightfield.NotConnected)
						{
							//(0, -1)
							int dx = x + MathHelper.GetDirOffsetX(3);
							int dy = y + MathHelper.GetDirOffsetY(3);
							int di = openField.Cells[dx + dy * openField.Width].StartIndex + OpenHeightfield.Span.GetConnection(3, ref s);
							OpenHeightfield.Span ds = openField.Spans[di];
							if (src[di] + 2 < src[i])
								src[i] = (ushort)(src[di] + 2);

							//(1, -1)
							if (OpenHeightfield.Span.GetConnection(2, ref ds) != OpenHeightfield.NotConnected)
							{
								int ddx = dx + MathHelper.GetDirOffsetX(2);
								int ddy = dy + MathHelper.GetDirOffsetY(2);
								int ddi = openField.Cells[ddx + ddy * openField.Width].StartIndex + OpenHeightfield.Span.GetConnection(2, ref ds);
								if (src[ddi] + 3 < src[i])
									src[i] = (ushort)(src[ddi] + 3);
							}
						}
					}
				}
			}

			//pass 2
			for (int y = openField.Length - 1; y >= 0; y--)
			{
				for (int x = openField.Width - 1; x >= 0; x--)
				{
					OpenHeightfield.Cell c = openField.Cells[y * openField.Width + x];
					for (int i = c.StartIndex, end = c.StartIndex + c.Count; i < end; i++)
					{
						OpenHeightfield.Span s = openField.Spans[i];

						if (OpenHeightfield.Span.GetConnection(2, ref s) != OpenHeightfield.NotConnected)
						{
							//(1, 0)
							int dx = x + MathHelper.GetDirOffsetX(2);
							int dy = y + MathHelper.GetDirOffsetY(2);
							int di = openField.Cells[dx + dy * openField.Width].StartIndex + OpenHeightfield.Span.GetConnection(2, ref s);
							OpenHeightfield.Span ds = openField.Spans[di];
							if (src[di] + 2 < src[i])
								src[i] = (ushort)(src[di] + 2);

							//(1, 1)
							if (OpenHeightfield.Span.GetConnection(1, ref ds) != OpenHeightfield.NotConnected)
							{
								int ddx = dx + MathHelper.GetDirOffsetX(1);
								int ddy = dy + MathHelper.GetDirOffsetY(1);
								int ddi = openField.Cells[ddx + ddy * openField.Width].StartIndex + OpenHeightfield.Span.GetConnection(1, ref ds);
								if (src[ddi] + 3 < src[i])
									src[i] = (ushort)(src[ddi] + 3);
							}
						}

						if (OpenHeightfield.Span.GetConnection(1, ref s) != OpenHeightfield.NotConnected)
						{
							//(0, 1)
							int dx = x + MathHelper.GetDirOffsetX(1);
							int dy = y + MathHelper.GetDirOffsetY(1);
							int di = openField.Cells[dx + dy * openField.Width].StartIndex + OpenHeightfield.Span.GetConnection(1, ref s);
							OpenHeightfield.Span ds = openField.Spans[di];
							if (src[di] + 2 < src[i])
								src[i] = (ushort)(src[di] + 2);

							//(-1, 1)
							if (OpenHeightfield.Span.GetConnection(0, ref ds) != OpenHeightfield.NotConnected)
							{
								int ddx = dx + MathHelper.GetDirOffsetX(0);
								int ddy = dy + MathHelper.GetDirOffsetY(0);
								int ddi = openField.Cells[ddx + ddy * openField.Width].StartIndex + OpenHeightfield.Span.GetConnection(0, ref ds);
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
		public ushort[] BoxBlur(OpenHeightfield openField, int thr)
		{
			thr *= 2;

			for (int y = 0; y < openField.Length; y++)
			{
				for (int x = 0; x < openField.Width; x++)
				{
					OpenHeightfield.Cell c = openField.Cells[y * openField.Width + x];
					for (int i = c.StartIndex, end = c.StartIndex + c.Count; i < end; i++)
					{
						OpenHeightfield.Span s = openField.Spans[i];
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
							if (OpenHeightfield.Span.GetConnection(dir, ref s) != OpenHeightfield.NotConnected)
							{
								int dx = x + MathHelper.GetDirOffsetX(dir);
								int dy = y + MathHelper.GetDirOffsetY(dir);
								int di = openField.Cells[dx + dy * openField.Width].StartIndex + OpenHeightfield.Span.GetConnection(dir, ref s);
								d += src[di];

								//check next span in next clockwise direction
								OpenHeightfield.Span ds = openField.Spans[di];
								int dir2 = (dir + 1) % 4;
								if (OpenHeightfield.Span.GetConnection(dir2, ref ds) != OpenHeightfield.NotConnected)
								{
									int dx2 = dx + MathHelper.GetDirOffsetX(dir2);
									int dy2 = dy + MathHelper.GetDirOffsetY(dir2);
									int di2 = openField.Cells[dx2 + dy2 * openField.Width].StartIndex + OpenHeightfield.Span.GetConnection(dir2, ref ds);
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
	}
}
