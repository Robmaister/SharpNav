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
		Failure = 1,

		/// <summary>
		/// Operation finished
		/// </summary>
		Success = 2,
		
		/// <summary>
		/// Operation in progress
		/// </summary>
		InProgress = 3
	}

	public static class StatusExtensions
	{
		public static Status ToStatus(this bool variable)
		{
			return variable ? Status.Success : Status.Failure;
		}
	}
}
