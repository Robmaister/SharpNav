// Copyright (c) 2014-2015 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
// Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE

using System;

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
	/// A crowd agent is a unit that moves across the navigation mesh
	/// </summary>
	public class Agent : IEquatable<Agent>
	{
		#region Fields

		/// <summary>
		/// The maximum number of corners a crowd agent will look ahead in the path
		/// </summary>
		private const int AgentMaxCorners = 4;
		public const int AgentMaxNeighbors = 6;

		private bool active;
		private AgentState state;
		private bool partial;
		private PathCorridor corridor;
		private LocalBoundary boundary;
		public float topologyOptTime;
		private CrowdNeighbor[] neighbors;	//size = CROWDAGENT_MAX_NEIGHBORS
		private int numNeis;
		public float DesiredSpeed;

		private Vector3 currentPos;
		public Vector3 Disp;
		public Vector3 DesiredVel;
		public Vector3 NVel;
		public Vector3 Vel;

		public AgentParams Parameters;

		public StraightPath Corners;

		private TargetState targetState;
		public NavPolyId TargetRef;
		private Vector3 targetPos;
		public int TargetPathQueryIndex;
		public bool TargetReplan;
		public float TargetReplanTime;

		#endregion

		#region Constructors

		public Agent()
		{
			active = false;
			corridor = new PathCorridor();
			boundary = new LocalBoundary();
			neighbors = new CrowdNeighbor[AgentMaxNeighbors];
			Corners = new StraightPath();
		}

		#endregion

		#region Properties

		public bool IsActive
		{
			get
			{
				return active;
			}

			set
			{
				active = value;
			}
		}

		public bool IsPartial
		{
			get
			{
				return partial;
			}

			set
			{
				partial = value;
			}
		}

		public AgentState State
		{
			get
			{
				return state;
			}

			set
			{
				state = value;
			}
		}

		public Vector3 Position
		{
			get
			{
				return currentPos;
			}

			set
			{
				currentPos = value;
			}
		}

		public LocalBoundary Boundary
		{
			get
			{
				return boundary;
			}
		}

		public PathCorridor Corridor
		{
			get
			{
				return corridor;
			}
		}

		public CrowdNeighbor[] Neighbors
		{
			get
			{
				return neighbors;
			}
		}

		public int NeighborCount
		{
			get
			{
				return numNeis;
			}

			set
			{
				numNeis = value;
			}
		}

		public TargetState TargetState
		{
			get
			{
				return targetState;
			}

			set
			{
				targetState = value;
			}
		}

		public Vector3 TargetPosition
		{
			get
			{
				return targetPos;
			}

			set
			{
				targetPos = value;
			}
		}

		#endregion

		#region Methods

		/// <summary>
		/// Update the position after a certain time 'dt'
		/// </summary>
		/// <param name="dt">Time that passed</param>
		public void Integrate(float dt)
		{
			//fake dyanmic constraint
			float maxDelta = Parameters.MaxAcceleration * dt;
			Vector3 dv = NVel - Vel;
			float ds = dv.Length();
			if (ds > maxDelta)
				dv = dv * (maxDelta / ds);
			Vel = Vel + dv;

			//integrate
			if (Vel.Length() > 0.0001f)
				currentPos = currentPos + Vel * dt;
			else
				Vel = new Vector3(0, 0, 0);
		}

		public void Reset(NavPolyId reference, Vector3 nearest)
		{
			this.corridor.Reset(reference, nearest);
			this.boundary.Reset();
			this.partial = false;

			this.topologyOptTime = 0;
			this.TargetReplanTime = 0;
			this.numNeis = 0;

			this.DesiredVel = new Vector3(0.0f, 0.0f, 0.0f);
			this.NVel = new Vector3(0.0f, 0.0f, 0.0f);
			this.Vel = new Vector3(0.0f, 0.0f, 0.0f);
			this.currentPos = nearest;

			this.DesiredSpeed = 0;

			if (reference != NavPolyId.Null)
				this.state = AgentState.Walking;
			else
				this.state = AgentState.Invalid;

			this.TargetState = TargetState.None;
		}

		/// <summary>
		/// Change the move target
		/// </summary>
		/// <param name="reference">The polygon reference</param>
		/// <param name="pos">The target's coordinates</param>
		public void RequestMoveTargetReplan(NavPolyId reference, Vector3 pos)
		{
			//initialize request
			this.TargetRef = reference;
			this.targetPos = pos;
			this.TargetPathQueryIndex = PathQueue.Invalid;
			this.TargetReplan = true;
			if (this.TargetRef != NavPolyId.Null)
				this.TargetState = TargetState.Requesting;
			else
				this.TargetState = TargetState.Failed;
		}

		/// <summary>
		/// Request a new move target
		/// </summary>
		/// <param name="reference">The polygon reference</param>
		/// <param name="pos">The target's coordinates</param>
		/// <returns>True if request met, false if not</returns>
		public bool RequestMoveTarget(NavPolyId reference, Vector3 pos)
		{
			if (reference == NavPolyId.Null)
				return false;

			//initialize request
			this.TargetRef = reference;
			this.targetPos = pos;
			this.TargetPathQueryIndex = PathQueue.Invalid;
			this.TargetReplan = false;
			if (this.TargetRef != NavPolyId.Null)
				this.targetState = TargetState.Requesting;
			else
				this.targetState = TargetState.Failed;

			return true;
		}

		/// <summary>
		/// Request a new move velocity
		/// </summary>
		/// <param name="vel">The agent's velocity</param>
		public void RequestMoveVelocity(Vector3 vel)
		{
			//initialize request
			this.TargetRef = NavPolyId.Null;
			this.targetPos = vel;
			this.TargetPathQueryIndex = PathQueue.Invalid;
			this.TargetReplan = false;
			this.targetState = TargetState.Velocity;
		}

		/// <summary>
		/// Reset the move target of an agent
		/// </summary>
		public void ResetMoveTarget()
		{
			//initialize request
			this.TargetRef = NavPolyId.Null;
			this.targetPos = new Vector3(0.0f, 0.0f, 0.0f);
			this.TargetPathQueryIndex = PathQueue.Invalid;
			this.TargetReplan = false;
			this.targetState = TargetState.None;
		}

		/// <summary>
		/// Modify the agent parameters
		/// </summary>
		/// <param name="parameters">The new parameters</param>
		public void UpdateAgentParameters(AgentParams parameters)
		{
			this.Parameters = parameters;
		}

		public static bool operator ==(Agent left, Agent right)
		{
			return left.Equals(right);
		}

		public static bool operator !=(Agent left, Agent right)
		{
			return !(left == right);
		}

		public bool Equals(Agent other)
		{
			//TODO find a way to actually compare for equality.
			return object.ReferenceEquals(this, other);
		}

		public override bool Equals(object obj)
		{
			var other = obj as Agent;
			if (other != null)
				return this.Equals(other);

			return false;
		}

		public override string ToString()
		{
			//TODO write an actual ToString.
			return base.ToString();
		}

		#endregion
	}
}
