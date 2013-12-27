#region License
/**
 * Copyright (c) 2013 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
 * Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE
 */
#endregion

using System;

namespace SharpNav
{
	/// <summary>
	/// A contour is formed from a region.
	/// </summary>
	public class Contour
	{
		//TODO properly encapsulate

		//simplified vertices have much less edges
		public ContourSet.SimplifiedVertex[] Vertices;

		//raw vertices derived directly from CompactHeightfield
		public ContourSet.RawVertex[] RawVertices;

		public int RegionId;
		public AreaFlags Area;
	}
}
