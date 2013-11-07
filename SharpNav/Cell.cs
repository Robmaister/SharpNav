using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpNav
{
	/// <summary>
	/// A cell is a column of voxels represented in <see cref="SharpNav.Heightfield+Span"/>s.
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
		/// Gets a readonly list of all the <see cref="Span"/>s contained in the cell.
		/// </summary>
		/// <value>A readonly list of spans.</value>
		public IReadOnlyList<Span> Spans
		{
			get
			{
				return spans.AsReadOnly();
			}
		}

		//HACK figure out how to make this only accessible to containing class.

		/// <summary>
		/// Gets a modifiable list of all the <see cref="Span"/>s contained in the cell.
		/// Should only be used for filtering in <see cref="Heightmap"/>.
		/// </summary>
		/// <value>A list of spans for modification</value>
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

		/// <summary>
		/// Adds a <see cref="Span"/> to the cell.
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
}
