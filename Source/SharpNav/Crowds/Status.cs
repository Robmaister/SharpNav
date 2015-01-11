// Copyright (c) 2014 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
// Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE

namespace SharpNav.Crowds
{
	/// <summary>
	/// Similar to a boolean, except there is an intermediate variable between true and false.
	/// </summary>
	public enum Status
	{
		/// <summary>
		/// Operation failed to complete
		/// </summary>
		Failure,

		/// <summary>
		/// Operation finished
		/// </summary>
		Success,
		
		/// <summary>
		/// Operation in progress
		/// </summary>
		InProgress
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
