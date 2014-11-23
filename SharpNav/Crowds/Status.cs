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

	public static class StatusExtensions
	{
		public static Status ToStatus(this bool variable)
		{
			return variable ? Status.Success : Status.Failure;
		}
	}
}
