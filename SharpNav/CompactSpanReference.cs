#region License
/**
 * Copyright (c) 2014 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
 * Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE
 */
#endregion

using System;
using System.Runtime.InteropServices;

namespace SharpNav
{
	[StructLayout(LayoutKind.Sequential)]
	public struct CompactSpanReference
	{
		private int x;
		private int y;
		private int index;

		public CompactSpanReference(int x, int y, int i)
		{
			this.x = x;
			this.y = y;
			this.index = i;
		}

		public int X { get { return x; } }

		public int Y { get { return y; } }

		public int Index { get { return index; } }
	}
}
