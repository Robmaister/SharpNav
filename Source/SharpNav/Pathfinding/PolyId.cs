// Copyright (c) 2015 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
// Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE

using System;
using System.Runtime.InteropServices;

namespace SharpNav.Pathfinding
{
	[Serializable]
	[StructLayout(LayoutKind.Sequential)]
	public struct PolyId : IEquatable<PolyId>
	{
		/// <summary>
		/// A null ID that isn't associated with any polygon or tile.
		/// </summary>
		public static readonly PolyId Null = new PolyId(0);

		private int bits;

		public PolyId(int raw)
		{
			bits = raw;
		}

		public int Id { get { return bits; } }

		public static bool operator ==(PolyId left, PolyId right)
		{
			return left.Equals(right);
		}

		public static bool operator !=(PolyId left, PolyId right)
		{
			return !(left == right);
		}

		public bool Equals(PolyId other)
		{
			return bits == other.bits;
		}

		public override bool Equals(object obj)
		{
			var polyObj = obj as PolyId?;

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

		public string ToString(PolyIdManager manager)
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
