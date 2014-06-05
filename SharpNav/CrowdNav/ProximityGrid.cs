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

namespace SharpNav.CrowdNav
{
	public class ProximityGrid
	{
		private int maxItems;
		private float cellSize;
		private float invCellSize;

		private Item[] pool;
		private int poolHead;
		private int poolSize;

		private int[] buckets;
		private int bucketsSize;

		private int[] bounds; //size = 4

		/// <summary>
		/// Initializes a new instance of the <see cref="ProximityGrid" /> class.
		/// </summary>
		/// <param name="poolSize">The size of the item array</param>
		/// <param name="cellSize">The size of each cell</param>
		public ProximityGrid(int poolSize, float cellSize)
		{
			this.cellSize = cellSize;
			this.invCellSize = 1.0f / cellSize;

			//allocate hash buckets
			this.bucketsSize = MathHelper.NextPowerOfTwo(poolSize);
			this.buckets = new int[this.bucketsSize];

			//allocate pool of items
			this.poolSize = poolSize;
			this.poolHead = 0;
			this.pool = new Item[this.poolSize];

			this.bounds = new int[4];

			Clear();
		}

		/// <summary>
		/// Reset all the data
		/// </summary>
		public void Clear()
		{
			for (int i = 0; i < bucketsSize; i++)
				buckets[i] = 0xff;
			poolHead = 0;
			bounds[0] = 0xffff;
			bounds[1] = 0xffff;
			bounds[2] = -0xffff;
			bounds[3] = -0xffff;
		}

		/// <summary>
		/// Take all the coordinates within a certain range and add them all to an array
		/// </summary>
		/// <param name="id">The id</param>
		/// <param name="minx">Minimum x-coordinate</param>
		/// <param name="miny">Minimum y-coordinate</param>
		/// <param name="maxx">Maximum x-coordinate</param>
		/// <param name="maxy">Maximum y-coordinate</param>
		public void AddItem(int id, float minx, float miny, float maxx, float maxy)
		{
			int iminx = (int)Math.Floor(minx * invCellSize);
			int iminy = (int)Math.Floor(miny * invCellSize);
			int imaxx = (int)Math.Floor(maxx * invCellSize);
			int imaxy = (int)Math.Floor(maxy * invCellSize);

			bounds[0] = Math.Min(bounds[0], iminx);
			bounds[1] = Math.Min(bounds[1], iminy);
			bounds[2] = Math.Max(bounds[2], imaxx);
			bounds[3] = Math.Max(bounds[3], imaxy);

			for (int y = iminy; y <= imaxy; y++)
			{
				for (int x = iminx; x <= imaxx; x++)
				{
					if (poolHead < poolSize)
					{
						int h = HashPos2(x, y, bucketsSize);
						int idx = poolHead;
						poolHead++;
						pool[idx].X = x;
						pool[idx].Y = y;
						pool[idx].Id = id;
						pool[idx].Next = buckets[h];
						buckets[h] = idx;
					}
				}
			}
		}

		/// <summary>
		/// Take all the items within a certain range and add their ids to an array.
		/// </summary>
		/// <param name="minx">The minimum x-coordinate</param>
		/// <param name="miny">The minimum y-coordinate</param>
		/// <param name="maxx">The maximum x-coordinate</param>
		/// <param name="maxy">The maximum y-coordinate</param>
		/// <param name="ids">The array of ids</param>
		/// <param name="maxIds">The maximum number of ids that can be stored</param>
		/// <returns>The number of unique ids</returns>
		public int QueryItems(float minx, float miny, float maxx, float maxy, int[] ids, int maxIds)
		{
			int iminx = (int)Math.Floor(minx * invCellSize);
			int iminy = (int)Math.Floor(miny * invCellSize);
			int imaxx = (int)Math.Floor(maxx * invCellSize);
			int imaxy = (int)Math.Floor(maxy * invCellSize);

			int n = 0;

			for (int y = iminy; y <= imaxy; y++)
			{
				for (int x = iminx; x <= imaxx; x++)
				{
					int h = HashPos2(x, y, bucketsSize);
					int idx = buckets[h];
					while (idx != 0xffff)
					{
						if (pool[idx].X == x && pool[idx].Y == y)
						{
							//check if the id exists already
							int i = 0;
							while (i != n && ids[i] != pool[idx].Id)
								i++;

							//item not found, add it
							if (i == n)
							{
								if (n >= maxIds)
									return n;
								ids[n++] = pool[idx].Id;
							}
						}
						idx = pool[idx].Next;
					}
				}
			}

			return n;
		}

		/// <summary>
		/// Hash function
		/// </summary>
		/// <param name="x">The x-coordinate</param>
		/// <param name="y">The y-coordinate</param>
		/// <param name="n">Total size of hash table</param>
		/// <returns>A hash value</returns>
		public int HashPos2(int x, int y, int n)
		{
			return ((x * 73856093) ^ (y * 19349663)) & (n - 1);
		}

		private struct Item
		{
			public int Id;
			public int X, Y;
			public int Next;
		}
	}
}
