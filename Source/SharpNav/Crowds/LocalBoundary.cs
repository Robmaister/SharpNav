// Copyright (c) 2014-2015 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
// Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE

using System;
using System.Collections.Generic;

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

namespace SharpNav.Crowds
{
	/// <summary>
	/// The LocalBoundary class stores segments and polygon indices for temporary use.
	/// </summary>
	public class LocalBoundary
	{
		#region Fields

		private const int MaxLocalSegs = 8;
		private const int MaxLocalPolys = 16;

		private Vector3 center;
		private Segment[] segs;
		private int segCount;

		private int[] polys;
		private int numPolys;

		#endregion

		#region Constructors

		/// <summary>
		/// Initializes a new instance of the <see cref="LocalBoundary" /> class.
		/// </summary>
		public LocalBoundary()
		{
			Reset();
			segs = new Segment[MaxLocalSegs];
			polys = new int[MaxLocalPolys];
		}

		#endregion

		#region Properties

		/// <summary>
		/// Gets the center
		/// </summary>
		public Vector3 Center
		{
			get
			{
				return center;
			}
		}

		/// <summary>
		/// Gets the segments
		/// </summary>
		public Segment[] Segs
		{
			get
			{
				return segs;
			}
		}

		/// <summary>
		/// Gets the number of segments
		/// </summary>
		public int SegCount
		{
			get
			{
				return segCount;
			}
		}

		#endregion

		#region Methods

		/// <summary>
		/// Reset all the internal data
		/// </summary>
		public void Reset()
		{
			center = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
			segCount = 0;
			numPolys = 0;
		}

		/// <summary>
		/// Add a line segment
		/// </summary>
		/// <param name="dist">The distance</param>
		/// <param name="s">The line segment</param>
		public void AddSegment(float dist, Segment s)
		{
			//insert neighbour based on distance
			int segPos = 0;
			if (segCount == 0)
			{
				segPos = 0;
			}
			else if (dist >= segs[segCount - 1].Dist)
			{
				//further than the last segment, skip
				if (segCount >= MaxLocalSegs)
					return;

				//last, trivial accept
				segPos = segCount;
			}
			else
			{
				//insert inbetween
				int i;
				for (i = 0; i < segCount; i++)
					if (dist <= segs[i].Dist)
						break;
				int tgt = i + 1;
				int n = Math.Min(segCount - i, MaxLocalSegs - tgt);
				if (n > 0)
				{
					for (int j = 0; j < n; j++)
						segs[tgt + j] = segs[i + j];
				}

				segPos = i;
			}

			segs[segPos].Dist = dist;
			segs[segPos].Start = s.Start;
			segs[segPos].End = s.End;

			if (segCount < MaxLocalSegs)
				segCount++;
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
			const int MAX_SEGS_PER_POLY = PathfindingCommon.VERTS_PER_POLYGON;

			if (reference == 0)
			{
				this.center = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
				this.segCount = 0;
				this.numPolys = 0;
				return;
			}

			this.center = pos;

			//first query non-overlapping polygons
			int[] tempArray = new int[polys.Length];
			navquery.FindLocalNeighbourhood(new NavPoint(reference, pos), collisionQueryRange, polys, tempArray, ref numPolys, MaxLocalPolys);

			//secondly, store all polygon edges
			this.segCount = 0;
			Segment[] segs = new Segment[MAX_SEGS_PER_POLY];
			int numSegs = 0;
			for (int j = 0; j < numPolys; j++)
			{
				tempArray = new int[segs.Length];
				navquery.GetPolyWallSegments(polys[j], segs, tempArray, ref numSegs, MAX_SEGS_PER_POLY);
				for (int k = 0; k < numSegs; k++)
				{
					//skip too distant segments
					float tseg;
					float distSqr = Distance.PointToSegment2DSquared(ref pos, ref segs[k].Start, ref segs[k].End, out tseg);
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
			if (numPolys == 0)
				return false;

			for (int i = 0; i < numPolys; i++)
			{
				if (!navquery.IsValidPolyRef(polys[i]))
					return false;
			}

			return true;
		}

		#endregion

		/// <summary>
		/// A line segment contains two points
		/// </summary>
		public struct Segment
		{
			/// <summary>
			/// Start and end points
			/// </summary>
			public Vector3 Start, End;
			
			/// <summary>
			/// Distance for pruning
			/// </summary>
			public float Dist; 
		}
	}
}
