// Copyright (c) 2013-2015 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
// Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE

using System;
using System.Collections.Generic;

using SharpNav.Collections;
using SharpNav.Geometry;
using SharpNav.Pathfinding;

#if MONOGAME
using Vector3 = Microsoft.Xna.Framework.Vector3;
#elif OPENTK
using Vector3 = OpenTK.Vector3;
#elif SHARPDX
using Vector3 = SharpDX.Vector3;
#endif

namespace SharpNav
{
	/// <summary>
	/// The NavMeshBuilder class converst PolyMesh and PolyMeshDetail into a different data structure suited for 
	/// pathfinding. This class will create tiled data.
	/// </summary>
	public class NavMeshBuilder
	{
		private PathfindingCommon.NavMeshInfo header;
		private Vector3[] navVerts;
		private Poly[] navPolys;
		private PolyMeshDetail.MeshData[] navDMeshes;
		private Vector3[] navDVerts;
		private PolyMeshDetail.TriangleData[] navDTris;
		private BVTree navBvTree;
		private OffMeshConnection[] offMeshConnections;

		/// <summary>
		/// Initializes a new instance of the <see cref="NavMeshBuilder" /> class.
		/// Add all the PolyMesh and PolyMeshDetail attributes to the Navigation Mesh.
		/// Then, add Off-Mesh connection support.
		/// </summary>
		/// <param name="polyMesh">The PolyMesh</param>
		/// <param name="polyMeshDetail">The PolyMeshDetail</param>
		/// <param name="offMeshCons">Offmesh connection data</param>
		/// <param name="settings">The settings used to build.</param>
		public NavMeshBuilder(PolyMesh polyMesh, PolyMeshDetail polyMeshDetail, OffMeshConnection[] offMeshCons, NavMeshGenerationSettings settings)
		{
			if (settings.VertsPerPoly > PathfindingCommon.VERTS_PER_POLYGON)
				throw new InvalidOperationException("The number of vertices per polygon is above SharpNav's limit");
			if (polyMesh.VertCount == 0)
				throw new InvalidOperationException("The provided PolyMesh has no vertices.");
			if (polyMesh.PolyCount == 0)
				throw new InvalidOperationException("The provided PolyMesh has not polys.");

			int nvp = settings.VertsPerPoly;

			//classify off-mesh connection points
			BoundarySide[] offMeshSides = new BoundarySide[offMeshCons.Length * 2];
			int storedOffMeshConCount = 0;
			int offMeshConLinkCount = 0;

			if (offMeshCons.Length > 0)
			{
				//find height bounds
				float hmin = float.MaxValue;
				float hmax = -float.MaxValue;

				if (polyMeshDetail != null)
				{
					for (int i = 0; i < polyMeshDetail.VertCount; i++)
					{
						float h = polyMeshDetail.Verts[i].Y;
						hmin = Math.Min(hmin, h);
						hmax = Math.Max(hmax, h);
					}
				}
				else
				{
					for (int i = 0; i < polyMesh.VertCount; i++)
					{
						PolyVertex iv = polyMesh.Verts[i];
						float h = polyMesh.Bounds.Min.Y + iv.Y * settings.CellHeight;
						hmin = Math.Min(hmin, h);
						hmax = Math.Max(hmax, h);
					}
				}

				hmin -= settings.MaxClimb;
				hmax += settings.MaxClimb;
				BBox3 bounds = polyMesh.Bounds;
				bounds.Min.Y = hmin;
				bounds.Max.Y = hmax;

				for (int i = 0; i < offMeshCons.Length; i++)
				{
					Vector3 p0 = offMeshCons[i].Pos0;
					Vector3 p1 = offMeshCons[i].Pos1;

					offMeshSides[i * 2 + 0] = BoundarySideExtensions.FromPoint(p0, bounds);
					offMeshSides[i * 2 + 1] = BoundarySideExtensions.FromPoint(p1, bounds);

					//off-mesh start position isn't touching mesh
					if (offMeshSides[i * 2 + 0] == BoundarySide.Internal)
					{
						if (p0.Y < bounds.Min.Y || p0.Y > bounds.Max.Y)
							offMeshSides[i * 2 + 0] = 0;
					}

					//count number of links to allocate
					if (offMeshSides[i * 2 + 0] == BoundarySide.Internal)
						offMeshConLinkCount++;
					if (offMeshSides[i * 2 + 1] == BoundarySide.Internal)
						offMeshConLinkCount++;

					if (offMeshSides[i * 2 + 0] == BoundarySide.Internal)
						storedOffMeshConCount++;
				}
			}

			//off-mesh connections stored as polygons, adjust values
			int totPolyCount = polyMesh.PolyCount + storedOffMeshConCount;
			int totVertCount = polyMesh.VertCount + storedOffMeshConCount * 2;

			//find portal edges
			int edgeCount = 0;
			int portalCount = 0;
			for (int i = 0; i < polyMesh.PolyCount; i++)
			{
				PolyMesh.Polygon p = polyMesh.Polys[i];
				for (int j = 0; j < nvp; j++)
				{
					if (p.Vertices[j] == PolyMesh.NullId)
						break;

					edgeCount++;
					
					if (PolyMesh.IsBoundaryEdge(p.NeighborEdges[j]))
					{
						int dir = p.NeighborEdges[j] % 16;
						if (dir != 15)
							portalCount++;
					}
				}
			}

			int maxLinkCount = edgeCount + portalCount * 2 + offMeshConLinkCount * 2;

			//find unique detail vertices
			int uniqueDetailVertCount = 0;
			int detailTriCount = 0;
			if (polyMeshDetail != null)
			{
				detailTriCount = polyMeshDetail.TrisCount;
				for (int i = 0; i < polyMesh.PolyCount; i++)
				{
					int numDetailVerts = polyMeshDetail.Meshes[i].VertexCount;
					int numPolyVerts = polyMesh.Polys[i].VertexCount;
					uniqueDetailVertCount += numDetailVerts - numPolyVerts;
				}
			}
			else
			{
				uniqueDetailVertCount = 0;
				detailTriCount = 0;
				for (int i = 0; i < polyMesh.PolyCount; i++)
				{
					int numPolyVerts = polyMesh.Polys[i].VertexCount;
					uniqueDetailVertCount += numPolyVerts - 2;
				}
			}

			//allocate data
			header = new PathfindingCommon.NavMeshInfo();
			navVerts = new Vector3[totVertCount];
			navPolys = new Poly[totPolyCount];
			navDMeshes = new PolyMeshDetail.MeshData[polyMesh.PolyCount];
			navDVerts = new Vector3[uniqueDetailVertCount];
			navDTris = new PolyMeshDetail.TriangleData[detailTriCount];
			offMeshConnections = new OffMeshConnection[storedOffMeshConCount];

			//store header
			//HACK TiledNavMesh should figure out the X/Y/layer instead of the user maybe?
			header.X = 0;
			header.Y = 0;
			header.Layer = 0;
			header.PolyCount = totPolyCount;
			header.VertCount = totVertCount;
			header.MaxLinkCount = maxLinkCount;
			header.Bounds = polyMesh.Bounds;
			header.DetailMeshCount = polyMesh.PolyCount;
			header.DetailVertCount = uniqueDetailVertCount;
			header.DetailTriCount = detailTriCount;
			header.OffMeshBase = polyMesh.PolyCount;
			header.WalkableHeight = settings.AgentHeight;
			header.WalkableRadius = settings.AgentRadius;
			header.WalkableClimb = settings.MaxClimb;
			header.OffMeshConCount = storedOffMeshConCount;
			header.BvNodeCount = settings.BuildBoundingVolumeTree ? polyMesh.PolyCount * 2 : 0;
			header.BvQuantFactor = 1f / settings.CellSize;

			int offMeshVertsBase = polyMesh.VertCount;
			int offMeshPolyBase = polyMesh.PolyCount;

			//store vertices
			for (int i = 0; i < polyMesh.VertCount; i++)
			{
				PolyVertex iv = polyMesh.Verts[i];
				navVerts[i].X = polyMesh.Bounds.Min.X + iv.X * settings.CellSize;
				navVerts[i].Y = polyMesh.Bounds.Min.Y + iv.Y * settings.CellHeight;
				navVerts[i].Z = polyMesh.Bounds.Min.Z + iv.Z * settings.CellSize;
			}

			//off-mesh link vertices
			int n = 0;
			for (int i = 0; i < offMeshCons.Length; i++)
			{
				//only store connections which start from this tile
				if (offMeshSides[i * 2 + 0] == BoundarySide.Internal)
				{
					navVerts[offMeshVertsBase + (n * 2 + 0)] = offMeshCons[i].Pos0;
					navVerts[offMeshVertsBase + (n * 2 + 1)] = offMeshCons[i].Pos1;
					n++;
				}
			}

			//store polygons
			for (int i = 0; i < polyMesh.PolyCount; i++)
			{
				navPolys[i] = new Poly();
				navPolys[i].VertCount = 0;
				navPolys[i].Flags = polyMesh.Polys[i].Flags;
				navPolys[i].Area = polyMesh.Polys[i].Area;
				navPolys[i].PolyType = PolygonType.Ground;
				navPolys[i].Verts = new int[nvp];
				navPolys[i].Neis = new int[nvp];
				for (int j = 0; j < nvp; j++)
				{
					if (polyMesh.Polys[i].Vertices[j] == PolyMesh.NullId)
						break;

					navPolys[i].Verts[j] = polyMesh.Polys[i].Vertices[j];
					if (PolyMesh.IsBoundaryEdge(polyMesh.Polys[i].NeighborEdges[j]))
					{
						//border or portal edge
						int dir = polyMesh.Polys[i].NeighborEdges[j] % 16;
						if (dir == 0xf) //border
							navPolys[i].Neis[j] = 0;
						else if (dir == 0) //portal x-
							navPolys[i].Neis[j] = Link.External | 4;
						else if (dir == 1) //portal z+
							navPolys[i].Neis[j] = Link.External | 2;
						else if (dir == 2) //portal x+
							navPolys[i].Neis[j] = Link.External | 0;
						else if (dir == 3) //portal z-
							navPolys[i].Neis[j] = Link.External | 6;
					}
					else
					{
						//normal connection
						navPolys[i].Neis[j] = polyMesh.Polys[i].NeighborEdges[j] + 1;
					}

					navPolys[i].VertCount++;
				}
			}

			//off-mesh connection vertices
			n = 0;
			for (int i = 0; i < offMeshCons.Length; i++)
			{
				//only store connections which start from this tile
				if (offMeshSides[i * 2 + 0] == BoundarySide.Internal)
				{
					navPolys[offMeshPolyBase + n].VertCount = 2;
					navPolys[offMeshPolyBase + n].Verts = new int[nvp];
					navPolys[offMeshPolyBase + n].Verts[0] = offMeshVertsBase + (n * 2 + 0);
					navPolys[offMeshPolyBase + n].Verts[1] = offMeshVertsBase + (n * 2 + 1);
					navPolys[offMeshPolyBase + n].Flags = (int)offMeshCons[i].Flags;
					navPolys[offMeshPolyBase + n].Area = polyMesh.Polys[offMeshCons[i].Poly].Area; //HACK is this correct?
					navPolys[offMeshPolyBase + n].PolyType = PolygonType.OffMeshConnection;
					n++;
				}
			}
			
			//store detail meshes and vertices
			if (polyMeshDetail != null)
			{
				int vbase = 0;
				List<Vector3> storedDetailVerts = new List<Vector3>();
				for (int i = 0; i < polyMesh.PolyCount; i++)
				{
					int vb = polyMeshDetail.Meshes[i].VertexIndex;
					int numDetailVerts = polyMeshDetail.Meshes[i].VertexCount;
					int numPolyVerts = navPolys[i].VertCount;
					navDMeshes[i].VertexIndex = vbase;
					navDMeshes[i].VertexCount = numDetailVerts - numPolyVerts;
					navDMeshes[i].TriangleIndex = polyMeshDetail.Meshes[i].TriangleIndex;
					navDMeshes[i].TriangleCount = polyMeshDetail.Meshes[i].TriangleCount;
					
					//Copy detail vertices 
					//first 'nv' verts are equal to nav poly verts
					//the rest are detail verts
					for (int j = 0; j < navDMeshes[i].VertexCount; j++)
					{
						storedDetailVerts.Add(polyMeshDetail.Verts[vb + numPolyVerts + j]);
					}

					vbase += numDetailVerts - numPolyVerts;
				}

				navDVerts = storedDetailVerts.ToArray();

				//store triangles
				for (int j = 0; j < polyMeshDetail.TrisCount; j++)
					navDTris[j] = polyMeshDetail.Tris[j];
			}
			else
			{
				//create dummy detail mesh by triangulating polys
				int tbase = 0;
				for (int i = 0; i < polyMesh.PolyCount; i++)
				{
					int numPolyVerts = navPolys[i].VertCount;
					navDMeshes[i].VertexIndex = 0;
					navDMeshes[i].VertexCount = 0;
					navDMeshes[i].TriangleIndex = tbase;
					navDMeshes[i].TriangleCount = numPolyVerts - 2;

					//triangulate polygon
					for (int j = 2; j < numPolyVerts; j++)
					{
						navDTris[tbase].VertexHash0 = 0;
						navDTris[tbase].VertexHash1 = j - 1;
						navDTris[tbase].VertexHash2 = j;

						//bit for each edge that belongs to the poly boundary
						navDTris[tbase].Flags = 1 << 2;
						if (j == 2) 
							navDTris[tbase].Flags |= 1 << 0;
						if (j == numPolyVerts - 1)
							navDTris[tbase].Flags |= 1 << 4;
						
						tbase++;
					}
				}
			}
			
			//store and create BV tree
			if (settings.BuildBoundingVolumeTree)
			{
				//build tree
				navBvTree = new BVTree(polyMesh.Verts, polyMesh.Polys, nvp, settings.CellSize, settings.CellHeight);
			}

			//store off-mesh connections
			n = 0;
			for (int i = 0; i < offMeshConnections.Length; i++)
			{
				//only store connections which start from this tile
				if (offMeshSides[i * 2 + 0] == BoundarySide.Internal)
				{
					offMeshConnections[n].Poly = offMeshPolyBase + n;

					//copy connection end points
					offMeshConnections[n].Pos0 = offMeshCons[i].Pos0;
					offMeshConnections[n].Pos1 = offMeshCons[i].Pos1;

					offMeshConnections[n].Radius = offMeshCons[i].Radius;
					offMeshConnections[n].Flags = offMeshCons[i].Flags;
					offMeshConnections[n].Side = offMeshSides[i * 2 + 1];
					offMeshConnections[n].Tag = offMeshCons[i].Tag;

					n++;
				}
			}
		}

		/// <summary>
		/// Gets the file header
		/// </summary>
		public PathfindingCommon.NavMeshInfo Header 
		{ 
			get 
			{ 
				return header; 
			} 
		}

		/// <summary>
		/// Gets the PolyMesh vertices
		/// </summary>
		public Vector3[] NavVerts 
		{ 
			get 
			{ 
				return navVerts; 
			} 
		}

		/// <summary>
		/// Gets the PolyMesh polygons
		/// </summary>
		public Poly[] NavPolys 
		{ 
			get 
			{
				return navPolys; 
			} 
		}

		/// <summary>
		/// Gets the PolyMeshDetail mesh data (the indices of the vertices and triagles)
		/// </summary>
		public PolyMeshDetail.MeshData[] NavDMeshes 
		{ 
			get 
			{ 
				return navDMeshes; 
			} 
		}

		/// <summary>
		/// Gets the PolyMeshDetail vertices
		/// </summary>
		public Vector3[] NavDVerts 
		{ 
			get 
			{ 
				return navDVerts; 
			} 
		}

		/// <summary>
		/// Gets the PolyMeshDetail triangles
		/// </summary>
		public PolyMeshDetail.TriangleData[] NavDTris 
		{ 
			get 
			{ 
				return navDTris; 
			} 
		}

		/// <summary>
		/// Gets the bounding volume tree
		/// </summary>
		public BVTree NavBvTree 
		{ 
			get 
			{ 
				return navBvTree; 
			} 
		}

		/// <summary>
		/// Gets the offmesh connection data
		/// </summary>
		public OffMeshConnection[] OffMeshCons 
		{ 
			get
			{ 
				return offMeshConnections; 
			} 
		}
	}
}
