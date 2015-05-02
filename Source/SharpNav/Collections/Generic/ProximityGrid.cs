// Copyright (c) 2014-2015 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
// Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE

using System;
using System.Collections.Generic;

using SharpNav.Collections.Generic;
using SharpNav.Geometry;

#if MONOGAME
using Vector3 = Microsoft.Xna.Framework.Vector3;
#elif OPENTK
using Vector3 = OpenTK.Vector3;
#elif SHARPDX
using Vector3 = SharpDX.Vector3;
#endif

namespace SharpNav.Collections.Generic
{
	/// <summary>
	/// A <see cref="ProximityGrid{T}"/> is a uniform 2d grid that can efficiently retrieve items near a specified grid cell.
	/// </summary>
	/// <typeparam name="T">An equatable type.</typeparam>
	public class ProximityGrid<T>
		where T : IEquatable<T>
	{
		#region Fields

		private const int Invalid = -1;

		private float cellSize;
		private float invCellSize;

		private Item[] pool;
		private int poolHead;

		private int[] buckets;

		private BBox2i bounds;

		#endregion

		#region Constructors

		/// <summary>
		/// Initializes a new instance of the <see cref="ProximityGrid{T}"/> class.
		/// </summary>
		/// <param name="poolSize">The size of the item array</param>
		/// <param name="cellSize">The size of each cell</param>
		public ProximityGrid(int poolSize, float cellSize)
		{
			this.cellSize = cellSize;
			this.invCellSize = 1.0f / cellSize;

			//allocate hash buckets
			this.buckets = new int[MathHelper.NextPowerOfTwo(poolSize)];

			//allocate pool of items
			this.poolHead = 0;
			this.pool = new Item[poolSize];
			for (int i = 0; i < this.pool.Length; i++)
				this.pool[i] = new Item();

			this.bounds = new BBox2i(Vector2i.Max, Vector2i.Min);

			Clear();
		}

		#endregion

		#region Methods

		/// <summary>
		/// Reset all the data
		/// </summary>
		public void Clear()
		{
			for (int i = 0; i < buckets.Length; i++)
				buckets[i] = Invalid;

			poolHead = 0;

			this.bounds = new BBox2i(Vector2i.Max, Vector2i.Min);
		}

		/// <summary>
		/// Take all the coordinates within a certain range and add them all to an array
		/// </summary>
		/// <param name="value">The value.</param>
		/// <param name="minX">Minimum x-coordinate</param>
		/// <param name="minY">Minimum y-coordinate</param>
		/// <param name="maxX">Maximum x-coordinate</param>
		/// <param name="maxY">Maximum y-coordinate</param>
		public void AddItem(T value, float minX, float minY, float maxX, float maxY)
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
					if (poolHead < pool.Length)
					{
						int h = HashPos2(x, y, buckets.Length);
						int idx = poolHead;
						poolHead++;
						pool[idx].X = x;
						pool[idx].Y = y;
						pool[idx].Value = value;
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
		/// <param name="values">The array of values</param>
		/// <param name="maxVals">The maximum number of values that can be stored</param>
		/// <returns>The number of unique values</returns>
		public int QueryItems(float minX, float minY, float maxX, float maxY, T[] values, int maxVals)
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
					int h = HashPos2(x, y, buckets.Length);
					int idx = buckets[h];
					
					while (idx != Invalid) 
					{
						if (pool[idx].X == x && pool[idx].Y == y)
						{
							//check if the id exists already
							int i = 0;
							while (i != n && !values[i].Equals(pool[idx].Value))
								i++;

							//item not found, add it
							if (i == n)
							{
								if (n >= maxVals)
									return n;
								values[n++] = pool[idx].Value;
							}
						}

						idx = pool[idx].Next;
					}
				}
			}

			return n;
		}

		/// <summary>
		/// Gets the number of items at a specific location.
		/// </summary>
		/// <param name="x">The X coordinate.</param>
		/// <param name="y">The Y coordinate.</param>
		/// <returns>The number of items at the specified coordinates.</returns>
		public int GetItemCountAtLocation(int x, int y)
		{
			int n = 0;
			int h = HashPos2(x, y, buckets.Length);
			int idx = buckets[h];

			while (idx != Invalid)
			{
				Item item = pool[idx];
				if (item.X == x && item.Y == y)
					n++;
				idx = item.Next;
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
		public static int HashPos2(int x, int y, int n)
		{
			return ((x * 73856093) ^ (y * 19349663)) & (n - 1);
		}

		#endregion

		/// <summary>
		/// An "item" is simply a coordinate on the proximity grid
		/// </summary>
		private class Item
		{
			public T Value { get; set; }

			public int X { get; set; }

			public int Y { get; set; }

			public int Next { get; set; }
		}
	}
}
