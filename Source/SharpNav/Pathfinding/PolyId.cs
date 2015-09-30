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

		/// <summary>
		/// Derive a standard polygon reference, which compresses salt, tile index, and poly index together.
		/// </summary>
		/// <param name="polyBits">The number of bits to use for the polygon value.</param>
		/// <param name="tileBits">The number of bits to use for the tile value.</param>
		/// <param name="salt">Salt value</param>
		/// <param name="tileIndex">Tile index</param>
		/// <param name="polyIndex">Poly index</param>
		/// <returns>Polygon reference</returns>
		public static PolyId Encode(int polyBits, int tileBits, int salt, int tileIndex, int polyIndex)
		{
			return new PolyId((salt << (int)(polyBits + tileBits)) | (tileIndex << (int)polyBits) | polyIndex);
		}

		public static void SetPolyIndex(ref PolyId polyBase, int polyIndex, out PolyId result)
		{
			result = new PolyId(polyBase.bits | polyIndex);
		}
		
		public static bool operator ==(PolyId left, PolyId right)
		{
			return left.Equals(right);
		}

		public static bool operator !=(PolyId left, PolyId right)
		{
			return !(left == right);
		}

		/// <summary>
		/// Extract a polygon's index (within its tile) from the specified polygon reference.
		/// </summary>
		/// <param name="polyBits">The number of bits to use for the polygon value.</param>
		/// <returns>The value's poly index.</returns>
		public int DecodePolyIndex(int polyBits)
		{
			int polyMask = (1 << polyBits) - 1;
			return bits & polyMask;
		}

		/// <summary>
		/// Extract a tile's index from the specified polygon reference.
		/// </summary>
		/// <param name="polyBits">The number of bits to use for the polygon value.</param>
		/// <param name="tileBits">The number of bits to use for the tile value.</param>
		/// <returns>The value's tile index.</returns>
		public int DecodeTileIndex(int polyBits, int tileBits)
		{
			int tileMask = (1 << tileBits) - 1;
			return (bits >> polyBits) & tileMask;
		}

		/// <summary>
		/// Extract a tile's salt value from the specified polygon reference.
		/// </summary>
		/// <param name="polyBits">The number of bits to use for the polygon value.</param>
		/// <param name="tileBits">The number of bits to use for the tile value.</param>
		/// <param name="saltBits">The number of bits to use for the salt.</param>
		/// <returns>The value's salt.</returns>
		public int DecodeSalt(int polyBits, int tileBits, int saltBits)
		{
			int saltMask = (1 << saltBits) - 1;
			return (bits >> (polyBits + tileBits)) & saltMask;
		}

		/// <summary>
		/// Decode a standard polygon reference.
		/// </summary>
		/// <param name="polyBits">The number of bits to use for the polygon value.</param>
		/// <param name="tileBits">The number of bits to use for the tile value.</param>
		/// <param name="saltBits">The number of bits to use for the salt.</param>
		/// <param name="polyIndex">Resulting poly index.</param>
		/// <param name="tileIndex">Resulting tile index.</param>
		/// <param name="salt">Resulting salt value.</param>
		public void Decode(int polyBits, int tileBits, int saltBits, out int polyIndex, out int tileIndex, out int salt)
		{
			int saltMask = (1 << saltBits) - 1;
			int tileMask = (1 << tileBits) - 1;
			int polyMask = (1 << polyBits) - 1;
			salt = (bits >> (polyBits + tileBits)) & saltMask;
			tileIndex = (bits >> polyBits) & tileMask;
			polyIndex = bits & polyMask;
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

		public override string ToString()
		{
			//TODO include poly/tile/salt bits for a real ToString?
			return bits.ToString();
		}
	}
}
