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
	public class LocalBoundary
	{
		private Vector3 center;
		private Segment[] segs;
		private int nsegs;

		private int[] polys;
		private int npolys;

		public LocalBoundary()
		{
			Reset();
			segs = new Segment[8];
			polys = new int[16];
		}

		public void Reset()
		{
			center = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
			nsegs = 0;
			npolys = 0;
		}

		private struct Segment
		{
			/// <summary>
			/// Start and end points
			/// </summary>
			public Vector3 start, end;
			
			/// <summary>
			/// Distance for pruning
			/// </summary>
			public float d; 
		}
	}
}
