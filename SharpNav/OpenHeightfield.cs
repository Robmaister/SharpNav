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

		public OpenHeightfield(Heightfield field)
		{
			this.bounds = field.Bounds;
			this.width = field.Width;
			this.height = field.Height;
			this.length = field.Length;
			this.cellSize = field.CellSizeXZ;
			this.cellHeight = field.CellHeight;

			cells = new Cell[width * length];
			for (int i = 0; i < cells.Length; i++)
				cells[i] = new Cell(field[i]);
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

				return cells[y * width + x];
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
				return cells[i];
			}
		}

		public class Cell
		{
			private Span[] spans;

			public Cell(Heightfield.Cell cell)
			{
				var closedSpans = cell.Spans;
				spans = new Span[closedSpans.Count];

				if (spans.Length > 0)
				{
					int lastInd = closedSpans.Count - 1;
					for (int i = 0; i < lastInd; i++)
						spans[i] = new Span(closedSpans[i].Maximum, closedSpans[i + 1].Minimum);

					spans[lastInd] = new Span(closedSpans[lastInd].Maximum, int.MaxValue);
				}
			}

			//HACK indexer/copy of array?

			/// <summary>
			/// Gets a readonly list of all the <see cref="SharpNav.OpenHeightfield+Span"/>s contained in the cell.
			/// </summary>
			/// <value>A readonly list of spans.</value>
			public Span[] Spans { get { return spans; } }
		}

		public struct Span
		{
			public int Minimum;
			public int Maximum;

			public Span(int minimum, int maximum)
			{
				this.Minimum = minimum;
				this.Maximum = maximum;
			}

			public bool HasUpperLimit { get { return Maximum != int.MaxValue; } }
			public int Length { get { return Maximum - Minimum + 1; } }

			public static void Overlap(ref Span a, ref Span b, out Span r)
			{
				r = a;

				if (b.Minimum > a.Minimum)
					r.Minimum = b.Minimum;
				if (b.Maximum < a.Maximum)
					r.Maximum = b.Maximum;
			}
		}
	}
}
