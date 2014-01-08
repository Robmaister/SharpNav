#region License
/**
 * Copyright (c) 2013 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
 * Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE
 */
#endregion

using System;
using System.Collections.Generic;

using SharpNav.Geometry;

#if MONOGAME || XNA
using Microsoft.Xna.Framework;
#elif OPENTK
using OpenTK;
#elif SHARPDX
using SharpDX;
#endif

namespace SharpNav
{
	public class NavMeshBuilder
	{
		//convert NavMesh and NavMeshDetail into a different data structure suited for pathfinding
		//This class will create tiled data.
		private PathfinderCommon.MeshHeader header;
		private Vector3[] navVerts;
		private PathfinderCommon.Poly[] navPolys;
		private PathfinderCommon.PolyDetail[] navDMeshes;
		private Vector3[] navDVerts;
		private PolyMeshDetail.TrisInfo[] navDTris;
		private PathfinderCommon.BVNode[] navBvTree;
		private PathfinderCommon.OffMeshConnection[] offMeshCons;

		public PathfinderCommon.MeshHeader Header { get { return header; } }
		public Vector3[] NavVerts { get { return navVerts; } }
		public PathfinderCommon.Poly[] NavPolys { get { return navPolys; } }
		public PathfinderCommon.PolyDetail[] NavDMeshes { get { return navDMeshes; } }
		public Vector3[] NavDVerts { get { return navDVerts; } }
		public PolyMeshDetail.TrisInfo[] NavDTris { get { return navDTris; } }
		public PathfinderCommon.BVNode[] NavBvTree { get { return navBvTree; } }
		public PathfinderCommon.OffMeshConnection[] OffMeshCons { get { return offMeshCons; } }

		public NavMeshBuilder(NavMeshCreateParams parameters)
		{
			if (parameters.numVertsPerPoly > PathfinderCommon.VERTS_PER_POLYGON)
				return;
			if (parameters.vertCount >= 0xffff)
				return;
			if (parameters.vertCount == 0 || parameters.verts.Length == 0)
				return;
			if (parameters.polyCount == 0 || parameters.polys.Length == 0)
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
						Vector3 iv = parameters.verts[i];
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
					if (p.Vertices[j] == PolyMesh.MESH_NULL_IDX)
						break;

					edgeCount++;
					
					if ((p.ExtraInfo[j] & 0x8000) != 0)
					{
						int dir = p.ExtraInfo[j] & 0xf;
						if (dir != 0xf)
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
					PolyMesh.Polygon p = parameters.polys[i];
					int ndv = parameters.detailMeshes[i].NewNumVerts;
					int nv = 0;
					for (int j = 0; j < nvp; j++)
					{
						if (p.Vertices[j] == PolyMesh.MESH_NULL_IDX)
							break;

						nv++;
					}

					ndv -= nv;
					uniqueDetailVertCount += ndv;
				}
			}
			else
			{
				uniqueDetailVertCount = 0;
				detailTriCount = 0;
				for (int i = 0; i < parameters.polyCount; i++)
				{
					PolyMesh.Polygon p = parameters.polys[i];
					int nv = 0;
					for (int j = 0; j < nvp; j++)
					{
						if (p.Vertices[j] == PolyMesh.MESH_NULL_IDX)
							break;

						nv++;
					}

					uniqueDetailVertCount += nv - 2;
				}
			}

			//allocate data
			header = new PathfinderCommon.MeshHeader();
			navVerts = new Vector3[totVertCount];
			navPolys = new PathfinderCommon.Poly[totPolyCount];
			navDMeshes = new PathfinderCommon.PolyDetail[parameters.polyCount];
			navDVerts = new Vector3[uniqueDetailVertCount];
			navDTris = new PolyMeshDetail.TrisInfo[detailTriCount];
			navBvTree = new PathfinderCommon.BVNode[parameters.polyCount * 2];
			offMeshCons = new PathfinderCommon.OffMeshConnection[storedOffMeshConCount];

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
			header.bvQuantFactor = 1.0f / parameters.cellSize;
			header.offMeshBase = parameters.polyCount;
			header.walkableHeight = parameters.walkableHeight;
			header.walkableRadius = parameters.walkableRadius;
			header.walkableClimb = parameters.walkableClimb;
			header.offMeshConCount = storedOffMeshConCount;
			header.bvNodeCount = parameters.buildBvTree ? parameters.polyCount * 2 : 0;

			int offMeshVertsBase = parameters.vertCount;
			int offMeshPolyBase = parameters.polyCount;

			//store vertices
			for (int i = 0; i < parameters.vertCount; i++)
			{
				Vector3 iv = parameters.verts[i];
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
				navPolys[i] = new PathfinderCommon.Poly();
				navPolys[i].vertCount = 0;
				navPolys[i].flags = parameters.polyFlags[i];
				navPolys[i].SetArea((int)parameters.polyAreas[i]);
				navPolys[i].SetType(PolygonType.Ground);
				
				navPolys[i].verts = new int[nvp];
				navPolys[i].neis = new int[nvp];
				for (int j = 0; j < nvp; j++)
				{
					if (parameters.polys[i].Vertices[j] == PolyMesh.MESH_NULL_IDX)
						break;

					navPolys[i].verts[j] = parameters.polys[i].Vertices[j];
					if ((parameters.polys[i].ExtraInfo[j] & 0x8000) != 0)
					{
						//border or portal edge
						int dir = parameters.polys[i].ExtraInfo[j] & 0xf;
						if (dir == 0xf) //border
							navPolys[i].neis[j] = 0;
						else if (dir == 0) //portal x-
							navPolys[i].neis[j] = PathfinderCommon.EXT_LINK | 4;
						else if (dir == 1) //portal z+
							navPolys[i].neis[j] = PathfinderCommon.EXT_LINK | 2;
						else if (dir == 2) //portal x+
							navPolys[i].neis[j] = PathfinderCommon.EXT_LINK | 0;
						else if (dir == 3) //portal z-
							navPolys[i].neis[j] = PathfinderCommon.EXT_LINK | 6;
					}
					else
					{
						//normal connection
						navPolys[i].neis[j] = parameters.polys[i].ExtraInfo[j] + 1;
					}

					navPolys[i].vertCount++;
				}
			}
			//off-mesh connection vertices
			n = 0;
			for (int i = 0; i < parameters.offMeshConCount; i++)
			{
				//only store connections which start from this tile
				if (offMeshConClass[i * 2 + 0] == 0xff)
				{
					navPolys[offMeshPolyBase + n].vertCount = 2;
					navPolys[offMeshPolyBase + n].verts = new int[nvp];
					navPolys[offMeshPolyBase + n].verts[0] = offMeshVertsBase + (n * 2 + 0);
					navPolys[offMeshPolyBase + n].verts[1] = offMeshVertsBase + (n * 2 + 1);
					navPolys[offMeshPolyBase + n].flags = parameters.offMeshConFlags[i];
					navPolys[offMeshPolyBase + n].SetArea(parameters.offMeshConAreas[i]);
					navPolys[offMeshPolyBase + n].SetType(PolygonType.OffMeshConnection);
					n++;
				}
			}
			
			//store detail meshes and vertices
			if (parameters.detailMeshes.Length != 0)
			{
				int vbase = 0;
				for (int i = 0; i < parameters.polyCount; i++)
				{
					int vb = parameters.detailMeshes[i].OldNumVerts;
					int ndv = parameters.detailMeshes[i].NewNumVerts;
					int nv = navPolys[i].vertCount;
					navDMeshes[i].vertBase = (uint)vbase;
					navDMeshes[i].vertCount = ndv - nv;
					navDMeshes[i].triBase = (uint)parameters.detailMeshes[i].OldNumTris;
					navDMeshes[i].triCount = parameters.detailMeshes[i].NewNumTris;

					//copy vertices except for first 'nv' verts which are equal to nav poly verts
					if (ndv - nv > 0)
					{
						for (int j = 0; j < ndv - nv; j++)
						{
							navDVerts[vbase + j] = parameters.detailVerts[vb + nv + j];
						}

						vbase += ndv - nv;
					}
				}

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
					int nv = navPolys[i].vertCount;
					navDMeshes[i].vertBase = 0;
					navDMeshes[i].vertCount = 0;
					navDMeshes[i].triBase = (uint)tbase;
					navDMeshes[i].triCount = nv - 2;

					//triangulate polygon
					for (int j = 2; j < nv; j++)
					{
						navDTris[tbase].VertexHash[0] = 0;
						navDTris[tbase].VertexHash[1] = j - 1;
						navDTris[tbase].VertexHash[2] = j;

						//bit for each edge that belongs to the poly boundary
						navDTris[tbase].Flag = 1 << 2;
						if (j == 2) 
							navDTris[tbase].Flag |= 1 << 0;
						if (j == nv - 1)
							navDTris[tbase].Flag |= 1 << 4;
						
						tbase++;
					}
				}
			}
			
			//store and create BV tree
			if (parameters.buildBvTree)
			{
				//build tree
				CreateBVTree(parameters.verts, parameters.polys, parameters.polyCount, nvp, parameters.cellSize, parameters.cellHeight, navBvTree);
			}

			//store off-mesh connections
			n = 0;
			for (int i = 0; i < parameters.offMeshConCount; i++)
			{
				//only store connections which start from this tile
				if (offMeshConClass[i * 2 + 0] == 0xff)
				{
					offMeshCons[n].poly = offMeshPolyBase + n;

					//copy connection end points
					offMeshCons[n].pos = new Vector3[2];
					offMeshCons[n].pos[0] = parameters.offMeshConVerts[i * 2 + 0];
					offMeshCons[n].pos[1] = parameters.offMeshConVerts[i * 2 + 1];

					offMeshCons[n].radius = parameters.offMeshConRadii[i];
					offMeshCons[n].flags = (parameters.offMeshConDir[i] != 0) ? PathfinderCommon.OFFMESH_CON_BIDIR : 0;
					offMeshCons[n].side = offMeshConClass[i * 2 + 1];
					if (parameters.offMeshConUserID.Length != 0)
						offMeshCons[n].userId = parameters.offMeshConUserID[i];

					n++;
				}
			}
		}

		public int ClassifyOffMeshPoint(Vector3 pt, BBox3 bounds)
		{
			const int XP = 1 << 0; //x plus
			const int ZP = 1 << 1; //z plus 
			const int XM = 1 << 2; //x minus
			const int ZM = 1 << 3; //z minus

			int outcode = 0;
			outcode |= (pt.X >= bounds.Max.X) ? XP : 0;
			outcode |= (pt.Z >= bounds.Max.Z) ? ZP : 0;
			outcode |= (pt.X < bounds.Min.X) ? XM : 0;
			outcode |= (pt.Z < bounds.Min.Z) ? ZM : 0;

			switch (outcode)
			{
				case XP:
					return 0;

				case XP | ZP:
					return 1;

				case ZP:
					return 2;

				case XM | ZP:
					return 3;

				case XM:
					return 4;

				case XM | ZM:
					return 5;

				case ZM:
					return 6;

				case XP | ZM:
					return 7;
			}

			return 0xff;
		}

		public int CreateBVTree(Vector3[] verts, PolyMesh.Polygon[] polys, int npolys, int nvp, float cellSize, float cellHeight, PathfinderCommon.BVNode[] nodes)
		{
			//build bounding volume tree
			PathfinderCommon.BVNode[] items = new PathfinderCommon.BVNode[npolys];
			for (int i = 0; i < npolys; i++)
			{
				items[i].index = i;

				//calcuate polygon bounds
				items[i].bounds.Min = items[i].bounds.Max = verts[polys[i].Vertices[0]];

				for (int j = 1; j < nvp; j++)
				{
					if (polys[i].Vertices[j] == PolyMesh.MESH_NULL_IDX)
						break;

					Vector3 v = verts[polys[i].Vertices[j]];
					float x = v.X, y = v.Y, z = v.Z;

					if (x < items[i].bounds.Min.X) items[i].bounds.Min.X = x;
					if (y < items[i].bounds.Min.Y) items[i].bounds.Min.Y = y;
					if (z < items[i].bounds.Min.Z) items[i].bounds.Min.Z = z;

					if (x > items[i].bounds.Max.X) items[i].bounds.Max.X = x;
					if (y > items[i].bounds.Max.Y) items[i].bounds.Max.Y = y;
					if (z > items[i].bounds.Max.Z) items[i].bounds.Max.Z = z;

					//remap y
					items[i].bounds.Min.Y = (int)Math.Floor((float)items[i].bounds.Min.Y * cellHeight / cellSize);
					items[i].bounds.Max.Y = (int)Math.Ceiling((float)items[i].bounds.Max.Y * cellHeight / cellSize);
				}
			}

			int curNode = 0;
			Subdivide(items, npolys, 0, npolys, ref curNode, nodes);

			return curNode;
		}

		public void Subdivide(PathfinderCommon.BVNode[] items, int nitems, int imin, int imax, ref int curNode, PathfinderCommon.BVNode[] nodes)
		{
			int inum = imax - imin;
			int icur = curNode;

			int oldNode = curNode;
			curNode++;

			if (inum == 1)
			{
				//leaf
				nodes[oldNode].bounds = items[imin].bounds;
				nodes[oldNode].index = items[imin].index;
			}
			else
			{
				//split
				CalcExtends(items, imin, imax, ref nodes[oldNode].bounds);

				BBox3 b = nodes[oldNode].bounds;
				int axis = LongestAxis((int)(b.Max.X - b.Min.X), (int)(b.Max.Y - b.Min.Y), (int)(b.Max.Z - b.Min.Z));

				if (axis == 0)
				{
					//sort along x-axis
					CompareItemX compX = new CompareItemX();
					Array.Sort(items, imin, inum, compX);
				}
				else if (axis == 1)
				{
					//sort along y-axis
					CompareItemY compY = new CompareItemY();
					Array.Sort(items, imin, inum, compY);
				}
				else if (axis == 2)
				{
					CompareItemZ compZ = new CompareItemZ();
					Array.Sort(items, imin, inum, compZ);
				}

				int isplit = imin + inum / 2;

				//left 
				Subdivide(items, nitems, imin, isplit, ref curNode, nodes);

				//right
				Subdivide(items, nitems, isplit, imax, ref curNode, nodes);

				int iescape = curNode - icur;
				nodes[oldNode].index = -iescape; //negative index means escape
			}
		}

		public void CalcExtends(PathfinderCommon.BVNode[] items, int imin, int imax, ref BBox3 bounds)
		{
			bounds = items[imin].bounds;

			for (int i = imin + 1; i < imax; i++)
			{
				if (items[i].bounds.Min.X < bounds.Min.X) bounds.Min.X = items[i].bounds.Min.X;
				if (items[i].bounds.Min.Y < bounds.Min.Y) bounds.Min.Y = items[i].bounds.Min.Y;
				if (items[i].bounds.Min.Z < bounds.Min.Z) bounds.Min.Z = items[i].bounds.Min.Z;

				if (items[i].bounds.Max.X > bounds.Max.X) bounds.Max.X = items[i].bounds.Max.X;
				if (items[i].bounds.Max.Y > bounds.Max.Y) bounds.Max.Y = items[i].bounds.Max.Y;
				if (items[i].bounds.Max.Z > bounds.Max.Z) bounds.Max.Z = items[i].bounds.Max.Z;
			}
		}

		public int LongestAxis(int x, int y, int z)
		{
			int axis = 0;
			int maxVal = x;

			if (y > maxVal)
			{
				axis = 1;
				maxVal = y;
			}

			if (z > maxVal)
			{
				axis = 2;
				maxVal = z;
			}

			return axis;
		}

		private class CompareItemX : IComparer<PathfinderCommon.BVNode>
		{
			public int Compare(PathfinderCommon.BVNode a, PathfinderCommon.BVNode b)
			{
				if (a.bounds.Min.X < b.bounds.Min.X)
					return -1;

				if (a.bounds.Min.X > b.bounds.Min.X)
					return 1;

				return 0;
			}
		}

		private class CompareItemY : IComparer<PathfinderCommon.BVNode>
		{
			public int Compare(PathfinderCommon.BVNode a, PathfinderCommon.BVNode b)
			{
				if (a.bounds.Min.Y < b.bounds.Min.Y)
					return -1;

				if (a.bounds.Min.Y > b.bounds.Min.Y)
					return 1;

				return 0;
			}
		}

		private class CompareItemZ : IComparer<PathfinderCommon.BVNode>
		{
			public int Compare(PathfinderCommon.BVNode a, PathfinderCommon.BVNode b)
			{
				if (a.bounds.Min.Z < b.bounds.Min.Z)
					return -1;

				if (a.bounds.Min.Z > b.bounds.Min.Z)
					return 1;

				return 0;
			}
		}
	}
}
