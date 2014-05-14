using System;
using System.Runtime.InteropServices;

namespace SharpNav
{
	[StructLayout(LayoutKind.Sequential)]
	public struct PolyBounds : IEquatable<PolyBounds>
	{
		public PolyVertex Min;
		public PolyVertex Max;

		public PolyBounds(PolyVertex min, PolyVertex max)
		{
			Min = min;
			Max = max;
		}

		/// <summary>
		/// Checks whether two boudning boxes are intersecting.
		/// </summary>
		/// <param name="a">The first bounding box.</param>
		/// <param name="b">The second bounding box.</param>
		/// <returns>A value indicating whether the two bounding boxes are overlapping.</returns>
		public static bool Overlapping(ref PolyBounds a, ref PolyBounds b)
		{
			return !(a.Min.X > b.Max.X || a.Max.X < b.Min.X
				|| a.Min.Y > b.Max.Y || a.Max.Y < b.Min.Y
				|| a.Min.Z > b.Max.Z || a.Max.Z < b.Min.Z);
		}

		public bool Equals(PolyBounds other)
		{
			return Min == other.Min && Max == other.Max;
		}

		public static bool operator ==(PolyBounds left, PolyBounds right)
		{
			return left.Equals(right);
		}

		public static bool operator !=(PolyBounds left, PolyBounds right)
		{
			return !(left == right);
		}

		public override bool Equals(object obj)
		{
			PolyBounds? b = obj as PolyBounds?;
			if (b.HasValue)
				return Equals(b.Value);

			return false;
		}

		public override int GetHashCode()
		{
			//TODO write a better hash code
			return Min.GetHashCode() ^ Max.GetHashCode();
		}

		public override string ToString()
		{
			return "[" + Min.ToString() + ", " + Max.ToString() + "]";
		}
	}
}
