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

		private int width, length;
		private float cellWidth, cellLength, cellHeight;

		private Cell[] cells;

		public Heightfield(Vector3 minimum, Vector3 maximum, int cellCountX, int cellCountZ)
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
			length = cellCountZ;

			cellWidth = (max.X - min.X) / cellCountX;
			cellLength = (max.Z - min.Z) / cellCountZ;
			cellHeight = (max.Y - min.Y);

			cells = new Cell[cellCountX * cellCountZ];
			for (int i = 0; i < cells.Length; i++)
				cells[i] = new Cell();
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

			public Cell()
			{
				openSpans = new List<Span>();
			}

			public void AddSpan(Span span)
			{
				throw new NotImplementedException();
			}
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct Span
		{
			public float Minimum;
			public float Maximum;
		}
	}
}
