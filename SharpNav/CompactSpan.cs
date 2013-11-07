using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpNav
{
	public struct CompactSpan
	{
		public int Minimum;
		public int Height;
		public int Connections;
		public int Region;

		public CompactSpan(int minimum, int height)
		{
			this.Minimum = minimum;
			this.Height = height;
			this.Connections = ~0;
			this.Region = 0;
		}

		public bool HasUpperBound { get { return Height != int.MaxValue; } }

		public int Maximum { get { return Minimum + Height; } }

		public static CompactSpan FromMinMax(int min, int max)
		{
			CompactSpan s;
			FromMinMax(min, max, out s);
			return s;
		}

		public static void FromMinMax(int min, int max, out CompactSpan span)
		{
			span.Minimum = min;
			span.Height = max - min;
			span.Connections = ~0;
			span.Region = 0;
		}

		public static void SetConnection(int dir, int i, ref CompactSpan s)
		{
			//split the int up into 4 parts, 8 bits each
			int shift = dir * 8;
			s.Connections = (s.Connections & ~(0xff << shift)) | ((i & 0xff) << shift);
		}

		public static int GetConnection(int dir, CompactSpan s)
		{
			return GetConnection(dir, ref s);
		}

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
