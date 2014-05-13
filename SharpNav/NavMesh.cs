using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SharpNav
{

	//TODO right now this is basically an alias for TiledNavMesh. Fix this in the future.
	public class NavMesh : TiledNavMesh
	{
		public NavMesh(NavMeshBuilder builder)
			: base(builder)
		{
		}

		public static NavMesh Create()
		{
			return null;
		}
	}
}
