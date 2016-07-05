// Copyright (c) 2013-2016 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
// Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE

using System;
using System.Collections.Generic;
using System.Linq;

using SharpNav.Collections;
using SharpNav.Geometry;
using SharpNav.Pathfinding;
using System.Collections.ObjectModel;

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
	/// A TiledNavMesh is a continuous region, which is used for pathfinding. 
	/// </summary>
	public class TiledNavMesh
	{
		private Vector3 origin;
		private float tileWidth, tileHeight;
		private int maxTiles;
		private int maxPolys;

		//TODO if we want to be able to remove tiles, turn tileList into a dict of <int, MeshTile>
		//     and add an int that always increases so that we don't have bad refs when somemthing
		//     is removed from tileList and all the indices after it change.
		//indexing of tiles in a few ways:
		//lookup by x,y (for list in layer)
		//lookup tile by ref
		//lookup ref by tile
		private Dictionary<Vector2i, List<NavTile>> tileSet;
		private Dictionary<NavTile, NavPolyId> tileRefs;
		private List<NavTile> tileList;

		private NavPolyIdManager idManager;

		/// <summary>
		/// Initializes a new instance of the <see cref="TiledNavMesh"/> class.
		/// </summary>
		/// <param name="data">The Navigation Mesh data</param>
		public TiledNavMesh(NavMeshBuilder data)
		{
			this.origin = data.Header.Bounds.Min;
			this.tileWidth = data.Header.Bounds.Max.X - data.Header.Bounds.Min.X;
			this.tileHeight = data.Header.Bounds.Max.Z - data.Header.Bounds.Min.Z;
			this.maxTiles = 1;
			this.maxPolys = data.Header.PolyCount;

			//init tiles
			tileSet = new Dictionary<Vector2i, List<NavTile>>();
			tileRefs = new Dictionary<NavTile, NavPolyId>();
			tileList = new List<NavTile>();

			//init ID generator values
			int tileBits = MathHelper.Log2(MathHelper.NextPowerOfTwo(maxTiles));
			int polyBits = MathHelper.Log2(MathHelper.NextPowerOfTwo(maxPolys));

			//only allow 31 salt bits, since salt mask is calculated using 32-bit int and it will overflow
			int saltBits = Math.Min(31, 32 - tileBits - polyBits);

			//TODO handle this in a sane way/do we need this?
			if (saltBits < 10)
				return;

			idManager = new NavPolyIdManager(polyBits, tileBits, saltBits);

			AddTile(data);
		}

		public TiledNavMesh(Vector3 origin, float tileWidth, float tileHeight, int maxTiles, int maxPolys)
		{
			this.origin = origin;
			this.tileWidth = tileWidth;
			this.tileHeight = tileHeight;
			this.maxTiles = maxTiles;
			this.maxPolys = maxPolys;

			//init tiles
			tileSet = new Dictionary<Vector2i, List<NavTile>>();
			tileRefs = new Dictionary<NavTile, NavPolyId>();
			tileList = new List<NavTile>();

			//init ID generator values
			int tileBits = MathHelper.Log2(MathHelper.NextPowerOfTwo(maxTiles));
			int polyBits = MathHelper.Log2(MathHelper.NextPowerOfTwo(maxPolys));

			//only allow 31 salt bits, since salt mask is calculated using 32-bit int and it will overflow
			int saltBits = Math.Min(31, 32 - tileBits - polyBits);

			//TODO handle this in a sane way/do we need this?
			if (saltBits < 10)
				return;

			idManager = new NavPolyIdManager(polyBits, tileBits, saltBits);
		}

		public Vector3 Origin { get { return origin; } }

		public float TileWidth { get { return tileWidth; } }

		public float TileHeight { get { return tileHeight; } }

		public float MaxTiles { get { return maxTiles; } }

		public float MaxPolys { get { return maxPolys; } }

		/// <summary>
		/// Gets the maximum number of tiles that can be stored
		/// </summary>
		public int TileCount
		{
			get
			{
				return tileList.Count;
			}
		}

		public NavPolyIdManager IdManager
		{
			get
			{
				return idManager;
			}
		}

		/// <summary>
		/// Gets the mesh tile at a specified index.
		/// </summary>
		/// <param name="index">The index referencing a tile.</param>
		/// <returns>The tile at the index.</returns>
		public ReadOnlyCollection<NavTile> this[Vector2i location]
		{
			get
			{
				return new ReadOnlyCollection<NavTile>(tileSet[location]);
			}
		}

		public ReadOnlyCollection<NavTile> this[int x, int y]
		{
			get
			{
				return this[new Vector2i(x, y)];
			}
		}

		public NavTile this[int reference]
		{
			get
			{
				return tileList[reference];
			}
		}

		public NavTile this[NavPolyId id]
		{
			get
			{
				int index = idManager.DecodeTileIndex(ref id);
				return this[index];
			}
		}

		public ReadOnlyCollection<NavTile> Tiles
		{
			get
			{
				return new ReadOnlyCollection<NavTile>(tileList);
			}
		}

		/// <summary>
		/// Gets or sets user data for this navmesh.
		/// </summary>
		public object Tag { get; set; }

		public void AddTileAt(NavTile tile, NavPolyId id)
		{
			//TODO more error checking, what if tile already exists?

			Vector2i loc = tile.Location;
			List<NavTile> locList;
			if (!tileSet.TryGetValue(loc, out locList))
			{
				locList = new List<NavTile>();
				locList.Add(tile);
				tileSet.Add(loc, locList);
			}
			else
			{
				locList.Add(tile);
			}

			tileRefs.Add(tile, id);

			int index = idManager.DecodeTileIndex(ref id);

			//HACK this is pretty bad but only way to insert at index
			//TODO tileIndex should have a level of indirection from the list?
			while (index >= tileList.Count)
				tileList.Add(null);

			tileList[index] = tile;
		}

		/// <summary>
		/// Build a tile and link all the polygons togther, both internally and externally.
		/// Make sure to link off-mesh connections as well.
		/// </summary>
		/// <param name="data">Navigation Mesh data</param>
		/// <param name="lastRef">Last polygon reference</param>
		/// <param name="result">Last tile reference</param>
		public NavPolyId AddTile(NavMeshBuilder data)
		{
			//make sure data is in right format
			PathfindingCommon.NavMeshInfo header = data.Header;

			//make sure location is free
			if (GetTileAt(header.X, header.Y, header.Layer) != null)
				return NavPolyId.Null;

			NavPolyId newTileId = GetNextTileRef();
			NavTile tile = new NavTile(new Vector2i(header.X, header.Y), header.Layer, idManager, newTileId);
			tile.Salt = idManager.DecodeSalt(ref newTileId);

			if (header.BvNodeCount == 0)
				tile.BVTree = null;

			//patch header
			tile.Verts = data.NavVerts;
			tile.Polys = data.NavPolys;
			tile.PolyCount = header.PolyCount;
			tile.DetailMeshes = data.NavDMeshes;
			tile.DetailVerts = data.NavDVerts;
			tile.DetailTris = data.NavDTris;
			tile.BVTree = data.NavBvTree;
			tile.OffMeshConnections = data.OffMeshCons;
			tile.OffMeshConnectionCount = header.OffMeshConCount;
			tile.BvQuantFactor = header.BvQuantFactor;
			tile.BvNodeCount = header.BvNodeCount;
			tile.Bounds = header.Bounds;
			tile.WalkableClimb = header.WalkableClimb;

			//create connections within tile

			tile.ConnectIntLinks();
			tile.BaseOffMeshLinks();

			//create connections with neighbor tiles

			//connect with layers in current tile
			foreach (NavTile layerTile in GetTilesAt(header.X, header.Y))
			{
				if (layerTile != tile)
				{
					tile.ConnectExtLinks(layerTile, BoundarySide.Internal);
					layerTile.ConnectExtLinks(tile, BoundarySide.Internal);
				}

				tile.ConnectExtOffMeshLinks(layerTile, BoundarySide.Internal);
				layerTile.ConnectExtOffMeshLinks(tile, BoundarySide.Internal);
			}

			//connect with neighbor tiles
			for (int i = 0; i < 8; i++)
			{
				BoundarySide b = (BoundarySide)i;
				BoundarySide bo = b.GetOpposite();
				foreach (NavTile neighborTile in GetNeighborTilesAt(header.X, header.Y, b))
				{
					tile.ConnectExtLinks(neighborTile, b);
					neighborTile.ConnectExtLinks(tile, bo);
					tile.ConnectExtOffMeshLinks(neighborTile, b);
					neighborTile.ConnectExtOffMeshLinks(tile, bo);
				}
			}

			AddTileAt(tile, GetNextTileRef());

			return newTileId;
		}

		public NavPolyId GetNextTileRef()
		{
			//Salt is 1 for first version. As tiles get edited, change salt.
			//Salt can't be 0, otherwise the first poly of tile 0 is incorrectly seen as PolyId.Null.
			return idManager.Encode(1, tileList.Count, 0);
		}

		/// <summary>
		/// Retrieve the endpoints of the offmesh connection at the specified polygon
		/// </summary>
		/// <param name="prevRef">The previous polygon reference</param>
		/// <param name="polyRef">The current polygon reference</param>
		/// <param name="startPos">The starting position</param>
		/// <param name="endPos">The ending position</param>
		/// <returns>True if endpoints found, false if not</returns>
		public bool GetOffMeshConnectionPolyEndPoints(NavPolyId prevRef, NavPolyId polyRef, ref Vector3 startPos, ref Vector3 endPos)
		{
			int salt = 0, indexTile = 0, indexPoly = 0;

			if (polyRef == NavPolyId.Null)
				return false;

			//get current polygon
			idManager.Decode(ref polyRef, out indexPoly, out indexTile, out salt);
			if (indexTile >= maxTiles)
				return false;
			if (tileList[indexTile].Salt != salt)
				return false;
			NavTile tile = tileList[indexTile];
			if (indexPoly >= tile.PolyCount)
				return false;
			NavPoly poly = tile.Polys[indexPoly];

			if (poly.PolyType != NavPolyType.OffMeshConnection)
				return false;

			int idx0 = 0, idx1 = 1;

			//find the link that points to the first vertex
			foreach (Link link in poly.Links)
			{
				if (link.Edge == 0)
				{
					if (link.Reference != prevRef)
					{
						idx0 = 1;
						idx1 = 0;
					}

					break;
				}
			}

			startPos = tile.Verts[poly.Verts[idx0]];
			endPos = tile.Verts[poly.Verts[idx1]];

			return true;
		}

		/// <summary>
		/// Get the tile reference
		/// </summary>
		/// <param name="tile">Tile to look for</param>
		/// <returns>Tile reference</returns>
		public NavPolyId GetTileRef(NavTile tile)
		{
			if (tile == null)
				return NavPolyId.Null;

			NavPolyId id;
			if (!tileRefs.TryGetValue(tile, out id))
				id = NavPolyId.Null;

			return id;
		}

		/// <summary>
		/// Find the tile at a specific location.
		/// </summary>
		/// <param name="x">The X coordinate of the tile.</param>
		/// <param name="y">The Y coordinate of the tile.</param>
		/// <param name="layer">The layer of the tile.</param>
		/// <returns>The MeshTile at the specified location.</returns>
		public NavTile GetTileAt(int x, int y, int layer)
		{
			return GetTileAt(new Vector2i(x, y), layer);
		}

		/// <summary>
		/// Find the tile at a specific location.
		/// </summary>
		/// <param name="location">The (X, Y) coordinate of the tile.</param>
		/// <param name="layer">The layer of the tile.</param>
		/// <returns>The MeshTile at the specified location.</returns>
		public NavTile GetTileAt(Vector2i location, int layer)
		{
			//Find tile based off hash
			List<NavTile> list;
			if (!tileSet.TryGetValue(location, out list))
				return null;

			return list.Find(t => t.Layer == layer);
		}

		/// <summary>
		/// Find and add a tile if it is found
		/// </summary>
		/// <param name="x">The x-coordinate</param>
		/// <param name="y">The y-coordinate</param>
		/// <returns>A read-only collection of tiles at the specified coordinate</returns>
		public IEnumerable<NavTile> GetTilesAt(int x, int y)
		{
			return GetTilesAt(new Vector2i(x, y));
		}

		public IEnumerable<NavTile> GetTilesAt(Vector2i location)
		{
			//Find tile based off hash
			List<NavTile> list;
			if (!tileSet.TryGetValue(location, out list))
				return Enumerable.Empty<NavTile>();

			return new ReadOnlyCollection<NavTile>(list);
		}

		public IEnumerable<NavTile> GetNeighborTilesAt(Vector2i location, BoundarySide side)
		{
			return GetNeighborTilesAt(location.X, location.Y, side);
		}

		/// <summary>
		/// Gets the neighboring tile at that position
		/// </summary>
		/// <param name="x">The x-coordinate</param>
		/// <param name="y">The y-coordinate</param>
		/// <param name="side">The side value</param>
		/// <param name="tiles">An array of MeshTiles</param>
		/// <returns>The number of tiles satisfying the condition</returns>
		public IEnumerable<NavTile> GetNeighborTilesAt(int x, int y, BoundarySide side)
		{
			int nx = x, ny = y;
			switch (side)
			{
				case BoundarySide.PlusX:
					nx++;
					break;

				case BoundarySide.PlusXPlusZ:
					nx++;
					ny++;
					break;

				case BoundarySide.PlusZ:
					ny++;
					break;

				case BoundarySide.MinusXPlusZ:
					nx--;
					ny++;
					break;

				case BoundarySide.MinusX:
					nx--;
					break;

				case BoundarySide.MinusXMinusZ:
					nx--;
					ny--;
					break;

				case BoundarySide.MinusZ:
					ny--;
					break;

				case BoundarySide.PlusXMinusZ:
					nx++;
					ny--;
					break;
			}

			return GetTilesAt(nx, ny);
		}

		/// <summary>
		/// Retrieve the tile and poly based off of a polygon reference
		/// </summary>
		/// <param name="reference">Polygon reference</param>
		/// <param name="tile">Resulting tile</param>
		/// <param name="poly">Resulting poly</param>
		/// <returns>True if tile and poly successfully retrieved</returns>
		public bool TryGetTileAndPolyByRef(NavPolyId reference, out NavTile tile, out NavPoly poly)
		{
			tile = null;
			poly = null;

			if (reference == NavPolyId.Null)
				return false;

			//Get tile and poly indices
			int salt, polyIndex, tileIndex;
			idManager.Decode(ref reference, out polyIndex, out tileIndex, out salt);
			
			//Make sure indices are valid
			if (tileIndex >= maxTiles)
				return false;

			if (tileList[tileIndex].Salt != salt)
				return false;

			if (polyIndex >= tileList[tileIndex].PolyCount)
				return false;

			//Retrieve tile and poly
			tile = tileList[tileIndex];
			poly = tileList[tileIndex].Polys[polyIndex];
			return true;
		}

		/// <summary>
		/// Only use this function if it is known that the provided polygon reference is valid.
		/// </summary>
		/// <param name="reference">Polygon reference</param>
		/// <param name="tile">Resulting tile</param>
		/// <param name="poly">Resulting poly</param>
		public void TryGetTileAndPolyByRefUnsafe(NavPolyId reference, out NavTile tile, out NavPoly poly)
		{
			int salt, polyIndex, tileIndex;
			idManager.Decode(ref reference, out polyIndex, out tileIndex, out salt);
			tile = tileList[tileIndex];
			poly = tileList[tileIndex].Polys[polyIndex];
		}

		/// <summary>
		/// Check if polygon reference is valid.
		/// </summary>
		/// <param name="reference">Polygon reference</param>
		/// <returns>True if valid</returns>
		public bool IsValidPolyRef(NavPolyId reference)
		{
			if (reference == NavPolyId.Null)
				return false;

			int salt, polyIndex, tileIndex;
			idManager.Decode(ref reference, out polyIndex, out tileIndex, out salt);

			if (tileIndex >= maxTiles)
				return false;

			if (tileList[tileIndex].Salt != salt)
				return false;

			if (polyIndex >= tileList[tileIndex].PolyCount)
				return false;

			return true;
		}

		/// <summary>
		/// Calculates the tile location.
		/// </summary>
		/// <param name="pos">The position</param>
		/// <param name="tx">The tile's x-coordinate</param>
		/// <param name="ty">The tile's y-coordinate</param>
		public void CalcTileLoc(ref Vector3 pos, out int tx, out int ty)
		{
			tx = (int)Math.Floor((pos.X - origin.X) / tileWidth);
			ty = (int)Math.Floor((pos.Z - origin.Z) / tileHeight);
		}
	}
}
