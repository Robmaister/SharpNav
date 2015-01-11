// Copyright (c) 2014 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
// Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE

using System;

namespace SharpNav.Crowds
{
	/// <summary>
	/// Describes the current state of a crowd agent
	/// </summary>
	[Flags]
	public enum AgentState
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
