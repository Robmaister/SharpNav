// Copyright (c) 2015-2016 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
// Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE

using System;
using System.Runtime.InteropServices;

namespace SharpNav.Pathfinding
{
	[Serializable]
	[StructLayout(LayoutKind.Sequential)]
	public struct NavPolyId : IEquatable<NavPolyId>
	{
		/// <summary>
		/// A null ID that isn't associated with any polygon or tile.
		/// </summary>
		public static readonly NavPolyId Null = new NavPolyId(0);

		private int bits;

		public NavPolyId(int raw)
		{
			bits = raw;
		}

		public int Id { get { return bits; } }

		public static bool operator ==(NavPolyId left, NavPolyId right)
		{
			return left.Equals(right);
		}

		public static bool operator !=(NavPolyId left, NavPolyId right)
		{
			return !(left == right);
		}

		public bool Equals(NavPolyId other)
		{
			return bits == other.bits;
		}

		public override bool Equals(object obj)
		{
			var polyObj = obj as NavPolyId?;

			if (polyObj.HasValue)
				return this.Equals(polyObj.Value);
			else
				return false;
		}

		public override int GetHashCode()
		{
			//TODO actual hash code
			return base.GetHashCode();
		}

		public string ToString(NavPolyIdManager manager)
		{
			int polyIndex, tileIndex, salt;
			manager.Decode(ref this, out polyIndex, out tileIndex, out salt);

			return "{ Poly: " + polyIndex + ", Tile: " + tileIndex + ", Salt: " + salt + "}";
		}

		public override string ToString()
		{
			return bits.ToString();
		}
	}
}
