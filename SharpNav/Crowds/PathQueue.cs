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

namespace SharpNav.Crowds
{
	public class PathQueue
	{
		public const byte PATHQ_INVALID = 0;
		private const int MaxQueue = 8;

		private PathQuery[] queue; //size = MAX_QUEUE
		private int nextHandle;
		private int maxPathSize;
		private int queueHead;
		private NavMeshQuery navquery;

		public PathQueue(int maxPathSize, int maxSearchNodeCount, ref TiledNavMesh nav)
		{
			this.navquery = new NavMeshQuery(nav, maxSearchNodeCount);

			this.maxPathSize = maxPathSize;
			this.queue = new PathQuery[MaxQueue];
			for (int i = 0; i < MaxQueue; i++)
			{
				queue[i].Reference = PATHQ_INVALID;
				queue[i].Path = new int[maxPathSize];
			}

			this.queueHead = 0;
		}

		public void Update(int maxIters)
		{
			const int MAX_KEEP_ALIVE = 2; //in update ticks

			//update path request until there is nothing left to update
			//or up to maxIters pathfinder iterations have been consumed
			int iterCount = maxIters;

			for (int i = 0; i < MaxQueue; i++)
			{
				PathQuery q = queue[queueHead % MaxQueue];

				//skip inactive requests
				if (q.Reference == PATHQ_INVALID)
				{
					queueHead++;
					continue;
				}

				//handle completed request
				if (q.status == Status.Success || q.status == Status.Failure)
				{
					q.KeepAlive++;
					if (q.KeepAlive > MAX_KEEP_ALIVE)
					{
						q.Reference = PATHQ_INVALID;
						q.status = 0;
					}

					queueHead++;
					continue;
				}

				//handle query start
				if (q.status == 0)
				{
					q.status = BoolToStatus(navquery.InitSlicedFindPath((int)q.StartRef, (int)q.EndRef, q.StartPos, q.EndPos));
				}

				//handle query in progress
				if (q.status == Status.InProgress)
				{
					int iters = 0;
					q.status = BoolToStatus(navquery.UpdateSlicedFindPath(iterCount, ref iters));

					iterCount -= iters;
				}

				if (q.status == Status.Success)
				{
					q.status = BoolToStatus(navquery.FinalizeSlicedFindPath(q.Path, ref q.PathCount, maxPathSize));
				}

				if (iterCount <= 0)
					break;

				queueHead++;
			}
		}

		/// <summary>
		/// Request an empty slot in the path queue
		/// </summary>
		/// <param name="startRef">Start polygon reference</param>
		/// <param name="endRef">End polygon reference</param>
		/// <param name="startPos">Start position</param>
		/// <param name="endPos">End position</param>
		/// <returns>Index of empty slot</returns>
		public int Request(int startRef, int endRef, Vector3 startPos, Vector3 endPos)
		{
			//find empty slot
			int slot = -1;
			for (int i = 0; i < MaxQueue; i++)
			{
				if (queue[i].Reference == PATHQ_INVALID)
				{
					slot = i;
					break;
				}
			}

			//could not find slot
			if (slot == -1)
				return PATHQ_INVALID;

			int reference = nextHandle++;
			if (nextHandle == PATHQ_INVALID) nextHandle++;

			PathQuery q = queue[slot];
			q.Reference = reference;
			q.StartPos = startPos;
			q.StartRef = startRef;
			q.EndPos = endPos;
			q.EndRef = endRef;

			q.status = 0;
			q.PathCount = 0;
			q.KeepAlive = 0;

			queue[slot] = q;

			return reference;
		}

		/// <summary>
		/// Get the status of the polygon in the path queue
		/// </summary>
		/// <param name="reference">The polygon reference</param>
		/// <returns>The status in the queue</returns>
		public Status GetRequestStatus(int reference)
		{
			for (int i = 0; i < MaxQueue; i++)
			{
				if (queue[i].Reference == reference)
					return queue[i].status;
			}

			return Status.Failure;
		}

		public bool GetPathResult(int reference, int[] path, ref int pathSize, int maxPath)
		{
			for (int i = 0; i < MaxQueue; i++)
			{
				if (queue[i].Reference == reference)
				{
					PathQuery q = queue[i];
					
					//free request for reuse
					q.Reference = PATHQ_INVALID;
					q.status = 0;
					
					//copy path
					int n = Math.Min(q.PathCount, maxPath);
					q.Path.CopyTo(path, 0);
					pathSize = n;

					queue[i] = q;

					return true;
				}
			}

			return false;
		}

		public Status BoolToStatus(bool variable)
		{
			if (variable)
				return Status.Success;
			else
				return Status.Failure;
		}

		private struct PathQuery
		{
			public int Reference;
			
			//path find start and end location
			public Vector3 StartPos, EndPos;
			public int StartRef, EndRef;

			//result
			public int[] Path;
			public int PathCount;

			//state
			public Status status;
			public int KeepAlive;
		}
	}
}
