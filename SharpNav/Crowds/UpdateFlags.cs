using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SharpNav.Crowds
{
	/// <summary>
	/// The UpdateFlag affects the way the agent moves acorss its path.
	/// </summary>
	[Flags]
	public enum UpdateFlags
	{
		/// <summary>
		/// The agent will be making turns in its path
		/// </summary>
		AnticipateTurns = 1,

		/// <summary>
		/// Avoid obstacles on the path
		/// </summary>
		ObstacleAvoidance = 2,

		/// <summary>
		/// Separate this agent from other agents
		/// </summary>
		Separation = 4,

		/// <summary>
		/// Optimize if the agent can see the next corner
		/// </summary>
		OptimizeVis = 8,

		/// <summary>
		/// Optimize the agent's path corridor
		/// </summary>
		OptimizeTopo = 16
	}
}
