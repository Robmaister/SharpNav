#region License
/**
 * Copyright (c) 2014 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
 * Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE
 */
#endregion

using System;

namespace SharpNav
{
	/// <summary>
	/// Store height data, which will later be merged with the NavMesh
	/// </summary>
	public class HeightPatch
	{
		public const int UnsetHeight = -1;

		private int xmin, ymin, width, height;
		private int[] data;

		public HeightPatch(int x, int y, int width, int height)
		{
			if (x < 0 || y < 0 || width <= 0 || height <= 0)
				throw new ArgumentOutOfRangeException("Invalid bounds.");

			this.xmin = x;
			this.ymin = y;
			this.width = width;
			this.height = height;

			this.data = new int[width * height];
			for (int i = 0; i < data.Length; i++)
				data[i] = UnsetHeight;
		}

		public int X { get { return xmin; } }
		public int Y { get { return ymin; } }
		public int Width { get { return width; } }
		public int Height { get { return height; } }

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

		public bool IsSet(int index)
		{
			return data[index] != UnsetHeight;
		}

		public void Resize(int x, int y, int width, int height)
		{
			if (data.Length < width * height)
				throw new ArgumentException("Only resizing down is allowed right now.");

			this.xmin = x;
			this.ymin = y;
			this.width = width;
			this.height = height;
		}

		public void Clear()
		{
			for (int i = 0; i < data.Length; i++)
				data[i] = UnsetHeight;
		}
	}
}
