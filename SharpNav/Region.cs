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
		VertexBorder = 0x20000000,
		AreaBorder = 0x40000000,
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

		public List<RegionId> FloorRegions
		{
			get
			{
				return floors;
			}
		}

		public List<RegionId> Connections
		{
			get
			{
				return connections;
			}
		}

		public static RegionId IdWithBorderFlag(RegionId id)
		{
			return id | RegionId.Border;
		}

		public static RegionId RemoveFlags(RegionId id)
		{
			return id & (RegionId)IdMask;
		}

		public static bool IsBorder(RegionId id)
		{
			return (id & RegionId.Border) == RegionId.Border;
		}

		public static bool IsNull(RegionId id)
		{
			return id == RegionId.Null;
		}

		public static bool IsBorderOrNull(RegionId id)
		{
			return id == RegionId.Null || (id & RegionId.Border) == RegionId.Border;
		}

		public static void SetBorderVertex(ref RegionId region)
		{
			region |= RegionId.VertexBorder;
		}

		public static void SetAreaBorder(ref RegionId region)
		{
			region |= RegionId.AreaBorder;
		}

		public static bool IsBorderVertex(RegionId r)
		{
			return (r & RegionId.VertexBorder) != 0;
		}

		public static bool IsAreaBorder(RegionId r)
		{
			return (r & RegionId.AreaBorder) != 0;
		}

		public static bool IsSameArea(RegionId region1, RegionId region2)
		{
			return (region1 & RegionId.AreaBorder) == (region2 & RegionId.AreaBorder);
		}

		public static bool IsSameRegion(RegionId region1, RegionId region2)
		{
			return ((int)region1 & IdMask) == ((int)region2 & IdMask);
		}

		public bool IsBorder()
		{
			return (id & RegionId.Border) == RegionId.Border;
		}

		public bool IsNull()
		{
			return id == RegionId.Null;
		}

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
		/// <returns></returns>
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
		/// <returns></returns>
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
		/// <returns></returns>
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
