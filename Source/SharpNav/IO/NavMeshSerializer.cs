// Copyright (c) 2015 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
// Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE

using System;

using SharpNav;

namespace SharpNav.IO
{
	//TODO make an interface if it doesn't need to be extended
	public abstract class NavMeshSerializer
	{
		public abstract void Serialize(string path);
		public abstract TiledNavMesh Deserialize(string path);
	}
}
