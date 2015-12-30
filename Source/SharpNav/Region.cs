// Copyright (c) 2013-2015 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
// Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using SharpNav.Geometry;

namespace SharpNav
{
	/// <summary>
	/// Flags that can be applied to a region.
	/// </summary>
	[Flags]
	public enum RegionFlags
	{
		/// <summary>
		/// The border flag
		/// </summary>
		Border = 0x20000000,

		/// <summary>
		/// The vertex border flag
		/// </summary>
		VertexBorder = 0x40000000,

		/// <summary>
		/// The area border flag
		/// </summary>
		AreaBorder = unchecked((int)0x80000000)
	}

	/// <summary>
	/// A <see cref="RegionId"/> is an identifier with flags marking borders.
	/// </summary>
	[Serializable]
	public struct RegionId : IEquatable<RegionId>, IEquatable<int>
	{
		/// <summary>
		/// A null region is one with an ID of 0.
		/// </summary>
		public static readonly RegionId Null = new RegionId(0, 0);

		/// <summary>
		/// A bitmask 
		/// </summary>
		public const int MaskId = 0x1fffffff;

		/// <summary>
		/// The internal storage of a <see cref="RegionId"/>. The <see cref="RegionFlags"/> portion are the most
		/// significant bits, the integer identifier are the least significant bits, marked by <see cref="MaskId"/>.
		/// </summary>
		private int bits;

		/// <summary>
		/// Initializes a new instance of the <see cref="RegionId"/> struct without any flags.
		/// </summary>
		/// <param name="id">The identifier.</param>
		public RegionId(int id)
			: this(id, 0)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="RegionId"/> struct.
		/// </summary>
		/// <param name="id"></param>
		/// <param name="flags"></param>
		public RegionId(int id, RegionFlags flags)
		{
			int masked = id & MaskId;

			if (masked != id)
				throw new ArgumentOutOfRangeException("id", "The provided id is outside of the valid range. The 3 most significant bits must be 0. Maybe you wanted RegionId.FromRawBits()?");

			if ((RegionFlags)((int)flags & ~MaskId) != flags)
				throw new ArgumentException("flags", "The provide region flags are invalid.");

			bits = masked | (int)flags;
		}

		/// <summary>
		/// Gets the ID of the region without any flags.
		/// </summary>
		public int Id
		{
			get
			{
				return bits & MaskId;
			}
		}

		/// <summary>
		/// Gets the flags set for this region.
		/// </summary>
		public RegionFlags Flags
		{
			get
			{
				return (RegionFlags)(bits & ~MaskId);
			}
		}

		/// <summary>
		/// Gets a value indicating whether the region is the null region (ID == 0).
		/// </summary>
		public bool IsNull
		{
			get
			{
				return (bits & MaskId) == 0;
			}
		}

		/// <summary>
		/// Creates a new <see cref="RegionId"/> from a value that contains both the region ID and the flags.
		/// </summary>
		/// <param name="bits">The int containing <see cref="RegionId"/> data.</param>
		/// <returns>A new instance of the <see cref="RegionId"/> struct with the specified data.</returns>
		public static RegionId FromRawBits(int bits)
		{
			RegionId id;
			id.bits = bits;
			return id;
		}

		/// <summary>
		/// Creates a new <see cref="RegionId"/> with extra flags.
		/// </summary>
		/// <param name="region">The region to add flags to.</param>
		/// <param name="flags">The flags to add.</param>
		/// <returns>A new instance of the <see cref="RegionId"/> struct with extra flags.</returns>
		public static RegionId WithFlags(RegionId region, RegionFlags flags)
		{
			if ((RegionFlags)((int)flags & ~MaskId) != flags)
				throw new ArgumentException("flags", "The provide region flags are invalid.");

			RegionFlags newFlags = region.Flags | flags;
			return RegionId.FromRawBits((region.bits & MaskId) | (int)newFlags);
		}

		/// <summary>
		/// Creates a new instance of the <see cref="RegionId"/> class without any flags set.
		/// </summary>
		/// <param name="region">The region to use.</param>
		/// <returns>A new instance of the <see cref="RegionId"/> struct without any flags set.</returns>
		public static RegionId WithoutFlags(RegionId region)
		{
			return new RegionId(region.Id);
		}

		/// <summary>
		/// Creates a new instance of the <see cref="RegionId"/> class without certain flags set.
		/// </summary>
		/// <param name="region">The region to use.</param>
		/// <param name="flags">The flags to unset.</param>
		/// <returns>A new instnace of the <see cref="RegionId"/> struct without certain flags set.</returns>
		public static RegionId WithoutFlags(RegionId region, RegionFlags flags)
		{
			if ((RegionFlags)((int)flags & ~MaskId) != flags)
				throw new ArgumentException("flags", "The provide region flags are invalid.");

			RegionFlags newFlags = region.Flags & ~flags;
			return RegionId.FromRawBits((region.bits & MaskId) | (int)newFlags);
		}

		/// <summary>
		/// Checks if a region has certain flags.
		/// </summary>
		/// <param name="region">The region to check.</param>
		/// <param name="flags">The flags to check.</param>
		/// <returns>A value indicating whether the region has all of the specified flags.</returns>
		public static bool HasFlags(RegionId region, RegionFlags flags)
		{
			return (region.Flags & flags) != 0;
		}

		/// <summary>
		/// Compares an instance of <see cref="RegionId"/> with an integer for equality.
		/// </summary>
		/// <remarks>
		/// This checks for both the ID and flags set on the region. If you want to only compare the IDs, use the
		/// following code:
		/// <code>
		/// RegionId left = ...;
		/// int right = ...;
		/// if (left.Id == right)
		/// {
		///    // ...
		/// }
		/// </code>
		/// </remarks>
		/// <param name="left">An instance of <see cref="RegionId"/>.</param>
		/// <param name="right">An integer.</param>
		/// <returns>A value indicating whether the two values are equal.</returns>
		public static bool operator ==(RegionId left, int right)
		{
			return left.Equals(right);
		}

		/// <summary>
		/// Compares an instance of <see cref="RegionId"/> with an integer for inequality.
		/// </summary>
		/// <remarks>
		/// This checks for both the ID and flags set on the region. If you want to only compare the IDs, use the
		/// following code:
		/// <code>
		/// RegionId left = ...;
		/// int right = ...;
		/// if (left.Id != right)
		/// {
		///    // ...
		/// }
		/// </code>
		/// </remarks>
		/// <param name="left">An instance of <see cref="RegionId"/>.</param>
		/// <param name="right">An integer.</param>
		/// <returns>A value indicating whether the two values are unequal.</returns>
		public static bool operator !=(RegionId left, int right)
		{
			return !(left == right);
		}

		/// <summary>
		/// Compares two instances of <see cref="RegionId"/> for equality.
		/// </summary>
		/// <remarks>
		/// This checks for both the ID and flags set on the regions. If you want to only compare the IDs, use the
		/// following code:
		/// <code>
		/// RegionId left = ...;
		/// RegionId right = ...;
		/// if (left.Id == right.Id)
		/// {
		///    // ...
		/// }
		/// </code>
		/// </remarks>
		/// <param name="left">An instance of <see cref="RegionId"/>.</param>
		/// <param name="right">Another instance of <see cref="RegionId"/>.</param>
		/// <returns>A value indicating whether the two instances are equal.</returns>
		public static bool operator ==(RegionId left, RegionId right)
		{
			return left.Equals(right);
		}

		/// <summary>
		/// Compares two instances of <see cref="RegionId"/> for inequality.
		/// </summary>
		/// <remarks>
		/// This checks for both the ID and flags set on the regions. If you want to only compare the IDs, use the
		/// following code:
		/// <code>
		/// RegionId left = ...;
		/// RegionId right = ...;
		/// if (left.Id != right.Id)
		/// {
		///    // ...
		/// }
		/// </code>
		/// </remarks>
		/// <param name="left">An instance of <see cref="RegionId"/>.</param>
		/// <param name="right">Another instance of <see cref="RegionId"/>.</param>
		/// <returns>A value indicating whether the two instances are unequal.</returns>
		public static bool operator !=(RegionId left, RegionId right)
		{
			return !(left == right);
		}

		/// <summary>
		/// Converts an instance of <see cref="RegionId"/> to an integer containing both the ID and the flags.
		/// </summary>
		/// <param name="id">An instance of <see cref="RegionId"/>.</param>
		/// <returns>An integer.</returns>
		public static explicit operator int(RegionId id)
		{
			return id.bits;
		}

		/// <summary>
		/// Compares this instance with another instance of <see cref="RegionId"/> for equality, including flags.
		/// </summary>
		/// <param name="other">An instance of <see cref="RegionId"/>.</param>
		/// <returns>A value indicating whether the two instances are equal.</returns>
		public bool Equals(RegionId other)
		{
			bool thisNull = this.IsNull;
			bool otherNull = other.IsNull;

			if (thisNull && otherNull)
				return true;
			else if (thisNull ^ otherNull)
				return false;
			else
				return this.bits == other.bits;
		}

		/// <summary>
		/// Compares this instance with another an intenger for equality, including flags.
		/// </summary>
		/// <param name="other">An integer.</param>
		/// <returns>A value indicating whether the two instances are equal.</returns>
		public bool Equals(int other)
		{
			RegionId otherId;
			otherId.bits = other;

			return this.Equals(otherId);
		}

		/// <summary>
		/// Compares this instance with an object for equality.
		/// </summary>
		/// <param name="obj">An object</param>
		/// <returns>A value indicating whether the two instances are equal.</returns>
		public override bool Equals(object obj)
		{
			var regObj = obj as RegionId?;
			var intObj = obj as int?;

			if (regObj.HasValue)
				return this.Equals(regObj.Value);
			else if (intObj.HasValue)
				return this.Equals(intObj.Value);
			else
				return false;
		}

		/// <summary>
		/// Gets a unique hash code for this instance.
		/// </summary>
		/// <returns>A hash code.</returns>
		public override int GetHashCode()
		{
			if (IsNull)
				return 0;

			return bits.GetHashCode();
		}

		/// <summary>
		/// Gets a human-readable version of this instance.
		/// </summary>
		/// <returns>A string representing this instance.</returns>
		public override string ToString()
		{
			return "{ Id: " + Id + ", Flags: " + Flags + "}";
		}
	}

	/// <summary>
	/// A Region contains a group of adjacent spans.
	/// </summary>
	public class Region
	{
		private int spanCount;
		private RegionId id;
		private Area areaType;
		private bool remap;
		private bool visited;
		private List<RegionId> connections;
		private List<RegionId> floors;

		/// <summary>
		/// Initializes a new instance of the <see cref="Region" /> class.
		/// </summary>
		/// <param name="idNum">The id</param>
		public Region(int idNum)
		{
			spanCount = 0;
			id = new RegionId(idNum);
			areaType = 0;
			remap = false;
			visited = false;

			connections = new List<RegionId>();
			floors = new List<RegionId>();
		}

		/// <summary>
		/// Gets or sets the number of spans
		/// </summary>
		public int SpanCount
		{
			get
			{
				return spanCount;
			}

			set
			{
				this.spanCount = value;
			}
		}

		/// <summary>
		/// Gets or sets the region id 
		/// </summary>
		public RegionId Id 
		{
			get
			{
				return id;
			}

			set
			{
				this.id = value;
			}
		}

		/// <summary>
		/// Gets or sets the AreaType of this region
		/// </summary>
		public Area AreaType
		{
			get
			{
				return areaType;
			}

			set
			{
				this.areaType = value;
			}
		}

		/// <summary>
		/// Gets or sets a value indicating whether this region has been remapped or not
		/// </summary>
		public bool Remap 
		{
			get
			{
				return remap;
			}

			set
			{
				this.remap = value;
			}
		}

		/// <summary>
		/// Gets or sets a value indicating whether this region has been visited or not
		/// </summary>
		public bool Visited
		{
			get
			{
				return visited;
			}

			set
			{
				this.visited = value;
			}
		}

		/// <summary>
		/// Gets the list of floor regions
		/// </summary>
		public List<RegionId> FloorRegions
		{
			get
			{
				return floors;
			}
		}

		/// <summary>
		/// Gets the list of connected regions
		/// </summary>
		public List<RegionId> Connections
		{
			get
			{
				return connections;
			}
		}

		/// <summary>
		/// Gets a value indicating whether the region is a border region.
		/// </summary>
		public bool IsBorder
		{
			get
			{
				return RegionId.HasFlags(id, RegionFlags.Border);
			}
		}

		/// <summary>
		/// Gets a value indicating whether the region is either a border region or the null region.
		/// </summary>
		public bool IsBorderOrNull
		{
			get
			{
				return id.IsNull || IsBorder;
			}
		}

		/// <summary>
		/// Remove adjacent connections if there is a duplicate
		/// </summary>
		public void RemoveAdjacentNeighbors()
		{
			if (connections.Count <= 1)
				return;

			// Remove adjacent duplicates.
			for (int i = 0; i < connections.Count; i++)
			{
				//get the next i
				int ni = (i + 1) % connections.Count;

				//remove duplicate if found
				if (connections[i] == connections[ni])
				{
					connections.RemoveAt(i);
					i--;
				}
			}
		}

		/// <summary>
		/// Replace all connection and floor values 
		/// </summary>
		/// <param name="oldId">The value you want to replace</param>
		/// <param name="newId">The new value that will be used</param>
		public void ReplaceNeighbor(RegionId oldId, RegionId newId)
		{
			//replace the connections
			bool neiChanged = false;
			for (int i = 0; i < connections.Count; ++i)
			{
				if (connections[i] == oldId)
				{
					connections[i] = newId;
					neiChanged = true;
				}
			}

			//replace the floors
			for (int i = 0; i < floors.Count; ++i)
			{
				if (floors[i] == oldId)
					floors[i] = newId;
			}

			//make sure to remove adjacent neighbors
			if (neiChanged)
				RemoveAdjacentNeighbors();
		}

		/// <summary>
		/// Determine whether this region can merge with another region.
		/// </summary>
		/// <param name="otherRegion">The other region to merge with</param>
		/// <returns>True if the two regions can be merged, false if otherwise</returns>
		public bool CanMergeWith(Region otherRegion)
		{
			//make sure areas are the same
			if (areaType != otherRegion.areaType)
				return false;
			
			//count the number of connections to the other region
			int n = 0;
			for (int i = 0; i < connections.Count; i++)
			{
				if (connections[i] == otherRegion.id)
						n++;
			}
		
			//make sure there's only one connection
			if (n > 1)
				return false;
		
			//make sure floors are separate
			if (floors.Contains(otherRegion.id))
				return false;

			return true;
		}

		/// <summary>
		/// Only add a floor if it hasn't been added already
		/// </summary>
		/// <param name="n">The value of the floor</param>
		public void AddUniqueFloorRegion(RegionId n)
		{
			if (!floors.Contains(n))
				floors.Add(n);
		}

		/// <summary>
		/// Merge two regions into one. Needs good testing
		/// </summary>
		/// <param name="otherRegion">The region to merge with</param>
		/// <returns>True if merged successfully, false if otherwise</returns>
		public bool MergeWithRegion(Region otherRegion)
		{
			RegionId thisId = id;
			RegionId otherId = otherRegion.id;

			// Duplicate current neighborhood.
			List<RegionId> thisConnected = new List<RegionId>();
			for (int i = 0; i < connections.Count; ++i)
				thisConnected.Add(connections[i]);
			List<RegionId> otherConnected = otherRegion.connections;

			// Find insertion point on this region
			int insertInThis = -1;
			for (int i = 0; i < thisConnected.Count; ++i)
			{
				if (thisConnected[i] == otherId)
				{
					insertInThis = i;
					break;
				}
			}

			if (insertInThis == -1)
				return false;

			// Find insertion point on the other region
			int insertInOther = -1;
			for (int i = 0; i < otherConnected.Count; ++i)
			{
				if (otherConnected[i] == thisId)
				{
					insertInOther = i;
					break;
				}
			}

			if (insertInOther == -1)
				return false;

			// Merge neighbors.
			connections = new List<RegionId>();
			for (int i = 0, ni = thisConnected.Count; i < ni - 1; ++i)
				connections.Add(thisConnected[(insertInThis + 1 + i) % ni]);

			for (int i = 0, ni = otherConnected.Count; i < ni - 1; ++i)
				connections.Add(otherConnected[(insertInOther + 1 + i) % ni]);

			RemoveAdjacentNeighbors();

			for (int j = 0; j < otherRegion.floors.Count; ++j)
				AddUniqueFloorRegion(otherRegion.floors[j]);
			spanCount += otherRegion.spanCount;
			otherRegion.spanCount = 0;
			otherRegion.connections.Clear();

			return true;
		}

		/// <summary>
		/// Test if region is connected to a border
		/// </summary>
		/// <returns>True if connected, false if not</returns>
		public bool IsConnectedToBorder()
		{
			// Region is connected to border if
			// one of the neighbors is null id.
			for (int i = 0; i < connections.Count; ++i)
			{
				if (connections[i] == 0)
					return true;
			}

			return false;
		}
	}
}
