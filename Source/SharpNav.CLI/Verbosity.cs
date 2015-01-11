using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpNav.CLI
{
	/// <summary>
	/// An enumeration of different levels of output.
	/// </summary>
	public enum Verbosity
	{
		/// <summary>
		/// Program outputs nothing.
		/// </summary>
		Silent,

		/// <summary>
		/// Program only outputs the bare minimum. Great for automation.
		/// </summary>
		Minimal,
		
		/// <summary>
		/// Standard verbosity level.
		/// </summary>
		Normal,

		/// <summary>
		/// Provides more details than most users would need.
		/// </summary>
		Verbose,

		/// <summary>
		/// Outputs everything. Great for tracking down a bug.
		/// </summary>
		Debug
	}
}
