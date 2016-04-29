// Copyright (c) 2016 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
// Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE

using System;

namespace SharpNav.Pathfinding
{
	/// <summary>
	/// Options for raycasting.
	/// </summary>
	[Flags]
	public enum RaycastOptions
	{
		None = 0,

		/// <summary>
		/// Calculate and use movement costs across the ray.
		/// </summary>
		UseCosts = 0x01,
	}
}
