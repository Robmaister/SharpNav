// Copyright (c) 2013-2015 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
// Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE

using System;
using System.Collections.Generic;

using SharpNav;

#if MONOGAME
using Vector3 = Microsoft.Xna.Framework.Vector3;
#elif OPENTK
using Vector3 = OpenTK.Vector3;
#elif SHARPDX
using Vector3 = SharpDX.Vector3;
#endif

namespace SharpNav.Geometry
{
	/// <summary>
	/// A 3d axis-aligned bounding box.
	/// </summary>
	[Serializable]
	public struct BBox3 : IEquatable<BBox3>
	{
		/// <summary>
		/// The minimum bounds.
		/// </summary>
		public Vector3 Min;

		/// <summary>
		/// The maximum bounds.
		/// </summary>
		public Vector3 Max;

		/// <summary>
		/// Initializes a new instance of the <see cref="BBox3"/> struct.
		/// </summary>
		/// <param name="min">The minimum bounds.</param>
		/// <param name="max">The maximum bounds.</param>
		public BBox3(Vector3 min, Vector3 max)
		{
			Min = min;
			Max = max;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="BBox3"/> struct.
		/// </summary>
		/// <param name="minX">The minimum on the X axis.</param>
		/// <param name="minY">The minimum on the Y axis.</param>
		/// <param name="minZ">The minimum on the Z axis.</param>
		/// <param name="maxX">The maximum on the X axis.</param>
		/// <param name="maxY">The maximum on the Y axis.</param>
		/// <param name="maxZ">The maximum on the Z axis.</param>
		public BBox3(float minX, float minY, float minZ, float maxX, float maxY, float maxZ)
		{
			Min.X = minX;
			Min.Y = minY;
			Min.Z = minZ;

			Max.X = maxX;
			Max.Y = maxY;
			Max.Z = maxZ;
		}

		/// <summary>
		/// Gets the center of the box.
		/// </summary>
		public Vector3 Center
		{
			get
			{
				return (Min + Max) / 2;
			}
		}

		/// <summary>
		/// Gets the size of the box.
		/// </summary>
		public Vector3 Size
		{
			get
			{
				return Max - Min;
			}
		}

		/// <summary>
		/// Checks whether two boudning boxes are intersecting.
		/// </summary>
		/// <param name="a">The first bounding box.</param>
		/// <param name="b">The second bounding box.</param>
		/// <returns>A value indicating whether the two bounding boxes are overlapping.</returns>
		public static bool Overlapping(ref BBox3 a, ref BBox3 b)
		{
			return !(a.Min.X > b.Max.X || a.Max.X < b.Min.X
				|| a.Min.Y > b.Max.Y || a.Max.Y < b.Min.Y
				|| a.Min.Z > b.Max.Z || a.Max.Z < b.Min.Z);
		}

		/// <summary>
		/// Determines whether a bounding box is valid. Validity consists of having no NaN values and the Min vector
		/// to be less than the Max vector on all axes.
		/// </summary>
		/// <param name="b">The bounding box to validate.</param>
		/// <returns>A value indicating whether the bounding box is valid.</returns>
		public static bool IsValid(ref BBox3 b)
		{
			//None of the values can be NaN.
			if (float.IsNaN(b.Min.X) ||
				float.IsNaN(b.Min.Y) ||
				float.IsNaN(b.Min.Z) ||
				float.IsNaN(b.Max.X) ||
				float.IsNaN(b.Max.Y) ||
				float.IsNaN(b.Max.Z))
				return false;

			//The min must be less than the max on all axes.
			if (b.Min.X > b.Max.X ||
				b.Min.Y > b.Max.Y ||
				b.Min.Z > b.Max.Z)
				return false;

			return true;
		}

		/// <summary>
		/// Compares two bounding boxes for equality.
		/// </summary>
		/// <param name="left">The first bounding box.</param>
		/// <param name="right">The second bounding box.</param>
		/// <returns>A value indicating the equality of the two boxes.</returns>
		public static bool operator ==(BBox3 left, BBox3 right)
		{
			return left.Equals(right);
		}

		/// <summary>
		/// Compares two bounding boxes for inequality.
		/// </summary>
		/// <param name="left">The first bounding box.</param>
		/// <param name="right">The second bounding box.</param>
		/// <returns>A value indicating the inequality of the two boxes.</returns>
		public static bool operator !=(BBox3 left, BBox3 right)
		{
			return !(left == right);
		}

		/// <summary>
		/// Compares this instance with another bounding box for equality.
		/// </summary>
		/// <param name="other">Another bounding box.</param>
		/// <returns>A value indicating the equality of the two boxes.</returns>
		public bool Equals(BBox3 other)
		{
			return Min == other.Min && Max == other.Max;
		}

		/// <summary>
		/// Compares this instance with another object for equality.
		/// </summary>
		/// <param name="obj">An object.</param>
		/// <returns>A value indicating equality between the two objects.</returns>
		public override bool Equals(object obj)
		{
			if (obj is BBox3)
				return this.Equals((BBox3)obj);
			else
				return false;
		}

		/// <summary>
		/// Generates a unique hashcode for this bouding box instance.
		/// </summary>
		/// <returns>A hash code.</returns>
		public override int GetHashCode()
		{
			//TODO write a better hash code
			return Min.GetHashCode() ^ Max.GetHashCode();
		}

		/// <summary>
		/// Returns a string containing the important information for this instance of <see cref="BBox3"/>.
		/// </summary>
		/// <returns>A human-readable string representation of this instance.</returns>
		public override string ToString()
		{
			return "{ Min: " + Min.ToString() + ", Max: " + Max.ToString() + " }";
		}
	}
}
