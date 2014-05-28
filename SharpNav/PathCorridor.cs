#region License
/**
 * Copyright (c) 2014 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
 * Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE
 */
#endregion

using System;
using System.Collections.Generic;

using SharpNav.Collections.Generic;
using SharpNav.Geometry;

#if MONOGAME || XNA
using Microsoft.Xna.Framework;
#elif OPENTK
using OpenTK;
#elif SHARPDX
using SharpDX;
#elif UNITY3D
using UnityEngine;
#endif

namespace SharpNav
{
	public class PathCorridor
	{
		private Vector3 pos;
		private Vector3 target;

		private uint[] path;
		private int npath;
		private int maxPath;

		public PathCorridor(int maxPath)
		{
			this.path = new uint[maxPath];
			this.npath = 0;
			this.maxPath = maxPath;
		}

		public void Reset(uint reference, Vector3 pos)
		{
			this.pos = pos;
			this.target = pos;
			this.path[0] = reference;
			this.npath = 1;
		}
	}
}
