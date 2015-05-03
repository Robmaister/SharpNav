// Copyright (c) 2014-2015 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
// Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE

using System;
using System.Collections.Generic;

using SharpNav.Collections.Generic;
using SharpNav.Geometry;

using SharpNav.Pathfinding;

#if MONOGAME
using Vector3 = Microsoft.Xna.Framework.Vector3;
#elif OPENTK
using Vector3 = OpenTK.Vector3;
#elif SHARPDX
using Vector3 = SharpDX.Vector3;
#endif

namespace SharpNav.Crowds
{
	public class PathCorridor
	{
		#region Fields

		private Vector3 pos;
		private Vector3 target;

		private int[] path;
		private int pathCount;
		private int maxPath;

		#endregion

		#region Constructors

		public PathCorridor(int maxPath)
		{
			this.path = new int[maxPath];
			this.pathCount = 0;
			this.maxPath = maxPath;
		}

		#endregion

		#region Properties

		public Vector3 Pos
		{
			get
			{
				return pos;
			}
		}

		public Vector3 Target
		{
			get
			{
				return target;
			}
		}

		public int[] Path
		{
			get
			{
				return path;
			}
		}

		public int PathCount
		{
			get
			{
				return pathCount;
			}
		}

		#endregion

		#region Methods

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
			this.pathCount = 1;
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
			this.pathCount = npath;
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
			const int MaxVisited = 16;
			int[] visited = new int[MaxVisited];
			List<int> listVisited = new List<int>(MaxVisited);
			bool status = navquery.MoveAlongSurface(new NavPoint(path[0], pos), npos, ref result, listVisited);
			visited = listVisited.ToArray();

			if (status == true)
			{
				pathCount = MergeCorridorStartMoved(path, pathCount, maxPath, visited, listVisited.Count);

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
			const float MinTargetDist = 0.01f;

			int numCorners = 0;
			navquery.FindStraightPath(pos, target, path, pathCount, cornerVerts, cornerFlags, cornerPolys, ref numCorners, maxCorners, 0);

			//prune points in the beginning of the path which are too close
			while (numCorners > 0)
			{
				if (((cornerFlags[0] & PathfindingCommon.STRAIGHTPATH_OFFMESH_CONNECTION) != 0) ||
					Vector3Extensions.Distance2D(cornerVerts[0], pos) > MinTargetDist)
					break;
				numCorners--;
				if (numCorners > 0)
				{
					for (int i = 0; i < numCorners; i++)
					{
						cornerFlags[i] = cornerFlags[i + 1];
						cornerPolys[i] = cornerPolys[i + 1];
						cornerVerts[i] = cornerVerts[i + 1];
					}
				}
			}

			//prune points after an off-mesh connection
			for (int i = 0; i < numCorners; i++)
			{
				if ((cornerFlags[i] & PathfindingCommon.STRAIGHTPATH_OFFMESH_CONNECTION) != 0)
				{
					numCorners = i + 1;
					break;
				}
			}

			return numCorners;
		}

		/// <summary>
		/// Use a local area path search to try to reoptimize this corridor
		/// </summary>
		/// <param name="navquery">The NavMeshQuery</param>
		/// <returns>True if optimized, false if not</returns>
		public bool OptimizePathTopology(NavMeshQuery navquery)
		{
			if (pathCount < 3)
				return false;

			const int MaxIter = 32;
			const int MaxRes = 32;

			int[] res = new int[MaxRes];
			int numRes = 0;
			int tempInt = 0;
			navquery.InitSlicedFindPath(new NavPoint(path[0], pos), new NavPoint(path[pathCount - 1], target));
			navquery.UpdateSlicedFindPath(MaxIter, ref tempInt);
			bool status = navquery.FinalizedSlicedPathPartial(path, pathCount, res, ref numRes, MaxRes);

			if (status == true && numRes > 0)
			{
				pathCount = MergeCorridorStartShortcut(path, pathCount, maxPath, res, numRes); 
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

			const int MaxRes = 32;
			int[] res = new int[MaxRes];
			float t = 0;
			Vector3 norm = new Vector3();
			int nres = 0;
			navquery.Raycast(new NavPoint(path[0], pos), goal, ref t, ref norm, res, ref nres, MaxRes);
			if (nres > 1 && t > 0.99f)
			{
				pathCount = MergeCorridorStartShortcut(path, pathCount, maxPath, res, nres);
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
						found = true;
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
			while (npos < pathCount && polyRef != offMeshConRef)
			{
				prevRef = polyRef;
				polyRef = path[npos];
				npos++;
			}

			if (npos == pathCount)
			{
				//could not find offMeshConRef
				return false;
			}

			//prune path
			for (int i = npos; i < pathCount; i++)
				path[i - npos] = path[i];
			pathCount -= npos;

			refs[0] = prevRef;
			refs[1] = polyRef;

			TiledNavMesh nav = navquery.NavMesh;

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
		/// <returns>True if path start changed, false if not</returns>
		public bool FixPathStart(int safeRef, Vector3 safePos)
		{
			pos = safePos;
			if (pathCount < 3 && pathCount > 0)
			{
				path[2] = path[pathCount - 1];
				path[0] = safeRef;
				path[1] = 0;
				pathCount = 3;
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
			int n = Math.Min(pathCount, maxLookAhead);
			for (int i = 0; i < n; i++)
			{
				if (!navquery.IsValidPolyRef(path[i]))
					return false;
			}

			return true;
		}

		public int GetFirstPoly()
		{
			return (pathCount != 0) ? path[0] : 0;
		}

		public int GetLastPoly()
		{
			return (pathCount != 0) ? path[pathCount - 1] : 0;
		}

		#endregion
	}
}
