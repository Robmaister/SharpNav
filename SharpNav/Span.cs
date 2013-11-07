using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace SharpNav
{
	/// <summary>
	/// A span is a range of integers which represents a range of voxels in a <see cref="Cell"/>.
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
		/// Initializes a new instance of the <see cref="Span"/> struct.
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
		/// Initializes a new instance of the <see cref="Span"/> struct.
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
		public int Height
		{
			get
			{
				return Maximum - Minimum;
			}
		}
	}
}
