using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SharpNav.Crowd
{
	/// <summary>
	/// Describes the current state of a crowd agent
	/// </summary>
	[Flags]
	public enum CrowdAgentState
	{
		/// <summary>
		/// Not in any state
		/// </summary>
		Invalid,

		/// <summary>
		/// Walking on the navigation mesh
		/// </summary>
		Walking,

		/// <summary>
		/// Handling an offmesh connection
		/// </summary>
		Offmesh
	}
}
