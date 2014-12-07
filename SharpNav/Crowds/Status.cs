using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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
