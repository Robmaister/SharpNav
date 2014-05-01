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
		public Vector3 Pos0 { get; set; } //the endpoints of the connection

		public Vector3 Pos1 { get; set; }

		public float Radius { get; set; }

		public int Poly { get; set; }

		public int Flags { get; set; } //assigned flag from Poly

		public int Side { get; set; } //endpoint side

		public uint UserId { get; set; } //id of offmesh connection
	}
}
