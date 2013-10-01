using System;
using System.Collections.Generic;

using SharpNav;

namespace SharpNav.Geometry
{
	[Serializable]
	public struct BBox3 : IEquatable<BBox3>
	{
		public Vector3 Min;
		public Vector3 Max;

		public BBox3(Vector3 min, Vector3 max)
		{
			Min = min;
			Max = max;
		}

		public Vector3 Center { get { return (Min + Max) / 2; } }

		public Vector3 Size { get { return (Max - Min); } }

		public static bool Overlapping(ref BBox3 a, ref BBox3 b)
		{
			return !(a.Min.X > b.Max.X || a.Max.X < b.Min.X
				|| a.Min.Y > b.Max.Y || a.Max.Y < b.Min.Y
				|| a.Min.Z > b.Max.Z || a.Max.Z < b.Min.Z);
		}

		public bool Equals(BBox3 other)
		{
			return Min == other.Min && Max == other.Max;
		}

		public override bool Equals(object obj)
		{
			if (obj is BBox3)
				return this.Equals((BBox3)obj);
			else
				return false;
		}

		public override int GetHashCode()
		{
			return Min.GetHashCode() ^ Max.GetHashCode();
		}

		public static bool operator ==(BBox3 left, BBox3 right)
		{
			return left.Equals(right);
		}

		public static bool operator !=(BBox3 left, BBox3 right)
		{
			return !(left == right);
		}
	}
}
