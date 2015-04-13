// Copyright (c) 2014-2015 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
// Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE

using System;

namespace SharpNav.Geometry
{
	/// <summary>
	/// A 2d vector represented by integers.
	/// </summary>
	[Serializable]
	public struct Vector2i : IEquatable<Vector2i>
	{
		/// <summary>
		/// A vector where both X and Y are <see cref="int.MinValue"/>.
		/// </summary>
		public static readonly Vector2i Min = new Vector2i(int.MinValue, int.MinValue);

		/// <summary>
		/// A vector where both X and Y are <see cref="int.MaxValue"/>.
		/// </summary>
		public static readonly Vector2i Max = new Vector2i(int.MaxValue, int.MaxValue);

		/// <summary>
		/// A vector where both X and Y are 0.
		/// </summary>
		public static readonly Vector2i Zero = new Vector2i(0, 0);

		/// <summary>
		/// The X coordinate.
		/// </summary>
		public int X;

		/// <summary>
		/// The Y coordinate.
		/// </summary>
		public int Y;

		/// <summary>
		/// Initializes a new instance of the <see cref="Vector2i"/> struct with a specified coordinate.
		/// </summary>
		/// <param name="x">The X coordinate.</param>
		/// <param name="y">The Y coordinate.</param>
		public Vector2i(int x, int y)
		{
			X = x;
			Y = y;
		}

		/// <summary>
		/// Compares two instances of <see cref="Vector2i"/> for equality.
		/// </summary>
		/// <param name="left">An instance of <see cref="Vector2i"/>.</param>
		/// <param name="right">Another instance of <see cref="Vector2i"/>.</param>
		/// <returns>A value indicating whether the two instances are equal.</returns>
		public static bool operator ==(Vector2i left, Vector2i right)
		{
			return left.Equals(right);
		}

		/// <summary>
		/// Compares two instances of <see cref="Vector2i"/> for inequality.
		/// </summary>
		/// <param name="left">An instance of <see cref="Vector2i"/>.</param>
		/// <param name="right">Another instance of <see cref="Vector2i"/>.</param>
		/// <returns>A value indicating whether the two instances are unequal.</returns>
		public static bool operator !=(Vector2i left, Vector2i right)
		{
			return !(left == right);
		}

		/// <summary>
		/// Gets a unique hash code for this instance.
		/// </summary>
		/// <returns>A hash code.</returns>
		public override int GetHashCode()
		{
			//TODO generate a good hashcode.
			return base.GetHashCode();
		}

		/// <summary>
		/// Turns the instance into a human-readable string.
		/// </summary>
		/// <returns>A string representing the instance.</returns>
		public override string ToString()
		{
			return "{ X: " + X.ToString() + ", Y: " + Y.ToString() + " }";
		}

		/// <summary>
		/// Checks for equality between this instance and a specified object.
		/// </summary>
		/// <param name="obj">An object.</param>
		/// <returns>A value indicating whether this instance and the object are equal.</returns>
		public override bool Equals(object obj)
		{
			Vector2i? objV = obj as Vector2i?;
			if (objV != null)
				return this.Equals(objV);

			return false;
		}

		/// <summary>
		/// Checks for equality between this instance and a specified instance of <see cref="Vector2i"/>.
		/// </summary>
		/// <param name="other">An instance of <see cref="Vector2i"/>.</param>
		/// <returns>A value indicating whether this instance and the other instance are equal.</returns>
		public bool Equals(Vector2i other)
		{
			return X == other.X && Y == other.Y;
		}
	}
}
