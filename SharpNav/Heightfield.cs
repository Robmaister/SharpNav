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
	public class Heightfield : IEnumerable<Heightfield.Cell>
	{
		private Vector3 min, max;

		private int width, height, length;
		private float cellWidth, cellHeight, cellLength;

		private Cell[] cells;

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

		public Cell this[int x, int y]
		{
			get
			{
				if (x < 0 || x >= width || y < 0 || y >= length)
					throw new IndexOutOfRangeException();

				return cells[y * width + x];
			}
		}

		public Vector3 Minimum { get { return min; } }
		public Vector3 Maximum { get { return max; } }

		public int Width { get { return width; } }
		public int Height { get { return height; } }
		public int Length { get { return length; } }

		public Vector3 CellSize { get { return new Vector3(cellWidth, cellHeight, cellLength); } }

		public IEnumerator<Heightfield.Cell> GetEnumerator()
		{
			return ((IEnumerable<Cell>)cells).GetEnumerator();
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return cells.GetEnumerator();
		}

		public class Cell
		{
			private List<Span> openSpans;
			private int maxHeight;

			public Cell(int height)
			{
				maxHeight = height - 1;
				openSpans = new List<Span>();
			}

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

			public IReadOnlyList<Span> Spans { get { return openSpans.AsReadOnly(); } }

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

		[StructLayout(LayoutKind.Sequential)]
		public struct Span
		{
			public int Minimum;
			public int Maximum;

			public Span(int min, int max)
			{
				Minimum = min;
				Maximum = max;
			}
		}
	}
}
