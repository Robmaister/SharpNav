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
			spans = new Span[field.SpanCount];

			//iterate over the Heightfield's cells
			int spanIndex = 0;
			for (int i = 0; i < cells.Length; i++)
			{
				//get the spans and create a new cell here
				var fs = field[i].Spans;
				Cell c = new Cell(spanIndex, fs.Count);
				cells[i] = c;

				//convert the closed spans to open spans, making sure the last span has no upper bound
				if (c.Count > 0)
				{
					int lastInd = c.Count - 1;
					for (int j = 0; j < lastInd; j++, spanIndex++)
						spans[spanIndex] = new Span(fs[j].Maximum, fs[j + 1].Minimum);

					spans[spanIndex] = new Span(fs[lastInd].Maximum, int.MaxValue);
					spanIndex++;
				}
			}
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
			public int Maximum;

			public Span(int minimum, int maximum)
			{
				this.Minimum = minimum;
				this.Maximum = maximum;
			}

			public bool HasUpperBound { get { return Maximum != int.MaxValue; } }
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
