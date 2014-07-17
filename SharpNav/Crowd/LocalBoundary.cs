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

namespace SharpNav.Crowd
{
	/// <summary>
	/// The LocalBoundary class stores segments and polygon indices for temporary use.
	/// </summary>
	public class LocalBoundary
	{
		private const int MAX_LOCAL_SEGS = 8;
		private const int MAX_LOCAL_POLYS = 16;

		private Vector3 center;
		private Segment[] segs;
		private int nsegs;

		private int[] polys;
		private int npolys;

		/// <summary>
		/// Initializes a new instance of the <see cref="LocalBoundart" /> class.
		/// </summary>
		public LocalBoundary()
		{
			Reset();
			segs = new Segment[8];
			polys = new int[16];
		}

		/// <summary>
		/// Reset all the internal data
		/// </summary>
		public void Reset()
		{
			center = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
			nsegs = 0;
			npolys = 0;
		}

		/// <summary>
		/// Add a line segment
		/// </summary>
		/// <param name="dist">The distance</param>
		/// <param name="start">Segment's start coordinate</param>
		/// <param name="end">Segment's end coordinate</param>
		public void AddSegment(float dist, Segment s)
		{
			//insert neighbour based on distance
			int segPos = 0;
			if (nsegs == 0)
			{
				segPos = 0;
			}
			else if (dist >= segs[nsegs - 1].D)
			{
				//further than the last segment, skip
				if (nsegs >= MAX_LOCAL_SEGS)
					return;
				//last, trivial accept
				segPos = nsegs;
			}
			else
			{
				//insert inbetween
				int i;
				for (i = 0; i < nsegs; i++)
					if (dist <= segs[i].D)
						break;
				int tgt = i + 1;
				int n = Math.Min(nsegs - i, MAX_LOCAL_SEGS - tgt);
				if (n > 0)
				{
					for (int j = 0; j < n; j++)
						segs[tgt + j] = segs[i + j];
				}
				segPos = i;
			}

			segs[segPos].D = dist;
			segs[segPos].Start = s.Start;
			segs[segPos].End = s.End;

			if (nsegs < MAX_LOCAL_SEGS)
				nsegs++;
		}

		/// <summary>
		/// Examine polygons in the NavMeshQuery and add polygon edges
		/// </summary>
		/// <param name="reference">The starting polygon reference</param>
		/// <param name="pos">Current position</param>
		/// <param name="collisionQueryRange">Range to query</param>
		/// <param name="navquery">The NavMeshQuery</param>
		public void Update(int reference, Vector3 pos, float collisionQueryRange, NavMeshQuery navquery)
		{
			const int MAX_SEGS_PER_POLY = PathfinderCommon.VERTS_PER_POLYGON;

			if (reference == 0)
			{
				this.center = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
				this.nsegs = 0;
				this.npolys = 0;
				return;
			}

			this.center = pos;

			//first query non-overlapping polygons
			int[] tempArray = new int[polys.Length];
			navquery.FindLocalNeighbourhood(reference, pos, collisionQueryRange, polys, tempArray, ref npolys, MAX_LOCAL_POLYS);

			//secondly, store all polygon edges
			this.nsegs = 0;
			Segment[] segs = new Segment[MAX_SEGS_PER_POLY];
			int nsegs = 0;
			for (int j = 0; j < npolys; j++)
			{
				tempArray = new int[segs.Length];
				navquery.GetPolyWallSegments(polys[j], segs, tempArray, ref nsegs, MAX_SEGS_PER_POLY);
				for (int k = 0; k < nsegs; k++)
				{
					//skip too distant segments
					float tseg;
					float distSqr = MathHelper.Distance.PointToSegment2DSquared(ref pos, ref segs[k].Start, ref segs[k].End, out tseg);
					if (distSqr > collisionQueryRange * collisionQueryRange)
						continue;
					AddSegment(distSqr, segs[k]);
				}
			}
		}

		/// <summary>
		/// Determines whether the polygon reference is a part of the NavMeshQuery.
		/// </summary>
		/// <param name="navquery">The NavMeshQuery</param>
		/// <returns>True if valid, false if not</returns>
		public bool IsValid(NavMeshQuery navquery)
		{
			if (npolys == 0)
				return false;

			for (int i = 0; i < npolys; i++)
			{
				if (!navquery.IsValidPolyRef(polys[i]))
					return false;
			}

			return true;
		}

		public Vector3 GetCenter()
		{
			return center;
		}

		public int GetSegmentCount()
		{
			return nsegs;
		}

		public Segment GetSegment(int i)
		{
			return segs[i];
		}

		public struct Segment
		{
			/// <summary>
			/// Start and end points
			/// </summary>
			public Vector3 Start, End;
			
			/// <summary>
			/// Distance for pruning
			/// </summary>
			public float D; 
		}
	}
}
