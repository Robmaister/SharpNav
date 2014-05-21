#region License
/**
 * Copyright (c) 2013-2014 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
 * Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE
 */
#endregion

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
	public enum RegionId : int
	{
		/// <summary>The null region.</summary>
		Null = 0,

		/// <summary>
		/// The vertex border flag
		/// </summary>
		VertexBorder = 0x20000000,

		/// <summary>
		/// The area border flag
		/// </summary>
		AreaBorder = 0x40000000,

		/// <summary>
		/// The border flag
		/// </summary>
		Border = unchecked((int)0x80000000)
	}

	/// <summary>
	/// A Region contains a group of adjacent spans.
	/// </summary>
	public class Region
	{
		public const int IdMask = 0x1fffffff;

		private int spanCount;
		private RegionId id;
		private AreaId areaType;
		private bool remap;
		private bool visited;
		private List<RegionId> connections;
		private List<RegionId> floors;

		/// <summary>
		/// Initializes a new instance of the <see cref="Region" /> class.
		/// </summary>
		/// <param name="idNum">The id</param>
		public Region(RegionId idNum)
		{
			spanCount = 0;
			id = idNum;
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
		public AreaId AreaType
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
		/// Add a border flag to the region 
		/// </summary>
		/// <param name="id">The region</param>
		/// <returns>The region with a border flag</returns>
		public static RegionId IdWithBorderFlag(RegionId id)
		{
			return id | RegionId.Border;
		}

		/// <summary>
		/// Remove the flags from the region
		/// </summary>
		/// <param name="id">The region</param>
		/// <returns>The new id without flags</returns>
		public static RegionId RemoveFlags(RegionId id)
		{
			return id & (RegionId)IdMask;
		}

		/// <summary>
		/// Determines whether the region is a border
		/// </summary>
		/// <param name="id">The region</param>
		/// <returns>True if it is a border, false if not</returns>
		public static bool IsBorder(RegionId id)
		{
			return (id & RegionId.Border) == RegionId.Border;
		}

		/// <summary>
		/// Determines whether the region is null
		/// </summary>
		/// <param name="id">The region</param>
		/// <returns>True if it is null, false if not</returns>
		public static bool IsNull(RegionId id)
		{
			return id == RegionId.Null;
		}

		/// <summary>
		/// Determines whether the region is a border or null
		/// </summary>
		/// <param name="id">The region</param>
		/// <returns>True if border or null, false if otherwise</returns>
		public static bool IsBorderOrNull(RegionId id)
		{
			return id == RegionId.Null || (id & RegionId.Border) == RegionId.Border;
		}

		/// <summary>
		/// Set the region as a border vertex
		/// </summary>
		/// <param name="region">The region</param>
		public static void SetBorderVertex(ref RegionId region)
		{
			region |= RegionId.VertexBorder;
		}

		/// <summary>
		/// Set the region as an area border
		/// </summary>
		/// <param name="region">The region</param>
		public static void SetAreaBorder(ref RegionId region)
		{
			region |= RegionId.AreaBorder;
		}

		/// <summary>
		/// Determines whether the region is a border vertex
		/// </summary>
		/// <param name="r">The region</param>
		/// <returns>True if it is a border vertex, false if not</returns>
		public static bool IsBorderVertex(RegionId r)
		{
			return (r & RegionId.VertexBorder) != 0;
		}

		/// <summary>
		/// Determines whether the region is an area border
		/// </summary>
		/// <param name="r">The region</param>
		/// <returns>True if an area border, false if not</returns>
		public static bool IsAreaBorder(RegionId r)
		{
			return (r & RegionId.AreaBorder) != 0;
		}

		/// <summary>
		/// Determine whether two regions have the same area border
		/// </summary>
		/// <param name="region1">The first region</param>
		/// <param name="region2">The second region</param>
		/// <returns>True if equal, false if not</returns>
		public static bool IsSameArea(RegionId region1, RegionId region2)
		{
			return (region1 & RegionId.AreaBorder) == (region2 & RegionId.AreaBorder);
		}

		/// <summary>
		/// Determines whether two regions have the same id
		/// </summary>
		/// <param name="region1">The first region</param>
		/// <param name="region2">The second region</param>
		/// <returns>True if equal, false if not</returns>
		public static bool IsSameRegion(RegionId region1, RegionId region2)
		{
			return ((int)region1 & IdMask) == ((int)region2 & IdMask);
		}

		/// <summary>
		/// Determines whether the region is a border
		/// </summary>
		/// <returns>True if border, false if not</returns>
		public bool IsBorder()
		{
			return (id & RegionId.Border) == RegionId.Border;
		}

		/// <summary>
		/// Determines whether the region is null
		/// </summary>
		/// <returns>True if null, false if not</returns>
		public bool IsNull()
		{
			return id == RegionId.Null;
		}

		/// <summary>
		/// Determines whether the region is a border or null
		/// </summary>
		/// <returns>True if border or null, false if otherwise</returns>
		public bool IsBorderOrNull()
		{
			return id == RegionId.Null || (id & RegionId.Border) == RegionId.Border;
		}

		/// <summary>
		/// Remove adjacent connections if there is a duplicate
		/// </summary>
		public void RemoveAdjacentNeighbours()
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
		public void ReplaceNeighbour(RegionId oldId, RegionId newId)
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
				RemoveAdjacentNeighbours();
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

			// Duplicate current neighbourhood.
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

			// Merge neighbours.
			connections = new List<RegionId>();
			for (int i = 0, ni = thisConnected.Count; i < ni - 1; ++i)
				connections.Add(thisConnected[(insertInThis + 1 + i) % ni]);

			for (int i = 0, ni = otherConnected.Count; i < ni - 1; ++i)
				connections.Add(otherConnected[(insertInOther + 1 + i) % ni]);

			RemoveAdjacentNeighbours();

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
			// one of the neighbours is null id.
			for (int i = 0; i < connections.Count; ++i)
			{
				if (connections[i] == 0)
					return true;
			}

			return false;
		}
	}
}
