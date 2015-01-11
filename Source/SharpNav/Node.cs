// Copyright (c) 2013-2015 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
// Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE

using System;

using SharpNav.Collections.Generic;
using SharpNav.Geometry;

#if MONOGAME
using Vector3 = Microsoft.Xna.Framework.Vector3;
#elif OPENTK
using Vector3 = OpenTK.Vector3;
#elif SHARPDX
using Vector3 = SharpDX.Vector3;
#endif

namespace SharpNav
{
	/// <summary>
	/// Every polygon becomes a Node, which contains a position and cost.
	/// </summary>
	public class Node : IValueWithCost
	{
		public Vector3 Pos;
		public float cost;
		public float total;
		public int ParentIdx = 30; //index to parent node
		public NodeFlags Flags = 0; //node flags 0/open/closed
		public int Id; //polygon ref the node corresponds to

		public float Cost 
		{ 
			get 
			{ 
				return total; 
			} 
		}
	}
}
