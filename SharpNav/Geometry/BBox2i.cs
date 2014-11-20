using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SharpNav.Geometry
{
	[Serializable]
	public struct BBox2i : IEquatable<BBox2i>
	{
		public Vector2i Min;
		public Vector2i Max;

		public BBox2i(Vector2i min, Vector2i max)
		{
			Min = min;
			Max = max;
		}

		public BBox2i(int minX, int minY, int maxX, int maxY)
		{
			Min.X = minX;
			Min.Y = minY;
			Max.X = maxX;
			Max.Y = maxY;
		}

		public static bool operator ==(BBox2i left, BBox2i right)
		{
			return left.Equals(right);
		}

		public static bool operator !=(BBox2i left, BBox2i right)
		{
			return !(left == right);
		}

		public override string ToString()
		{
			return "{ Min: " + Min.ToString() + ", Max: " + Max.ToString() + " }";
		}

		public override bool Equals(object obj)
		{
			BBox2i? objV = obj as BBox2i?;
			if (objV != null)
				return Equals(objV);

			return false;
		}

		public bool Equals(BBox2i other)
		{
			return Min == other.Min && Max == other.Max;
		}
	}
}
