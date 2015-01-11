// Copyright (c) 2013-2015 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
// Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE

using System;
using System.Runtime.InteropServices;

namespace SharpNav
{
	/// <summary>
	/// An area groups together pieces of data through the navmesh generation process.
	/// </summary>
	[Serializable]
	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public struct Area : IEquatable<Area>, IEquatable<byte>
	{
		/// <summary>
		/// The null area is one that is considered unwalkable.
		/// </summary>
		public static readonly Area Null = new Area(0);

		/// <summary>
		/// This is a default <see cref="Area"/> in the event that the user does not provide one.
		/// </summary>
		/// <remarks>
		/// If a user only applies IDs to some parts of a <see cref="Heightfield"/>, they will most likely choose low
		/// integer values. Choosing the maximum value makes it unlikely for the "default" area to collide with any
		/// user-defined areas.
		/// </remarks>
		public static readonly Area Default = new Area(0xff);

		/// <summary>
		/// The identifier for an area, represented as a byte.
		/// </summary>
		public readonly byte Id;

		/// <summary>
		/// Initializes a new instance of the <see cref="Area"/> struct.
		/// </summary>
		/// <param name="id">An identifier for the area.</param>
		public Area(byte id)
		{
			this.Id = id;
		}

		/// <summary>
		/// Gets a value indicating whether the area is considered walkable (not <see cref="Area.Null"/>).
		/// </summary>
		public bool IsWalkable
		{
			get
			{
				return Id != 0;
			}
		}

		/// <summary>
		/// Implicitly casts a byte to an Area. This is included since an Area is a very thin wrapper around a byte.
		/// </summary>
		/// <param name="value">The identifier for an area.</param>
		/// <returns>An area with the specified identifier.</returns>
		public static implicit operator Area(byte value)
		{
			return new Area(value);
		}

		/// <summary>
		/// Compares two areas for equality.
		/// </summary>
		/// <param name="left">An <see cref="Area"/>.</param>
		/// <param name="right">Another <see cref="Area"/></param>
		/// <returns>A value indicating whether the two <see cref="Area"/>s are equal.</returns>
		public static bool operator ==(Area left, Area right)
		{
			return left.Equals(right);
		}

		/// <summary>
		/// Compares two areas for inequality.
		/// </summary>
		/// <param name="left">An <see cref="Area"/>.</param>
		/// <param name="right">Another <see cref="Area"/></param>
		/// <returns>A value indicating whether the two <see cref="Area"/>s are unequal.</returns>
		public static bool operator !=(Area left, Area right)
		{
			return !(left == right);
		}
		
		/// <summary>
		/// Compares this instance with another instance of <see cref="Area"/> for equality.
		/// </summary>
		/// <param name="other">An <see cref="Area"/>.</param>
		/// <returns>A value indicating whether the two <see cref="Area"/>s are equal.</returns>
		public bool Equals(Area other)
		{
			return this.Id == other.Id;
		}

		/// <summary>
		/// Compares this instance with a byte representing another <see cref="Area"/> for equality.
		/// </summary>
		/// <param name="other">A byte.</param>
		/// <returns>A value indicating whether this instance and the specified byte are equal.</returns>
		public bool Equals(byte other)
		{
			return this.Id == other;
		}

		/// <summary>
		/// Compares this instance with another object for equality.
		/// </summary>
		/// <param name="obj">An object.</param>
		/// <returns>A value indicating whether this instance and the specified object are equal.</returns>
		public override bool Equals(object obj)
		{
			var areaObj = obj as Area?;
			var byteObj = obj as byte?;

			if (areaObj.HasValue)
				return this.Equals(areaObj.Value);
			else if (byteObj.HasValue)
				return this.Equals(byteObj.Value);
			else
				return false;
		}

		/// <summary>
		/// Generates a hashcode unique to the <see cref="Id"/> of this instance.
		/// </summary>
		/// <returns>A hash code.</returns>
		public override int GetHashCode()
		{
			return Id.GetHashCode();
		}

		/// <summary>
		/// Converts this instance to a human-readable string.
		/// </summary>
		/// <returns>A string representing this instance.</returns>
		public override string ToString()
		{
			if (Id == 0)
				return "Null/Unwalkable";
			else
				return Id.ToString();
		}
	}
}
