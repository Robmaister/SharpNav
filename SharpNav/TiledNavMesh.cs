#region License
/**
 * Copyright (c) 2013 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
 * Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE
 */
#endregion

using System;
using System.Collections.Generic;
using SharpNav.Geometry;

namespace SharpNav
{
	class TiledNavMesh
	{
		private PathfinderCommon.NavMeshParams m_params;
		private Vector3 m_origin;
		private float m_tileWidth, m_tileHeight;
		private int m_maxTiles;
		private int m_tileLutSize; //tile hash lookup size
		private int m_tileLutMask; //tile hash lookup mask

		private PathfinderCommon.MeshTile[] m_posLookup; //tile hash lookup
		private PathfinderCommon.MeshTile[] m_nextFree; //freelist of tiles
		private PathfinderCommon.MeshTile[] m_tiles; //list of tiles

		private uint m_saltBits; //number of salt bits in ID
		private uint m_tileBits; //number of tile bits in ID
		private uint m_polyBits; //number of poly bits in ID


		public TiledNavMesh(NavMeshBuilder data, int flags)
		{
			if (data.Header.magic != PathfinderCommon.NAVMESH_MAGIC) //TODO: output error message?
				return;

			if (data.Header.version != PathfinderCommon.NAVMESH_VERSION) //TODO: output error message?
				return;

			PathfinderCommon.NavMeshParams parameters;
			parameters.origin = data.Header.bounds.Min;
			parameters.tileWidth = data.Header.bounds.Max.X - data.Header.bounds.Min.X;
			parameters.tileHeight = data.Header.bounds.Max.Y - data.Header.bounds.Min.Y;
			parameters.maxTiles = 1;
			parameters.maxPolys = data.Header.polyCount;

			if (initTileNavMesh(parameters) == false)
				return;

			uint tileRef = 0;
			AddTile(data, flags, 0, ref tileRef);
		}

		public bool initTileNavMesh(PathfinderCommon.NavMeshParams parameters)
		{
			m_params = parameters;
			m_origin = parameters.origin;
			m_tileWidth = parameters.tileWidth;
			m_tileHeight = parameters.tileHeight;

			//init tiles
			m_maxTiles = parameters.maxTiles;
			m_tileLutSize = (int)NextPow2((uint)parameters.maxTiles / 4);
			if (m_tileLutSize == 0)
				m_tileLutSize = 1;
			m_tileLutMask = m_tileLutSize - 1;

			m_tiles = new PathfinderCommon.MeshTile[m_maxTiles];
			m_posLookup = new PathfinderCommon.MeshTile[m_tileLutSize];
			for (int i = 0; i < m_tiles.Length; i++)
				m_tiles[i] = null;
			for (int i = 0; i < m_posLookup.Length; i++)
				m_posLookup[i] = null;

			//create a linked list of tiles
			m_nextFree = new PathfinderCommon.MeshTile[m_maxTiles];
			m_nextFree[0] = null;
			for (int i = m_maxTiles - 1; i >= 0; i--)
			{
				int endIndex = m_nextFree.Length - 1;
				m_tiles[i].salt = 1;
				m_tiles[i].next = m_nextFree[endIndex];
				m_nextFree[endIndex] = m_tiles[i];
			}

			//init ID generator values
			m_tileBits = Ilog2(NextPow2((uint)parameters.maxTiles));
			m_polyBits = Ilog2(NextPow2((uint)parameters.maxPolys));

			//only allow 31 salt bits, since salt mask is calculated using 32-bit uint and it will overflow
			m_saltBits = Math.Min(31, 32 - m_tileBits - m_polyBits);
			if (m_saltBits < 10)
				return false;

			return true;
		}

		public uint NextPow2(uint v)
		{
			v--;
			v |= v >> 1;
			v |= v >> 2;
			v |= v >> 4;
			v |= v >> 8;
			v |= v >> 16;
			v++;

			return v;
		}

		public uint Ilog2(uint v)
		{
			uint r;
			int shift;
			r = (uint)((v > 0xffff) ? 1 << 4 : 0 << 4); v >>= (int)r;
			shift = (v > 0xff) ? 1 << 3 : 0 << 3; v >>= shift; r |= (uint)shift;
			shift = (v > 0xf) ? 1 << 2 : 0 << 2; v >>= shift; r |= (uint)shift;
			shift = (v > 0x3) ? 1 << 1 : 0 << 1; v >>= shift; r |= (uint)shift;
			r |= (v >> 1);
			return r;
		}

		public void AddTile(NavMeshBuilder data, int flags, uint lastRef, ref uint result)
		{
			//make sure data is in right format
			PathfinderCommon.MeshHeader header = data.Header;
			if (header.magic != PathfinderCommon.NAVMESH_MAGIC)
				return;
			if (header.version != PathfinderCommon.NAVMESH_VERSION)
				return;

			//make sure location is free
			if (GetTileAt(header.x, header.y, header.layer) != null)
				return;

			//allocate a tile
			PathfinderCommon.MeshTile tile = null;
			if (lastRef == 0)
			{
				int endIndex = m_nextFree.Length - 1;
				if (m_nextFree[endIndex] != null)
				{
					tile = m_nextFree[endIndex];
					m_nextFree[endIndex] = tile.next;
					tile.next = null;
				}
			}
			else
			{
				//try to relocate tile to specific index with the same salt
				int tileIndex = (int)DecodePolyIdTile((uint)lastRef);
				if (tileIndex >= m_maxTiles)
					return;

				//try to find specific tile id from free list
				PathfinderCommon.MeshTile target = m_tiles[tileIndex];
				PathfinderCommon.MeshTile prev = null;
				int endIndex = m_nextFree.Length - 1;
				tile = m_nextFree[endIndex];
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
					m_nextFree[endIndex] = tile.next;
				else
					prev.next = tile.next;

				//restore salt
				tile.salt = DecodePolyIdSalt((uint)lastRef);
			}

			//make sure we could allocate a tile
			if (tile == null)
				return;

			//insert tile into position LookUp Table (lut)
			int h = ComputeTileHash(header.x, header.y, m_tileLutMask);
			tile.next = m_posLookup[h];
			m_posLookup[h] = tile;

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
			tile.linkesFreeList = 0;
			tile.links[header.maxLinkCount - 1].next = PathfinderCommon.NULL_LINK;
			for (int i = 0; i < header.maxLinkCount - 1; i++)
				tile.links[i].next = (uint)i + 1;

			//init tile
			tile.header = header;
			tile.data = data;
			tile.flags = flags;

			ConnectIntLinks(tile);
			BaseOffMeshLinks(tile);

			//create connections with neighbor tiles
			const int MAX_NEIS = 32;
			PathfinderCommon.MeshTile[] neis = new PathfinderCommon.MeshTile[MAX_NEIS];
			int nneis;

			//connect with layers in current tile
			//...

			//connect with neighbour tiles
			//...
		}

		public void ConnectIntLinks(PathfinderCommon.MeshTile tile)
		{
			if (tile == null)
				return;

			uint polyBase = GetPolyRefBase(tile);

			for (int i = 0; i < tile.header.polyCount; i++)
			{
				tile.polys[i].firstLink = PathfinderCommon.NULL_LINK;

				if (tile.polys[i].GetType() == PathfinderCommon.POLTYPE_OFFMESH_CONNECTION)
					continue;

				//build edge links backwards so that the links will be in the linked list
				//from lowest index to highest
				for (int j = tile.polys[i].vertCount - 1; j >= 0; j--)
				{
					//skip hard and non-internal edges
					if (tile.polys[i].neis[j] == 0 || (tile.polys[i].neis[j] & PathfinderCommon.EXT_LINK) != 0)
						continue;

					uint idx = AllocLink(tile);
					if (idx != PathfinderCommon.NULL_LINK)
					{
						tile.links[idx].reference = polyBase | (uint)(tile.polys[i].neis[j] - 1);
						tile.links[idx].edge = j;
						tile.links[idx].side = 0xff;
						tile.links[idx].bounds = new BBox3();

						//add to linked list
						tile.links[idx].next = tile.polys[i].firstLink;
						tile.polys[i].firstLink = idx;
					}
				}
			}
		}

		public void BaseOffMeshLinks(PathfinderCommon.MeshTile tile)
		{
			if (tile == null)
				return;

			uint polyBase = GetPolyRefBase(tile);

			//base off-mesh connection start points
			for (int i = 0; i < tile.header.offMeshConCount; i++)
			{
				int con = i;
				int poly = tile.offMeshCons[con].poly;

				Vector3 ext = new Vector3(tile.offMeshCons[con].radius, tile.header.walkableClimb, tile.offMeshCons[con].radius);

				//find polygon to connect to
				Vector3 p = tile.offMeshCons[con].pos[0];
				Vector3 nearestPt = new Vector3();
				uint reference = FindNearestPolyInTile(tile, p, ext, ref nearestPt);
				if (reference == 0)
					continue;

				//do extra checks
				if ((nearestPt.X - p.X) * (nearestPt.X - p.X) + (nearestPt.Z - p.Z) * (nearestPt.Z - p.Z) >
					tile.offMeshCons[con].radius * tile.offMeshCons[con].radius)
					continue;

				//make sure location is on current mesh
				tile.verts[tile.polys[poly].verts[0]] = nearestPt;

				//link off-mesh connection to target poly
				uint idx = AllocLink(tile);
				if (idx != PathfinderCommon.NULL_LINK)
				{
					tile.links[idx].reference = reference;
					tile.links[idx].edge = 0;
					tile.links[idx].side = 0xff;
					tile.links[idx].bounds = new BBox3();

					//add to linked list
					tile.links[idx].next = tile.polys[poly].firstLink;
					tile.polys[poly].firstLink = idx;
				}

				//start end-point always conects back to off-mesh connection
				uint tidx = AllocLink(tile);
				if (tidx != PathfinderCommon.NULL_LINK)
				{
					int landPolyIdx = (int)DecodePolyIdPoly(reference);
					tile.links[idx].reference = polyBase | (uint)(tile.offMeshCons[con].poly);
					tile.links[idx].edge = 0xff;
					tile.links[idx].side = 0xff;
					tile.links[idx].bounds = new BBox3();

					//add to linked list
					tile.links[idx].next = tile.polys[landPolyIdx].firstLink;
					tile.polys[landPolyIdx].firstLink = tidx;
				}
			}
		}

		public uint FindNearestPolyInTile(PathfinderCommon.MeshTile tile, Vector3 center, Vector3 extents, ref Vector3 nearestPt)
		{
			BBox3 bounds;
			bounds.Min = center - extents;
			bounds.Max = center + extents;

			//get nearby polygons from proximity grid
			uint[] polys = new uint[128];
			int polyCount = QueryPolygonsInTile(tile, bounds.Min, bounds.Max, polys, 128);

			//find nearest polygon amongst the nearby polygons
			uint nearest = 0;
			float nearestDistanceSqr = float.MaxValue;

			for (int i = 0; i < polyCount; i++)
			{
				uint reference = polys[i];
				Vector3 closestPtPoly = new Vector3();
				ClosestPointOnPolyInTile(tile, DecodePolyIdPoly(reference), center, ref closestPtPoly);
				float d = (new Vector3(center - closestPtPoly)).LengthSquared;
				if (d < nearestDistanceSqr)
				{
					nearestPt = closestPtPoly;
					nearestDistanceSqr = d;
					nearest = reference;
				}
			}

			return nearest;
		}

		public int QueryPolygonsInTile(PathfinderCommon.MeshTile tile, Vector3 qmin, Vector3 qmax, uint[] polys, int maxPolys)
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
				float minx = MathHelper.Clamp(qmin.X, tbmin.X, tbmax.X) - tbmin.X;
				float miny = MathHelper.Clamp(qmin.Y, tbmin.Y, tbmax.Y) - tbmin.Y;
				float minz = MathHelper.Clamp(qmin.Z, tbmin.Z, tbmax.Z) - tbmin.Z;
				float maxx = MathHelper.Clamp(qmax.X, tbmin.X, tbmax.X) - tbmin.X;
				float maxy = MathHelper.Clamp(qmax.Y, tbmin.Y, tbmax.Y) - tbmin.Y;
				float maxz = MathHelper.Clamp(qmax.Z, tbmin.Z, tbmax.Z) - tbmin.Z;

				//quantize
				bmin.X = (int)(qfac * minx) & 0xfffe;
				bmin.Y = (int)(qfac * miny) & 0xfffe;
				bmin.Z = (int)(qfac * minz) & 0xfffe;
				bmax.X = (int)(qfac * maxx + 1) | 1;
				bmax.Y = (int)(qfac * maxy + 1) | 1;
				bmax.Z = (int)(qfac * maxz + 1) | 1;

				//traverse tree
				uint polyBase = GetPolyRefBase(tile);
				int n = 0;
				
				while (node < end)
				{
					bool overlap = OverlapQuantBounds(bmin, bmax, tile.bvTree[node].bounds.Min, tile.bvTree[node].bounds.Max);
					bool isLeafNode = tile.bvTree[node].index >= 0;

					if (isLeafNode && overlap)
					{
						if (n < maxPolys)
							polys[n++] = polyBase | (uint)tile.bvTree[node].index;
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
				uint polyBase = GetPolyRefBase(tile);

				for (int i = 0; i < tile.header.polyCount; i++)
				{	
					//don't return off-mesh connection polygons
					if (tile.polys[i].GetType() == PathfinderCommon.POLTYPE_OFFMESH_CONNECTION)
						continue;

					//calculate polygon bounds
					bmin = tile.verts[tile.polys[i].verts[0]];
					bmax = tile.verts[tile.polys[i].verts[0]];

					for (int j = 1; j < tile.polys[i].vertCount; j++)
					{
						bmin = Vector3.Min(bmin, tile.verts[tile.polys[i].verts[j]]);
						bmax = Vector3.Min(bmax, tile.verts[tile.polys[i].verts[j]]);
					}

					if (OverlapQuantBounds(qmin, qmax, bmin, bmax))
					{
						if (n < maxPolys)
							polys[n++] = polyBase | (uint)i;
					}
				}

				return n;
			}
		}

		public void ClosestPointOnPolyInTile(PathfinderCommon.MeshTile tile, uint indexPoly, Vector3 pos, ref Vector3 closest)
		{
			PathfinderCommon.Poly poly = tile.polys[indexPoly];

			//off-mesh connections don't have detail polygons
			if (tile.polys[indexPoly].GetType() == PathfinderCommon.POLTYPE_OFFMESH_CONNECTION)
			{
				Vector3 v0 = tile.verts[poly.verts[0]];
				Vector3 v1 = tile.verts[poly.verts[1]];
				float d0 = (new Vector3(pos - v0)).Length;
				float d1 = (new Vector3(pos - v1)).Length;
				float u = d0 / (d0 + d1);
				VectorLinearInterpolation(ref closest, v0, v1, u);
				return;
			}

			PathfinderCommon.PolyDetail pd = tile.detailMeshes[indexPoly];

			//clamp point to be inside the polygon
			Vector3[] verts = new Vector3[PathfinderCommon.VERTS_PER_POLYGON];
			float[] edged = new float[PathfinderCommon.VERTS_PER_POLYGON];
			float[] edget = new float[PathfinderCommon.VERTS_PER_POLYGON];
			int nv = poly.vertCount;
			for (int i = 0; i < nv; i++)
				verts[i] = tile.verts[poly.verts[i]];

			closest = pos;
			if (!DistancePointPolyEdgesSquare(pos, verts, nv, edged, edget))
			{
				//point is outside polygon so clamp to nearest edge
				float dmin = float.MaxValue;
				int imin = -1;

				for (int i = 0; i < nv; i++)
				{
					if (edged[i] < dmin)
					{
						dmin = edged[i];
						imin = i;
					}
				}

				Vector3 va = verts[imin];
				Vector3 vb = verts[(imin + 1) % nv];
				VectorLinearInterpolation(ref closest, va, vb, edget[imin]);
			}

			//find height at the location
			for (int j = 0; j < tile.detailMeshes[indexPoly].triCount; j++)
			{
				NavMeshDetail.TrisInfo t = tile.detailTris[pd.triBase + j];
				Vector3[] v = new Vector3[3];
		
				for (int k = 0; k < 3; k++)
				{
					if (t.VertexHash[k] < poly.vertCount)
						v[k] = tile.verts[poly.verts[t.VertexHash[k]]];
					else
						v[k] = tile.detailVerts[pd.vertBase + (t.VertexHash[k] - poly.vertCount)];
				}

				float h = 0;
				if (ClosestHeightPointTriangle(pos, v[0], v[1], v[2], ref h))
				{
					closest.Y = h;
					break;
				}
			}
		}

		public bool OverlapQuantBounds(Vector3 amin, Vector3 amax, Vector3 bmin, Vector3 bmax)
		{
			bool overlap = true;
			overlap = (amin.X > bmax.X || amax.X < bmin.X) ? false : overlap;
			overlap = (amin.Y > bmax.Y || amax.Y < bmin.Y) ? false : overlap;
			overlap = (amin.Z > bmax.Z || amax.Z < bmin.Z) ? false : overlap;
			return overlap;
		}

		public void VectorLinearInterpolation(ref Vector3 dest, Vector3 v1, Vector3 v2, float t)
		{
			dest = new Vector3();
			dest.X = v1.X + (v2.X - v1.X) * t;
			dest.Y = v1.Y + (v2.Y - v1.Y) * t;
			dest.Z = v1.Z + (v2.Z - v1.Z) * t;
		}

		public bool DistancePointPolyEdgesSquare(Vector3 pt, Vector3[] verts, int nverts, float[] ed, float[] et)
		{
			bool c = false;

			for (int i = 0, j = nverts - 1; i < nverts; j = i++)
			{
				Vector3 vi = verts[i];
				Vector3 vj = verts[j];
				if (((vi.Z > pt.Z) != (vj.Z > pt.Z)) &&
					(pt.X < (vj.X - vi.X) * (pt.Z - vi.Z) / (vj.Z - vi.Z) + vi.X))
				{
					c = !c;
				}

				ed[j] = DistancePointSegmentSquare2D(pt, vj, vi, ref et[j]);
			}

			return c;
		}

		public float DistancePointSegmentSquare2D(Vector3 pt, Vector3 p, Vector3 q, ref float t)
		{
			float pqx = q.X - p.X;
			float pqz = q.Z - p.Z;
			float dx = pt.X - p.X;
			float dz = pt.Z - p.Z;
			float d = pqx * pqx + pqz * pqz;
			t = pqx * dx + pqz * dz;
			
			if (d > 0)
				t /= d;

			if (t < 0)
				t = 0;
			else if (t > 1)
				t = 1;

			dx = p.X + t * pqx - pt.X;
			dz = p.Z + t * pqz - pt.Z;

			return dx * dx + dz * dz;
		}

		public bool ClosestHeightPointTriangle(Vector3 p, Vector3 a, Vector3 b, Vector3 c, ref float h)
		{
			Vector3 v0 = c - a;
			Vector3 v1 = b - a;
			Vector3 v2 = p - a;

			float dot00 = VDot2D(v0, v0);
			float dot01 = VDot2D(v0, v1);
			float dot02 = VDot2D(v0, v2);
			float dot11 = VDot2D(v1, v1);
			float dot12 = VDot2D(v1, v2);

			//computer barycentric coordinates
			float invDenom = 1.0f / (dot00 * dot11 - dot01 * dot01);
			float u = (dot11 * dot02 - dot01 * dot12) * invDenom;
			float v = (dot00 * dot12 - dot01 * dot02) * invDenom;

			float EPS = 1E-4f;

			//if point lies inside triangle, return interpolated y-coordinate
			if (u >= -EPS && v >= -EPS && (u + v) <= 1 + EPS)
			{
				h = a.Y + v0.Y * u + v1.Y * v;
				return true;
			}

			return false;
		}

		public float VDot2D(Vector3 u, Vector3 v)
		{
			return u.X * v.X + u.Z * v.Z;
		}

		public uint AllocLink(PathfinderCommon.MeshTile tile)
		{
			if (tile.linkesFreeList == PathfinderCommon.NULL_LINK)
				return PathfinderCommon.NULL_LINK;

			uint link = tile.linkesFreeList;
			tile.linkesFreeList = tile.links[link].next;
			return link;
		}

		public PathfinderCommon.MeshTile GetTileAt(int x, int y, int layer)
		{
			//find tile based off hash
			int h = ComputeTileHash(x, y, m_tileLutMask);
			PathfinderCommon.MeshTile tile = m_posLookup[h];
			while (tile != null)
			{
				if (tile.header != null && tile.header.x == x && tile.header.y == y && tile.header.layer == layer)
					return tile;

				tile = tile.next;
			}
			return null;
		}

		public int ComputeTileHash(int x, int y, int mask)
		{
			//choose large multiplicative constants which are primes
			uint h1 = 0x8da6b343;
			uint h2 = 0xd8163841;
			uint n = (uint)(h1 * x + h2 * y);
			return (int)(n & mask);
		}
		
		public uint GetPolyRefBase(PathfinderCommon.MeshTile tile)
		{
			if (tile == null)
				return 0;

			uint it = 0;
			for (int i = 0; i < m_tiles.Length; i++)
			{
				if (m_tiles[i] == tile)
				{
					it = (uint)i;
					break;
				}
			}
			return EncodePolyId(tile.salt, it, 0);
		}

		//extract a tile's salt value from the specified polygon reference
		public uint DecodePolyIdSalt(uint reference)
		{
			uint saltMask = (uint)(1 << (int)m_saltBits) - 1;
			return (uint)((reference >> (int)(m_polyBits + m_tileBits)) & saltMask);
		}

		//extract a tile's index from the specified polygon reference
		public uint DecodePolyIdTile(uint reference)
		{
			uint tileMask = (uint)(1 << (int)m_tileBits) - 1;
			return (uint)((reference >> (int)m_polyBits) & tileMask);
		}

		//extract a polygon's index (within its tile) from the specified polygon reference
		public uint DecodePolyIdPoly(uint reference)
		{
			uint polyMask = ((uint)1 << (int)m_polyBits) - 1;
			return (uint)(reference & polyMask);
		}

		//derive a standard polygon reference
		public uint EncodePolyId(uint salt, uint indexTile, uint indexPoly)
		{
			return (salt << (int)(m_polyBits + m_tileBits)) | (indexTile << (int)m_polyBits) | indexPoly;
		}
	}
}
