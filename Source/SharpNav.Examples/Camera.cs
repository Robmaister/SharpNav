// Copyright (c) 2013, 2015 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
// Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE

using System;

using OpenTK;
using OpenTK.Graphics.OpenGL;

using SharpNav.Geometry;

#if STANDALONE
using Vector3 = OpenTK.Vector3;
using Vector2 = OpenTK.Vector2;
using SVector3 = SharpNav.Geometry.Vector3;
using SVector2 = SharpNav.Geometry.Vector2;
#elif OPENTK
//using Vector2 = OpenTK.Vector2;
//using Vector3 = OpenTK.Vector3;
#endif

//Doesn't compile if in an unsupported configuration
#if STANDALONE || OPENTK

namespace SharpNav.Examples
{
	/// <summary>
	/// A camera that provides the necessary matrices to view a world in 3d.
	/// </summary>
	public class Camera
	{
		#region Fields

		//position vector
		private Vector3 position;

		//unit vectors for camera angles
		private Vector3 upAxis;
		private Vector3 rightAxis;
		private Vector3 lookAxis;

		//camera angles
		private float heading;
		private float pitch;

		private Matrix4 view;

		#endregion

		#region Constructors

		/// <summary>
		/// Initializes a new instance of the Camera class.
		/// </summary>
		public Camera()
		{
			//initalize position
			position = new Vector3();

			//initial axis locations
			upAxis = Vector3.UnitY;
			rightAxis = Vector3.UnitX;
			lookAxis = Vector3.UnitZ;

			//initialize view matrix
			view = Matrix4.Identity;
			RebuildView();
		}

		/// <summary>
		/// Initializes a new instance of the Camera class. Allows for an initial poisition.
		/// </summary>
		/// <param name="position">The camera's position.</param>
		public Camera(Vector3 position)
			: this()
		{
			Position = position;
		}

		#endregion

		#region Properties

		/// <summary>
		/// Gets the view matrix based off the camera's angle and position.
		/// </summary>
		public Matrix4 ViewMatrix { get { return view; } }

		/// <summary>
		/// Gets or sets the position of the camera.
		/// </summary>
		public Vector3 Position { get { return position; } set { position = value; UpdateViewPosition(); } }

		/// <summary>
		/// Gets the camera's up axis.
		/// </summary>
		public Vector3 UpAxis { get { return upAxis; } }

		/// <summary>
		/// Gets the camera's right axis.
		/// </summary>
		public Vector3 RightAxis { get { return rightAxis; } }

		/// <summary>
		/// Gets or sets the camera's look (forward) axis.
		/// </summary>
		public Vector3 LookAxis
		{
			get
			{
				return lookAxis;
			}
			set
			{
				this.lookAxis = value;
				rightAxis = Vector3.Cross(lookAxis, Vector3.UnitY);
				upAxis = Vector3.Cross(lookAxis, rightAxis);
				RebuildView();
			}
		}

		/// <summary>
		/// Gets the camera's heading.
		/// </summary>
		public float Heading { get { return heading; } }

		/// <summary>
		/// Gets the camera's pitch.
		/// </summary>
		public float Pitch { get { return pitch; } }

		#endregion

		#region Public Methods

		/// <summary>
		/// Move the camera by it's look (forward) axis.
		/// </summary>
		/// <param name="value">The distance to move, 1 will move the camera by it's unit look vector.</param>
		public void Move(float value)
		{
			Position += lookAxis * value;
		}

		/// <summary>
		/// Move the camera by it's right axis.
		/// </summary>
		/// <param name="value">The distance to strafe, 1 will strafe the camera by it's unit right vector.</param>
		public void Strafe(float value)
		{
			Position += rightAxis * value;
		}

		/// <summary>
		/// Move the camera by it's up axis.
		/// </summary>
		/// <param name="value">The distance to elevate, 1 will elevate the camera by it's unit up vector.</param>
		public void Elevate(float value)
		{
			Position += upAxis * value;
		}

		/// <summary>
		/// Change the heading of the camera by an angle in degrees.
		/// </summary>
		/// <param name="angle">The angle to add to the current heading.</param>
		public void RotateHeading(float angle)
		{
			RotateHeadingTo(heading + angle);
		}

		/// <summary>
		/// Set the heading of the camera as an angle in degrees.
		/// </summary>
		/// <param name="angle">The camera's new heading (clamped -90 to 90).</param>
		public void RotateHeadingTo(float angle)
		{
			heading = angle;

			//heading constraints of (-90 <= heading <= 90)
			if (heading >= 90)
				heading = 90;

			if (heading <= -90)
				heading = -90;

			//the radius of the circle that the up axis will have to lie on - think of a unit sphere as differently sized circles stacked vertically.
			float headingRad = heading * (MathHelper.Pi / 180f);
			float radius = (float)Math.Sin(headingRad);

			//The cos of the heading will get us the height of the up axis.
			upAxis.Y = (float)Math.Cos(headingRad);

			//X and Z: rotate right axis 90 degrees for the direction of upAxis on the X/Z plane, 
			//use cos/sin to get the X and Z coordinates in the unit circle in the middle of the sphere (think of stacked circles again), 
			//scale by our new radius to get X and Z coordinates on the circle that upAxis.Y is on (multiply by radius)
			float headingDirection = (pitch + 90) * (MathHelper.Pi / 180f);
			upAxis.X = radius * (float)Math.Cos(headingDirection);
			upAxis.Z = radius * (float)Math.Sin(headingDirection);

			//update the lookAxis
			lookAxis = Vector3.Normalize(Vector3.Cross(rightAxis, upAxis));

			//update the view matrix so our changes are reflected in the world
			RebuildView();
		}

		/// <summary>
		/// Change the pitch of the camera by an angle in degrees.
		/// </summary>
		/// <param name="angle">The angle to add to the current pitch.</param>
		public void RotatePitch(float angle)
		{
			RotatePitchTo(pitch + angle);
		}
		
		/// <summary>
		/// Set the pitch of the camera to an angle in degreees.
		/// </summary>
		/// <param name="angle">The camera's new pitch (clamped 0 to 360).</param>
		public void RotatePitchTo(float angle)
		{
			pitch = angle;

			//pitch constraints of (0 < pitch <= 360)
			if (pitch >= 360)
				pitch %= 360;

			if (pitch < 0)
				pitch = 360 + (pitch % -360);

			//Simple 2d trig. Excluding roll, the right axis will always be parallel to the X/Z plane.
			//With this assumption, the right axis will always lie on the unit circle.
			float pitchRad = pitch * (MathHelper.Pi / 180f);
			rightAxis.X = (float)Math.Cos(pitchRad);
			rightAxis.Z = (float)Math.Sin(pitchRad);

			//Use this to recalculate the proper position of the up axis, without it we get shrinking/scaling issues because the axes are not coordinated
			//not necessary to rotate right axis in RotUpAxis because the right vector is always parallel to the X/Z plane
			RotateHeading(0);

			//update the lookAxis
			lookAxis = Vector3.Normalize(Vector3.Cross(rightAxis, upAxis));

			//update the view matrix so our changes are reflected in the world
			RebuildView();
		}

		/// <summary>
		/// Make this camera look at a specific point in the world.
		/// </summary>
		/// <param name="position">The point to look at.</param>
		public void LookAt(Vector3 position)
		{
			//store the line between the camera and the other IPoint3D, but only on the X/Z plane, then normalize it to get a unit vector.
			Vector2 pitchVec = Vector2.Normalize(new Vector2(position.X - Position.X, position.Z - Position.Z));

			//convert vector to angle and roatate the right axis to that angle
			RotatePitchTo((float)(Math.Atan2(pitchVec.Y, pitchVec.X) * (180f / MathHelper.Pi) - 90));

			//update lookAxis, the normalized vector of the line between this camera and the IPoint3D.
			lookAxis = Vector3.Normalize(Vector3.Subtract(position, Position));

			//update the heading variable and upAxis
			RotateHeadingTo((float)Math.Asin(-lookAxis.Y) * (180f / MathHelper.Pi));

			//update the view matrix so our changes are reflected in the world
			RebuildView();
		}

		/// <summary>
		/// Loads the view matrix to the fixed-function matrix stack.
		/// </summary>
		public void LoadView()
		{
			GL.LoadMatrix(ref view);
		}

		#endregion

		#region Private Methods

		/// <summary>
		/// Updates the entire view matrix.
		/// </summary>
		private void RebuildView()
		{
			//update the rotation part of the view matrix
			view.Row0.X = rightAxis.X;
			view.Row0.Y = upAxis.X;
			view.Row0.Z = lookAxis.X;

			view.Row1.X = rightAxis.Y;
			view.Row1.Y = upAxis.Y;
			view.Row1.Z = lookAxis.Y;

			view.Row2.X = rightAxis.Z;
			view.Row2.Y = upAxis.Z;
			view.Row2.Z = lookAxis.Z;

			//update the position part of the view matrix
			UpdateViewPosition();
		}

		/// <summary>
		/// Only updates the view matrix's position variables.
		/// </summary>
		private void UpdateViewPosition()
		{
			view.Row3.X = -Vector3.Dot(rightAxis, Position);
			view.Row3.Y = -Vector3.Dot(upAxis, Position);
			view.Row3.Z = -Vector3.Dot(lookAxis, Position);
		}

		#endregion
	}
}

#endif
