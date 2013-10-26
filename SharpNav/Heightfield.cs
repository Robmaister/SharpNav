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
	/// <summary>
	/// A Heightfield represents a "voxel" grid represented as a 2-dimensional grid of <see cref="SharpNav.Heightfield+Cell"/>s.
	/// </summary>
	public class Heightfield : IEnumerable<Heightfield.Cell>
	{
		private BBox3 bounds;

		private int width, height, length;
		private float cellSize, cellHeight;

		private Cell[] cells;

		public Heightfield(Vector3 min, Vector3 max, float cellSize, float cellHeight)
		{
			if (min.X > max.X || min.Y > max.Y || min.Z > max.Z)
				throw new ArgumentException("The minimum bound of the heightfield must be less than the maximum bound of the heightfield on all axes.");

			if (cellSize <= 0)
				throw new ArgumentOutOfRangeException("cellSize", "Cell size must be greater than 0.");

			if (cellHeight <= 0)
				throw new ArgumentOutOfRangeException("cellHeight", "Cell height must be greater than 0.");

			this.cellSize = cellSize;
			this.cellHeight = cellHeight;

			width = (int)Math.Ceiling((max.X - min.X) / cellSize);
			height = (int)Math.Ceiling((max.Y - min.Y) / cellHeight);
			length = (int)Math.Ceiling((max.Z - min.Z) / cellSize);

			bounds.Min = min;

			max.X = min.X + width * cellSize;
			max.Y = min.Y + height * cellHeight;
			max.Z = min.Z + length * cellSize;
			bounds.Max = max;

			cells = new Cell[width * length];
			for (int i = 0; i < cells.Length; i++)
				cells[i] = new Cell(height);
		}

		/// <summary>
		/// Gets the <see cref="Heightfield.Cell"/> at the specified coordinate.
		/// </summary>
		/// <param name="x">The x coordinate.</param>
		/// <param name="y">The y coordinate.</param>
		public Cell this[int x, int y]
		{
			get
			{
				if (x < 0 || x >= width || y < 0 || y >= length)
					throw new IndexOutOfRangeException();

				return cells[y * width + x];
			}
		}

		/// <summary>
		/// Gets the <see cref="Heightfield.Cell"/> at the specified index.
		/// </summary>
		/// <param name="i">The index.</param>
		public Cell this[int i]
		{
			get
			{
				return cells[i];
			}
		}

		/// <summary>
		/// Gets the bounding box of the heightfield.
		/// </summary>
		public BBox3 Bounds { get { return bounds; } }

		/// <summary>
		/// Gets the world-space minimum.
		/// </summary>
		/// <value>The minimum.</value>
		public Vector3 Minimum { get { return bounds.Min; } }

		/// <summary>
		/// Gets the world-space maximum.
		/// </summary>
		/// <value>The maximum.</value>
		public Vector3 Maximum { get { return bounds.Max; } }

		/// <summary>
		/// Gets the number of cells in the X direction.
		/// </summary>
		/// <value>The width.</value>
		public int Width { get { return width; } }

		/// <summary>
		/// Gets the number of cells in the Y (up) direction.
		/// </summary>
		/// <value>The height.</value>
		public int Height { get { return height; } }

		/// <summary>
		/// Gets the number of cells in the Z direction.
		/// </summary>
		/// <value>The length.</value>
		public int Length { get { return length; } }

		/// <summary>
		/// Gets the size of a cell (voxel).
		/// </summary>
		/// <value>The size of the cell.</value>
		public Vector3 CellSize { get { return new Vector3(cellSize, cellHeight, cellSize); } }

		/// <summary>
		/// Gets the size of a cell on the X and Z axes.
		/// </summary>
		public float CellSizeXZ { get { return cellSize; } }

		/// <summary>
		/// Gets the size of a cell on the Y axis.
		/// </summary>
		public float CellHeight { get { return cellHeight; } }

		/// <summary>
		/// Gets the total number of spans.
		/// </summary>
		public int SpanCount
		{
			get
			{
				int count = 0;
				for (int i = 0; i < cells.Length; i++)
					count += cells[i].Spans.Count;

				return count;
			}
		}

		public void RasterizeTriangles(Triangle3[] tris)
		{
			for (int i = 0; i < tris.Length; i++)
				RasterizeTriangle(tris[i]);
		}

		public void RasterizeTriangle(Triangle3 tri)
		{
			float invCellSize = 1f / cellSize;
			float invCellHeight = 1f / cellHeight;

			BBox3 bbox;
			Triangle3.GetBoundingBox(ref tri, out bbox);

			//make sure that the triangle is at least in one cell.
			if (!BBox3.Overlapping(ref bbox, ref bounds))
				return;

			float boundHeight = bounds.Max.Y - bounds.Min.Y;

			//figure out which cells the triangle touches.
			int x0 = (int)((bbox.Min.X - bounds.Min.X) * invCellSize);
			int z0 = (int)((bbox.Min.Z - bounds.Min.Z) * invCellSize);
			int x1 = (int)((bbox.Max.X - bounds.Min.X) * invCellSize);
			int z1 = (int)((bbox.Max.Z - bounds.Min.Z) * invCellSize);

			//clamp to the field boundaries.
			MathHelper.Clamp(x0, 0, width - 1);
			MathHelper.Clamp(z0, 0, length - 1);
			MathHelper.Clamp(x1, 0, width - 1);
			MathHelper.Clamp(z1, 0, length - 1);

			Vector3[] inVerts = new Vector3[7], outVerts = new Vector3[7], inRowVerts = new Vector3[7];

			for (int z = z0; z <= z1; z++)
			{
				//copy the original vertices to the array.
				inVerts[0] = tri.A;
				inVerts[1] = tri.B;
				inVerts[2] = tri.C;

				//clip the triangle to the row
				int nvrow = 3;
				float cz = bounds.Min.Z + z * cellSize;
				nvrow = ClipPolygon(inVerts, outVerts, nvrow, 0, 1, -cz);
				if (nvrow < 3)
					continue;
				nvrow = ClipPolygon(outVerts, inRowVerts, nvrow, 0, -1, cz + cellSize);
				if (nvrow < 3)
					continue;

				for (int x = x0; x <= x1; x++)
				{
					//clip the triangle to the column
					int nv = nvrow;
					float cx = bounds.Min.X + x * cellSize;
					nv = ClipPolygon(inRowVerts, outVerts, nv, 1, 0, -cx);
					if (nv < 3)
						continue;
					nv = ClipPolygon(outVerts, inVerts, nv, -1, 0, cx + cellSize);
					if (nv < 3)
						continue;

					//calculate the min/max of the polygon
					float sMin = inVerts[0].Y, sMax = sMin;
					for (int i = 1; i < nv; i++)
					{
						float y = inVerts[i].Y;
						sMin = Math.Min(sMin, y);
						sMax = Math.Max(sMax, y);
					}


					//normalize span bounds to bottom of heightfield
					float bMinY = bounds.Min.Y;
					sMin -= bMinY;
					sMax -= bMinY;

					//if the spans are outside the heightfield, skip.
					if (sMax < 0f || sMin > boundHeight)
						continue;

					//clamp the span to the heightfield.
					if (sMin < 0)
						sMin = 0;
					if (sMax > boundHeight)
						sMax = boundHeight;

					//snap to grid
					int spanMin = MathHelper.Clamp((int)(sMin * invCellHeight), 0, height);
					int spanMax = MathHelper.Clamp((int)Math.Ceiling(sMax * invCellHeight), spanMin + 1, height);

					if (spanMin == spanMax)
					{
						Console.WriteLine("No-thickness span");
						continue;
					}

					//add the span
					cells[z * width + x].AddSpan(new Span(spanMin, spanMax, AreaFlags.Walkable));
				}
			}
		}

		/// <summary>
		/// If two neighboring spans have a small difference in maximum height (such as stairs), 
		/// then make sure the spans are walkable.
		///
		/// Warning: Known to override the effect of filterLedgeSpans() function.
		///</summary>
		public void FilterLowHangingWalkableObstacles(int walkableClimb)
		{
			for (int y = 0; y < length; ++y)
			{
				for (int x = 0; x < width; ++x)
				{
					Span previousSpan = new Span(0, 0, AreaFlags.Null);
					bool previousWalkable = false;
					AreaFlags previousArea = AreaFlags.Null;

					Cell c = cells[x + y * width];
					List<Span> spans = c.Spans;

					for (int i = 0; i < spans.Count; i++)
					{
						Span currentSpan = spans[i];

						bool walkable;
						walkable = (currentSpan.Area != AreaFlags.Null);

						// If current span is not walkable, but there is walkable
						// span just below it, mark the span above it walkable too.
						if (!walkable && previousWalkable)
						{
							if (Math.Abs((int)currentSpan.Maximum - (int)previousSpan.Maximum) <= walkableClimb)
								currentSpan.Area = previousArea;
						}
						// Copy walkable flag so that it cannot propagate
						// past multiple non-walkable objects.
						previousWalkable = walkable;
						previousArea = currentSpan.Area;

						//advance to the next span, keeping track of the previous span
						previousSpan = currentSpan;

						//make sure to save the new span information
						spans[i] = currentSpan;
					}
				}
			}
		}

		/// <summary>
		/// If two spans have little vertical space in between them, 
		/// then span is considered unwalkable
		/// </summary>
		/// <param name="walkableHeight">The clearance</param>
		public void FilterWalkableLowHeightSpans(int walkableHeight)
		{
			const int MAX_HEIGHT = int.MaxValue;

			// Remove walkable flag from spans which do not have enough
			// space above them for the agent to stand there.
			for (int y = 0; y < length; ++y)
			{
				for (int x = 0; x < width; ++x)
				{
					Cell c = cells[x + y * width];
					List<Span> spans = c.Spans;

					for (int i = 0; i < spans.Count; i++)
					{
						Span currentSpan = spans[i];
						Span nextSpan;

						int bot = (int)(currentSpan.Maximum);
						int top;
						if (i != spans.Count - 1)
						{
							nextSpan = spans[i + 1];
							top = (int)(nextSpan.Minimum);
						}
						else
						{
							top = MAX_HEIGHT;
						}

						//too low, not enough space to walk through
						if ((top - bot) <= walkableHeight)
							currentSpan.Area = AreaFlags.Null;

						//save span data
						spans[i] = currentSpan;
					}
				}
			}
		}

		/// <summary>
		/// ---NEEDS TESTING!---
		/// A ledge is unwalkable because the differenc between the maximum height of two spans 
		/// is too large of a drop (i.e. greater than walkableClimb).
		/// </summary>
		public void FilterLedgeSpans(int walkableHeight, int walkableClimb)
		{
			const int MAX_HEIGHT = int.MaxValue;

			// Mark border spans.
			for (int y = 0; y < length; ++y)
			{
				for (int x = 0; x < width; ++x)
				{
					Cell c = cells[x + y * width];
					List<Span> spans = c.Spans;

					//Examine all the spans in each cell
					for (int i = 0; i < spans.Count; i++)
					{
						Span currentSpan = spans[i];
						Span nextSpan;

						// Skip non walkable spans.
						if (currentSpan.Area == AreaFlags.Null)
							continue;

						int bottom = (int)(currentSpan.Maximum);
						int top;
						if (i != spans.Count - 1)
						{
							nextSpan = spans[i + 1];
							top = (int)(nextSpan.Minimum);
						}
						else
						{
							top = MAX_HEIGHT;
						}

						// Find neighbours minimum height.
						int minHeight = MAX_HEIGHT;

						// Min and max height of accessible neighbours.
						int accessibleMin = currentSpan.Maximum;
						int accessibleMax = currentSpan.Maximum;

						for (int dir = 0; dir < 4; ++dir)
						{
							int dx = x + MathHelper.GetDirOffsetX(dir);
							int dy = y + MathHelper.GetDirOffsetY(dir);
							// Skip neighbours which are out of bounds.
							if (dx < 0 || dy < 0 || dx >= width || dy >= length)
							{
								minHeight = Math.Min(minHeight, -walkableClimb - bottom);
								continue;
							}

							// From minus infinity to the first span.
							Cell neighborCell = cells[dx + dy * width];
							List<Span> neighborSpans = neighborCell.Spans;
							Span currentNeighborSpan;
							int neighborBottom = -walkableClimb;
							int neighborTop;
							if (neighborSpans.Count >= 1 && !neighborSpans[0].Equals(null))
							{
								currentNeighborSpan = neighborCell.Spans[0];
								neighborTop = currentNeighborSpan.Minimum;
							}
							else
							{
								neighborTop = MAX_HEIGHT;
							}

							// Skip neightbour if the gap between the spans is too small.
							if (Math.Min(top, neighborTop) - Math.Max(bottom, neighborBottom) > walkableHeight)
								minHeight = Math.Min(minHeight, neighborBottom - bottom);

							// Rest of the spans.
							for (int j = 0; j < neighborSpans.Count; j++)
							{
								currentNeighborSpan = neighborSpans[j];
								Span nextNeighborSpan;
								neighborBottom = (int)currentNeighborSpan.Maximum;
								if (j != neighborSpans.Count - 1)
								{
									nextNeighborSpan = neighborSpans[j + 1];
									neighborTop = (int)nextNeighborSpan.Minimum;
								}
								else
								{
									neighborTop = MAX_HEIGHT;
								}

								// Skip neightbour if the gap between the spans is too small.
								if (Math.Min(top, neighborTop) - Math.Max(bottom, neighborBottom) > walkableHeight)
								{
									minHeight = Math.Min(minHeight, neighborBottom - bottom);

									// Find min/max accessible neighbour height.
									if (Math.Abs(neighborBottom - bottom) <= walkableClimb)
									{
										if (neighborBottom < accessibleMin) accessibleMin = neighborBottom;
										if (neighborBottom > accessibleMax) accessibleMax = neighborBottom;
									}

								}

							}
						}

						// The current span is close to a ledge if the drop to any
						// neighbour span is less than the walkableClimb.
						if (minHeight < -walkableClimb)
							currentSpan.Area = AreaFlags.Null;

						// If the difference between all neighbours is too large,
						// we are at steep slope, mark the span as ledge.
						if ((accessibleMax - accessibleMin) > walkableClimb)
						{
							currentSpan.Area = AreaFlags.Null;
						}

						//save span data
						spans[i] = currentSpan;
					}
				}
			}
		}

		/// <summary>
		/// Enumerates over the heightfield row-by-row.
		/// </summary>
		/// <returns>The enumerator.</returns>
		public IEnumerator<Heightfield.Cell> GetEnumerator()
		{
			return ((IEnumerable<Cell>)cells).GetEnumerator();
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return cells.GetEnumerator();
		}

		/// <summary>
		/// Clips a polygon to a plane using the Sutherland-Hodgman algorithm.
		/// </summary>
		/// <param name="inVertices">The input array of vertices.</param>
		/// <param name="outVertices">The output array of vertices.</param>
		/// <param name="numVerts">The number of vertices to read from the arrays.</param>
		/// <param name="planeX">The clip plane's X component.</param>
		/// <param name="planeZ">The clip plane's Z component.</param>
		/// <param name="planeD">The clip plane's D component.</param>
		/// <returns>The number of vertices stored in outVertices.</returns>
		private int ClipPolygon(Vector3[] inVertices, Vector3[] outVertices, int numVerts, float planeX, float planeZ, float planeD)
		{
			float[] distances = new float[12];
			for (int i = 0; i < numVerts; i++)
				distances[i] = planeX * inVertices[i].X + planeZ * inVertices[i].Z + planeD;

			int m = 0;
			for (int i = 0, j = numVerts - 1; i < numVerts; j = i, i++)
			{
				bool inj = distances[j] >= 0;
				bool ini = distances[i] >= 0;
				if (inj != ini)
				{
					float s = distances[j] / (distances[j] - distances[i]);
					outVertices[m].X = inVertices[j].X + (inVertices[i].X - inVertices[j].X) * s;
					outVertices[m].Y = inVertices[j].Y + (inVertices[i].Y - inVertices[j].Y) * s;
					outVertices[m].Z = inVertices[j].Z + (inVertices[i].Z - inVertices[j].Z) * s;
					m++;
				}
				if (ini)
				{
					outVertices[m] = inVertices[i];
					m++;
				}
			}

			return m;
		}

		/// <summary>
		/// A cell is a column of voxels represented in <see cref="SharpNav.Heightfield+Span"/>s.
		/// </summary>
		public class Cell
		{
			private List<Span> spans;
			private int height;

			/// <summary>
			/// Initializes a new instance of the <see cref="SharpNav.Heightfield+Cell"/> class.
			/// </summary>
			/// <param name="height">The number of voxels in the column.</param>
			public Cell(int height)
			{
				this.height = height;
				spans = new List<Span>();
			}

			/// <summary>
			/// Gets the <see cref="SharpNav.Heightfield+Span"/> that contains the specified voxel.
			/// </summary>
			/// <param name="location">The voxel to search for.</param>
			public Span? this[int location]
			{
				get
				{
					if (location < 0 || location >= height)
						throw new IndexOutOfRangeException("Location must be a value between 0 and " + height + ".");

					//iterate the list of spans
					foreach (Span s in spans)
					{
						if (s.Minimum > location)
							break;
						else if (s.Maximum >= location)
							return s;
					}
					return null;
				}
			}

			public int Height { get { return height; } }

			//public int SpanCount { get { return spans.Count; } }

			/// <summary>
			/// [REMOVED] Reason: Spans need to be modified [REMOVED]
			/// Gets a readonly list of all the <see cref="SharpNav.Heightfield+Span"/>s contained in the cell.
			/// </summary>
			/// <value>A readonly list of spans.</value>
			//public IReadOnlyList<Span> Spans { get { return spans.AsReadOnly(); } }

			/// <summary>
			/// Gets a list of all the <see cref="SharpNav.Heightfield+Span"/>s contained in the cell.
			/// </summary>
			/// <value>A list of spans for modification</value>
			public List<Span> Spans { get { return spans; } }

			/// <summary>
			/// Adds a <see cref="SharpNav.Heightfield+Span"/> to the cell.
			/// </summary>
			/// <param name="span">A span.</param>
			public void AddSpan(Span span)
			{
				//clamp the span to the cell's range of [0, maxHeight]
				MathHelper.Clamp(ref span.Minimum, 0, height);
				MathHelper.Clamp(ref span.Maximum, 0, height);

				if (span.Minimum == span.Maximum)
					throw new ArgumentException("Span has no thickness.");
				else if (span.Maximum < span.Minimum)
					throw new ArgumentException("Span is inverted. Maximum is less than minimum.");

				for (int i = 0; i < spans.Count; i++)
				{
					//check whether the current span is below, or overlapping existing spans.
					//if the span is completely above the current span the loop will continue.
					Span cur = spans[i];
					if (cur.Minimum > span.Maximum)
					{
						//The new span is below the current one and is not intersecting.
						spans.Insert(i, span);
						return;
					}
					else if (cur.Maximum >= span.Minimum)
					{
						//merge spans so that the span to add includes the current span.
						if (cur.Minimum < span.Minimum)
							span.Minimum = cur.Minimum;
						if (cur.Maximum > span.Maximum)
							span.Maximum = cur.Maximum;

						//remove the current span and adjust i.
						//we do this to avoid duplicating the current span.
						spans.RemoveAt(i);
						i--;
					}
				}

				//if the span is not inserted, it is the highest span and will be added to the end.
				spans.Add(span);
			}
		}

		/// <summary>
		/// A span is a range of integers which represents a range of voxels in a <see cref="SharpNav.Heightfield+Cell"/>.
		/// </summary>
		[StructLayout(LayoutKind.Sequential)]
		public struct Span
		{
			/// <summary>
			/// The lowest value in the span.
			/// </summary>
			public int Minimum;

			/// <summary>
			/// The highest value in the span.
			/// </summary>
			public int Maximum;

			/// <summary>
			/// The span area id
			/// </summary>
			public AreaFlags Area;

			/// <summary>
			/// Initializes a new instance of the <see cref="SharpNav.Heightfield+Span"/> struct.
			/// </summary>
			/// <param name="min">The lowest value in the span.</param>
			/// <param name="max">The highest value in the span.</param>
			public Span(int min, int max)
			{
				Minimum = min;
				Maximum = max;
				Area = AreaFlags.Null;
			}

			/// <summary>
			/// Initializes a new instance of the <see cref="SharpNav.Heightfield+Span"/> struct.
			/// </summary>
			/// <param name="min">The lowest value in the span.</param>
			/// <param name="max">The highest value in the span.</param>
			/// <param name="area">The area flags for the span.</param>
			public Span(int min, int max, AreaFlags area)
			{
				Minimum = min;
				Maximum = max;
				Area = area;
			}

			/// <summary>
			/// Gets the height of the span.
			/// </summary>
			public int Height { get { return Maximum - Minimum; } }
		}
	}
}
