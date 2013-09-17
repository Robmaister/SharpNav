#region License
/**
 * Copyright (c) 2013 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
 * Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace SharpNav
{
	/// <summary>
	/// A Heightfield represents a "voxel" grid represented as a 2-dimensional grid of <see cref="SharpNav.Heightfield+Cell"/>s.
	/// </summary>
	public class Heightfield : IEnumerable<Heightfield.Cell>
	{
		private Vector3 min, max;

		private int width, height, length;
		private float cellWidth, cellHeight, cellLength;

		private Cell[] cells;

		/// <summary>
		/// Initializes a new instance of the <see cref="SharpNav.Heightfield"/> class.
		/// </summary>
		/// <param name="minimum">World space minimum.</param>
		/// <param name="maximum">World space aximum.</param>
		/// <param name="cellCountX">Number of cells on the X axis.</param>
		/// <param name="cellCountY">Number of cells on the Y (up) axis.</param>
		/// <param name="cellCountZ">Number of cells on the Z axis.</param>
		public Heightfield(Vector3 minimum, Vector3 maximum, int cellCountX, int cellCountY, int cellCountZ)
		{
			if (min.X > max.X || min.Y > max.Y || min.Z > max.Z)
				throw new ArgumentException("The minimum bound of the heightfield must be less than the maximum bound of the heightfield on all axes.");

			if (cellCountX < 1)
				throw new ArgumentOutOfRangeException("cellCountX", "Cell counts must be at least 1.");

			if (cellCountZ < 1)
				throw new ArgumentOutOfRangeException("cellCountZ", "Cell counts must be at least 1.");

			min = minimum;
			max = maximum;

			width = cellCountX;
			height = cellCountY;
			length = cellCountZ;

			cellWidth = (max.X - min.X) / cellCountX;
			cellLength = (max.Z - min.Z) / cellCountZ;
			cellHeight = (max.Y - min.Y) / cellCountY;

			cells = new Cell[cellCountX * cellCountZ];
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
		/// Gets the world-space minimum.
		/// </summary>
		/// <value>The minimum.</value>
		public Vector3 Minimum { get { return min; } }

		/// <summary>
		/// Gets the world-space maximum.
		/// </summary>
		/// <value>The maximum.</value>
		public Vector3 Maximum { get { return max; } }

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
		public Vector3 CellSize { get { return new Vector3(cellWidth, cellHeight, cellLength); } }

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
		/// A cell is a column of voxels represented in <see cref="SharpNav.Heightfield+Span"/>s.
		/// </summary>
		public class Cell
		{
			private List<Span> openSpans;
			private int maxHeight;

			/// <summary>
			/// Initializes a new instance of the <see cref="SharpNav.Heightfield+Cell"/> class.
			/// </summary>
			/// <param name="height">The number of voxels in the column.</param>
			public Cell(int height)
			{
				maxHeight = height - 1;
				openSpans = new List<Span>();
			}

			/// <summary>
			/// Gets the <see cref="SharpNav.Heightfield+Span"/> that contains the specified voxel.
			/// </summary>
			/// <param name="location">The voxel to search for.</param>
			public Span? this[int location]
			{
				get
				{
					if (location < 0 || location > maxHeight)
						throw new IndexOutOfRangeException("Location must be a value between 0 and " + maxHeight + ".");

					//iterate the list of spans
					foreach (Span s in openSpans)
					{
						if (s.Minimum > location)
							break;
						else if (s.Maximum < location)
							continue;
						else
							return s;
					}
					return null;
				}
			}

			/// <summary>
			/// Gets a readonly list of all the <see cref="SharpNav.Heightfield+Span"/>s contained in the cell.
			/// </summary>
			/// <value>A readonly list of spans.</value>
			public IReadOnlyList<Span> Spans { get { return openSpans.AsReadOnly(); } }

			/// <summary>
			/// Adds a <see cref="SharpNav.Heightfield+Span"/> to the cell.
			/// </summary>
			/// <param name="span">A span.</param>
			public void AddSpan(Span span)
			{
				//clamp the span to the cell's range of [0, maxHeight]
				MathHelper.Clamp(ref span.Minimum, 0, maxHeight);
				MathHelper.Clamp(ref span.Maximum, 0, maxHeight);

				if (span.Minimum == span.Maximum)
					throw new ArgumentException("Span has no thickness.");
				else if (span.Maximum < span.Minimum)
					throw new ArgumentException("Span is inverted. Maximum is less than minimum.");

				for (int i = 0; i < openSpans.Count; i++)
				{
					//check whether the current span is below, or overlapping existing spans.
					//if the span is completely above the current span the loop will continue.
					Span cur = openSpans[i];
					if (cur.Minimum > span.Maximum)
					{
						//The new span is below the current one and is not intersecting.
						openSpans.Insert(i, span);
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
						openSpans.RemoveAt(i);
						i--;
					}
				}

				//if the span is not inserted, it is the highest span and will be added to the end.
				openSpans.Add(span);
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
			/// Initializes a new instance of the <see cref="SharpNav.Heightfield+Span"/> struct.
			/// </summary>
			/// <param name="min">The lowest value in the span.</param>
			/// <param name="max">The highest value in the span.</param>
			public Span(int min, int max)
			{
				Minimum = min;
				Maximum = max;
			}
		}
	}
}
