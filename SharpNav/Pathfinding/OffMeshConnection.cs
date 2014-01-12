#region License
/**
 * Copyright (c) 2014 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
 * Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE
 */
#endregion

using System;

#if MONOGAME || XNA
using Microsoft.Xna.Framework;
#elif OPENTK
using OpenTK;
#elif SHARPDX
using SharpDX;
#endif

namespace SharpNav.Pathfinding
{
	public class OffMeshConnection
	{
		public Vector3 pos0; //the endpoints of the connection
		public Vector3 pos1;
		public float radius;
		public int poly;
		public int flags; //assigned flag from Poly
		public int side; //endpoint side
		public uint userId; //id of offmesh connection
	}
}
