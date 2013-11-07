#region License
/**
 * Copyright (c) 2013 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
 * Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpNav
{
	/// <summary>
	/// A set of flags that define the properties of the area a span is in.
	/// </summary>
	[Flags]
	public enum AreaFlags : byte
	{
		/// <summary>
		/// The null area, unwalkable.
		/// </summary>
		Null = 0,

		/// <summary>
		/// A walkable area.
		/// </summary>
		Walkable = 1
	}
}
