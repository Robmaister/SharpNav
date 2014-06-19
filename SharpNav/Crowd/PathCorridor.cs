#region License
/**
 * Copyright (c) 2014 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
 * Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE
 */
#endregion

using System;
using System.Collections.Generic;

using SharpNav.Collections.Generic;
using SharpNav.Geometry;

#if MONOGAME || XNA
using Microsoft.Xna.Framework;
#elif OPENTK
using OpenTK;
#elif SHARPDX
using SharpDX;
#elif UNITY3D
using UnityEngine;
#endif

namespace SharpNav.Crowd
{
	public class PathCorridor
	{
		private Vector3 pos;
		private Vector3 target;

		private int[] path;
		private int npath;
		private int maxPath;

		public PathCorridor(int maxPath)
		{
			this.path = new int[maxPath];
			this.npath = 0;
			this.maxPath = maxPath;
		}

		/// <summary>
		/// Resets the path to the first polygon.
		/// </summary>
		/// <param name="reference">The starting polygon reference</param>
		/// <param name="pos">Starting position</param>
		public void Reset(int reference, Vector3 pos)
		{
			this.pos = pos;
			this.target = pos;
			this.path[0] = reference;
			this.npath = 1;
		}

		/// <summary>
		/// The current corridor position is expected to be within the first polygon in the path. The target
		/// is expected to be in the last polygon.
		/// </summary>
		/// <param name="target">The target</param>
		/// <param name="path">The polygon path</param>
		/// <param name="npath">The path length</param>
		public void SetCorridor(Vector3 target, int[] path, int npath)
		{
			this.target = target;
			path.CopyTo(this.path, 0);
			this.npath = npath;
		}

		/// <summary>
		/// Move along the NavMeshQuery and update the position
		/// </summary>
		/// <param name="npos">Current position</param>
		/// <param name="navquery">The NavMeshQuery</param>
		/// <returns>True if position changed, false if not</returns>
		public bool MovePosition(Vector3 npos, NavMeshQuery navquery)
		{
			//move along navmesh and update new position
			Vector3 result = new Vector3();
			const int MAX_VISITED = 16;
			int[] visited = new int[MAX_VISITED];
			List<int> listVisited = new List<int>(MAX_VISITED);
			int nvisited = 0;
			bool status = navquery.MoveAlongSurface(path[0], pos, npos, ref result, listVisited);
			visited = listVisited.ToArray();

			if (status == true)
			{
				npath = MergeCorridorStartMoved(path, npath, maxPath, visited, nvisited);

				//adjust the position to stay on top of the navmesh
				float h = pos.Y;
				navquery.GetPolyHeight(path[0], result, ref h);
				result.Y = h;
				pos = result;
				return true;
			}

			return false;
		}

		public int FindCorners(Vector3[] cornerVerts, int[] cornerFlags, int[] cornerPolys, int maxCorners, NavMeshQuery navquery)
		{
			float MIN_TARGET_DIST = 0.01f;

			int ncorners = 0;
			navquery.FindStraightPath(pos, target, path, npath, cornerVerts, cornerFlags, cornerPolys, ref ncorners, maxCorners, 0);

			//prune points in the beginning of the path which are too close
			while (ncorners > 0)
			{
				if (((cornerFlags[0] & PathfinderCommon.STRAIGHTPATH_OFFMESH_CONNECTION) != 0) ||
					Vector3Extensions.Distance2D(cornerVerts[0], pos) > MIN_TARGET_DIST)
					break;
				ncorners--;
				if (ncorners > 0)
				{
					for (int i = 0; i < ncorners; i++)
					{
						cornerFlags[i] = cornerFlags[i + 1];
						cornerPolys[i] = cornerPolys[i + 1];
						cornerVerts[i] = cornerVerts[i + 1];
					}
				}
			}

			//prune points after an off-mesh connection
			for (int i = 0; i < ncorners; i++)
			{
				if ((cornerFlags[i] & PathfinderCommon.STRAIGHTPATH_OFFMESH_CONNECTION) != 0)
				{
					ncorners = i + 1;
					break;
				}
			}

			return ncorners;
		}

		/// <summary>
		/// Use a local area path search to try to reoptimize this corridor
		/// </summary>
		/// <param name="navquery">The NavMeshQuery</param>
		/// <returns>True if optimized, false if not</returns>
		public bool OptimizePathTopology(NavMeshQuery navquery)
		{
			if (npath < 3)
				return false;

			const int MAX_ITER = 32;
			const int MAX_RES = 32;

			int[] res = new int[MAX_RES];
			int nres = 0;
			int tempInt = 0;
			navquery.InitSlicedFindPath(path[0], path[npath - 1], pos, target);
			navquery.UpdateSlicedFindPath(MAX_ITER, ref tempInt);
			bool status = navquery.FinalizedSlicedPathPartial(path, npath, res, ref nres, MAX_RES);

			if (status == true && nres > 0)
			{
				npath = MergeCorridorStartShortcut(path, npath, maxPath, res, nres); 
				return true;
			}

			return false;
		}

		/// <summary>
		/// Use an efficient local visibility search to try to optimize the corridor between the current position and the next.
		/// </summary>
		/// <param name="next">The next postion</param>
		/// <param name="pathOptimizationRange">The range</param>
		/// <param name="navquery">The NavMeshQuery</param>
		public void OptimizePathVisibility(Vector3 next, float pathOptimizationRange, NavMeshQuery navquery)
		{
			//clamp the ray to max distance
			Vector3 goal = next;
			float dist = Vector3Extensions.Distance2D(pos, goal);

			//if too close to the goal, do not try to optimize
			if (dist < 0.01f)
				return;

			dist = Math.Min(dist + 0.01f, pathOptimizationRange);

			//adjust ray length
			Vector3 delta = goal - pos;
			goal = pos + delta * (pathOptimizationRange / dist);

			const int MAX_RES = 32;
			int[] res = new int[MAX_RES];
			float t = 0;
			Vector3 norm = new Vector3();
			int nres = 0;
			navquery.Raycast(path[0], pos, goal, ref t, ref norm, res, ref nres, MAX_RES);
			if (nres > 1 && t > 0.99f)
			{
				npath = MergeCorridorStartShortcut(path, npath, maxPath, res, nres);
			}
		}

		/// <summary>
		/// Merge two paths after the start is changed
		/// </summary>
		/// <param name="path">The current path</param>
		/// <param name="npath">Current path length</param>
		/// <param name="maxPath">Maximum path length allowed</param>
		/// <param name="visited">The visited polygons</param>
		/// <param name="nvisited">Visited path length</param>
		/// <returns>New path length</returns>
		public int MergeCorridorStartMoved(int[] path, int npath, int maxPath, int[] visited, int nvisited)
		{
			int furthestPath = -1;
			int furthestVisited = -1;

			//find furthest common polygon
			for (int i = npath - 1; i >= 0; i--)
			{
				bool found = false;
				for (int j = nvisited - 1; j >= 0; j--)
				{
					if (path[i] == visited[j])
					{
						furthestPath = i;
						furthestVisited = j;
						found = false;
					}
				}
				if (found)
					break;
			}

			//if no intersection found just return current path
			if (furthestPath == -1 || furthestVisited == -1)
				return npath;

			//concatenate paths

			//adjust beginning of buffer to include the visited
			int req = nvisited - furthestVisited;
			int orig = Math.Min(furthestPath + 1, npath);
			int size = Math.Max(0, npath - orig);
			if (req + size > maxPath)
				size = maxPath - req;
			if (size > 0)
			{
				for (int i = 0; i < size; i++)
					path[req + i] = path[orig + i];
			}

			//store visited
			for (int i = 0; i < req; i++)
				path[i] = visited[(nvisited - 1) - i];

			return req + size;
		}

		/// <summary>
		/// Merge two paths when a shorter path is found
		/// </summary>
		/// <param name="path">The current path</param>
		/// <param name="npath">Current path length</param>
		/// <param name="maxPath">Maximum path length allowed</param>
		/// <param name="visited">The visited polygons</param>
		/// <param name="nvisited">Visited path length</param>
		/// <returns>New path length</returns>
		public int MergeCorridorStartShortcut(int[] path, int npath, int maxPath, int[] visited, int nvisited)
		{
			int furthestPath = -1;
			int furthestVisited = -1;

			//find furthest common polygon
			for (int i = npath - 1; i >= 0; i--)
			{
				bool found = false;
				for (int j = nvisited - 1; j >= 0; j--)
				{
					if (path[i] == visited[j])
					{
						furthestPath = i;
						furthestVisited = j;
						found = true;
					}
				}

				if (found)
					break;
			}

			//if no intersection found, return current path
			if (furthestPath == -1 || furthestVisited == -1)
				return npath;

			//concatenate paths
			//adjust beginning of the buffer to include the visited
			int req = furthestVisited;
			if (req <= 0)
				return npath;

			int orig = furthestPath;
			int size = Math.Max(0, npath - orig);
			if (req + size > maxPath)
				size = maxPath - req;
			for (int i = 0; i < size; i++)
				path[req + i] = path[orig + i];

			//store visited
			for (int i = 0; i < req; i++)
				path[i] = visited[i];

			return req + size;
		}

		public bool MoveOverOffmeshConnection(int offMeshConRef, int[] refs, ref Vector3 startPos, ref Vector3 endPos, NavMeshQuery navquery)
		{
			//advance the path up to and over the off-mesh connection
			int prevRef = 0, polyRef = path[0];
			int npos = 0;
			while (npos < npath && polyRef != offMeshConRef)
			{
				prevRef = polyRef;
				polyRef = path[npos];
				npos++;
			}
			if (npos == npath)
			{
				//could not find offMeshConRef
				return false;
			}

			//prune path
			for (int i = npos; i < npath; i++)
				path[i - npos] = path[i];
			npath -= npos;

			refs[0] = prevRef;
			refs[1] = polyRef;

			TiledNavMesh nav = navquery.Nav;

			if (nav.GetOffMeshConnectionPolyEndPoints(refs[0], refs[1], ref startPos, ref endPos) == true)
			{
				pos = endPos;
				return true;
			}

			return false;
		}

		/// <summary>
		/// Adjust the beginning of the path
		/// </summary>
		/// <param name="safeRef">The starting polygon reference</param>
		/// <param name="safePos">The starting position</param>
		/// <returns></returns>
		public bool FixPathStart(int safeRef, Vector3 safePos)
		{
			pos = safePos;
			if (npath < 3 && npath > 0)
			{
				path[2] = path[npath - 1];
				path[0] = safeRef;
				path[1] = 0;
				npath = 3;
			}
			else
			{
				path[0] = safeRef;
				path[1] = 0;
			}

			return true;
		}

		/// <summary>
		/// Determines whether all the polygons in the path are valid
		/// </summary>
		/// <param name="maxLookAhead">The amount of polygons to examine</param>
		/// <param name="navquery">The NavMeshQuery</param>
		/// <returns>True if all valid, false if otherwise</returns>
		public bool IsValid(int maxLookAhead, NavMeshQuery navquery)
		{
			int n = Math.Min(npath, maxLookAhead);
			for (int i = 0; i < n; i++)
			{
				if (!navquery.IsValidPolyRef(path[i]))
					return false;
			}

			return true;
		}

		public Vector3 GetPos()
		{
			return pos;
		}

		public int[] GetPath()
		{
			return path;
		}

		public int GetPathCount()
		{
			return npath;
		}

		public int GetFirstPoly()
		{
			return (npath != 0) ? path[0] : 0;
		}

		public int GetLastPoly()
		{
			return (npath != 0) ? path[npath - 1] : 0;
		}

		public Vector3 GetTarget()
		{
			return target;
		}
	}
}
