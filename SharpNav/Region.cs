#region License
/**
 * Copyright (c) 2013 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
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
		private int spanCount;
		private ushort id;
		private byte areaType;
		private bool remap;
		private bool visited;
		private List<int> connections;
		private List<int> floors;

		public Region(ushort idNum)
		{
			spanCount = 0;
			id = idNum;
			areaType = 0;
			remap = false;
			visited = false;

			connections = new List<int>();
			floors = new List<int>();
		}

		/// <summary>
		/// Returns the list of floor regions
		/// </summary>
		public List<int> getFloorRegions() { return floors; }

		/// <summary>
		/// Returns the list of neighbors.
		/// </summary>
		/// <returns></returns>
		public List<int> getConnections() { return connections; }

		/// <summary>
		/// Remove adjacent connections if there is a duplicate
		/// </summary>
		public void removeAdjacentNeighbours()
		{
			// Remove adjacent duplicates.
			for (int i = 0; i < connections.Count && connections.Count > 1; )
			{
				//get the next i
				int ni = (i + 1) % connections.Count;

				//remove duplicate if found
				if (connections[i] == connections[ni])
					connections.RemoveAt(i);
				else
					++i;
			}
		}

		/// <summary>
		/// Replace all connection and floor values 
		/// </summary>
		/// <param name="oldId">The value you want to replace</param>
		/// <param name="newId">The new value that will be used</param>
		public void replaceNeighbour(ushort oldId, ushort newId)
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
				removeAdjacentNeighbours();
		}

		/// <summary>
		/// Determine whether this region can merge with another region.
		/// </summary>
		/// <param name="otherRegion">The other region to merge with</param>
		/// <returns></returns>
		public bool canMergeWithRegion(Region otherRegion)
		{
			//make sure area types are different
			if (areaType != otherRegion.areaType)
				return false;
			
			//count the number of connections
			int n = 0;
			for (int i = 0; i < connections.Count; ++i)
			{
				if (connections[i] == otherRegion.id)
						n++;
			}
		
			//make sure only one connection
			if (n > 1)
				return false;
		
			//make sure floors are separate
			for (int i = 0; i < floors.Count; ++i)
			{
				if (floors[i] == otherRegion.id)
						return false;
			}

			return true;
		}

		/// <summary>
		/// Only add a floor if it hasn't been added already
		/// </summary>
		/// <param name="n">The value of the floor</param>
		public void addUniqueFloorRegion(int n)
		{
			//check if floor currently exists
			for (int i = 0; i < floors.Count; ++i)
				if (floors[i] == n)
					return;

			//region floor doesn't exist so add
			floors.Add(n);
		}

		/// <summary>
		/// Merge two regions into one. Needs good testing
		/// </summary>
		/// <param name="otherRegion">The region to merge with</param>
		/// <returns></returns>
		public bool mergeRegions(Region otherRegion)
		{
			ushort thisId = id;
			ushort otherId = otherRegion.id;
		
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
		
			removeAdjacentNeighbours();
		
			for (int j = 0; j < otherRegion.floors.Count; ++j)
				addUniqueFloorRegion(otherRegion.floors[j]);
			spanCount += otherRegion.spanCount;
			otherRegion.spanCount = 0;
			otherRegion.connections.Clear();

			return true;
		}



		/// <summary>
		/// Test if region is connected to a border
		/// </summary>
		/// <returns></returns>
		public bool isRegionConnectedToBorder()
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
