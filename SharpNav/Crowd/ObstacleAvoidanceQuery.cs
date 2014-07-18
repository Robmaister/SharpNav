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
	public class ObstacleAvoidanceQuery
	{
		private const int MAX_PATTERN_DIVS = 32;
		private const int MAX_PATTERN_RINGS = 4;

		private ObstacleAvoidanceParams parameters;
		private float invHorizTime;
		private float vmax;
		private float invVmax;

		private int maxCircles;
		private ObstacleCircle[] circles;
		private int ncircles;

		private int maxSegments;
		private ObstacleSegment[] segments;
		private int nsegments;

		public ObstacleAvoidanceQuery(int maxCircles, int maxSegments)
		{
			this.maxCircles = maxCircles;
			this.ncircles = 0;
			this.circles = new ObstacleCircle[this.maxCircles];

			this.maxSegments = maxSegments;
			this.nsegments = 0;
			this.segments = new ObstacleSegment[this.maxSegments];
		}

		public void Reset()
		{
			ncircles = 0;
			nsegments = 0;
		}

		/// <summary>
		/// Add a new circle to the array
		/// </summary>
		/// <param name="pos">The position</param>
		/// <param name="rad">The radius</param>
		/// <param name="vel">The velocity</param>
		/// <param name="dvel">The desired velocity</param>
		public void AddCircle(Vector3 pos, float rad, Vector3 vel, Vector3 dvel)
		{
			if (ncircles >= maxCircles)
				return;

			circles[ncircles].P = pos;
			circles[ncircles].Rad = rad;
			circles[ncircles].Vel = vel;
			circles[ncircles].DVel = dvel;
			ncircles++;
		}

		/// <summary>
		/// Add a segment to the array
		/// </summary>
		/// <param name="p">One endpoint</param>
		/// <param name="q">The other endpoint</param>
		public void AddSegment(Vector3 p, Vector3 q)
		{
			if (nsegments > maxSegments)
				return;

			segments[nsegments].P = p;
			segments[nsegments].Q = q;
			nsegments++;
		}

		/// <summary>
		/// Prepare the obstacles for further calculations
		/// </summary>
		/// <param name="pos">Current position</param>
		/// <param name="dvel">Desired velocity</param>
		public void Prepare(Vector3 pos, Vector3 dvel)
		{
			//prepare obstacles
			for (int i = 0; i < ncircles; i++)
			{
				//side
				Vector3 pa = pos;
				Vector3 pb = circles[i].P;

				Vector3 orig = new Vector3(0, 0, 0);
				circles[i].Dp = pb - pa;
				circles[i].Dp.Normalize();
				Vector3 dv = circles[i].DVel - dvel;

				float a = Triangle3.Area2D(orig, circles[i].Dp, dv);
				if (a < 0.01f)
				{
					circles[i].Np.X = -circles[i].Dp.Z;
					circles[i].Np.Z = circles[i].Dp.X;
				}
				else
				{
					circles[i].Np.X = circles[i].Dp.Z;
					circles[i].Np.Z = -circles[i].Dp.X;
				}
			}

			for (int i = 0; i < nsegments; i++)
			{
				//precalculate if the agent is close to the segment
				float r = 0.01f;
				float t;
				segments[i].Touch = MathHelper.Distance.PointToSegment2DSquared(ref pos, ref segments[i].P, 
					ref segments[i].Q, out t) < (r * r);
			}
		}

		public float ProcessSample(Vector3 vcand, float cs, Vector3 pos, float rad, Vector3 vel, Vector3 dvel)
		{
			//find min time of impact and exit amongst all obstacles
			float tmin = parameters.HorizTime;
			float side = 0;
			int nside = 0;

			for (int i = 0; i < ncircles; i++)
			{
				ObstacleCircle cir = circles[i];

				//RVO
				Vector3 vab = vcand * 2;
				vab = vab - vel;
				vab = vab - cir.Vel;

				//side
				side += MathHelper.Clamp(Math.Min(Vector3Extensions.Dot2D(ref cir.Dp, ref vab) * 0.5f + 0.5f, 
					Vector3Extensions.Dot2D(ref cir.Np, ref vab) * 2.0f), 0.0f, 1.0f);
				nside++;

				float htmin = 0, htmax = 0;
				if (!SweepCircleCircle(pos, rad, vab, cir.P, cir.Rad, ref htmin, ref htmax))
					continue;

				//handle overlapping obstacles
				if (htmin < 0.0f && htmax > 0.0f)
				{
					//avoid more when overlapped
					htmin = -htmin * 0.5f;
				}

				if (htmin >= 0.0f)
				{
					//the closest obstacle is sometime ahead of us, keep track of nearest obstacle
					if (htmin < tmin)
						tmin = htmin;
				}
			}

			for (int i = 0; i < nsegments; i++)
			{
				ObstacleSegment seg = segments[i];
				float htmin = 0;

				if (seg.Touch)
				{
					//special case when the agent is very close to the segment
					Vector3 sdir = seg.Q - seg.P;
					Vector3 snorm = new Vector3(0, 0, 0);
					snorm.X = -sdir.Z;
					snorm.Z = sdir.X;

					//if the velocity is pointing towards the segment, no collision
					if (Vector3Extensions.Dot2D(ref snorm, ref vcand) < 0.0f)
						continue;

					//else immediate collision
					htmin = 0.0f;
				}
				else
				{
					if (!IntersectRaySegment(pos, vcand, seg.P, seg.Q, ref htmin))
						continue;
				}

				//avoid less when facing walls
				htmin *= 2.0f;

				//the closest obstacle is somewhere ahead of us, keep track of the nearest obstacle
				if (htmin < tmin)
					tmin = htmin;
			}

			//normalize side bias
			if (nside != 0)
				side /= nside;

			float vpen = parameters.WeightDesVel * (Vector3Extensions.Distance2D(vcand, dvel) * invVmax);
			float vcpen = parameters.WeightCurVel * (Vector3Extensions.Distance2D(vcand, vel) * invVmax);
			float spen = parameters.WeightSide * side;
			float tpen = parameters.WeightToi * (1.0f / (0.1f + tmin * invHorizTime));

			float penalty = vpen + vcpen + spen + tpen;

			return penalty;
		}

		public bool SweepCircleCircle(Vector3 c0, float r0, Vector3 v, Vector3 c1, float r1, ref float tmin, ref float tmax)
		{
			const float EPS = 0.0001f;
			Vector3 s = c1 - c0;
			float r = r0 + r1;
			float c = Vector3Extensions.Dot2D(ref s, ref s) - r * r;
			float a = Vector3Extensions.Dot2D(ref v, ref v);
			if (a < EPS)
				return false; //not moving

			//overlap, calculate time to exit
			float b = Vector3Extensions.Dot2D(ref v, ref s);
			float d = b * b - a * c;
			if (d < 0.0f)
				return false; //no intersection
			a = 1.0f / a;
			float rd = (float)Math.Sqrt(d);
			tmin = (b - rd) * a;
			tmax = (b + rd) * a;
			return true;
		}

		/// <summary>
		/// Determine whether the ray intersects the segment
		/// </summary>
		/// <param name="ap">A point</param>
		/// <param name="u">A vector</param>
		/// <param name="bp">Segment B endpoint</param>
		/// <param name="bq">Another one of segment B's endpoints</param>
		/// <param name="t">The parameter t</param>
		/// <returns>True if intersect, false if not</returns>
		public bool IntersectRaySegment(Vector3 ap, Vector3 u, Vector3 bp, Vector3 bq, ref float t)
		{
			Vector3 v = bq - bp;
			Vector3 w = ap - bp;
			float d;
			Vector3Extensions.PerpDotXZ(ref u, ref v, out d);
			d *= -1;
			if (Math.Abs(d) < 1e-6f)
				return false;

			d = 1.0f / d;
			Vector3Extensions.PerpDotXZ(ref v, ref w, out t);
			t *= -d;
			if (t < 0 || t > 1)
				return false;

			float s;
			Vector3Extensions.PerpDotXZ(ref u, ref w, out s);
			s *= -d;
			if (s < 0 || s > 1)
				return false;

			return true;
		}

		public int SampleVelocityGrid(Vector3 pos, float rad, float vmax, Vector3 vel, Vector3 dvel, 
			ref Vector3 nvel, ObstacleAvoidanceParams parameters)
		{
			Prepare(pos, dvel);
			this.parameters = parameters;
			this.invHorizTime = 1.0f / this.parameters.HorizTime;
			this.vmax = vmax;
			this.invVmax = 1.0f / vmax;

			nvel = new Vector3(0, 0, 0);

			float cvx = dvel.X * this.parameters.VelBias;
			float cvz = dvel.Z * this.parameters.VelBias;
			float cs = vmax * 2 * (1 - this.parameters.VelBias) / (float)(this.parameters.GridSize - 1);
			float half = (this.parameters.GridSize - 1) * cs * 0.5f;

			float minPenalty = float.MaxValue;
			int ns = 0;

			for (int y = 0; y < this.parameters.GridSize; y++)
			{
				for (int x = 0; x < this.parameters.GridSize; x++)
				{
					Vector3 vcand = new Vector3(0, 0, 0);
					vcand.X = cvx + x * cs - half;
					vcand.Y = 0;
					vcand.Z = cvz + y * cs - half;

					if (vcand.X * vcand.X + vcand.Z * vcand.Z > (vmax + cs / 2) * (vmax + cs / 2))
						continue;

					float penalty = ProcessSample(vcand, cs, pos, rad, vel, dvel);
					ns++;
					if (penalty < minPenalty)
					{
						minPenalty = penalty;
						nvel = vcand;
					}
				}
			}

			return ns;
		}

		public int SampleVelocityAdaptive(Vector3 pos, float rad, float vmax, Vector3 vel, 
			Vector3 dvel, ref Vector3 nvel, ObstacleAvoidanceParams parameters)
		{
			Prepare(pos, dvel);

			this.parameters = parameters;
			this.invHorizTime = 1.0f / parameters.HorizTime;
			this.vmax = vmax;
			this.invVmax = 1.0f / vmax;

			nvel = new Vector3(0, 0, 0);

			//build sampling pattern aligned to desired velocity
			float[] pat = new float[(MAX_PATTERN_DIVS * MAX_PATTERN_RINGS + 1) * 2];
			int npat = 0;

			int ndivs = parameters.AdaptiveDivs;
			int nrings = parameters.AdaptiveRings;
			int depth = parameters.AdaptiveDepth;

			int nd = MathHelper.Clamp(ndivs, 1, MAX_PATTERN_DIVS);
			int nr = MathHelper.Clamp(nrings, 1, MAX_PATTERN_RINGS);
			float da = (1.0f / nd) * (float)Math.PI * 2;
			float dang = (float)Math.Atan2(dvel.Z, dvel.X);

			//always add sample at zero
			pat[npat * 2 + 0] = 0;
			pat[npat * 2 + 1] = 0;
			npat++;

			for (int j = 0; j < nr; j++)
			{
				float r = (float)(nr - j) / (float)nr;
				float a = dang + (j & 1) * 0.5f * da;
				for (int i = 0; i < nd; i++)
				{
					pat[npat * 2 + 0] = (float)Math.Cos(a) * r;
					pat[npat * 2 + 1] = (float)Math.Sin(a) * r;
					npat++;
					a += da;
				}
			}

			//start sampling
			float cr = vmax * (1.0f - parameters.VelBias);
			Vector3 res = new Vector3(dvel.X * parameters.VelBias, 0, dvel.Z * parameters.VelBias);
			int ns = 0;

			for (int k = 0; k < depth; k++)
			{
				float minPenalty = float.MaxValue;
				Vector3 bvel = new Vector3(0, 0, 0);

				for (int i = 0; i < npat; i++)
				{
					Vector3 vcand = new Vector3();
					vcand.X = res.X + pat[i * 2 + 0] * cr;
					vcand.Y = 0;
					vcand.Z = res.Z + pat[i * 2 + 1] * cr;

					if (vcand.X * vcand.X + vcand.Z * vcand.Z > (vmax + 0.001f) * (vmax + 0.001f))
						continue;

					float penalty = ProcessSample(vcand, cr / 10, pos, rad, vel, dvel);
					ns++;
					if (penalty < minPenalty)
					{
						minPenalty = penalty;
						bvel = vcand;
					}
				}

				res = bvel;

				cr *= 0.5f;
			}

			nvel = res;

			return ns;

		}

		private struct ObstacleCircle
		{
			/// <summary>
			/// The position of the obstacle
			/// </summary>
			public Vector3 P;

			/// <summary>
			/// The velocity of the obstacle
			/// </summary>
			public Vector3 Vel;

			/// <summary>
			/// The desired velocity of the obstacle
			/// </summary>
			public Vector3 DVel;

			/// <summary>
			/// The radius of the obstacle
			/// </summary>
			public float Rad;

			/// <summary>
			/// Used for side selection during sampling
			/// </summary>
			public Vector3 Dp, Np;
		}

		private struct ObstacleSegment
		{
			/// <summary>
			/// Endpoints of the obstacle segment
			/// </summary>
			public Vector3 P, Q;

			public bool Touch;
		}

		public struct ObstacleAvoidanceParams
		{
			public float VelBias;
			public float WeightDesVel;
			public float WeightCurVel;
			public float WeightSide;
			public float WeightToi;
			public float HorizTime;
			public int GridSize;
			public int AdaptiveDivs;
			public int AdaptiveRings;
			public int AdaptiveDepth;
		}
	}
}