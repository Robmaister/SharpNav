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
    public class Region
    {
        private int spanCount;                                 
        private short id;                            
        private char areaType;                        
        private bool remap;
        private bool visited;
        private List<int> connections;
        private List<int> floors;

        public Region(short idNum) 
        {
            spanCount = 0;
            id = idNum;
            areaType = '\0';
            remap = false;
            visited = false;

            connections = new List<int>();
            floors = new List<int>();
        }

        public List<int> getFloorRegions()
        {
            return floors;
        }

        public int getFloor(int i)
        {
            return floors[i];
        }
        
        public void addUniqueFloorRegion(int n)
        {
            //check if region floor currently exists
            for (int i = 0; i < floors.Count; ++i)
                if (floors[i] == n)
                        return;
            
            //region floor doesn't exist so add
            floors.Add(n);
        }

        public void removeAdjacentNeighbours()
        {
            // Remove adjacent duplicates.
            for (int i = 0; i < connections.Count && connections.Count > 1; )
            {
                //get the next i
                int ni = (i+1) % connections.Count;
                
                //remove duplicate if found
                if (connections[i] == connections[ni])
                        connections.RemoveAt(i);
                else
                        ++i;
            }
        }
    }
}
