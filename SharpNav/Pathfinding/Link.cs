#region License
/**
 * Copyright (c) 2014 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
 * Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE
 */
#endregion

using System;

namespace SharpNav.Pathfinding
{
	public class Link
	{
		public int reference; //neighbor reference (the one it's linked to)
		public int next; //index of next link
		public int edge; //index of polygon edge
		public int side;
		public int bmin;
		public int bmax;
	}
}
