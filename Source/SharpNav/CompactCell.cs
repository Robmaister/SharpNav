// Copyright (c) 2013 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
// Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE

using System.Runtime.InteropServices;

namespace SharpNav
{
	/// <summary>
	/// Represents a cell in a <see cref="CompactHeightfield"/>.
	/// </summary>
	[StructLayout(LayoutKind.Sequential)]
	public struct CompactCell
	{
		/// <summary>
		/// The starting index of spans in a <see cref="CompactHeightfield"/> for this cell.
		/// </summary>
		public int StartIndex;

		/// <summary>
		/// The number of spans in a <see cref="CompactHeightfield"/> for this cell.
		/// </summary>
		public int Count;

		/// <summary>
		/// Initializes a new instance of the <see cref="CompactCell"/> struct.
		/// </summary>
		/// <param name="start">The start index.</param>
		/// <param name="count">The count.</param>
		public CompactCell(int start, int count)
		{
			StartIndex = start;
			Count = count;
		}
	}
}
