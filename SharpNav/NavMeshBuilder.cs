#region License
/**
 * Copyright (c) 2013-2014 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
 * Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE
 */
#endregion

using System;
using System.Collections.Generic;
using SharpNav.Collections.Generic;
using SharpNav.Geometry;
using SharpNav.Pathfinding;

#if MONOGAME || XNA
using Microsoft.Xna.Framework;
#elif OPENTK
using OpenTK;
#elif SHARPDX
using SharpDX;
#elif UNITY3D
using UnityEngine;
#endif

namespace SharpNav
{
	public class NavMeshBuilder
	{
		//convert NavMesh and NavMeshDetail into a different data structure suited for pathfinding
		//This class will create tiled data.
		private PathfinderCommon.NavMeshInfo header;
		private Vector3[] navVerts;
		private Poly[] navPolys;
		private PolyMeshDetail.MeshData[] navDMeshes;
		private Vector3[] navDVerts;
		private PolyMeshDetail.TriangleData[] navDTris;
		private BVTree navBvTree;
		private OffMeshConnection[] offMeshCons;

		/// <summary>
		/// Add all the PolyMesh and PolyMeshDetail attributes to the Navigation Mesh.
		/// Then, add Off-Mesh connection support.
		/// </summary>
		/// <param name="parameters">All the member variables that create the Navigation Mesh</param>
		public NavMeshBuilder(NavMeshCreateParams parameters)
		{
			if (parameters.numVertsPerPoly > PathfinderCommon.VERTS_PER_POLYGON)
				return;
			if (parameters.vertCount >= 0xffff)
				return;
			if (parameters.vertCount == 0)
				return;
			if (parameters.polyCount == 0)
				return;

			int nvp = parameters.numVertsPerPoly;

			//classify off-mesh connection points
			int[] offMeshConClass = new int[parameters.offMeshConCount * 2];
			int storedOffMeshConCount = 0;
			int offMeshConLinkCount = 0;

			if (parameters.offMeshConCount > 0)
			{
				//find height bounds
				float hmin = float.MaxValue;
				float hmax = -float.MaxValue;

				if (parameters.detailVerts.Length != 0 && parameters.detailVertsCount != 0)
				{
					for (int i = 0; i < parameters.detailVertsCount; i++)
					{
						float h = parameters.detailVerts[i].Y;
						hmin = Math.Min(hmin, h);
						hmax = Math.Max(hmax, h);
					}
				}
				else
				{
					for (int i = 0; i < parameters.vertCount; i++)
					{
						Vector3 iv = parameters.Verts[i];
						float h = parameters.bounds.Min.Y + iv.Y * parameters.cellHeight;
						hmin = Math.Min(hmin, h);
						hmax = Math.Max(hmax, h);
					}
				}

				hmin -= parameters.walkableClimb;
				hmax += parameters.walkableClimb;
				BBox3 bounds = parameters.bounds;
				bounds.Min.Y = hmin;
				bounds.Max.Y = hmax;

				for (int i = 0; i < parameters.offMeshConCount; i++)
				{
					Vector3 p0 = parameters.offMeshConVerts[i * 2 + 0];
					Vector3 p1 = parameters.offMeshConVerts[i * 2 + 1];

					offMeshConClass[i * 2 + 0] = ClassifyOffMeshPoint(p0, bounds);
					offMeshConClass[i * 2 + 1] = ClassifyOffMeshPoint(p1, bounds);

					//off-mesh start position isn't touching mesh
					if (offMeshConClass[i * 2 + 0] == 0xff)
					{
						if (p0.Y < bounds.Min.Y || p0.Y > bounds.Max.Y)
							offMeshConClass[i * 2 + 0] = 0;
					}

					//count number of links to allocate
					if (offMeshConClass[i * 2 + 0] == 0xff)
						offMeshConLinkCount++;
					if (offMeshConClass[i * 2 + 1] == 0xff)
						offMeshConLinkCount++;

					if (offMeshConClass[i * 2 + 0] == 0xff)
						storedOffMeshConCount++;
				}
			}

			//off-mesh connections stored as polygons, adjust values
			int totPolyCount = parameters.polyCount + storedOffMeshConCount;
			int totVertCount = parameters.vertCount + storedOffMeshConCount * 2;

			//find portal edges
			int edgeCount = 0;
			int portalCount = 0;
			for (int i = 0; i < parameters.polyCount; i++)
			{
				PolyMesh.Polygon p = parameters.polys[i];
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
			if (parameters.detailMeshes.Length != 0)
			{
				detailTriCount = parameters.detailTriCount;
				for (int i = 0; i < parameters.polyCount; i++)
				{
					int numDetailVerts = parameters.detailMeshes[i].VertexCount;
					int numPolyVerts = parameters.polys[i].VertexCount;
					uniqueDetailVertCount += numDetailVerts - numPolyVerts;
				}
			}
			else
			{
				uniqueDetailVertCount = 0;
				detailTriCount = 0;
				for (int i = 0; i < parameters.polyCount; i++)
				{
					int numPolyVerts = parameters.polys[i].VertexCount;
					uniqueDetailVertCount += numPolyVerts - 2;
				}
			}

			//allocate data
			header = new PathfinderCommon.NavMeshInfo();
			navVerts = new Vector3[totVertCount];
			navPolys = new Poly[totPolyCount];
			navDMeshes = new PolyMeshDetail.MeshData[parameters.polyCount];
			navDVerts = new Vector3[uniqueDetailVertCount];
			navDTris = new PolyMeshDetail.TriangleData[detailTriCount];
			offMeshCons = new OffMeshConnection[storedOffMeshConCount];

			//store header
			//header.magic = PathfinderCommon.NAVMESH_MAGIC;
			//header.version = PathfinderCommon.NAVMESH_VERSION;
			header.x = parameters.tileX;
			header.y = parameters.tileY;
			header.layer = parameters.tileLayer;
			header.userId = parameters.userId;
			header.polyCount = totPolyCount;
			header.vertCount = totVertCount;
			header.maxLinkCount = maxLinkCount;
			header.bounds = parameters.bounds;
			header.detailMeshCount = parameters.polyCount;
			header.detailVertCount = uniqueDetailVertCount;
			header.detailTriCount = detailTriCount;
			header.offMeshBase = parameters.polyCount;
			header.walkableHeight = parameters.walkableHeight;
			header.walkableRadius = parameters.walkableRadius;
			header.walkableClimb = parameters.walkableClimb;
			header.offMeshConCount = storedOffMeshConCount;
			header.bvNodeCount = parameters.buildBvTree ? parameters.polyCount * 2 : 0;
			header.bvQuantFactor = 1f / parameters.cellSize;

			int offMeshVertsBase = parameters.vertCount;
			int offMeshPolyBase = parameters.polyCount;

			//store vertices
			for (int i = 0; i < parameters.vertCount; i++)
			{
				Vector3 iv = parameters.Verts[i];
				navVerts[i].X = parameters.bounds.Min.X + iv.X * parameters.cellSize;
				navVerts[i].Y = parameters.bounds.Min.Y + iv.Y * parameters.cellHeight;
				navVerts[i].Z = parameters.bounds.Min.Z + iv.Z * parameters.cellSize;
			}

			//off-mesh link vertices
			int n = 0;
			for (int i = 0; i < parameters.offMeshConCount; i++)
			{
				//only store connections which start from this tile
				if (offMeshConClass[i * 2 + 0] == 0xff)
				{
					navVerts[offMeshVertsBase + (n * 2 + 0)] = parameters.offMeshConVerts[i * 2 + 0];
					navVerts[offMeshVertsBase + (n * 2 + 1)] = parameters.offMeshConVerts[i * 2 + 1];
					n++;
				}
			}

			//store polygons
			for (int i = 0; i < parameters.polyCount; i++)
			{
				navPolys[i] = new Poly();
				navPolys[i].VertCount = 0;
				navPolys[i].Flags = parameters.polys[i].Flags;
				navPolys[i].Area = parameters.polys[i].Area;
				navPolys[i].PolyType = PolygonType.Ground;
				navPolys[i].Verts = new int[nvp];
				navPolys[i].Neis = new int[nvp];
				for (int j = 0; j < nvp; j++)
				{
					if (parameters.polys[i].Vertices[j] == PolyMesh.NullId)
						break;

					navPolys[i].Verts[j] = parameters.polys[i].Vertices[j];
					if (PolyMesh.IsBoundaryEdge(parameters.polys[i].NeighborEdges[j]))
					{
						//border or portal edge
						int dir = parameters.polys[i].NeighborEdges[j] % 16;
						if (dir == 0xf) //border
							navPolys[i].Neis[j] = 0;
						else if (dir == 0) //portal x-
							navPolys[i].Neis[j] = PathfinderCommon.EXT_LINK | 4;
						else if (dir == 1) //portal z+
							navPolys[i].Neis[j] = PathfinderCommon.EXT_LINK | 2;
						else if (dir == 2) //portal x+
							navPolys[i].Neis[j] = PathfinderCommon.EXT_LINK | 0;
						else if (dir == 3) //portal z-
							navPolys[i].Neis[j] = PathfinderCommon.EXT_LINK | 6;
					}
					else
					{
						//normal connection
						navPolys[i].Neis[j] = parameters.polys[i].NeighborEdges[j] + 1;
					}

					navPolys[i].VertCount++;
				}
			}

			//off-mesh connection vertices
			n = 0;
			for (int i = 0; i < parameters.offMeshConCount; i++)
			{
				//only store connections which start from this tile
				if (offMeshConClass[i * 2 + 0] == 0xff)
				{
					navPolys[offMeshPolyBase + n].VertCount = 2;
					navPolys[offMeshPolyBase + n].Verts = new int[nvp];
					navPolys[offMeshPolyBase + n].Verts[0] = offMeshVertsBase + (n * 2 + 0);
					navPolys[offMeshPolyBase + n].Verts[1] = offMeshVertsBase + (n * 2 + 1);
					navPolys[offMeshPolyBase + n].Flags = parameters.offMeshConFlags[i];
					navPolys[offMeshPolyBase + n].Area = parameters.offMeshConAreas[i];
					navPolys[offMeshPolyBase + n].PolyType = PolygonType.OffMeshConnection;
					n++;
				}
			}
			
			//store detail meshes and vertices
			if (parameters.detailMeshes.Length != 0)
			{
				int vbase = 0;
				List<Vector3> storedDetailVerts = new List<Vector3>();
				for (int i = 0; i < parameters.polyCount; i++)
				{
					int vb = parameters.detailMeshes[i].VertexIndex;
					int numDetailVerts = parameters.detailMeshes[i].VertexCount;
					int numPolyVerts = navPolys[i].VertCount;
					navDMeshes[i].VertexIndex = vbase;
					navDMeshes[i].VertexCount = numDetailVerts - numPolyVerts;
					navDMeshes[i].TriangleIndex = parameters.detailMeshes[i].TriangleIndex;
					navDMeshes[i].TriangleCount = parameters.detailMeshes[i].TriangleCount;
					
					//Copy detail vertices 
					//first 'nv' verts are equal to nav poly verts
					//the rest are detail verts
					for (int j = 0; j < navDMeshes[i].VertexCount; j++)
					{
						storedDetailVerts.Add(parameters.detailVerts[vb + numPolyVerts + j]);
					}

					vbase += numDetailVerts - numPolyVerts;
				}

				navDVerts = storedDetailVerts.ToArray();

				//store triangles
				for (int j = 0; j < parameters.detailTriCount; j++)
					navDTris[j] = parameters.detailTris[j];
			}
			else
			{
				//create dummy detail mesh by triangulating polys
				int tbase = 0;
				for (int i = 0; i < parameters.polyCount; i++)
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
			if (parameters.buildBvTree)
			{
				//build tree
				navBvTree = new BVTree(parameters.Verts, parameters.polys, nvp, parameters.cellSize, parameters.cellHeight);
			}

			//store off-mesh connections
			n = 0;
			for (int i = 0; i < parameters.offMeshConCount; i++)
			{
				//only store connections which start from this tile
				if (offMeshConClass[i * 2 + 0] == 0xff)
				{
					offMeshCons[n].Poly = offMeshPolyBase + n;

					//copy connection end points
					offMeshCons[n].Pos0 = parameters.offMeshConVerts[i * 2 + 0];
					offMeshCons[n].Pos1 = parameters.offMeshConVerts[i * 2 + 1];

					offMeshCons[n].Radius = parameters.offMeshConRadii[i];
					offMeshCons[n].Flags = (parameters.offMeshConDir[i] != 0) ? PathfinderCommon.OFFMESH_CON_BIDIR : 0;
					offMeshCons[n].Side = offMeshConClass[i * 2 + 1];
					if (parameters.offMeshConUserID.Length != 0)
						offMeshCons[n].UserId = parameters.offMeshConUserID[i];

					n++;
				}
			}
		}

		public PathfinderCommon.NavMeshInfo Header { get { return header; } }

		public Vector3[] NavVerts { get { return navVerts; } }

		public Poly[] NavPolys { get { return navPolys; } }

		public PolyMeshDetail.MeshData[] NavDMeshes { get { return navDMeshes; } }

		public Vector3[] NavDVerts { get { return navDVerts; } }

		public PolyMeshDetail.TriangleData[] NavDTris { get { return navDTris; } }

		public BVTree NavBvTree { get { return navBvTree; } }

		public OffMeshConnection[] OffMeshCons { get { return offMeshCons; } }

		/// <summary>
		/// Decide which sector the offmesh point is a part of.
		/// </summary>
		/// <param name="pt">The point</param>
		/// <param name="bounds">The bounds</param>
		/// <returns>An integer representing a sector</returns>
		public int ClassifyOffMeshPoint(Vector3 pt, BBox3 bounds)
		{
			const int xPlus = 1;
			const int zPlus = 2;  
			const int xMinus = 4; 
			const int zMinus = 8; 

			int outcode = 0;
			outcode += (pt.X >= bounds.Max.X) ? xPlus : 0;
			outcode += (pt.Z >= bounds.Max.Z) ? zPlus : 0;
			outcode += (pt.X < bounds.Min.X) ? xMinus : 0;
			outcode += (pt.Z < bounds.Min.Z) ? zMinus : 0;

			switch (outcode)
			{
				case xPlus:
					return 0;

				case xPlus + zPlus:
					return 1;

				case zPlus:
					return 2;

				case xMinus + zPlus:
					return 3;

				case xMinus:
					return 4;

				case xMinus + zMinus:
					return 5;

				case zMinus:
					return 6;

				case xPlus + zMinus:
					return 7;
			}

			return 0xff;
		}
	}
}
