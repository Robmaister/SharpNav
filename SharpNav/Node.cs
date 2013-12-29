#region License
/**
 * Copyright (c) 2013 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
 * Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE
 */
#endregion

using System;

using SharpNav.Collections.Generic;
using SharpNav.Geometry;

#if MONOGAME || XNA
using Microsoft.Xna.Framework;
#elif OPENTK
using OpenTK;
#elif SHARPDX
using SharpDX;
#endif

namespace SharpNav
{
	/// <summary>
	/// Every polygon becomes a Node, which contains a position and cost.
	/// </summary>
	public class Node : IValueWithCost
	{
		public Vector3 pos;
		public float cost;
		public float total;
		public int pidx = 30; //index to parent node
		public NodeFlags flags = 0; //node flags 0/open/closed
		public uint id; //polygon ref the node corresponds to

		public float Cost { get { return total; } }
	}
}
