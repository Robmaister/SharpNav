using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpNav
{
	/// <summary>
	/// Represents a voxel span in a <see cref="CompactHeightfield"/>.
	/// </summary>
	public struct CompactSpan
	{
		public const int NotConnected = 0xff; //HACK this could be cleaner

		/// <summary>
		/// The span minimum.
		/// </summary>
		public int Minimum;

		/// <summary>
		/// The number of voxels contained in the span.
		/// </summary>
		public int Height;

		/// <summary>
		/// An int (split into 4 bytes) containing span connection data to neighboring cells.
		/// </summary>
		public int Connections;

		/// <summary>
		/// The region the span belongs to.
		/// </summary>
		public int Region;

		/// <summary>
		/// Initializes a new instance of the <see cref="CompactSpan"/> struct.
		/// </summary>
		/// <param name="minimum">The span minimum.</param>
		/// <param name="height">The number of voxels the span contains.</param>
		public CompactSpan(int minimum, int height)
		{
			this.Minimum = minimum;
			this.Height = height;
			this.Connections = ~0;
			this.Region = 0;
		}

		/// <summary>
		/// Gets a value indicating whether the span has an upper bound or goes to "infinity".
		/// </summary>
		public bool HasUpperBound
		{
			get
			{
				return Height != int.MaxValue;
			}
		}

		/// <summary>
		/// Gets the upper bound of the span.
		/// </summary>
		public int Maximum
		{
			get
			{
				return Minimum + Height;
			}
		}

		/// <summary>
		/// Creates a <see cref="CompactSpan"/> from a minimum boundary and a maximum boundary.
		/// </summary>
		/// <param name="min">The minimum.</param>
		/// <param name="max">The maximum.</param>
		/// <returns>A <see cref="CompactSpan"/>.</returns>
		public static CompactSpan FromMinMax(int min, int max)
		{
			CompactSpan s;
			FromMinMax(min, max, out s);
			return s;
		}

		/// <summary>
		/// Creates a <see cref="CompactSpan"/> from a minimum boundary and a maximum boundary.
		/// </summary>
		/// <param name="min">The minimum.</param>
		/// <param name="max">The maximum.</param>
		/// <param name="span">A <see cref="CompactSpan"/>.</param>
		public static void FromMinMax(int min, int max, out CompactSpan span)
		{
			span.Minimum = min;
			span.Height = max - min;
			span.Connections = ~0;
			span.Region = 0;
		}

		/// <summary>
		/// Sets connection data to a span contained in a neighboring cell.
		/// </summary>
		/// <param name="dir">The direction of the cell.</param>
		/// <param name="i">The index of the span in the neighboring cell.</param>
		/// <param name="s">The <see cref="CompactSpan"/> to set the data for.</param>
		public static void SetConnection(int dir, int i, ref CompactSpan s)
		{
			//split the int up into 4 parts, 8 bits each
			int shift = dir * 8;
			s.Connections = (s.Connections & ~(0xff << shift)) | ((i & 0xff) << shift);
		}

		/// <summary>
		/// Gets the connection data for a neighboring cell in a specified direction.
		/// </summary>
		/// <param name="dir">The direction.</param>
		/// <param name="s">The <see cref="CompactSpan"/> to get the connection data from.</param>
		/// <returns>The index of the span in the neighboring cell.</returns>
		public static int GetConnection(int dir, CompactSpan s)
		{
			return GetConnection(dir, ref s);
		}

		/// <summary>
		/// Gets the connection data for a neighboring cell in a specified direction.
		/// </summary>
		/// <param name="dir">The direction.</param>
		/// <param name="s">The <see cref="CompactSpan"/> to get the connection data from.</param>
		/// <returns>The index of the span in the neighboring cell.</returns>
		public static int GetConnection(int dir, ref CompactSpan s)
		{
			return (s.Connections >> (dir * 8)) & 0xff;
		}

		/*public static void Overlap(ref CompactSpan a, ref CompactSpan b, out CompactSpan r)
		{
			int max = Math.Min(a.Minimum + a.Height, b.Minimum + b.Height);
			r.Minimum = a.Minimum > b.Minimum ? a.Minimum : b.Minimum;
			r.Height = max - r.Minimum;
			r.Connections = 0;
		}*/
	}
}
