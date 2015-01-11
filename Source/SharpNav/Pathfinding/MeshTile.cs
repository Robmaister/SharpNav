// Copyright (c) 2014-2015 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
// Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE

using SharpNav.Collections;
using SharpNav.Geometry;

#if MONOGAME
using Vector3 = Microsoft.Xna.Framework.Vector3;
#elif OPENTK
using Vector3 = OpenTK.Vector3;
#elif SHARPDX
using Vector3 = SharpDX.Vector3;
#endif

namespace SharpNav.Pathfinding
{
	/// <summary>
	/// The MeshTile contains the map data for pathfinding
	/// </summary>
	public class MeshTile
	{
		/// <summary>
		/// Gets or sets the counter describing modifications to the tile
		/// </summary>
		public int Salt { get; set; }

		/// <summary>
		/// Gets or sets the index to the next free link
		/// </summary>
		public int LinksFreeList { get; set; }

		/// <summary>
		/// Gets or sets the header
		/// </summary>
		public PathfinderCommon.NavMeshInfo Header { get; set; }

		/// <summary>
		/// Gets or sets the PolyMesh polygons
		/// </summary>
		public Poly[] Polys { get; set; }

		/// <summary>
		/// Gets or sets the PolyMesh vertices
		/// </summary>
		public Vector3[] Verts { get; set; }
																	
		/// <summary>
		/// Gets or sets the links between polygons
		/// </summary>
		public Link[] Links { get; set; }

		/// <summary>
		/// Gets or sets the PolyMeshDetail data
		/// </summary>
		public PolyMeshDetail.MeshData[] DetailMeshes { get; set; }

		/// <summary>
		/// Gets or sets the PolyMeshDetail vertices
		/// </summary>
		public Vector3[] DetailVerts { get; set; }

		/// <summary>
		/// Gets or sets the PolyMeshDetail triangles
		/// </summary>
		public PolyMeshDetail.TriangleData[] DetailTris { get; set; }

		/// <summary>
		/// Gets or sets the OffmeshConnections
		/// </summary>
		public OffMeshConnection[] OffMeshConnections { get; set; }

		/// <summary>
		/// Gets or sets the bounding volume tree
		/// </summary>
		public BVTree BVTree { get; set; }

		/// <summary>
		/// Gets or sets the NavMeshBuilder data
		/// </summary>
		public NavMeshBuilder Data { get; set; }

		/// <summary>
		/// Gets or sets the next MeshTile
		/// </summary>
		public MeshTile Next { get; set; }
	}
}
