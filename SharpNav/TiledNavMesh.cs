#region License
/**
 * Copyright (c) 2013-2014 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
 * Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE
 */
#endregion

using System;
using System.Collections.Generic;

using SharpNav.Geometry;
using SharpNav.Pathfinding;

#if MONOGAME || XNA
using Microsoft.Xna.Framework;
#elif OPENTK
using OpenTK;
#elif SHARPDX
using SharpDX;
#endif

namespace SharpNav
{
	public class TiledNavMesh
	{
		private TiledNavMeshParams parameters;
		private Vector3 origin;
		private float tileWidth, tileHeight;
		private int maxTiles;
		private int tileLookupTableSize; //tile hash lookup size
		private int tileLookupTableMask; //tile hash lookup mask

		private MeshTile[] posLookup; //tile hash lookup
		private MeshTile nextFree; //freelist of tiles
		private MeshTile[] tiles; //list of tiles

		private int saltBits; //number of salt bits in ID
		private int tileBits; //number of tile bits in ID
		private int polyBits; //number of poly bits in ID

		public int GetReference(int polyBase, int poly)
		{
			return polyBase | poly;
		}

		public struct TiledNavMeshParams
		{
			public Vector3 origin;
			public float tileWidth;
			public float tileHeight;
			public int maxTiles;
			public int maxPolys;
		}

		public TiledNavMesh(NavMeshBuilder data)
		{
			//if (data.Header.magic != PathfinderCommon.NAVMESH_MAGIC) //TODO: output error message?
			//	return;

			//if (data.Header.version != PathfinderCommon.NAVMESH_VERSION) //TODO: output error message?
			//	return;

			TiledNavMeshParams parameters;
			parameters.origin = data.Header.bounds.Min;
			parameters.tileWidth = data.Header.bounds.Max.X - data.Header.bounds.Min.X;
			parameters.tileHeight = data.Header.bounds.Max.Z - data.Header.bounds.Min.Z;
			parameters.maxTiles = 1;
			parameters.maxPolys = data.Header.polyCount;

			if (!InitTileNavMesh(parameters))
				return;

			int tileRef = 0;
			AddTile(data, 0, ref tileRef);
		}

		public MeshTile this[int index]
		{
			get
			{
				return tiles[index];
			}
		}

		public int TileCount { get { return maxTiles; } }

		public bool InitTileNavMesh(TiledNavMeshParams parameters)
		{
			this.parameters = parameters;
			origin = parameters.origin;
			tileWidth = parameters.tileWidth;
			tileHeight = parameters.tileHeight;

			//init tiles
			maxTiles = parameters.maxTiles;
			tileLookupTableSize = MathHelper.NextPowerOfTwo(parameters.maxTiles / 4);
			if (tileLookupTableSize == 0)
				tileLookupTableSize = 1;
			tileLookupTableMask = tileLookupTableSize - 1;

			tiles = new MeshTile[maxTiles];
			posLookup = new MeshTile[tileLookupTableSize];
			for (int i = 0; i < tiles.Length; i++)
				tiles[i] = new MeshTile();
			for (int i = 0; i < posLookup.Length; i++)
				posLookup[i] = null;

			//create a linked list of tiles
			nextFree = null;
			for (int i = maxTiles - 1; i >= 0; i--)
			{
				tiles[i].salt = 1;
				tiles[i].next = nextFree;
				nextFree = tiles[i];
			}
			
			//init ID generator values
			tileBits = MathHelper.Log2(MathHelper.NextPowerOfTwo(parameters.maxTiles));
			polyBits = MathHelper.Log2(MathHelper.NextPowerOfTwo(parameters.maxPolys));

			//only allow 31 salt bits, since salt mask is calculated using 32-bit int and it will overflow
			saltBits = Math.Min(31, 32 - tileBits - polyBits);
			if (saltBits < 10)
				return false;

			return true;
		}

		public void AddTile(NavMeshBuilder data, int lastRef, ref int result)
		{
			//make sure data is in right format
			PathfinderCommon.MeshHeader header = data.Header;
			//if (header.magic != PathfinderCommon.NAVMESH_MAGIC)
			//	return;
			//if (header.version != PathfinderCommon.NAVMESH_VERSION)
			//	return;

			//make sure location is free
			if (GetTileAt(header.x, header.y, header.layer) != null)
				return;

			//allocate a tile
			MeshTile tile = null;
			if (lastRef == 0)
			{
				if (nextFree != null)
				{
					tile = nextFree;
					nextFree = tile.next;
					tile.next = null;
				}
			}
			else
			{
				//try to relocate tile to specific index with the same salt
				int tileIndex = DecodePolyIdTile(lastRef);
				if (tileIndex >= maxTiles)
					return;

				//try to find specific tile id from free list
				MeshTile target = tiles[tileIndex];
				MeshTile prev = null;
				tile = nextFree;
				while (tile != null && tile != target)
				{
					prev = tile;
					tile = tile.next;
				}

				//couldn't find correct location
				if (tile != target)
					return;

				//remove from freelist
				if (prev == null)
					nextFree = tile.next;
				else
					prev.next = tile.next;

				//restore salt
				tile.salt = DecodePolyIdSalt(lastRef);
			}

			//make sure we could allocate a tile
			if (tile == null)
				return;

			//insert tile into position LookUp Table (lut)
			int h = ComputeTileHash(header.x, header.y, tileLookupTableMask);
			tile.next = posLookup[h];
			posLookup[h] = tile;

			if (header.bvNodeCount == 0)
				tile.bvTree = null;

			//patch header
			tile.verts = data.NavVerts;
			tile.polys = data.NavPolys;
			tile.detailMeshes = data.NavDMeshes;
			tile.detailVerts = data.NavDVerts;
			tile.detailTris = data.NavDTris;
			tile.bvTree = data.NavBvTree;
			tile.offMeshCons = data.OffMeshCons;

			//build links freelist
			tile.linksFreeList = 0;
			tile.links = new Link[header.maxLinkCount];
			for (int i = 0; i < header.maxLinkCount; i++)
				tile.links[i] = new Link();

			tile.links[header.maxLinkCount - 1].next = PathfinderCommon.NULL_LINK;
			for (int i = 0; i < header.maxLinkCount - 1; i++)
				tile.links[i].next = i + 1;

			//init tile
			tile.header = header;
			tile.data = data;

			ConnectIntLinks(ref tile);
			BaseOffMeshLinks(ref tile);

			//create connections with neighbor tiles
			const int MAX_NEIS = 32;
			MeshTile[] neis = new MeshTile[MAX_NEIS];
			int nneis;

			//connect with layers in current tile
			nneis = GetTilesAt(header.x, header.y, neis, MAX_NEIS);
			for (int j = 0; j < nneis; j++)
			{
				if (neis[j] != tile)
				{
					ConnectExtLinks(ref tile, ref neis[j], -1);
					ConnectExtLinks(ref neis[j], ref tile, -1);
				}

				ConnectExtOffMeshLinks(ref tile, ref neis[j], -1);
				ConnectExtOffMeshLinks(ref neis[j], ref tile, -1);
			}

			//connect with neighbour tiles
			for (int i = 0; i < 8; i++)
			{
				nneis = GetNeighbourTilesAt(header.x, header.y, i, neis, MAX_NEIS);
				for (int j = 0; j < nneis; j++)
				{
					ConnectExtLinks(ref tile, ref neis[j], i);
					ConnectExtLinks(ref neis[j], ref tile, OppositeTile(i));
					ConnectExtOffMeshLinks(ref tile, ref neis[j], i);
					ConnectExtOffMeshLinks(ref neis[j], ref tile, OppositeTile(i));
				}
			}

			result = GetTileRef(tile);
		}

		/// <summary>
		/// Allocate links for each of the tile's polygons' vertices
		/// </summary>
		/// <param name="tile"></param>
		public void ConnectIntLinks(ref MeshTile tile)
		{
			if (tile == null)
				return;

			int polyBase = GetPolyRefBase(tile);

			for (int i = 0; i < tile.header.polyCount; i++)
			{
				tile.polys[i].firstLink = PathfinderCommon.NULL_LINK;

				if (tile.polys[i].PolyType == PolygonType.OffMeshConnection)
					continue;

				//build edge links backwards so that the links will be in the linked list
				//from lowest index to highest
				for (int j = tile.polys[i].vertCount - 1; j >= 0; j--)
				{
					//skip hard and non-internal edges
					if (tile.polys[i].neis[j] == 0 || (tile.polys[i].neis[j] & PathfinderCommon.EXT_LINK) != 0)
						continue;

					int idx = AllocLink(tile);
					if (idx != PathfinderCommon.NULL_LINK)
					{
						tile.links[idx].reference = GetReference(polyBase, tile.polys[i].neis[j] - 1);
						tile.links[idx].edge = j;
						tile.links[idx].side = 0xff;
						tile.links[idx].bmin = tile.links[idx].bmax = 0;

						//add to linked list
						tile.links[idx].next = tile.polys[i].firstLink;
						tile.polys[i].firstLink = idx;
					}
				}
			}
		}

		public void BaseOffMeshLinks(ref MeshTile tile)
		{
			if (tile == null)
				return;

			int polyBase = GetPolyRefBase(tile);

			//base off-mesh connection start points
			for (int i = 0; i < tile.header.offMeshConCount; i++)
			{
				int con = i;
				int poly = tile.offMeshCons[con].poly;

				Vector3 ext = new Vector3(tile.offMeshCons[con].radius, tile.header.walkableClimb, tile.offMeshCons[con].radius);
				
				//find polygon to connect to
				Vector3 p = tile.offMeshCons[con].pos0;
				Vector3 nearestPt = new Vector3();
				int reference = FindNearestPolyInTile(tile, p, ext, ref nearestPt);
				if (reference == 0)
					continue;

				//do extra checks
				if ((nearestPt.X - p.X) * (nearestPt.X - p.X) + (nearestPt.Z - p.Z) * (nearestPt.Z - p.Z) >
					tile.offMeshCons[con].radius * tile.offMeshCons[con].radius)
					continue;

				//make sure location is on current mesh
				tile.verts[tile.polys[poly].verts[0]] = nearestPt;

				//link off-mesh connection to target poly
				int idx = AllocLink(tile);
				if (idx != PathfinderCommon.NULL_LINK)
				{
					tile.links[idx].reference = reference;
					tile.links[idx].edge = 0;
					tile.links[idx].side = 0xff;
					tile.links[idx].bmin = tile.links[idx].bmax = 0;

					//add to linked list
					tile.links[idx].next = tile.polys[poly].firstLink;
					tile.polys[poly].firstLink = idx;
				}

				//start end-point always conects back to off-mesh connection
				int tidx = AllocLink(tile);
				if (tidx != PathfinderCommon.NULL_LINK)
				{
					int landPolyIdx = DecodePolyIdPoly(reference);
					tile.links[idx].reference = GetReference(polyBase, tile.offMeshCons[con].poly);
					tile.links[idx].edge = 0xff;
					tile.links[idx].side = 0xff;
					tile.links[idx].bmin = tile.links[idx].bmax = 0;

					//add to linked list
					tile.links[idx].next = tile.polys[landPolyIdx].firstLink;
					tile.polys[landPolyIdx].firstLink = tidx;
				}
			}
		}

		public void ConnectExtLinks(ref MeshTile tile, ref MeshTile target, int side)
		{
			if (tile == null)
				return;

			//connect border links
			for (int i = 0; i < tile.header.polyCount; i++)
			{
				int nv = tile.polys[i].vertCount;

				for (int j = 0; j < nv; j++)
				{
					//skip non-portal edges
					if ((tile.polys[i].neis[j] & PathfinderCommon.EXT_LINK) == 0)
						continue;

					int dir = tile.polys[i].neis[j] & 0xff;
					if (side != -1 && dir != side)
						continue;

					//create new links
					Vector3 va = tile.verts[tile.polys[i].verts[j]];
					Vector3 vb = tile.verts[tile.polys[i].verts[(j + 1) % nv]];
					int[] nei = new int[4];
					float[] neia = new float[4 * 2];
					int nnei = FindConnectingPolys(va, vb, target, OppositeTile(dir), nei, neia, 4);

					for (int k = 0; k < nnei; k++)
					{
						int idx = AllocLink(tile);

						if (idx != PathfinderCommon.NULL_LINK)
						{
							tile.links[idx].reference = nei[k];
							tile.links[idx].edge = j;
							tile.links[idx].side = dir;

							tile.links[idx].next = tile.polys[i].firstLink;
							tile.polys[i].firstLink = idx;

							//compress portal limits to a value
							if (dir == 0 || dir == 4)
							{
								float tmin = (neia[k * 2 + 0] - va.Z) / (vb.Z - va.Z);
								float tmax = (neia[k * 2 + 1] - va.Z) / (vb.Z - va.Z);

								if (tmin > tmax)
								{
									float temp = tmin;
									tmin = tmax;
									tmax = temp;
								}

								tile.links[idx].bmin = (int)(MathHelper.Clamp(tmin, 0.0f, 1.0f) * 255.0f);
								tile.links[idx].bmax = (int)(MathHelper.Clamp(tmax, 0.0f, 1.0f) * 255.0f);
							}
							else if (dir == 2 || dir == 6)
							{
								float tmin = (neia[k * 2 + 0] - va.X) / (vb.X - va.X);
								float tmax = (neia[k * 2 + 1] - va.X) / (vb.X - va.X);

								if (tmin > tmax)
								{
									float temp = tmin;
									tmin = tmax;
									tmax = temp;
								}

								tile.links[idx].bmin = (int)(MathHelper.Clamp(tmin, 0.0f, 1.0f) * 255.0f);
								tile.links[idx].bmax = (int)(MathHelper.Clamp(tmax, 0.0f, 1.0f) * 255.0f);
							}
						}
					}
				}
			}
		}

		public void ConnectExtOffMeshLinks(ref MeshTile tile, ref MeshTile target, int side)
		{
			if (tile == null)
				return;

			//connect off-mesh links, specifically links which land from target tile to this tile
			int oppositeSide = (side == -1) ? 0xff : OppositeTile(side);

			for (int i = 0; i < target.header.offMeshConCount; i++)
			{
				OffMeshConnection targetCon = target.offMeshCons[i];
				if (targetCon.side != oppositeSide)
					continue;

				Poly targetPoly = target.polys[targetCon.poly];

				//skip off-mesh connections which start location could not be connected at all
				if (targetPoly.firstLink == PathfinderCommon.NULL_LINK)
					continue;

				Vector3 ext = new Vector3(targetCon.radius, target.header.walkableClimb, targetCon.radius);

				//find polygon to connect to
				Vector3 p = targetCon.pos1;
				Vector3 nearestPt = new Vector3();
				int reference = FindNearestPolyInTile(tile, p, ext, ref nearestPt);
				if (reference == 0)
					continue;

				//further check
				if ((nearestPt.X - p.X) * (nearestPt.X - p.X) + (nearestPt.Z - p.Z) * (nearestPt.Z - p.Z) >
					(targetCon.radius * targetCon.radius))
					continue;

				//make sure the location is on the current mesh
				target.verts[targetPoly.verts[1]] = nearestPt;

				//link off-mesh connection to target poly
				int idx = AllocLink(target);
				if (idx != PathfinderCommon.NULL_LINK)
				{
					target.links[idx].reference = reference;
					target.links[idx].edge = i;
					target.links[idx].side = oppositeSide;
					target.links[idx].bmin = target.links[idx].bmax = 0;

					//add to linked list
					target.links[idx].next = target.polys[i].firstLink;
					target.polys[i].firstLink = idx;
				}

				//link target poly to off-mesh connection
				if ((targetCon.flags & PathfinderCommon.OFFMESH_CON_BIDIR) != 0)
				{
					int tidx = AllocLink(tile);
					if (tidx != PathfinderCommon.NULL_LINK)
					{
						int landPolyIdx = DecodePolyIdPoly(reference);
						tile.links[tidx].reference = GetReference(GetPolyRefBase(target), targetCon.poly);
						tile.links[tidx].edge = 0xff;
						tile.links[tidx].side = (side == -1) ? 0xff : side;
						tile.links[tidx].bmin = tile.links[tidx].bmax = 0;

						//add to linked list
						tile.links[tidx].next = tile.polys[landPolyIdx].firstLink;
						tile.polys[landPolyIdx].firstLink = tidx;
					}
				}
			}
		}

		public int OppositeTile(int side)
		{
			return (side + 4) % 8;
		}

		public int FindConnectingPolys(Vector3 va, Vector3 vb, MeshTile tile, int side, int[] con, float[] conarea, int maxcon)
		{
			if (tile == null)
				return 0;

			float[] amin = new float[2];
			float[] amax = new float[2];
			CalcSlabEndPoints(va, vb, amin, amax, side);
			float apos = GetSlabCoord(va, side);

			//remove links pointing to 'side' and compact the links array
			float[] bmin = new float[2];
			float[] bmax = new float[2];
			int m = PathfinderCommon.EXT_LINK | side;
			int n = 0;

			int polyBase = GetPolyRefBase(tile);

			for (int i = 0; i < tile.header.polyCount; i++)
			{
				int nv = tile.polys[i].vertCount;

				for (int j = 0; j < nv; j++)
				{
					//skip edges which do not point to the right side
					if (tile.polys[i].neis[j] != m)
						continue;

					Vector3 vc = tile.verts[tile.polys[i].verts[j]];
					Vector3 vd = tile.verts[tile.polys[i].verts[(j + 1) % nv]];
					float bpos = GetSlabCoord(vc, side);

					//segments are not close enough
					if (Math.Abs(apos - bpos) > 0.01f)
						continue;

					//check if the segments touch
					CalcSlabEndPoints(vc, vd, bmin, bmax, side);

					if (!OverlapSlabs(amin, amax, bmin, bmax, 0.01f, tile.header.walkableClimb))
						continue;

					//add return value
					if (n < maxcon)
					{
						conarea[n * 2 + 0] = Math.Max(amin[0], bmin[0]);
						conarea[n * 2 + 1] = Math.Min(amax[0], bmax[0]);
						con[n] = GetReference(polyBase, i);
						n++;
					}

					break;
				}
			}

			return n;
		}

		public void CalcSlabEndPoints(Vector3 va, Vector3 vb, float[] bmin, float[] bmax, int side)
		{
			if (side == 0 || side == 4)
			{
				if (va.Z < vb.Z)
				{
					bmin[0] = va.Z;
					bmin[1] = va.Y;
					
					bmax[0] = vb.Z;
					bmax[1] = vb.Y;
				}
				else
				{
					bmin[0] = vb.Z;
					bmin[1] = vb.Y;
					
					bmax[0] = va.Z;
					bmax[1] = va.Y;
				}
			}
			else if (side == 2 || side == 6)
			{
				if (va.X < vb.X)
				{
					bmin[0] = va.X;
					bmin[1] = va.Y;
					
					bmax[0] = vb.X;
					bmax[1] = vb.Y;
				}
				else
				{
					bmin[0] = vb.X;
					bmin[1] = vb.Y;
					
					bmax[0] = va.X;
					bmax[1] = va.Y;
				}
			}
		}

		public float GetSlabCoord(Vector3 va, int side)
		{
			if (side == 0 || side == 4)
				return va.X;
			else if (side == 2 || side == 6)
				return va.Z;
			
			return 0;
		}

		public bool OverlapSlabs(float[] amin, float[] amax, float[] bmin, float[] bmax, float px, float py)
		{
			//check for horizontal overlap
			//segment shrunk a little so that slabs which touch at endpoints aren't connected
			float minx = Math.Max(amin[0] + px, bmin[0] + px);
			float maxx = Math.Min(amax[0] - px, bmax[0] - px);
			if (minx > maxx)
				return false;

			//check vertical overlap
			float ad = (amax[1] - amin[1]) / (amax[0] - amin[0]);
			float ak = amin[1] - ad * amin[0];
			float bd = (bmax[1] - bmin[1]) / (bmax[0] - bmin[0]);
			float bk = bmin[1] - bd * bmin[0];
			float aminy = ad * minx + ak;
			float amaxy = ad * maxx + ak;
			float bminy = bd * minx + bk;
			float bmaxy = bd * maxx + bk;
			float dmin = bminy - aminy;
			float dmax = bmaxy - amaxy;

			//crossing segments always overlap
			if (dmin * dmax < 0)
				return true;

			//check for overlap at endpoints
			float thr = (py * 2) * (py * 2);
			if (dmin * dmin <= thr || dmax * dmax <= thr)
				return true;

			return false;
		}

		public int FindNearestPolyInTile(MeshTile tile, Vector3 center, Vector3 extents, ref Vector3 nearestPt)
		{
			BBox3 bounds;
			bounds.Min = center - extents;
			bounds.Max = center + extents;

			//get nearby polygons from proximity grid
			int[] polys = new int[128];
			int polyCount = QueryPolygonsInTile(tile, bounds, polys, 128);

			//find nearest polygon amongst the nearby polygons
			int nearest = 0;
			float nearestDistanceSqr = float.MaxValue;

			for (int i = 0; i < polyCount; i++)
			{
				int reference = polys[i];
				Vector3 closestPtPoly = new Vector3();
				PathfinderCommon.ClosestPointOnPolyInTile(tile, DecodePolyIdPoly(reference), center, ref closestPtPoly);
				float d = (center - closestPtPoly).LengthSquared();
				if (d < nearestDistanceSqr)
				{
					nearestPt = closestPtPoly;
					nearestDistanceSqr = d;
					nearest = reference;
				}
			}

			return nearest;
		}

		public int QueryPolygonsInTile(MeshTile tile, BBox3 qbounds, int[] polys, int maxPolys)
		{
			if (tile.bvTree.Length != 0)
			{
				int node = 0;
				int end = tile.header.bvNodeCount;
				Vector3 tbmin = tile.header.bounds.Min;
				Vector3 tbmax = tile.header.bounds.Max;
				float qfac = tile.header.bvQuantFactor;

				//calculate quantized box
				Vector3 bmin = new Vector3();
				Vector3 bmax = new Vector3();
				
				//Clamp query box to world box
				float minx = MathHelper.Clamp(qbounds.Min.X, tbmin.X, tbmax.X) - tbmin.X;
				float miny = MathHelper.Clamp(qbounds.Min.Y, tbmin.Y, tbmax.Y) - tbmin.Y;
				float minz = MathHelper.Clamp(qbounds.Min.Z, tbmin.Z, tbmax.Z) - tbmin.Z;
				float maxx = MathHelper.Clamp(qbounds.Max.X, tbmin.X, tbmax.X) - tbmin.X;
				float maxy = MathHelper.Clamp(qbounds.Max.Y, tbmin.Y, tbmax.Y) - tbmin.Y;
				float maxz = MathHelper.Clamp(qbounds.Max.Z, tbmin.Z, tbmax.Z) - tbmin.Z;

				//quantize
				bmin.X = (int)(qfac * minx) & 0xfffe;
				bmin.Y = (int)(qfac * miny) & 0xfffe;
				bmin.Z = (int)(qfac * minz) & 0xfffe;
				bmax.X = (int)(qfac * maxx + 1) | 1;
				bmax.Y = (int)(qfac * maxy + 1) | 1;
				bmax.Z = (int)(qfac * maxz + 1) | 1;

				//traverse tree
				int polyBase = GetPolyRefBase(tile);
				int n = 0;
				
				while (node < end)
				{
					bool overlap = PathfinderCommon.OverlapQuantBounds(bmin, bmax, tile.bvTree[node].bounds.Min, tile.bvTree[node].bounds.Max);
					bool isLeafNode = tile.bvTree[node].index >= 0;

					if (isLeafNode && overlap)
					{
						if (n < maxPolys)
							polys[n++] = GetReference(polyBase, tile.bvTree[node].index);
					}

					if (overlap || isLeafNode)
					{
						node++;
					}
					else
					{
						int escapeIndex = -tile.bvTree[node].index;
						node += escapeIndex;
					}
				}

				return n;
			}
			else
			{
				Vector3 bmin = new Vector3();
				Vector3 bmax = new Vector3();
				int n = 0;
				int polyBase = GetPolyRefBase(tile);

				for (int i = 0; i < tile.header.polyCount; i++)
				{
					var poly = tile.polys[i];

					//don't return off-mesh connection polygons
					if (poly.PolyType == PolygonType.OffMeshConnection)
						continue;

					//calculate polygon bounds
					bmin = tile.verts[poly.verts[0]];
					bmax = bmin;

					for (int j = 1; j < poly.vertCount; j++)
					{
						int index = poly.verts[j];
						Vector3Extensions.ComponentMin(ref bmin, ref tile.verts[index], out bmin);
						Vector3Extensions.ComponentMax(ref bmax, ref tile.verts[index], out bmax);
					}

					if (PathfinderCommon.OverlapQuantBounds(qbounds.Min, qbounds.Max, bmin, bmax))
					{
						if (n < maxPolys)
							polys[n++] = GetReference(polyBase, i);
					}
				}

				return n;
			}
		}

		public int AllocLink(MeshTile tile)
		{
			if (tile.linksFreeList == PathfinderCommon.NULL_LINK)
				return PathfinderCommon.NULL_LINK;

			int link = tile.linksFreeList;
			tile.linksFreeList = tile.links[link].next;
			return link;
		}

		public int GetTileRef(MeshTile tile)
		{
			if (tile == null)
				return 0;

			int it = 0;
			for (int i = 0; i < tiles.Length; i++)
			{
				if (tiles[i] == tile)
				{
					it = i;
					break;
				}
			}

			return EncodePolyId(tile.salt, it, 0);
		}

		public MeshTile GetTileAt(int x, int y, int layer)
		{
			//find tile based off hash
			int h = ComputeTileHash(x, y, tileLookupTableMask);
			MeshTile tile = posLookup[h];
			
			while (tile != null)
			{
				if (tile.header != null && tile.header.x == x && tile.header.y == y && tile.header.layer == layer)
					return tile;

				tile = tile.next;
			}
			
			return null;
		}

		public int GetTilesAt(int x, int y, MeshTile[] tiles, int maxTiles)
		{
			int n = 0;

			//find tile based on hash
			int h = ComputeTileHash(x, y, tileLookupTableMask);
			MeshTile tile = posLookup[h];
			
			while (tile != null)
			{
				if (tile.header != null && tile.header.x == x && tile.header.y == y)
				{
					if (n < maxTiles)
						tiles[n++] = tile;
				}

				tile = tile.next;
			}

			return n;
		}

		public int GetNeighbourTilesAt(int x, int y, int side, MeshTile[] tiles, int maxTiles)
		{
			int nx = x, ny = y;
			switch (side)
			{
				case 0:
					nx++;
					break;

				case 1:
					nx++;
					ny++;
					break;

				case 2:
					ny++;
					break;

				case 3:
					nx--;
					ny++;
					break;

				case 4:
					nx--;
					break;

				case 5:
					nx--;
					ny--;
					break;

				case 6:
					ny--;
					break;

				case 7:
					nx++;
					ny--;
					break;
			}

			return GetTilesAt(nx, ny, tiles, maxTiles);
		}

		public int ComputeTileHash(int x, int y, int mask)
		{
			//choose large multiplicative constants which are primes
			uint h1 = 0x8da6b343;
			uint h2 = 0xd8163841;
			uint n = (uint)(h1 * x + h2 * y);
			return (int)(n & mask);
		}
		
		public int GetPolyRefBase(MeshTile tile)
		{
			if (tile == null)
				return 0;

			int it = 0;
			for (int i = 0; i < tiles.Length; i++)
			{
				if (tiles[i] == tile)
				{
					it = i;
					break;
				}
			}

			return EncodePolyId(tile.salt, it, 0);
		}

		public bool GetTileAndPolyByRef(int reference, ref MeshTile tile, ref Poly poly)
		{
			if (reference == 0)
				return false;

			int salt = 0, indexTile = 0, indexPoly = 0;
			DecodePolyId(reference, ref salt, ref indexTile, ref indexPoly);
			
			if (indexTile >= maxTiles)
				return false;

			if (tiles[indexTile].salt != salt || tiles[indexTile].header == null)
				return false;

			if (indexPoly >= tiles[indexTile].header.polyCount)
				return false;

			tile = tiles[indexTile];
			poly = tiles[indexTile].polys[indexPoly];
			return true;
		}

		/// <summary>
		/// Only use this function if it is known that the provided polygon reference is valid.
		/// </summary>
		public void GetTileAndPolyByRefUnsafe(int reference, ref MeshTile tile, ref Poly poly)
		{
			int salt = 0, indexTile = 0, indexPoly = 0;
			DecodePolyId(reference, ref salt, ref indexTile, ref indexPoly);
			tile = tiles[indexTile];
			poly = tiles[indexTile].polys[indexPoly];
		}

		public bool IsValidPolyRef(int reference)
		{
			if (reference == 0)
				return false;

			int salt = 0, indexTile = 0, indexPoly = 0;
			DecodePolyId(reference, ref salt, ref indexTile, ref indexPoly);

			if (indexTile >= maxTiles)
				return false;

			if (tiles[indexTile].salt != salt || tiles[indexTile].header == null)
				return false;

			if (indexPoly >= tiles[indexTile].header.polyCount)
				return false;

			return true;
		}

		//decode a standard polygon reference
		public void DecodePolyId(int reference, ref int salt, ref int indexTile, ref int indexPoly)
		{
			int saltMask = (1 << saltBits) - 1;
			int tileMask = (1 << tileBits) - 1;
			int polyMask = (1 << polyBits) - 1;
			salt = (reference >> (polyBits + tileBits)) & saltMask;
			indexTile = (reference >> polyBits) & tileMask;
			indexPoly = reference & polyMask;
		}

		//extract a tile's salt value from the specified polygon reference
		public int DecodePolyIdSalt(int reference)
		{
			int saltMask = (1 << saltBits) - 1;
			return (reference >> (polyBits + tileBits)) & saltMask;
		}

		//extract a tile's index from the specified polygon reference
		public int DecodePolyIdTile(int reference)
		{
			int tileMask = (1 << tileBits) - 1;
			return (reference >> polyBits) & tileMask;
		}

		//extract a polygon's index (within its tile) from the specified polygon reference
		public int DecodePolyIdPoly(int reference)
		{
			int polyMask = (1 << polyBits) - 1;
			return reference & polyMask;
		}

		//derive a standard polygon reference
		public int EncodePolyId(int salt, int indexTile, int indexPoly)
		{
			return (salt << (int)(polyBits + tileBits)) | (indexTile << (int)polyBits) | indexPoly;
		}
	}
}
