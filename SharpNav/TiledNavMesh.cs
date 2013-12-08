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
		private NavMeshParams m_params;
		private Vector3 m_origin;
		private float m_tileWidth, m_tileHeight;
		private int m_maxTiles;
		private int m_tileLutSize; //tile hash lookup size
		private int m_tileLutMask; //tile hash lookup mask

		private MeshTile[] m_posLookup; //tile hash lookup
		private MeshTile[] m_nextFree; //freelist of tiles
		private MeshTile[] m_tiles; //list of tiles

		private uint m_saltBits; //number of salt bits in ID
		private uint m_tileBits; //number of tile bits in ID
		private uint m_polyBits; //number of poly bits in ID


		public TiledNavMesh(NavMeshBuilder data, int flags)
		{
			if (data.Header.magic != NavMeshBuilder.NAVMESH_MAGIC) //TODO: output error message?
				return;

			if (data.Header.version != NavMeshBuilder.NAVMESH_VERSION) //TODO: output error message?
				return;

			NavMeshParams parameters;
			parameters.origin = data.Header.bounds.Min;
			parameters.tileWidth = data.Header.bounds.Max.X - data.Header.bounds.Min.X;
			parameters.tileHeight = data.Header.bounds.Max.Y - data.Header.bounds.Min.Y;
			parameters.maxTiles = 1;
			parameters.maxPolys = data.Header.polyCount;

			if (initTileNavMesh(parameters) == false)
				return;
		}

		public bool initTileNavMesh(NavMeshParams parameters)
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

			m_tiles = new MeshTile[m_maxTiles];
			m_posLookup = new MeshTile[m_tileLutSize];
			for (int i = 0; i < m_tiles.Length; i++)
				m_tiles[i] = null;
			for (int i = 0; i < m_posLookup.Length; i++)
				m_posLookup[i] = null;
			m_nextFree = new MeshTile[m_maxTiles];
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
		
		public struct NavMeshParams
		{
			public Vector3 origin;
			public float tileWidth;
			public float tileHeight;
			public int maxTiles;
			public int maxPolys;
		}

		public struct Link
		{
			public uint reference; //neighbor reference (the one it's linked to)
			public uint next; //index of next link
			public int edge; //index of polygon edge
			public int side;
			public BBox3 bounds;
		}

		public class MeshTile
		{
			public uint salt; //counter describing modifications to the tile
			
			public uint linkesFreeList; //index to the next free link
			public NavMeshBuilder.MeshHeader header;
			public NavMeshBuilder.Poly[] polys;
			public Vector3[] verts;
			public Link[] links;
			public NavMeshBuilder.PolyDetail[] detailMeshes;

			public Vector3[] detailVerts;
			public NavMeshDetail.TrisInfo[] detailTris;

			public NavMeshBuilder.BVNode[] bvTree; //bounding volume nodes

			public NavMeshBuilder.OffMeshConnection[] offMeshCons;

			public NavMeshBuilder data;
			public int flags;
			public MeshTile next; 
		}
	}
}
