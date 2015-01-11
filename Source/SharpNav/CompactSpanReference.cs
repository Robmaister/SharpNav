// Copyright (c) 2014 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
// Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE

using System;
using System.Runtime.InteropServices;

namespace SharpNav
{
	/// <summary>
	/// A reference to a <see cref="CompactSpan"/> in a <see cref="CompactHeightfield"/>.
	/// </summary>
	[StructLayout(LayoutKind.Sequential)]
	public struct CompactSpanReference : IEquatable<CompactSpanReference>
	{
		/// <summary>
		/// A "null" reference is one with a negative index.
		/// </summary>
		public static readonly CompactSpanReference Null = new CompactSpanReference(0, 0, -1);

		/// <summary>
		/// The X coordinate of the referenced span.
		/// </summary>
		public readonly int X;

		/// <summary>
		/// The Y coordinate of the referenced span.
		/// </summary>
		public readonly int Y;

		/// <summary>
		/// The index of the referenced span in the spans array.
		/// </summary>
		public readonly int Index;

		/// <summary>
		/// Initializes a new instance of the <see cref="CompactSpanReference"/> struct.
		/// </summary>
		/// <param name="x">The X coordinate.</param>
		/// <param name="y">The Y coordinate.</param>
		/// <param name="i">The index of the span in the spans array.</param>
		public CompactSpanReference(int x, int y, int i)
		{
			this.X = x;
			this.Y = y;
			this.Index = i;
		}

		/// <summary>
		/// Compares two instances of <see cref="CompactSpanReference"/> for equality.
		/// </summary>
		/// <remarks>
		/// If both references have a negative <see cref="Index"/>, they are considered equal, as both would be considered "null".
		/// </remarks>
		/// <param name="left">A reference.</param>
		/// <param name="right">Another reference.</param>
		/// <returns>A value indicating whether the two references are equal.</returns>
		public static bool operator ==(CompactSpanReference left, CompactSpanReference right)
		{
			//A negative index is considered null.
			//these two cases quickly compare null references.
			bool leftNull = left.Index < 0, rightNull = right.Index < 0;
			if (leftNull && rightNull)
				return true;
			else if (leftNull ^ rightNull)
				return false;

			//if the references are not null, 
			else if (left.X == right.X && left.Y == right.Y && left.Index == right.Index)
				return true;

			return false;
		}

		/// <summary>
		/// Compare two instances of <see cref="CompactSpanReference"/> for inequality.
		/// </summary>
		/// <remarks>
		/// If both references have a negative <see cref="Index"/>, they are considered equal, as both would be considered "null".
		/// </remarks>
		/// <param name="left">A reference.</param>
		/// <param name="right">Another reference.</param>
		/// <returns>A value indicating whether the two references are not equal.</returns>
		public static bool operator !=(CompactSpanReference left, CompactSpanReference right)
		{
			return !(left == right);
		}

		/// <summary>
		/// Compares this instance to another instance of <see cref="CompactSpanReference"/> for equality.
		/// </summary>
		/// <param name="other">Another instance of <see cref="CompactSpanReference"/>.</param>
		/// <returns>A value indicating whether this instance and another instance are equal.</returns>
		public bool Equals(CompactSpanReference other)
		{
			return this == other;
		}

		/// <summary>
		/// Compares this instance to another object for equality.
		/// </summary>
		/// <param name="obj">An object.</param>
		/// <returns>A value indicating whether the object is equal to this instance.</returns>
		public override bool Equals(object obj)
		{
			CompactSpanReference? r = obj as CompactSpanReference?;
			if (r.HasValue)
				return this == r.Value;
			return false;
		}

		/// <summary>
		/// Gets a hash code unique to this instance.
		/// </summary>
		/// <returns>A hash code.</returns>
		public override int GetHashCode()
		{
			//TODO should "null" references all have the same hash?
			int hash = 27;
			hash = (13 * hash) + X.GetHashCode();
			hash = (13 * hash) + Y.GetHashCode();
			hash = (13 * hash) + Index.GetHashCode();

			return hash;
		}
	}
}
