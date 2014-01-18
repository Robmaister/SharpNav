#region License
/**
 * Copyright (c) 2013-2014 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
 * Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE
 */
#endregion

using System;

namespace SharpNav
{
	/// <summary>
	/// An area groups together pieces of data through the navmesh generation process.
	/// </summary>
	[Flags]
	public enum AreaId : byte
	{
		/// <summary>The null area, unwalkable.</summary>
		Null = 0,

		/// <summary>Any walkable area.</summary>
		Walkable = 0xff
	}
}
