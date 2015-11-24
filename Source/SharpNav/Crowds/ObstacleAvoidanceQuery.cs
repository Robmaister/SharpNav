// Copyright (c) 2014-2015 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
// Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE

using System;
using System.Collections.Generic;

using SharpNav.Collections.Generic;
using SharpNav.Geometry;

#if MONOGAME
using Vector3 = Microsoft.Xna.Framework.Vector3;
#elif OPENTK
using Vector3 = OpenTK.Vector3;
#elif SHARPDX
using Vector3 = SharpDX.Vector3;
#endif

namespace SharpNav.Crowds
{
	public class ObstacleAvoidanceQuery
	{
		#region Fields

		private const int MaxPatternDivs = 32;
		private const int MaxPatternRings = 4;

		private ObstacleAvoidanceParams parameters;
		private float invHorizTime;
		private float vmax;
		private float invVmax;

		private int maxCircles;
		private ObstacleCircle[] circles;
		private int numCircles;

		private int maxSegments;
		private ObstacleSegment[] segments;
		private int numSegments;

		#endregion

		#region Constructors

		/// <summary>
		/// Initializes a new instance of the <see cref="ObstacleAvoidanceQuery" /> class.
		/// </summary>
		/// <param name="maxCircles">The maximum number of circles</param>
		/// <param name="maxSegments">The maximum number of segments</param>
		public ObstacleAvoidanceQuery(int maxCircles, int maxSegments)
		{
			this.maxCircles = maxCircles;
			this.numCircles = 0;
			this.circles = new ObstacleCircle[this.maxCircles];

			this.maxSegments = maxSegments;
			this.numSegments = 0;
			this.segments = new ObstacleSegment[this.maxSegments];
		}

		#endregion

		#region Methods

		/// <summary>
		/// Resets the ObstacleAvoidanceQuery's internal data
		/// </summary>
		public void Reset()
		{
			numCircles = 0;
			numSegments = 0;
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
			if (numCircles >= maxCircles)
				return;

			circles[numCircles].Position = pos;
			circles[numCircles].Radius = rad;
			circles[numCircles].Vel = vel;
			circles[numCircles].DesiredVel = dvel;
			numCircles++;
		}

		/// <summary>
		/// Add a segment to the array
		/// </summary>
		/// <param name="p">One endpoint</param>
		/// <param name="q">The other endpoint</param>
		public void AddSegment(Vector3 p, Vector3 q)
		{
			if (numSegments > maxSegments)
				return;

			segments[numSegments].P = p;
			segments[numSegments].Q = q;
			numSegments++;
		}

		/// <summary>
		/// Prepare the obstacles for further calculations
		/// </summary>
		/// <param name="position">Current position</param>
		/// <param name="desiredVel">Desired velocity</param>
		public void Prepare(Vector3 position, Vector3 desiredVel)
		{
			//prepare obstacles
			for (int i = 0; i < numCircles; i++)
			{
				//side
				Vector3 pa = position;
				Vector3 pb = circles[i].Position;

				Vector3 orig = new Vector3(0, 0, 0);
				circles[i].Dp = pb - pa;
				circles[i].Dp.Normalize();
				Vector3 dv = circles[i].DesiredVel - desiredVel;

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

			for (int i = 0; i < numSegments; i++)
			{
				//precalculate if the agent is close to the segment
				float r = 0.01f;
				float t;
				segments[i].Touch = Distance.PointToSegment2DSquared(ref position, ref segments[i].P, ref segments[i].Q, out t) < (r * r);
			}
		}

		public float ProcessSample(Vector3 vcand, float cs, Vector3 position, float radius, Vector3 vel, Vector3 desiredVel)
		{
			//find min time of impact and exit amongst all obstacles
			float tmin = parameters.HorizTime;
			float side = 0;
			int numSide = 0;

			for (int i = 0; i < numCircles; i++)
			{
				ObstacleCircle cir = circles[i];

				//RVO
				Vector3 vab = vcand * 2;
				vab = vab - vel;
				vab = vab - cir.Vel;

				//side
				side += MathHelper.Clamp(Math.Min(Vector3Extensions.Dot2D(ref cir.Dp, ref vab) * 0.5f + 0.5f, Vector3Extensions.Dot2D(ref cir.Np, ref vab) * 2.0f), 0.0f, 1.0f);
				numSide++;

				float htmin = 0, htmax = 0;
				if (!SweepCircleCircle(position, radius, vab, cir.Position, cir.Radius, ref htmin, ref htmax))
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

			for (int i = 0; i < numSegments; i++)
			{
				ObstacleSegment seg = segments[i];

				float htmin;
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
					if (!Intersection.RaySegment(position, vcand, seg.P, seg.Q, out htmin))
						continue;
				}

				//avoid less when facing walls
				htmin *= 2.0f;

				//the closest obstacle is somewhere ahead of us, keep track of the nearest obstacle
				if (htmin < tmin)
					tmin = htmin;
			}

			//normalize side bias
			if (numSide != 0)
				side /= numSide;

			float vpen = parameters.WeightDesVel * (Vector3Extensions.Distance2D(vcand, desiredVel) * invVmax);
			float vcpen = parameters.WeightCurVel * (Vector3Extensions.Distance2D(vcand, vel) * invVmax);
			float spen = parameters.WeightSide * side;
			float tpen = parameters.WeightToi * (1.0f / (0.1f + tmin * invHorizTime));

			float penalty = vpen + vcpen + spen + tpen;

			return penalty;
		}

		public static bool SweepCircleCircle(Vector3 center0, float radius0, Vector3 v, Vector3 center1, float radius1, ref float tmin, ref float tmax)
		{
			const float EPS = 0.0001f;
			Vector3 s = center1 - center0;
			float r = radius0 + radius1;
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

		public int SampleVelocityGrid(Vector3 pos, float rad, float vmax, Vector3 vel, Vector3 desiredVel, ref Vector3 nvel, ObstacleAvoidanceParams parameters)
		{
			Prepare(pos, desiredVel);
			this.parameters = parameters;
			this.invHorizTime = 1.0f / this.parameters.HorizTime;
			this.vmax = vmax;
			this.invVmax = 1.0f / vmax;

			nvel = new Vector3(0, 0, 0);

			float cvx = desiredVel.X * this.parameters.VelBias;
			float cvz = desiredVel.Z * this.parameters.VelBias;
			float cs = vmax * 2 * (1 - this.parameters.VelBias) / (float)(this.parameters.GridSize - 1);
			float half = (this.parameters.GridSize - 1) * cs * 0.5f;

			float minPenalty = float.MaxValue;
			int numSamples = 0;

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

					float penalty = ProcessSample(vcand, cs, pos, rad, vel, desiredVel);
					numSamples++;
					if (penalty < minPenalty)
					{
						minPenalty = penalty;
						nvel = vcand;
					}
				}
			}

			return numSamples;
		}

		public int SampleVelocityAdaptive(Vector3 position, float radius, float vmax, Vector3 vel, Vector3 desiredVel, ref Vector3 nvel, ObstacleAvoidanceParams parameters)
		{
			Prepare(position, desiredVel);

			this.parameters = parameters;
			this.invHorizTime = 1.0f / parameters.HorizTime;
			this.vmax = vmax;
			this.invVmax = 1.0f / vmax;

			nvel = new Vector3(0, 0, 0);

			//build sampling pattern aligned to desired velocity
			float[] pattern = new float[(MaxPatternDivs * MaxPatternRings + 1) * 2];
			int numPatterns = 0;

			int numDivs = parameters.AdaptiveDivs;
			int numRings = parameters.AdaptiveRings;
			int depth = parameters.AdaptiveDepth;

			int newNumDivs = MathHelper.Clamp(numDivs, 1, MaxPatternDivs);
			int newNumRings = MathHelper.Clamp(numRings, 1, MaxPatternRings);
			float da = (1.0f / newNumDivs) * (float)Math.PI * 2;
			float dang = (float)Math.Atan2(desiredVel.Z, desiredVel.X);

			//always add sample at zero
			pattern[numPatterns * 2 + 0] = 0;
			pattern[numPatterns * 2 + 1] = 0;
			numPatterns++;

			for (int j = 0; j < newNumRings; j++)
			{
				float r = (float)(newNumRings - j) / (float)newNumRings;
				float a = dang + (j & 1) * 0.5f * da;
				for (int i = 0; i < newNumDivs; i++)
				{
					pattern[numPatterns * 2 + 0] = (float)Math.Cos(a) * r;
					pattern[numPatterns * 2 + 1] = (float)Math.Sin(a) * r;
					numPatterns++;
					a += da;
				}
			}

			//start sampling
			float cr = vmax * (1.0f - parameters.VelBias);
			Vector3 res = new Vector3(desiredVel.X * parameters.VelBias, 0, desiredVel.Z * parameters.VelBias);
			int ns = 0;

			for (int k = 0; k < depth; k++)
			{
				float minPenalty = float.MaxValue;
				Vector3 bvel = new Vector3(0, 0, 0);

				for (int i = 0; i < numPatterns; i++)
				{
					Vector3 vcand = new Vector3();
					vcand.X = res.X + pattern[i * 2 + 0] * cr;
					vcand.Y = 0;
					vcand.Z = res.Z + pattern[i * 2 + 1] * cr;

					if (vcand.X * vcand.X + vcand.Z * vcand.Z > (vmax + 0.001f) * (vmax + 0.001f))
						continue;

					float penalty = ProcessSample(vcand, cr / 10, position, radius, vel, desiredVel);
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

		#endregion

		private struct ObstacleCircle
		{
			/// <summary>
			/// The position of the obstacle
			/// </summary>
			public Vector3 Position;

			/// <summary>
			/// The velocity of the obstacle
			/// </summary>
			public Vector3 Vel;

			/// <summary>
			/// The desired velocity of the obstacle
			/// </summary>
			public Vector3 DesiredVel;

			/// <summary>
			/// The radius of the obstacle
			/// </summary>
			public float Radius;

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
