// Copyright (c) 2015 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
// Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE

namespace SharpNav.Pathfinding
{
	/// <summary>
	/// Helps encode and decode <see cref="PolyId"/> by storing the number of
	/// bits the salt, tile, and poly sections of an ID.
	/// </summary>
	/// <remarks>
	/// IDs should not be used between different instances of
	/// <see cref="PolyIdManager"/> as the bits for each section may be
	/// diffrent, causing incorrect decoded values.
	/// </remarks>
	public class PolyIdManager
	{
		private int polyBits;
		private int tileBits;
		private int saltBits;
		private int polyMask;
		private int tileMask;
		private int saltMask;
		private int tileOffset;
		private int saltOffset;

		public PolyIdManager(int polyBits, int tileBits, int saltBits)
		{
			this.polyBits = polyBits;
			this.tileBits = tileBits;
			this.saltBits = saltBits;

			this.polyMask = (1 << polyBits) - 1;
			this.tileMask = (1 << tileBits) - 1;
			this.saltMask = (1 << saltBits) - 1;

			this.tileOffset = polyBits;
			this.saltOffset = polyBits + tileBits;
		}

		public int PolyBits { get { return polyBits; } }
		public int TileBits { get { return tileBits; } }
		public int SaltBits { get { return saltBits; } }

		public PolyId Encode(int salt, int tileIndex, int polyIndex)
		{
			PolyId id;
			Encode(salt, tileIndex, polyIndex, out id);
			return id;
		}

		/// <summary>
		/// Derive a standard polygon reference, which compresses salt, tile index, and poly index together.
		/// </summary>
		/// <param name="polyBits">The number of bits to use for the polygon value.</param>
		/// <param name="tileBits">The number of bits to use for the tile value.</param>
		/// <param name="salt">Salt value</param>
		/// <param name="tileIndex">Tile index</param>
		/// <param name="polyIndex">Poly index</param>
		/// <returns>Polygon reference</returns>
		public void Encode(int salt, int tileIndex, int polyIndex, out PolyId result)
		{
			polyIndex &= polyMask;
			tileIndex &= tileMask;
			salt &= saltMask;

			result = new PolyId((salt << saltOffset) | (tileIndex << tileOffset) | polyIndex);
		}

		public void SetPolyIndex(ref PolyId polyBase, int newPoly, out PolyId result)
		{
			newPoly &= polyMask;

			//first clear poly then OR with new poly
			result = new PolyId((polyBase.Id & ~polyMask) | newPoly);
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
		public void Decode(ref PolyId id, out int polyIndex, out int tileIndex, out int salt)
		{
			int bits = id.Id;

			salt = (bits >> saltOffset) & saltMask;
			tileIndex = (bits >> tileOffset) & tileMask;
			polyIndex = bits & polyMask;
		}

		/// <summary>
		/// Extract a polygon's index (within its tile) from the specified polygon reference.
		/// </summary>
		/// <param name="polyBits">The number of bits to use for the polygon value.</param>
		/// <returns>The value's poly index.</returns>
		public int DecodePolyIndex(ref PolyId id)
		{
			return id.Id & polyMask;
		}

		/// <summary>
		/// Extract a tile's index from the specified polygon reference.
		/// </summary>
		/// <param name="polyBits">The number of bits to use for the polygon value.</param>
		/// <param name="tileBits">The number of bits to use for the tile value.</param>
		/// <returns>The value's tile index.</returns>
		public int DecodeTileIndex(ref PolyId id)
		{
			return (id.Id >> tileOffset) & tileMask;
		}

		/// <summary>
		/// Extract a tile's salt value from the specified polygon reference.
		/// </summary>
		/// <param name="polyBits">The number of bits to use for the polygon value.</param>
		/// <param name="tileBits">The number of bits to use for the tile value.</param>
		/// <param name="saltBits">The number of bits to use for the salt.</param>
		/// <returns>The value's salt.</returns>
		public int DecodeSalt(ref PolyId id)
		{
			return (id.Id >> saltOffset) & saltMask;
		}
	}
}
