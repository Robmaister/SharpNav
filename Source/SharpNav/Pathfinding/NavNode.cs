// Copyright (c) 2013-2016 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
// Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE

using System;

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

namespace SharpNav
{
	/// <summary>
	/// Every polygon becomes a Node, which contains a position and cost.
	/// </summary>
	public class NavNode : IValueWithCost
	{
		public const int NullIndex = ~0;

		public Vector3 Position;

		/// <summary>
		/// Cost from previous node/poly to current.
		/// </summary>
		public float PolyCost;

		/// <summary>
		/// Total cost up to this node
		/// </summary>
		public float TotalCost;
		public int ParentIndex; //index to parent node
		public byte State;
		public NodeFlags Flags = 0; //node flags 0/open/closed
		public NavPolyId Id; //polygon ref the node corresponds to

		//TODO should make more generic or move to Pathfinding namespace

		public float Cost 
		{ 
			get 
			{ 
				return TotalCost; 
			} 
		}
	}
}
