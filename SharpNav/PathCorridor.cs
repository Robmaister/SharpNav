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

		private int[] path;
		private int npath;
		private int maxPath;

		public PathCorridor(int maxPath)
		{
			this.path = new int[maxPath];
			this.npath = 0;
			this.maxPath = maxPath;
		}

		/// <summary>
		/// Resets the path to the first polygon.
		/// </summary>
		/// <param name="reference">The starting polygon reference</param>
		/// <param name="pos">Starting position</param>
		public void Reset(int reference, Vector3 pos)
		{
			this.pos = pos;
			this.target = pos;
			this.path[0] = reference;
			this.npath = 1;
		}

		/// <summary>
		/// The current corridor position is expected to be within the first polygon in the path. The target
		/// is expected to be in the last polygon.
		/// </summary>
		/// <param name="target">The target</param>
		/// <param name="path">The polygon path</param>
		/// <param name="npath">The path length</param>
		public void SetCorridor(Vector3 target, int[] path, int npath)
		{
			this.target = target;
			path.CopyTo(this.path, 0);
			this.npath = npath;
		}

		public int[] GetPath()
		{
			return path;
		}

		public int GetPathCount()
		{
			return npath;
		}

		public int GetLastPoly()
		{
			return (npath != 0) ? path[npath - 1] : 0;
		}

		public Vector3 GetTarget()
		{
			return target;
		}
	}
}
