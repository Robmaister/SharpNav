#region License
/**
 * Copyright (c) 2014 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
 * Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE
 */
#endregion

using System;
using System.Runtime.InteropServices;

namespace SharpNav
{
	/// <summary>
	/// A reference to a <see cref="CompactSpan"/> in a <see cref="CompactHeightfield"/>.
	/// </summary>
	[StructLayout(LayoutKind.Sequential)]
	public struct CompactSpanReference
	{
		/// <summary>
		/// A "null" reference is one with a negative index.
		/// </summary>
		public static readonly CompactSpanReference Null = new CompactSpanReference(0, 0, -1);

		private int x;
		private int y;
		private int index;

		/// <summary>
		/// Initializes a new instance of the <see cref="CompactSpanReference"/> struct.
		/// </summary>
		/// <param name="x">The X coordinate.</param>
		/// <param name="y">The Y coordinate.</param>
		/// <param name="i">The index of the span in the spans array.</param>
		public CompactSpanReference(int x, int y, int i)
		{
			this.x = x;
			this.y = y;
			this.index = i;
		}

		/// <summary>
		/// Gets the X coordinate of the referenced span.
		/// </summary>
		public int X { get { return x; } }

		/// <summary>
		/// Gets the Y coordinate of the referenced span.
		/// </summary>
		public int Y { get { return y; } }

		/// <summary>
		/// Gets the index of the referenced span in the spans array.
		/// </summary>
		public int Index { get { return index; } }

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
			bool leftNull = left.index < 0, rightNull = right.index < 0;
			if (leftNull && rightNull)
				return true;
			else if (leftNull ^ rightNull)
				return false;

			//if the references are not null, 
			if (left.x == right.x && left.y == right.y && left.index == right.index)
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
			hash = (13 * hash) + x.GetHashCode();
			hash = (13 * hash) + y.GetHashCode();
			hash = (13 * hash) + index.GetHashCode();

			return hash;
		}
	}
}
