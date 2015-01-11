// Copyright (c) 2014 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
// Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE

using System;

namespace SharpNav
{
	/// <summary>
	/// Stores height data in a grid.
	/// </summary>
	public class HeightPatch
	{
		/// <summary>
		/// The value used when a height value has not yet been set.
		/// </summary>
		public const int UnsetHeight = -1;

		private int xmin, ymin, width, length;
		private int[] data;

		/// <summary>
		/// Initializes a new instance of the <see cref="HeightPatch"/> class.
		/// </summary>
		/// <param name="x">The initial X coordinate of the patch.</param>
		/// <param name="y">The initial Y coordinate of the patch.</param>
		/// <param name="width">The width of the patch.</param>
		/// <param name="length">The length of the patch.</param>
		public HeightPatch(int x, int y, int width, int length)
		{
			if (x < 0 || y < 0 || width <= 0 || length <= 0)
				throw new ArgumentOutOfRangeException("Invalid bounds.");

			this.xmin = x;
			this.ymin = y;
			this.width = width;
			this.length = length;

			this.data = new int[width * length];
			Clear();
		}

		/// <summary>
		/// Gets the X coordinate of the patch.
		/// </summary>
		public int X
		{
			get
			{
				return xmin;
			}
		}

		/// <summary>
		/// Gets the Y coordinate of the patch.
		/// </summary>
		public int Y
		{
			get
			{
				return ymin;
			}
		}

		/// <summary>
		/// Gets the width of the patch.
		/// </summary>
		public int Width
		{
			get
			{
				return width;
			}
		}

		/// <summary>
		/// Gets the length of the patch.
		/// </summary>
		public int Length
		{
			get
			{
				return length;
			}
		}

		/// <summary>
		/// Gets or sets the height at a specified index.
		/// </summary>
		/// <param name="index">The index inside the patch.</param>
		/// <returns>The height at the specified index.</returns>
		public int this[int index]
		{
			get
			{
				return data[index];
			}

			set
			{
				data[index] = value;
			}
		}

		/// <summary>
		/// Gets or sets the height at a specified coordinate (x, y).
		/// </summary>
		/// <param name="x">The X coordinate.</param>
		/// <param name="y">The Y coordinate.</param>
		/// <returns>The height at the specified index.</returns>
		public int this[int x, int y]
		{
			get
			{
				return data[y * width + x];
			}

			set
			{
				data[y * width + x] = value;
			}
		}

		/// <summary>
		/// Checks an index to see whether or not it's height value has been set.
		/// </summary>
		/// <param name="index">The index to check.</param>
		/// <returns>A value indicating whether or not the height value at the index is set.</returns>
		public bool IsSet(int index)
		{
			return data[index] != UnsetHeight;
		}

		/// <summary>
		/// Gets the height value at a specified index. A return value indicates whether the height value is set.
		/// </summary>
		/// <param name="index">The index to use.</param>
		/// <param name="value">Contains the height at the value.</param>
		/// <returns>A value indicating whether the value at the specified index is set.</returns>
		public bool TryGetHeight(int index, out int value)
		{
			value = this[index];
			return value != UnsetHeight;
		}

		/// <summary>
		/// Gets the height value at a specified index. A return value indicates whether the height value is set.
		/// </summary>
		/// <param name="x">The X coordinate.</param>
		/// <param name="y">The Y coordinate.</param>
		/// <param name="value">Contains the height at the value.</param>
		/// <returns>A value indicating whether the value at the specified index is set.</returns>
		public bool TryGetHeight(int x, int y, out int value)
		{
			value = this[x, y];
			return value != UnsetHeight;
		}

		/// <summary>
		/// Resizes the patch. Only works if the new size is smaller than or equal to the initial size.
		/// </summary>
		/// <param name="x">The new X coordinate.</param>
		/// <param name="y">The new Y coordinate.</param>
		/// <param name="width">The new width.</param>
		/// <param name="length">The new length.</param>
		public void Resize(int x, int y, int width, int length)
		{
			if (data.Length < width * length)
				throw new ArgumentException("Only resizing down is allowed right now.");

			this.xmin = x;
			this.ymin = y;
			this.width = width;
			this.length = length;
		}

		/// <summary>
		/// Clears the <see cref="HeightPatch"/> by unsetting every value.
		/// </summary>
		public void Clear()
		{
			for (int i = 0; i < data.Length; i++)
				data[i] = UnsetHeight;
		}

		/// <summary>
		/// Sets all of the height values to the same value.
		/// </summary>
		/// <param name="h">The height to apply to all values.</param>
		public void SetAll(int h)
		{
			for (int i = 0; i < data.Length; i++)
				data[i] = h;
		}
	}
}
