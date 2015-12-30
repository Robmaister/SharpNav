// Copyright (c) 2015 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
// Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

		//TODO maybe a better system?
		//increase this once every time the file format changes.
		private static readonly int FormatVersion = 2;

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

			var tiles = (List<MeshTile>) GetPrivateField(mesh, typeof(TiledNavMesh), "tileList");
			var tileRefs = (Dictionary<MeshTile, int>)GetPrivateField(mesh, typeof(TiledNavMesh), "tileRefs");
			foreach (MeshTile tile in tiles)
			{
				tilesArray.Add(SerializeMeshTile(tile));
			}

			root.Add("tiles", tilesArray);
			
			File.WriteAllText(path, root.ToString());
		}

		public override TiledNavMesh Deserialize(string path)
		{
			JObject root = JObject.Parse(File.ReadAllText(path));

			//TODO error message?
			if (root["meta"]["version"]["snj"].ToObject<int>() != FormatVersion)
				return null;

			Vector3 origin = root["origin"].ToObject<Vector3>(serializer);
			float tileWidth = root["tileWidth"].ToObject<float>(serializer);
			float tileHeight = root["tileHeight"].ToObject<float>(serializer);
			int maxTiles = root["maxTiles"].ToObject<int>(serializer);
			int maxPolys = root["maxPolys"].ToObject<int>(serializer);

			var mesh = new TiledNavMesh(origin, tileWidth, tileHeight, maxTiles, maxPolys);

			JArray tilesToken = (JArray) root["tiles"];
			List<MeshTile> tiles = new List<MeshTile>();
			foreach (JToken tileToken in tilesToken)
			{
				mesh.AddTile(DeserializeMeshTile(tileToken, mesh.IdManager, mesh.GetNextTileRef()));
			}

			return mesh;
		}

		private JObject SerializeMeshTile(MeshTile tile)
		{
			var result = new JObject();
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

			var treeNodes = (BVTree.Node[])GetPrivateField(tile.BVTree, "nodes");
			JObject treeObject = new JObject();
			treeObject.Add("nodes", JToken.FromObject(treeNodes, serializer));

			result.Add("bvTree", treeObject);
			result.Add("bvQuantFactor", JToken.FromObject(tile.BvQuantFactor, serializer));
			result.Add("bvNodeCount", JToken.FromObject(tile.BvNodeCount, serializer));
			result.Add("walkableClimb", JToken.FromObject(tile.WalkableClimb, serializer));

			return result;
		}

		private MeshTile DeserializeMeshTile(JToken token, PolyIdManager manager, PolyId refId)
		{
			Vector2i location = token["location"].ToObject<Vector2i>(serializer);
			int layer = token["layer"].ToObject<int>(serializer);
			MeshTile result = new MeshTile(location, layer, manager, refId);

			result.Salt = token["salt"].ToObject<int>(serializer);
			result.Bounds = token["bounds"].ToObject<BBox3>(serializer);
			result.Polys = token["polys"].ToObject<Poly[]>(serializer);
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
	
			var tree = (BVTree) FormatterServices.GetUninitializedObject(typeof(BVTree));
			var treeObject = (JObject) token["bvTree"];
			var nodes = treeObject.GetValue("nodes").ToObject<BVTree.Node[]>();

			SetPrivateField(tree, "nodes", nodes);
			result.BVTree = tree;

			return result;
		}
	}
}
