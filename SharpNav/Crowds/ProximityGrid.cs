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

		private BBox2i bounds;

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

			this.bounds = new BBox2i(Vector2i.Max, Vector2i.Min);

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

			this.bounds = new BBox2i(Vector2i.Max, Vector2i.Min);
		}

		/// <summary>
		/// Take all the coordinates within a certain range and add them all to an array
		/// </summary>
		/// <param name="id">The id</param>
		/// <param name="minX">Minimum x-coordinate</param>
		/// <param name="minY">Minimum y-coordinate</param>
		/// <param name="maxX">Maximum x-coordinate</param>
		/// <param name="maxY">Maximum y-coordinate</param>
		public void AddItem(int id, float minX, float minY, float maxX, float maxY)
		{
			int invMinX = (int)Math.Floor(minX * invCellSize);
			int invMinY = (int)Math.Floor(minY * invCellSize);
			int invMaxX = (int)Math.Floor(maxX * invCellSize);
			int invMaxY = (int)Math.Floor(maxY * invCellSize);

			bounds.Min.X = Math.Min(bounds.Min.X, invMinX);
			bounds.Min.Y = Math.Min(bounds.Min.Y, invMinY);
			bounds.Max.X = Math.Max(bounds.Max.X, invMaxX);
			bounds.Max.Y = Math.Max(bounds.Max.Y, invMaxY);

			for (int y = invMinY; y <= invMaxY; y++)
			{
				for (int x = invMinX; x <= invMaxX; x++)
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
		/// <param name="minX">The minimum x-coordinate</param>
		/// <param name="minY">The minimum y-coordinate</param>
		/// <param name="maxX">The maximum x-coordinate</param>
		/// <param name="maxY">The maximum y-coordinate</param>
		/// <param name="ids">The array of ids</param>
		/// <param name="maxIds">The maximum number of ids that can be stored</param>
		/// <returns>The number of unique ids</returns>
		public int QueryItems(float minX, float minY, float maxX, float maxY, int[] ids, int maxIds)
		{
			int invMinX = (int)Math.Floor(minX * invCellSize);
			int invMinY = (int)Math.Floor(minY * invCellSize);
			int invMaxX = (int)Math.Floor(maxX * invCellSize);
			int invMaxY = (int)Math.Floor(maxY * invCellSize);

			int n = 0;

			for (int y = invMinY; y <= invMaxY; y++)
			{
				for (int x = invMinX; x <= invMaxX; x++)
				{
					int h = HashPos2(x, y, bucketsSize);
					int idx = buckets[h];
					
					//NOTE: the idx value will never be 0xfff f(because the bucket will never store such
					//a high number). The idx could equal 0xff, which is the default value
					while (idx != 0xff) 
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

		/// <summary>
		/// An "item" is simply a coordinate on the proximity grid
		/// </summary>
		private struct Item
		{
			public int Id;
			public int X, Y;
			public int Next;
		}
	}
}
