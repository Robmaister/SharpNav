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

namespace SharpNav
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

		public int MergeCorridorStartShortcut(int[] path, int npath, int maxPath, int[] visited, int nvisited)
		{
			int furthestPath = -1;
			int furthestVisited = -1;

			//find furhtest common polygon
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
