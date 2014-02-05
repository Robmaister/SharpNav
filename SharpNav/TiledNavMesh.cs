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

		public struct TiledNavMeshParams
		{
			public Vector3 origin;
			public float tileWidth;
			public float tileHeight;
			public int maxTiles;
			public int maxPolys;
		}

		/// <summary>
		/// Link all the polygons in the mesh together for pathfinding purposes.
		/// </summary>
		/// <param name="data">The Navigation Mesh data</param>
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

		/// <summary>
		/// Initialize the Tiled Navigation Mesh variables and arrays.
		/// </summary>
		/// <param name="parameters">Tiled Navigation Mesh attributes</param>
		/// <returns>True if initialization is successful</returns>
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

		/// <summary>
		/// Build a tile and link all the polygons togther, both internally and externally.
		/// Make sure to link off-mesh connections as well.
		/// </summary>
		/// <param name="data">Navigation Mesh data</param>
		/// <param name="lastRef">Last polygon reference</param>
		/// <param name="result">Last tile reference</param>
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

			SetNullLink(ref tile.links[header.maxLinkCount - 1].next);
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
		/// <param name="tile">A tile contains a set of polygons, which are linked to each other</param>
		public void ConnectIntLinks(ref MeshTile tile)
		{
			if (tile == null)
				return;

			int polyBase = GetPolyRefBase(tile);

			//Iterate through all the polygons
			for (int i = 0; i < tile.header.polyCount; i++)
			{
				//The polygon links will end in a null link
				SetNullLink(ref tile.polys[i].firstLink);

				//Avoid Off-Mesh Connection polygons
				if (tile.polys[i].PolyType == PolygonType.OffMeshConnection)
					continue;

				//Build edge links
				for (int j = tile.polys[i].vertCount - 1; j >= 0; j--)
				{
					//Skip hard and non-internal edges
					if (tile.polys[i].neis[j] == 0 || IsExternalLink(tile.polys[i].neis[j]))
						continue;

					//Allocate a new link if possible
					int idx = AllocLink(tile);

					//Allocation of link should be successful
					if (IsLinkAllocated(idx))
					{
						//Initialize a new link
						tile.links[idx].reference = GetReference(polyBase, tile.polys[i].neis[j] - 1);
						tile.links[idx].edge = j;
						tile.links[idx].side = 0xff;
						tile.links[idx].bmin = tile.links[idx].bmax = 0;

						//Add to polygon's links list
						tile.links[idx].next = tile.polys[i].firstLink;
						tile.polys[i].firstLink = idx;
					}
				}
			}
		}

		/// <summary>
		/// Begin creating off-mesh links between the tile polygons.
		/// </summary>
		/// <param name="tile">Current Tile</param>
		public void BaseOffMeshLinks(ref MeshTile tile)
		{
			if (tile == null)
				return;

			int polyBase = GetPolyRefBase(tile);

			//Base off-mesh connection start points
			for (int i = 0; i < tile.header.offMeshConCount; i++)
			{
				int con = i;
				int poly = tile.offMeshCons[con].poly;

				Vector3 extents = new Vector3(tile.offMeshCons[con].radius, tile.header.walkableClimb, tile.offMeshCons[con].radius);
				
				//Find polygon to connect to
				Vector3 p = tile.offMeshCons[con].pos0;
				Vector3 nearestPt = new Vector3();
				int reference = FindNearestPolyInTile(tile, p, extents, ref nearestPt);
				if (reference == 0)
					continue;

				//Do extra checks
				if ((nearestPt.X - p.X) * (nearestPt.X - p.X) + (nearestPt.Z - p.Z) * (nearestPt.Z - p.Z) >
					tile.offMeshCons[con].radius * tile.offMeshCons[con].radius)
					continue;

				//Make sure location is on current mesh
				tile.verts[tile.polys[poly].verts[0]] = nearestPt;

				//Link off-mesh connection to target poly
				int idx = AllocLink(tile);
				if (IsLinkAllocated(idx))
				{
					//Initialize a new link
					tile.links[idx].reference = reference;
					tile.links[idx].edge = 0;
					tile.links[idx].side = 0xff;
					tile.links[idx].bmin = tile.links[idx].bmax = 0;

					//Add to polygon's links list
					tile.links[idx].next = tile.polys[poly].firstLink;
					tile.polys[poly].firstLink = idx;
				}

				//Start end-point always conects back to off-mesh connection
				int tidx = AllocLink(tile);
				if (IsLinkAllocated(tidx))
				{
					//Initialize a new link
					int landPolyIdx = DecodePolyIdPoly(reference);
					tile.links[idx].reference = GetReference(polyBase, tile.offMeshCons[con].poly);
					tile.links[idx].edge = 0xff;
					tile.links[idx].side = 0xff;
					tile.links[idx].bmin = tile.links[idx].bmax = 0;

					//Add to polygon's links list
					tile.links[idx].next = tile.polys[landPolyIdx].firstLink;
					tile.polys[landPolyIdx].firstLink = tidx;
				}
			}
		}

		/// <summary>
		/// Connect polygons from two different tiles.
		/// </summary>
		/// <param name="tile">Current Tile</param>
		/// <param name="target">Target Tile</param>
		/// <param name="side">Polygon edge</param>
		public void ConnectExtLinks(ref MeshTile tile, ref MeshTile target, int side)
		{
			if (tile == null)
				return;

			//Connect border links
			for (int i = 0; i < tile.header.polyCount; i++)
			{
				int numPolyVerts = tile.polys[i].vertCount;

				for (int j = 0; j < numPolyVerts; j++)
				{
					//Skip non-portal edges
					if ((tile.polys[i].neis[j] & PathfinderCommon.EXT_LINK) == 0)
						continue;

					int dir = tile.polys[i].neis[j] & 0xff;
					if (side != -1 && dir != side)
						continue;

					//Create new links
					Vector3 va = tile.verts[tile.polys[i].verts[j]];
					Vector3 vb = tile.verts[tile.polys[i].verts[(j + 1) % numPolyVerts]];
					List<int> nei = new List<int>(4);
					List<float> neia = new List<float>(4 * 2);
					FindConnectingPolys(va, vb, target, OppositeTile(dir), nei, neia);

					//Iterate through neighbors
					for (int k = 0; k < nei.Count; k++)
					{
						//Allocate a new link if possible
						int idx = AllocLink(tile);

						if (IsLinkAllocated(idx))
						{
							tile.links[idx].reference = nei[k];
							tile.links[idx].edge = j;
							tile.links[idx].side = dir;

							tile.links[idx].next = tile.polys[i].firstLink;
							tile.polys[i].firstLink = idx;

							//Compress portal limits to a value
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

		/// <summary>
		/// Find the opposite side
		/// </summary>
		/// <param name="side">Current side</param>
		/// <returns>Opposite side</returns>
		public int OppositeTile(int side)
		{
			return (side + 4) % 8;
		}

		/// <summary>
		/// Connect Off-Mesh links between polygons from two different tiles.
		/// </summary>
		/// <param name="tile">Current Tile</param>
		/// <param name="target">Target Tile</param>
		/// <param name="side">Polygon edge</param>
		public void ConnectExtOffMeshLinks(ref MeshTile tile, ref MeshTile target, int side)
		{
			if (tile == null)
				return;

			//Connect off-mesh links, specifically links which land from target tile to this tile
			int oppositeSide = (side == -1) ? 0xff : OppositeTile(side);

			//Iterate through all the off-mesh connections of target tile
			for (int i = 0; i < target.header.offMeshConCount; i++)
			{
				OffMeshConnection targetCon = target.offMeshCons[i];
				if (targetCon.side != oppositeSide)
					continue;

				Poly targetPoly = target.polys[targetCon.poly];

				//Skip off-mesh connections which start location could not be connected at all
				if (!IsLinkAllocated(targetPoly.firstLink))
					continue;

				Vector3 extents = new Vector3(targetCon.radius, target.header.walkableClimb, targetCon.radius);

				//Find polygon to connect to
				Vector3 p = targetCon.pos1;
				Vector3 nearestPt = new Vector3();
				int reference = FindNearestPolyInTile(tile, p, extents, ref nearestPt);
				if (reference == 0)
					continue;

				//Further checks
				if ((nearestPt.X - p.X) * (nearestPt.X - p.X) + (nearestPt.Z - p.Z) * (nearestPt.Z - p.Z) >
					(targetCon.radius * targetCon.radius))
					continue;

				//Make sure the location is on the current mesh
				target.verts[targetPoly.verts[1]] = nearestPt;

				//Link off-mesh connection to target poly
				int idx = AllocLink(target);
				if (IsLinkAllocated(idx))
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
					if (IsLinkAllocated(tidx))
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

		/// <summary>
		/// Search for neighbor polygons in the tile.
		/// </summary>
		/// <param name="va">Vertex A</param>
		/// <param name="vb">Vertex B</param>
		/// <param name="tile">Current tile</param>
		/// <param name="side">Polygon edge</param>
		/// <param name="con">Resulting Connection polygon</param>
		/// <param name="conarea">Resulting Connection area</param>
		/// <param name="maxcon">Maximum number of connections</param>
		/// <returns>Number of neighboring polys</returns>
		public void FindConnectingPolys(Vector3 va, Vector3 vb, MeshTile tile, int side, List<int> con, List<float> conarea)
		{
			if (tile == null)
				return;

			Vector3 amin = new Vector3();
			Vector3 amax = new Vector3();
			CalcSlabEndPoints(va, vb, amin, amax, side);
			float apos = GetSlabCoord(va, side);

			//Remove links pointing to 'side' and compact the links array
			Vector3 bmin = new Vector3();
			Vector3 bmax = new Vector3();

			int polyBase = GetPolyRefBase(tile);

			//Iterate through all the tile's polygons
			for (int i = 0; i < tile.header.polyCount; i++)
			{
				int numPolyVerts = tile.polys[i].vertCount;

				//Iterate through all the vertices
				for (int j = 0; j < numPolyVerts; j++)
				{
					//Skip edges which do not point to the right side
					if (tile.polys[i].neis[j] != (PathfinderCommon.EXT_LINK | side))
						continue;

					//Grab two adjacent vertices
					Vector3 vc = tile.verts[tile.polys[i].verts[j]];
					Vector3 vd = tile.verts[tile.polys[i].verts[(j + 1) % numPolyVerts]];
					float bpos = GetSlabCoord(vc, side);

					//Segments are not close enough
					if (Math.Abs(apos - bpos) > 0.01f)
						continue;

					//Check if the segments touch
					CalcSlabEndPoints(vc, vd, bmin, bmax, side);

					//Skip if slabs don't overlap
					if (!OverlapSlabs(amin, amax, bmin, bmax, 0.01f, tile.header.walkableClimb))
						continue;

					//Add return value
					if (con.Count < con.Capacity)
					{
						conarea.Add(Math.Max(amin[0], bmin[0]));
						conarea.Add(Math.Min(amax[0], bmax[0]));
						con.Add(GetReference(polyBase, i));
					}

					break;
				}
			}
		}

		/// <summary>
		/// Find the slab endpoints based off of the 'side' value.
		/// </summary>
		/// <param name="va">Vertex A</param>
		/// <param name="vb">Vertex B</param>
		/// <param name="bmin">Minimum bounds</param>
		/// <param name="bmax">Maximum bounds</param>
		/// <param name="side">Side</param>
		public void CalcSlabEndPoints(Vector3 va, Vector3 vb, Vector3 bmin, Vector3 bmax, int side)
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

		/// <summary>
		/// Return the proper slab coordinate value depending on the 'side' value.
		/// </summary>
		/// <param name="va">Vertex A</param>
		/// <param name="side">Side</param>
		/// <returns>Slab coordinate value</returns>
		public float GetSlabCoord(Vector3 va, int side)
		{
			if (side == 0 || side == 4)
				return va.X;
			else if (side == 2 || side == 6)
				return va.Z;
			
			return 0;
		}

		/// <summary>
		/// Check if two slabs overlap.
		/// </summary>
		/// <param name="amin">Minimum bounds A</param>
		/// <param name="amax">Maximum bounds A</param>
		/// <param name="bmin">Minimum bounds B</param>
		/// <param name="bmax">Maximum bounds B</param>
		/// <param name="px">Point's x</param>
		/// <param name="py">Point's y</param>
		/// <returns>True if slabs overlap</returns>
		public bool OverlapSlabs(Vector3 amin, Vector3 amax, Vector3 bmin, Vector3 bmax, float px, float py)
		{
			//Check for horizontal overlap
			//Segment shrunk a little so that slabs which touch at endpoints aren't connected
			float minX = Math.Max(amin[0] + px, bmin[0] + px);
			float maxX = Math.Min(amax[0] - px, bmax[0] - px);
			if (minX > maxX)
				return false;

			//Check vertical overlap
			float aSlope = (amax[1] - amin[1]) / (amax[0] - amin[0]);
			float aConstant = amin[1] - aSlope * amin[0];
			float bSlope = (bmax[1] - bmin[1]) / (bmax[0] - bmin[0]);
			float bConstant = bmin[1] - bSlope * bmin[0];
			float aMinY = aSlope * minX + aConstant;
			float aMaxY = aSlope * maxX + aConstant;
			float bMinY = bSlope * minX + bConstant;
			float bMaxY = bSlope * maxX + bConstant;
			float dmin = bMinY - aMinY;
			float dmax = bMaxY - aMaxY;

			//Crossing segments always overlap
			if (dmin * dmax < 0)
				return true;

			//Check for overlap at endpoints
			float threshold = (py * 2) * (py * 2);
			if (dmin * dmin <= threshold || dmax * dmax <= threshold)
				return true;

			return false;
		}

		/// <summary>
		/// Find the closest polygon possible in the tile under certain constraints.
		/// </summary>
		/// <param name="tile">Current tile</param>
		/// <param name="center">Center starting point</param>
		/// <param name="extents">Range of search</param>
		/// <param name="nearestPt">Resulting nearest point</param>
		/// <returns>Polygon Reference which contains nearest point</returns>
		public int FindNearestPolyInTile(MeshTile tile, Vector3 center, Vector3 extents, ref Vector3 nearestPt)
		{
			BBox3 bounds;
			bounds.Min = center - extents;
			bounds.Max = center + extents;

			//Get nearby polygons from proximity grid
			List<int> polys = new List<int>(128);
			int polyCount = QueryPolygonsInTile(tile, bounds, polys);

			//Find nearest polygon amongst the nearby polygons
			int nearest = 0;
			float nearestDistanceSqr = float.MaxValue;

			//Iterate throuh all the polygons
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

		/// <summary>
		/// Find all the polygons within a certain bounding box.
		/// </summary>
		/// <param name="tile">Current tile</param>
		/// <param name="qbounds">Bounds</param>
		/// <param name="polys">List of polygons</param>
		/// <returns>Number of polygons found</returns>
		public int QueryPolygonsInTile(MeshTile tile, BBox3 qbounds, List<int> polys)
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
				
				while (node < end)
				{
					bool overlap = PathfinderCommon.OverlapQuantBounds(bmin, bmax, tile.bvTree[node].bounds.Min, tile.bvTree[node].bounds.Max);
					bool isLeafNode = tile.bvTree[node].index >= 0;

					if (isLeafNode && overlap)
					{
						if (polys.Count < polys.Capacity)
							polys.Add(GetReference(polyBase, tile.bvTree[node].index));
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

				return polys.Count;
			}
			else
			{
				Vector3 bmin = new Vector3();
				Vector3 bmax = new Vector3();
				int polyBase = GetPolyRefBase(tile);

				for (int i = 0; i < tile.header.polyCount; i++)
				{
					var poly = tile.polys[i];

					//don't return off-mesh connection polygons
					if (poly.PolyType == PolygonType.OffMeshConnection)
						continue;

					//calculate polygon bounds
					bmax = bmin = tile.verts[poly.verts[0]];
					for (int j = 1; j < poly.vertCount; j++)
					{
						int index = poly.verts[j];
						Vector3Extensions.ComponentMin(ref bmin, ref tile.verts[index], out bmin);
						Vector3Extensions.ComponentMax(ref bmax, ref tile.verts[index], out bmax);
					}

					if (PathfinderCommon.OverlapQuantBounds(qbounds.Min, qbounds.Max, bmin, bmax))
					{
						if (polys.Count < polys.Capacity)
							polys.Add(GetReference(polyBase, i));
					}
				}

				return polys.Count;
			}
		}

		/// <summary>
		/// Allocate a new link if possible.
		/// </summary>
		/// <param name="tile">Current tile</param>
		/// <returns>New link number</returns>
		public int AllocLink(MeshTile tile)
		{
			if (!IsLinkAllocated(tile.linksFreeList))
				return PathfinderCommon.NULL_LINK;

			int link = tile.linksFreeList;
			tile.linksFreeList = tile.links[link].next;
			return link;
		}

		/// <summary>
		/// Get the tile reference
		/// </summary>
		/// <param name="tile">Tile to look for</param>
		/// <returns>Tile reference</returns>
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

		/// <summary>
		/// Find the tile at a specific location
		/// </summary>
		/// <param name="x">X-coordinate</param>
		/// <param name="y">Y-coordinate</param>
		/// <param name="layer">Layer number</param>
		/// <returns></returns>
		public MeshTile GetTileAt(int x, int y, int layer)
		{
			//Find tile based off hash
			int h = ComputeTileHash(x, y, tileLookupTableMask);
			MeshTile tile = posLookup[h];
			
			while (tile != null)
			{
				//Found
				if (tile.header != null && tile.header.x == x && tile.header.y == y && tile.header.layer == layer)
					return tile;

				//Keep searching
				tile = tile.next;
			}
			
			return null;
		}

		/// <summary>
		/// Find and add a tile if it is found
		/// </summary>
		/// <param name="x">X-coordinate</param>
		/// <param name="y">Y-coordinate</param>
		/// <param name="tiles">Tile array</param>
		/// <param name="maxTiles">Maximum number of tiles allowed</param>
		/// <returns>Number of tiles satisfying condition</returns>
		public int GetTilesAt(int x, int y, MeshTile[] tiles, int maxTiles)
		{
			int n = 0;

			//Find tile based on hash
			int h = ComputeTileHash(x, y, tileLookupTableMask);
			MeshTile tile = posLookup[h];
			
			while (tile != null)
			{
				//Tile found. 
				//Add to tile array
				if (tile.header != null && tile.header.x == x && tile.header.y == y)
				{
					if (n < maxTiles)
						tiles[n++] = tile;
				}

				//Keep searching
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

		/// <summary>
		/// Compute the tile hash code, which can be used in a hash table for quick lookup.
		/// </summary>
		/// <param name="x">X-coordinate</param>
		/// <param name="y">Y-coordinate</param>
		/// <param name="mask">Mask</param>
		/// <returns>Tile hash code</returns>
		public int ComputeTileHash(int x, int y, int mask)
		{
			//choose large multiplicative constants which are primes
			uint h1 = 0x8da6b343;
			uint h2 = 0xd8163841;
			uint n = (uint)(h1 * x + h2 * y);
			return (int)(n & mask);
		}

		public int GetReference(int polyBase, int poly)
		{
			return polyBase | poly;
		}

		public void SetNullLink(ref int index)
		{
			index = PathfinderCommon.NULL_LINK;
		}

		public bool IsLinkAllocated(int index)
		{
			return index != PathfinderCommon.NULL_LINK;
		}

		public bool IsExternalLink(int neighbor)
		{
			return (neighbor & PathfinderCommon.EXT_LINK) != 0;
		}
		
		/// <summary>
		/// Get the base reference for the polygons in a tile.
		/// </summary>
		/// <param name="tile">Current Tile</param>
		/// <returns>Base poly reference</returns>
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

		/// <summary>
		/// Retrieve the tile and poly based off of a polygon reference
		/// </summary>
		/// <param name="reference">Polygon reference</param>
		/// <param name="tile">Resulting tile</param>
		/// <param name="poly">Resulting poly</param>
		/// <returns>True if tile and poly successfully retrieved</returns>
		public bool GetTileAndPolyByRef(int reference, ref MeshTile tile, ref Poly poly)
		{
			if (reference == 0)
				return false;

			//Get tile and poly indices
			int salt = 0, indexTile = 0, indexPoly = 0;
			DecodePolyId(reference, ref salt, ref indexTile, ref indexPoly);
			
			//Make sure indices are valid
			if (indexTile >= maxTiles)
				return false;

			if (tiles[indexTile].salt != salt || tiles[indexTile].header == null)
				return false;

			if (indexPoly >= tiles[indexTile].header.polyCount)
				return false;

			//Retrieve tile and poly
			tile = tiles[indexTile];
			poly = tiles[indexTile].polys[indexPoly];
			return true;
		}

		/// <summary>
		/// Only use this function if it is known that the provided polygon reference is valid.
		/// </summary>
		/// <param name="reference">Polygon reference</param>
		/// <param name="tile">Resulting tile</param>
		/// <param name="poly">Resulting poly</param>
		public void GetTileAndPolyByRefUnsafe(int reference, ref MeshTile tile, ref Poly poly)
		{
			int salt = 0, indexTile = 0, indexPoly = 0;
			DecodePolyId(reference, ref salt, ref indexTile, ref indexPoly);
			tile = tiles[indexTile];
			poly = tiles[indexTile].polys[indexPoly];
		}

		/// <summary>
		/// Check if polygon reference is valid.
		/// </summary>
		/// <param name="reference">Polygon reference</param>
		/// <returns>True if valid</returns>
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

		/// <summary>
		/// Decode a standard polygon reference 
		/// </summary>
		/// <param name="reference">Polygon reference</param>
		/// <param name="salt">Resulting salt value</param>
		/// <param name="indexTile">Resulting tile index</param>
		/// <param name="indexPoly">Resulting poly index</param>
		public void DecodePolyId(int reference, ref int salt, ref int indexTile, ref int indexPoly)
		{
			int saltMask = (1 << saltBits) - 1;
			int tileMask = (1 << tileBits) - 1;
			int polyMask = (1 << polyBits) - 1;
			salt = (reference >> (polyBits + tileBits)) & saltMask;
			indexTile = (reference >> polyBits) & tileMask;
			indexPoly = reference & polyMask;
		}

		/// <summary>
		/// Extract a tile's salt value from the specified polygon reference 
		/// </summary>
		/// <param name="reference">Polygon reference</param>
		/// <returns>Salt value</returns>
		public int DecodePolyIdSalt(int reference)
		{
			int saltMask = (1 << saltBits) - 1;
			return (reference >> (polyBits + tileBits)) & saltMask;
		}

		/// <summary>
		/// Extract a tile's index from the specified polygon reference
		/// </summary>
		/// <param name="reference">Polygon reference</param>
		/// <returns>Tile index</returns>
		public int DecodePolyIdTile(int reference)
		{
			int tileMask = (1 << tileBits) - 1;
			return (reference >> polyBits) & tileMask;
		}

		/// <summary>
		/// Extract a polygon's index (within its tile) from the specified polygon reference 
		/// </summary>
		/// <param name="reference">Polygon reference</param>
		/// <returns>Poly index</returns>
		public int DecodePolyIdPoly(int reference)
		{
			int polyMask = (1 << polyBits) - 1;
			return reference & polyMask;
		}

		/// <summary>
		/// Derive a standard polygon reference, which compresses salt, tile index, and poly index together
		/// </summary>
		/// <param name="salt">Salt value</param>
		/// <param name="indexTile">Tile index</param>
		/// <param name="indexPoly">Poly index</param>
		/// <returns>Polygon reference</returns>
		public int EncodePolyId(int salt, int indexTile, int indexPoly)
		{
			return (salt << (int)(polyBits + tileBits)) | (indexTile << (int)polyBits) | indexPoly;
		}
	}
}
