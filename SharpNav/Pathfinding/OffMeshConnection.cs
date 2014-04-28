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
#elif UNITY3D
using UnityEngine;
#endif

namespace SharpNav.Pathfinding
{
	public class OffMeshConnection
	{
		public Vector3 Pos0; //the endpoints of the connection
		public Vector3 Pos1;
		public float Radius;
		public int Poly;
		public int Flags; //assigned flag from Poly
		public int Side; //endpoint side
		public uint UserId; //id of offmesh connection
	}
}
