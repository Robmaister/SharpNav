// Copyright (c) 2013-2014 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
// Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace SharpNav
{
	/// <summary>
	/// A cell is a column of voxels represented in <see cref="Span"/>s.
	/// </summary>
	public class Cell
	{
		private List<Span> spans;
		private int height;

		/// <summary>
		/// Initializes a new instance of the <see cref="Cell"/> class.
		/// </summary>
		/// <param name="height">The number of voxels in the column.</param>
		public Cell(int height)
		{
			this.height = height;
			spans = new List<Span>();
		}

		/// <summary>
		/// Gets the height of the cell in number of voxels.
		/// </summary>
		public int Height
		{
			get
			{
				return height;
			}
		}

		/// <summary>
		/// Gets the number of spans in the cell.
		/// </summary>
		public int SpanCount
		{
			get
			{
				return spans.Count;
			}
		}

		/// <summary>
		/// Gets the number of spans that are in walkable <see cref="Area"/>s.
		/// </summary>
		public int WalkableSpanCount
		{
			get
			{
				int count = 0;
				for (int i = 0; i < spans.Count; i++)
					if (spans[i].Area.IsWalkable)
						count++;

				return count;
			}
		}

		/// <summary>
		/// Gets a readonly list of all the <see cref="Span"/>s contained in the cell.
		/// </summary>
		/// <value>A readonly list of spans.</value>
		public ReadOnlyCollection<Span> Spans
		{
			get
			{
				return spans.AsReadOnly();
			}
		}

		/// <summary>
		/// Gets a modifiable list of all the <see cref="Span"/>s contained in the cell.
		/// Should only be used for filtering in <see cref="Heightfield"/>.
		/// </summary>
		/// <value>A list of spans for modification.</value>
		internal List<Span> MutableSpans
		{
			get
			{
				return spans;
			}
		}

		/// <summary>
		/// Gets the <see cref="Span"/> that contains the specified voxel.
		/// </summary>
		/// <param name="location">The voxel to search for.</param>
		/// <returns>The span containing the voxel. Null if the voxel is empty.</returns>
		public Span? this[int location]
		{
			get
			{
				if (location < 0 || location >= height)
					throw new ArgumentOutOfRangeException("Location must be a value between 0 and " + height + ".");

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

		/// <summary>
		/// Adds a <see cref="Span"/> to the cell.
		/// </summary>
		/// <param name="span">A span.</param>
		/// <exception cref="ArgumentException">Thrown if an invalid span is provided.</exception>
		public void AddSpan(Span span)
		{
			if (span.Minimum > span.Maximum)
			{
				int tmp = span.Minimum;
				span.Minimum = span.Maximum;
				span.Maximum = tmp;
			}

			//clamp the span to the cell's range of [0, maxHeight]
			MathHelper.Clamp(ref span.Minimum, 0, height);
			MathHelper.Clamp(ref span.Maximum, 0, height);

			lock (spans)
			{
				for (int i = 0; i < spans.Count; i++)
				{
					//Check whether the current span is below, or overlapping existing spans.
					//If the span is completely above the current span the loop will continue.
					Span cur = spans[i];
					if (cur.Minimum > span.Maximum)
					{
						//The new span is below the current one and is not intersecting.
						spans.Insert(i, span);
						return;
					}
					else if (cur.Maximum >= span.Minimum)
					{
						//The new span is colliding with the current one, merge them together.
						if (cur.Minimum < span.Minimum)
							span.Minimum = cur.Minimum;

						if (cur.Maximum == span.Maximum)
						{
							//In the case that both spans end at the same voxel, the area gets merged. The new span's area
							//has priority if both spans are walkable, so the only case where the area gets set is when
							//the new area isn't walkable and the old one is.
							if (!span.Area.IsWalkable && cur.Area.IsWalkable)
								span.Area = cur.Area;
						}
						else if (cur.Maximum > span.Maximum)
						{
							span.Maximum = cur.Maximum;
							span.Area = cur.Area;
						}

						//Remove the current span and adjust i.
						//We do this to avoid duplicating the current span.
						spans.RemoveAt(i);
						i--;
					}
				}

				//If the span is not inserted, it is the highest span and will be added to the end.
				spans.Add(span);
			}
		}
	}
}
