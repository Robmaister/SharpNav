// Copyright (c) 2014-2015 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
// Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE

using System;

namespace SharpNav.Geometry
{
	/// <summary>
	/// A 2d bounding box represeted by integers.
	/// </summary>
	[Serializable]
	public struct BBox2i : IEquatable<BBox2i>
	{
		/// <summary>
		/// The minimum of the bounding box.
		/// </summary>
		public Vector2i Min;

		/// <summary>
		/// The maximum of the bounding box.
		/// </summary>
		public Vector2i Max;

		/// <summary>
		/// Initializes a new instance of the <see cref="BBox2i"/> struct.
		/// </summary>
		/// <param name="min">A minimum bound.</param>
		/// <param name="max">A maximum bound.</param>
		public BBox2i(Vector2i min, Vector2i max)
		{
			Min = min;
			Max = max;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="BBox2i"/> struct.
		/// </summary>
		/// <param name="minX">The minimum X bound.</param>
		/// <param name="minY">The minimum Y bound.</param>
		/// <param name="maxX">The maximum X bound.</param>
		/// <param name="maxY">The maximum Y bound.</param>
		public BBox2i(int minX, int minY, int maxX, int maxY)
		{
			Min.X = minX;
			Min.Y = minY;
			Max.X = maxX;
			Max.Y = maxY;
		}

		/// <summary>
		/// Compares two instances of <see cref="BBox2i"/> for equality.
		/// </summary>
		/// <param name="left">An instance of <see cref="BBox2i"/>.</param>
		/// <param name="right">Another instance of <see cref="BBox2i"/>.</param>
		/// <returns>A value indicating whether the two instances are equal.</returns>
		public static bool operator ==(BBox2i left, BBox2i right)
		{
			return left.Equals(right);
		}

		/// <summary>
		/// Compares two instances of <see cref="BBox2i"/> for inequality.
		/// </summary>
		/// <param name="left">An instance of <see cref="BBox2i"/>.</param>
		/// <param name="right">Another instance of <see cref="BBox2i"/>.</param>
		/// <returns>A value indicating whether the two instances are unequal.</returns>
		public static bool operator !=(BBox2i left, BBox2i right)
		{
			return !(left == right);
		}

		/// <summary>
		/// Turns the instance into a human-readable string.
		/// </summary>
		/// <returns>A string representing the instance.</returns>
		public override string ToString()
		{
			return "{ Min: " + Min.ToString() + ", Max: " + Max.ToString() + " }";
		}

		/// <summary>
		/// Gets a unique hash code for this instance.
		/// </summary>
		/// <returns>A hash code.</returns>
		public override int GetHashCode()
		{
			//TODO write a good hash code.
			return Min.GetHashCode() ^ Max.GetHashCode();
		}

		/// <summary>
		/// Checks for equality between this instance and a specified object.
		/// </summary>
		/// <param name="obj">An object.</param>
		/// <returns>A value indicating whether this instance and the object are equal.</returns>
		public override bool Equals(object obj)
		{
			BBox2i? objV = obj as BBox2i?;
			if (objV != null)
				return this.Equals(objV);

			return false;
		}

		/// <summary>
		/// Checks for equality between this instance and a specified instance of <see cref="BBox2i"/>.
		/// </summary>
		/// <param name="other">An instance of <see cref="BBox2i"/>.</param>
		/// <returns>A value indicating whether this instance and the other instance are equal.</returns>
		public bool Equals(BBox2i other)
		{
			return Min == other.Min && Max == other.Max;
		}
	}
}
