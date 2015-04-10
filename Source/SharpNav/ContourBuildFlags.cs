// Copyright (c) 2013, 2015 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
// Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE

using System;

namespace SharpNav
{
	/// <summary>
	/// A set of flags that control the way contours are built.
	/// </summary>
	[Flags]
	public enum ContourBuildFlags
	{
		/// <summary>Build normally.</summary>
		None = 0,

		/// <summary>Tessellate solid edges during contour simplification.</summary>
		TessellateWallEdges = 0x01,

		/// <summary>Tessellate edges between areas during contour simplification.</summary>
		TessellateAreaEdges = 0x02
	}
}
