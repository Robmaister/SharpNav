// Copyright (c) 2014 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
// Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE

namespace SharpNav.Crowds
{
	/// <summary>
	/// The status of an asynchronous task.
	/// </summary>
	public enum Status
	{
		/* Detailed information */

		/// <summary>Something is wrong with the input data</summary>
		InvalidData = 0x00000001,

		/// <summary>A parameter was invalid</summary>
		InvalidParam = 0x00000002,

		/// <summary>Result buffer was too small for the output</summary>
		BufferTooSmall = 0x00000004,

		/// <summary>Query ran out of nodes during search</summary>
		OutOfNodes = 0x00000008,

		/// <summary>Query didn't reach the end. Result is the best guess.</summary>
		PartialResult = 0x00000010,

		/// <summary>A bitmask for detailed status values</summary>
		DetailMask = 0x0fffffff,

		/* High level status */

		/// <summary>Operation in progress</summary>
		InProgress = 0x20000000,

		/// <summary>Operation finished</summary>
		Success = 0x40000000,

		/// <summary>Operation failed to complete</summary>
		Failure = unchecked((int)0x80000000)
	}

	/// <summary>
	/// A static class containing extension methods related to the <see cref="Status"/> enum.
	/// </summary>
	public static class StatusExtensions
	{
		/// <summary>
		/// Converts a boolean value to a <see cref="Status"/>.
		/// </summary>
		/// <param name="variable">The boolean value.</param>
		/// <returns>The equivalent status.</returns>
		public static Status ToStatus(this bool variable)
		{
			return variable ? Status.Success : Status.Failure;
		}
	}
}
