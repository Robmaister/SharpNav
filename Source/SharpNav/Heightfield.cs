// Copyright (c) 2013-2015 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
// Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using SharpNav.Geometry;

#if MONOGAME
using Vector3 = Microsoft.Xna.Framework.Vector3;
#elif OPENTK
using Vector3 = OpenTK.Vector3;
#elif SHARPDX
using Vector3 = SharpDX.Vector3;
#endif

namespace SharpNav
{
	/// <summary>
	/// A Heightfield represents a "voxel" grid represented as a 2-dimensional grid of <see cref="Cell"/>s.
	/// </summary>
	public partial class Heightfield
	{
		private BBox3 bounds;

		private int width, height, length;
		private float cellSize, cellHeight;

		private Cell[] cells;

		/// <summary>
		/// Initializes a new instance of the <see cref="Heightfield"/> class.
		/// </summary>
		/// <param name="b">The world-space bounds.</param>
		/// <param name="settings">The settings to build with.</param>
		public Heightfield(BBox3 b, NavMeshGenerationSettings settings)
			: this(b, settings.CellSize, settings.CellHeight)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="Heightfield"/> class.
		/// </summary>
		/// <param name="b">The world-space bounds.</param>
		/// <param name="cellSize">The world-space size of each cell in the XZ plane.</param>
		/// <param name="cellHeight">The world-space height of each cell.</param>
		public Heightfield(BBox3 b, float cellSize, float cellHeight)
		{
			if (!BBox3.IsValid(ref bounds))
				throw new ArgumentException("The bounds are considered invalid. See BBox3.IsValid for details.");

			if (cellSize <= 0)
				throw new ArgumentOutOfRangeException("cellSize", "Cell size must be greater than 0.");

			if (cellHeight <= 0)
				throw new ArgumentOutOfRangeException("cellHeight", "Cell height must be greater than 0.");

			this.cellSize = cellSize;
			this.cellHeight = cellHeight;
			this.bounds = b;

			//make sure the bbox contains all the possible voxels.
			width = (int)Math.Ceiling((b.Max.X - b.Min.X) / cellSize);
			height = (int)Math.Ceiling((b.Max.Y - b.Min.Y) / cellHeight);
			length = (int)Math.Ceiling((b.Max.Z - b.Min.Z) / cellSize);

			bounds.Max.X = bounds.Min.X + width * cellSize;
			bounds.Max.Y = bounds.Min.Y + height * cellHeight;
			bounds.Max.Z = bounds.Min.Z + length * cellSize;

			cells = new Cell[width * length];
			for (int i = 0; i < cells.Length; i++)
				cells[i] = new Cell(height);
		}

		/// <summary>
		/// Gets the bounding box of the heightfield.
		/// </summary>
		public BBox3 Bounds
		{
			get
			{
				return bounds;
			}
		}

		/// <summary>
		/// Gets the world-space minimum.
		/// </summary>
		/// <value>The minimum.</value>
		public Vector3 Minimum
		{
			get
			{
				return bounds.Min;
			}
		}

		/// <summary>
		/// Gets the world-space maximum.
		/// </summary>
		/// <value>The maximum.</value>
		public Vector3 Maximum
		{
			get
			{
				return bounds.Max;
			}
		}

		/// <summary>
		/// Gets the number of cells in the X direction.
		/// </summary>
		/// <value>The width.</value>
		public int Width
		{
			get
			{
				return width;
			}
		}

		/// <summary>
		/// Gets the number of cells in the Y (up) direction.
		/// </summary>
		/// <value>The height.</value>
		public int Height
		{
			get
			{
				return height;
			}
		}

		/// <summary>
		/// Gets the number of cells in the Z direction.
		/// </summary>
		/// <value>The length.</value>
		public int Length
		{
			get
			{
				return length;
			}
		}

		/// <summary>
		/// Gets the size of a cell (voxel).
		/// </summary>
		/// <value>The size of the cell.</value>
		public Vector3 CellSize
		{
			get
			{
				return new Vector3(cellSize, cellHeight, cellSize);
			}
		}

		/// <summary>
		/// Gets the size of a cell on the X and Z axes.
		/// </summary>
		public float CellSizeXZ
		{
			get
			{
				return cellSize;
			}
		}

		/// <summary>
		/// Gets the size of a cell on the Y axis.
		/// </summary>
		public float CellHeight
		{
			get
			{
				return cellHeight;
			}
		}

		/// <summary>
		/// Gets the total number of spans.
		/// </summary>
		public int SpanCount
		{
			get
			{
				int count = 0;
				for (int i = 0; i < cells.Length; i++)
					count += cells[i].WalkableSpanCount;

				return count;
			}
		}

		/// <summary>
		/// Gets the <see cref="Cell"/> at the specified coordinate.
		/// </summary>
		/// <param name="x">The x coordinate.</param>
		/// <param name="y">The y coordinate.</param>
		/// <returns>The cell at [x, y].</returns>
		public Cell this[int x, int y]
		{
			get
			{
				if (x < 0 || x >= width || y < 0 || y >= length)
					throw new ArgumentOutOfRangeException();

				return cells[y * width + x];
			}
		}

		/// <summary>
		/// Gets the <see cref="Cell"/> at the specified index.
		/// </summary>
		/// <param name="i">The index.</param>
		/// <returns>The cell at index i.</returns>
		public Cell this[int i]
		{
			get
			{
				if (i < 0 || i >= cells.Length)
					throw new ArgumentOutOfRangeException();

				return cells[i];
			}
		}

		/// <summary>
		/// Gets the <see cref="Span"/> at the reference.
		/// </summary>
		/// <param name="spanRef">A reference to a span.</param>
		/// <returns>The span at the reference.</returns>
		public Span this[SpanReference spanRef]
		{
			get
			{
				return cells[spanRef.Y * width + spanRef.X].Spans[spanRef.Index];
			}
		}

		/// <summary>
		/// Filters the heightmap to allow two neighboring spans have a small difference in maximum height (such as
		/// stairs) to be walkable.
		/// </summary>
		/// <remarks>
		/// This filter may override the results of <see cref="FilterLedgeSpans"/>.
		/// </remarks>
		/// <param name="walkableClimb">The maximum difference in height to filter.</param>
		public void FilterLowHangingWalkableObstacles(int walkableClimb)
		{
			//Loop through every cell in the Heightfield
			for (int i = 0; i < cells.Length; i++)
			{
				Cell c = cells[i];
				List<Span> spans = c.MutableSpans;

				//store the first span's data as the "previous" data
				Area prevArea = Area.Null;
				bool prevWalkable = prevArea != Area.Null;
				int prevMax = 0;

				//iterate over all the spans in the cell
				for (int j = 0; j < spans.Count; j++)
				{
					Span s = spans[j];
					bool walkable = s.Area != Area.Null;

					//if the current span isn't walkable but there's a walkable span right below it,
					//mark this span as walkable too.
					if (!walkable && prevWalkable)
					{
						if (Math.Abs(s.Maximum - prevMax) < walkableClimb)
							s.Area = prevArea;
					}

					//save changes back to the span list.
					spans[j] = s;

					//set the previous data for the next iteration
					prevArea = s.Area;
					prevWalkable = walkable;
					prevMax = s.Maximum;
				}
			}
		}

		/// <summary>
		/// If two spans have little vertical space in between them, 
		/// then span is considered unwalkable
		/// </summary>
		/// <param name="walkableHeight">The clearance.</param>
		public void FilterWalkableLowHeightSpans(int walkableHeight)
		{
			for (int i = 0; i < cells.Length; i++)
			{
				Cell c = cells[i];
				List<Span> spans = c.MutableSpans;

				//Iterate over all spans
				for (int j = 0; j < spans.Count - 1; j++)
				{
					Span currentSpan = spans[j];

					//too low, not enough space to walk through
					if ((spans[j + 1].Minimum - currentSpan.Maximum) <= walkableHeight)
					{
						currentSpan.Area = Area.Null;
						spans[j] = currentSpan;
					}
				}
			}
		}

		/// <summary>
		/// A ledge is unwalkable because the difference between the maximum height of two spans
		/// is too large of a drop (i.e. greater than walkableClimb).
		/// </summary>
		/// <param name="walkableHeight">The maximum walkable height to filter.</param>
		/// <param name="walkableClimb">The maximum walkable climb to filter.</param>
		public void FilterLedgeSpans(int walkableHeight, int walkableClimb)
		{
			//Mark border spans.
			Parallel.For(0, length, y =>
			{
			//for (int y = 0; y < length; y++)
			//{
				for (int x = 0; x < width; x++)
				{
					Cell c = cells[x + y * width];
					List<Span> spans = c.MutableSpans;

					//Examine all the spans in each cell
					for (int i = 0; i < spans.Count; i++)
					{
						Span currentSpan = spans[i];

						// Skip non walkable spans.
						if (currentSpan.Area == Area.Null)
							continue;

						int bottom = (int)currentSpan.Maximum;
						int top = (i == spans.Count - 1) ? int.MaxValue : spans[i + 1].Minimum;

						// Find neighbors minimum height.
						int minHeight = int.MaxValue;

						// Min and max height of accessible neighbors.
						int accessibleMin = currentSpan.Maximum;
						int accessibleMax = currentSpan.Maximum;

						for (var dir = Direction.West; dir <= Direction.South; dir++)
						{
							int dx = x + dir.GetHorizontalOffset();
							int dy = y + dir.GetVerticalOffset();

							// Skip neighbors which are out of bounds.
							if (dx < 0 || dy < 0 || dx >= width || dy >= length)
							{
								minHeight = Math.Min(minHeight, -walkableClimb - bottom);
								continue;
							}

							// From minus infinity to the first span.
							Cell neighborCell = cells[dy * width + dx];
							List<Span> neighborSpans = neighborCell.MutableSpans;
							int neighborBottom = -walkableClimb;
							int neighborTop = neighborSpans.Count > 0 ? neighborSpans[0].Minimum : int.MaxValue;

							// Skip neighbor if the gap between the spans is too small.
							if (Math.Min(top, neighborTop) - Math.Max(bottom, neighborBottom) > walkableHeight)
								minHeight = Math.Min(minHeight, neighborBottom - bottom);

							// Rest of the spans.
							for (int j = 0; j < neighborSpans.Count; j++)
							{
								Span currentNeighborSpan = neighborSpans[j];

								neighborBottom = currentNeighborSpan.Maximum;
								neighborTop = (j == neighborSpans.Count - 1) ? int.MaxValue : neighborSpans[j + 1].Minimum;

								// Skip neighbor if the gap between the spans is too small.
								if (Math.Min(top, neighborTop) - Math.Max(bottom, neighborBottom) > walkableHeight)
								{
									minHeight = Math.Min(minHeight, neighborBottom - bottom);

									// Find min/max accessible neighbor height.
									if (Math.Abs(neighborBottom - bottom) <= walkableClimb)
									{
										if (neighborBottom < accessibleMin) accessibleMin = neighborBottom;
										if (neighborBottom > accessibleMax) accessibleMax = neighborBottom;
									}
								}
							}
						}

						// The current span is close to a ledge if the drop to any
						// neighbor span is less than the walkableClimb.
						if (minHeight < -walkableClimb)
							currentSpan.Area = Area.Null;

						// If the difference between all neighbors is too large,
						// we are at steep slope, mark the span as ledge.
						if ((accessibleMax - accessibleMin) > walkableClimb)
							currentSpan.Area = Area.Null;

						//save span data
						spans[i] = currentSpan;
					}
				}
			//}
			});
		}
	}
}
