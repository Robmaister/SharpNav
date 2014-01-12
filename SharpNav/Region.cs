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
	/// A Region contains a group of adjacent spans.
	/// </summary>
	public class Region
	{
		/// <summary>
		/// The border region flag.
		/// </summary>
		public const int BorderFlag = unchecked((int)0x80000000);

		public const int VertexBorderFlag = unchecked((int)0x20000000);

		public const int AreaBorderFlag = unchecked((int)0x40000000);

		public const int IdMask = 0x1fffffff;

		private int spanCount;
		private int id;
		private AreaFlags areaType;
		private bool remap;
		private bool visited;
		private List<int> connections;
		private List<int> floors;

		public Region(int idNum)
		{
			spanCount = 0;
			id = idNum;
			areaType = 0;
			remap = false;
			visited = false;

			connections = new List<int>();
			floors = new List<int>();
		}

		public int SpanCount
		{
			get { return spanCount; }
			set { this.spanCount = value; }
		}

		public int Id 
		{ 
			get { return id; }
			set { this.id = value; }
		}

		public AreaFlags AreaType
		{
			set { this.areaType = value; }
		}

		public bool Remap 
		{
			get { return remap; }
			set { this.remap = value; } 
		}

		public bool Visited
		{
			get { return visited; }
			set { this.visited = value; }
		}

		public List<int> FloorRegions { get { return floors; } }

		public List<int> Connections { get { return connections; } }

		public static int IdWithBorderFlag(int id)
		{
			return id | BorderFlag;
		}

		public static int RemoveFlags(int id)
		{
			return id & IdMask;
		}

		public static bool IsBorder(int id)
		{
			return (id & BorderFlag) == BorderFlag;
		}

		public static bool IsNull(int id)
		{
			return id == 0;
		}

		public static bool IsBorderOrNull(int id)
		{
			return id == 0 || (id & BorderFlag) == BorderFlag;
		}

		public bool IsBorder()
		{
			return (id & BorderFlag) == BorderFlag;
		}

		public bool IsNull()
		{
			return id == 0;
		}

		public bool IsBorderOrNull()
		{
			return id == 0 || (id & BorderFlag) == BorderFlag;
		}

		public static void SetBorderVertex(ref int region)
		{
			region |= VertexBorderFlag;
		}

		public static void SetAreaBorder(ref int region)
		{
			region |= AreaBorderFlag;
		}

		public static bool IsBorderVertex(int r)
		{
			return (r & VertexBorderFlag) != 0;
		}

		public static bool IsAreaBorder(int r)
		{
			return (r & AreaBorderFlag) != 0;
		}

		public static bool IsSameArea(int region1, int region2)
		{
			return (region1 & AreaBorderFlag) == (region2 & AreaBorderFlag);
		}

		public static bool IsSameRegion(int region1, int region2)
		{
			return (region1 & IdMask) == (region2 & IdMask);
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
		public void ReplaceNeighbour(int oldId, int newId)
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
		public void AddUniqueFloorRegion(int n)
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
			int thisId = id;
			int otherId = otherRegion.id;

			// Duplicate current neighbourhood.
			List<int> thisConnected = new List<int>();
			for (int i = 0; i < connections.Count; ++i)
				thisConnected.Add(connections[i]);
			List<int> otherConnected = otherRegion.connections;

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
			connections = new List<int>();
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
