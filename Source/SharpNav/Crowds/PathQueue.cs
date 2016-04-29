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
	public class PathQueue
	{
		#region Fields

		public const int Invalid = 0;
		private const int MaxQueue = 8;
		private const int MaxKeepAlive = 2; //in number of times Update() is called

		private PathQuery[] queue;
		private int nextHandle = 1;
		private int queueHead;
		private NavMeshQuery navquery;
		private NavQueryFilter navqueryfilter;

		#endregion

		#region Constructors

		public PathQueue(int maxSearchNodeCount, ref TiledNavMesh nav)
		{
			this.navquery = new NavMeshQuery(nav, maxSearchNodeCount);
			this.navqueryfilter = new NavQueryFilter();

			this.queue = new PathQuery[MaxQueue];
			for (int i = 0; i < MaxQueue; i++)
			{
				queue[i].Index = 0;
				queue[i].Path = new Path();
			}

			this.queueHead = 0;
		}

		#endregion

		#region Methods

		public void Update(int maxIters)
		{
			//update path request until there is nothing left to update
			//or up to maxIters pathfinder iterations have been consumed
			int iterCount = maxIters;

			for (int i = 0; i < MaxQueue; i++)
			{
				PathQuery q = queue[queueHead % MaxQueue];

				//skip inactive requests
				if (q.Index == 0)
				{
					queueHead++;
					continue;
				}

				//handle completed request
				if (q.Status == Status.Success || q.Status == Status.Failure)
				{
					q.KeepAlive++;
					if (q.KeepAlive > MaxKeepAlive)
					{
						q.Index = 0;
						q.Status = 0;
					}

					queueHead++;
					continue;
				}

				//handle query start
				if (q.Status == 0)
				{
					q.Status = navquery.InitSlicedFindPath(ref q.Start, ref q.End, navqueryfilter, FindPathOptions.None).ToStatus();
				}

				//handle query in progress
				if (q.Status == Status.InProgress)
				{
					int iters = 0;
					q.Status = navquery.UpdateSlicedFindPath(iterCount, ref iters).ToStatus();

					iterCount -= iters;
				}

				if (q.Status == Status.Success)
				{
					q.Status = navquery.FinalizeSlicedFindPath(q.Path).ToStatus();
				}

				if (iterCount <= 0)
					break;

				queueHead++;
			}
		}

		/// <summary>
		/// Request an empty slot in the path queue
		/// </summary>
		/// <param name="start">Start position</param>
		/// <param name="end">End position</param>
		/// <returns>Index of empty slot</returns>
		public int Request(NavPoint start, NavPoint end)
		{
			//find empty slot
			int slot = -1;
			for (int i = 0; i < MaxQueue; i++)
			{
				if (queue[i].Index == 0)
				{
					slot = i;
					break;
				}
			}

			//could not find slot
			if (slot == -1)
				return PathQueue.Invalid;

			int index = nextHandle++;
			if (nextHandle == 0) nextHandle++;

			PathQuery q = queue[slot];
			q.Index = index;
			q.Start = start;
			q.End = end;

			q.Status = 0;
			q.PathCount = 0;
			q.KeepAlive = 0;

			queue[slot] = q;

			return index;
		}

		/// <summary>
		/// Get the status of the polygon in the path queue
		/// </summary>
		/// <param name="reference">The polygon reference</param>
		/// <returns>The status in the queue</returns>
		public Status GetRequestStatus(int index)
		{
			for (int i = 0; i < MaxQueue; i++)
			{
				if (queue[i].Index == index)
					return queue[i].Status;
			}

			return Status.Failure;
		}

		public bool GetPathResult(int index, out Path path)
		{
			path = null;

			for (int i = 0; i < MaxQueue; i++)
			{
				if (queue[i].Index == index)
				{
					PathQuery q = queue[i];
					
					//free request for reuse
					q.Index = 0;
					q.Status = 0;

					path = new Path(q.Path);

					queue[i] = q;

					return true;
				}
			}

			return false;
		}

		#endregion

		private struct PathQuery
		{
			public int Index;
			
			//path find start and end location
			public NavPoint Start, End;

			//result
			public Path Path;
			public int PathCount;

			//state
			public Status Status;
			public int KeepAlive;
		}
	}
}
