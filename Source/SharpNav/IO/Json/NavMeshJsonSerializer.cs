// Copyright (c) 2015-2016 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
// Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using SharpNav.Collections;
using SharpNav.Geometry;
using SharpNav.Pathfinding;
using System.Reflection;

#if MONOGAME
using Vector3 = Microsoft.Xna.Framework.Vector3;
#elif OPENTK
using Vector3 = OpenTK.Vector3;
#elif SHARPDX
using Vector3 = SharpDX.Vector3;
#endif

namespace SharpNav.IO.Json
{
	/// <summary>
	/// Subclass of NavMeshSerializer that implements 
	/// serialization/deserializtion in text files with json format
	/// </summary>
	public class NavMeshJsonSerializer : NavMeshSerializer
	{
		private JsonSerializer serializer;

		//increase this once every time the file format changes.
		private static readonly int FormatVersion = 3;

		public NavMeshJsonSerializer()
		{
			serializer = JsonSerializer.Create(new JsonSerializerSettings()
			{
				ReferenceLoopHandling = ReferenceLoopHandling.Error,
				Converters = new List<JsonConverter>() { new Vector3Converter(), new AreaConverter(), new PolyIdConverter() }
			});
		}

		public override void Serialize(string path, TiledNavMesh mesh)
		{
			JObject root = new JObject();

			root.Add("meta", JToken.FromObject(new
			{
				version = new
				{
					
					snj = FormatVersion,
					sharpnav = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion
				}
			}));

			root.Add("origin", JToken.FromObject(mesh.Origin, serializer));
			root.Add("tileWidth", JToken.FromObject(mesh.TileWidth, serializer));
			root.Add("tileHeight", JToken.FromObject(mesh.TileHeight, serializer));
			root.Add("maxTiles", JToken.FromObject(mesh.MaxTiles, serializer));
			root.Add("maxPolys", JToken.FromObject(mesh.MaxPolys, serializer));

			var tilesArray = new JArray();
			foreach (NavTile tile in mesh.Tiles)
			{
				NavPolyId id = mesh.GetTileRef(tile);
				tilesArray.Add(SerializeMeshTile(tile, id));
			}

			root.Add("tiles", tilesArray);
			
			File.WriteAllText(path, root.ToString());
		}

		public override TiledNavMesh Deserialize(string path)
		{
			JObject root = JObject.Parse(File.ReadAllText(path));

			if (root["meta"]["version"]["snj"].ToObject<int>() != FormatVersion)
				throw new ArgumentException("The version of the file does not match the version of the parser. Consider using an older version of SharpNav or re-generating your .snj meshes.");

			Vector3 origin = root["origin"].ToObject<Vector3>(serializer);
			float tileWidth = root["tileWidth"].ToObject<float>(serializer);
			float tileHeight = root["tileHeight"].ToObject<float>(serializer);
			int maxTiles = root["maxTiles"].ToObject<int>(serializer);
			int maxPolys = root["maxPolys"].ToObject<int>(serializer);

			var mesh = new TiledNavMesh(origin, tileWidth, tileHeight, maxTiles, maxPolys);

			JArray tilesToken = (JArray) root["tiles"];
			List<NavTile> tiles = new List<NavTile>();
			foreach (JToken tileToken in tilesToken)
			{
				NavPolyId tileRef;
				NavTile tile = DeserializeMeshTile(tileToken, mesh.IdManager, out tileRef);
				mesh.AddTileAt(tile, tileRef);
			}

			return mesh;
		}

		private JObject SerializeMeshTile(NavTile tile, NavPolyId id)
		{
			var result = new JObject();
			result.Add("polyId", JToken.FromObject(id, serializer));
			result.Add("location", JToken.FromObject(tile.Location, serializer));
			result.Add("layer", JToken.FromObject(tile.Layer, serializer));
			result.Add("salt", JToken.FromObject(tile.Salt, serializer));
			result.Add("bounds", JToken.FromObject(tile.Bounds, serializer));
			result.Add("polys", JToken.FromObject(tile.Polys, serializer));
			result.Add("verts", JToken.FromObject(tile.Verts, serializer));
			result.Add("detailMeshes", JToken.FromObject(tile.DetailMeshes, serializer));
			result.Add("detailVerts", JToken.FromObject(tile.DetailVerts, serializer));
			result.Add("detailTris", JToken.FromObject(tile.DetailTris, serializer));
			result.Add("offMeshConnections", JToken.FromObject(tile.OffMeshConnections, serializer));

			JObject treeObject = new JObject();
			JArray treeNodes = new JArray();
			for (int i = 0; i < tile.BVTree.Count; i++)
				treeNodes.Add(JToken.FromObject(tile.BVTree[i], serializer));
			treeObject.Add("nodes", treeNodes);

			result.Add("bvTree", treeObject);
			result.Add("bvQuantFactor", JToken.FromObject(tile.BvQuantFactor, serializer));
			result.Add("bvNodeCount", JToken.FromObject(tile.BvNodeCount, serializer));
			result.Add("walkableClimb", JToken.FromObject(tile.WalkableClimb, serializer));

			return result;
		}

		private NavTile DeserializeMeshTile(JToken token, NavPolyIdManager manager, out NavPolyId refId)
		{
			refId = token["polyId"].ToObject<NavPolyId>(serializer);
			Vector2i location = token["location"].ToObject<Vector2i>(serializer);
			int layer = token["layer"].ToObject<int>(serializer);
			NavTile result = new NavTile(location, layer, manager, refId);

			result.Salt = token["salt"].ToObject<int>(serializer);
			result.Bounds = token["bounds"].ToObject<BBox3>(serializer);
			result.Polys = token["polys"].ToObject<NavPoly[]>(serializer);
			result.PolyCount = result.Polys.Length;
			result.Verts = token["verts"].ToObject<Vector3[]>(serializer);
			result.DetailMeshes = token["detailMeshes"].ToObject<PolyMeshDetail.MeshData[]>(serializer);
			result.DetailVerts = token["detailVerts"].ToObject<Vector3[]>(serializer);
			result.DetailTris = token["detailTris"].ToObject<PolyMeshDetail.TriangleData[]>(serializer);
			result.OffMeshConnections = token["offMeshConnections"].ToObject<OffMeshConnection[]>(serializer);
			result.OffMeshConnectionCount = result.OffMeshConnections.Length;
			result.BvNodeCount = token["bvNodeCount"].ToObject<int>(serializer);
			result.BvQuantFactor = token["bvQuantFactor"].ToObject<float>(serializer);
			result.WalkableClimb = token["walkableClimb"].ToObject<float>(serializer);
	
			var treeObject = (JObject) token["bvTree"];
			var nodes = treeObject.GetValue("nodes").ToObject<BVTree.Node[]>();

			result.BVTree = new BVTree(nodes);

			return result;
		}
	}
}
