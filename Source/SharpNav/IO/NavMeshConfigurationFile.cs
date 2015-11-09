using System;
using System.Collections.Generic;
using System.IO;

using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

using SharpNav.Geometry;

#if MONOGAME
using Vector3 = Microsoft.Xna.Framework.Vector3;
#elif OPENTK
using Vector3 = OpenTK.Vector3;
#elif SHARPDX
using Vector3 = SharpDX.Vector3;
#endif

namespace SharpNav.IO
{
	public class NavMeshConfigurationFile
	{
		public NavMeshGenerationSettings GenerationSettings { get; set; }
		public string ExportPath { get; set; }
		public List<MeshSettings> InputMeshes { get; set; }

		public NavMeshConfigurationFile()
		{
			GenerationSettings = NavMeshGenerationSettings.Default;
			InputMeshes = new List<MeshSettings>();
		}

		public NavMeshConfigurationFile(StreamReader input)
		{
			var deserializer = new Deserializer(namingConvention: new HyphenatedNamingConvention());
			var data = deserializer.Deserialize<YamlData>(input);

			GenerationSettings = data.Config;
			ExportPath = data.Export;
			InputMeshes = data.Meshes;
		}

		public void Save(string path)
		{
			var data = new YamlData();
			data.Config = GenerationSettings;
			data.Export = ExportPath;
			data.Meshes = InputMeshes;

			var serializer = new Serializer(SerializationOptions.None, new HyphenatedNamingConvention());
			using (StreamWriter writer = new StreamWriter(path))
				serializer.Serialize(writer, data);
		}

		private class YamlData
		{
			public NavMeshGenerationSettings Config { get; set; }
			public string Export { get; set; }
			public List<MeshSettings> Meshes { get; set; }
		}

		public class MeshSettings
		{
			public string Path { get; set; }
			public float Scale { get; set; }
			//TODO make this class private, public one with Vector3 instead?
			public float[] Position { get; set; }
			//TODO: rotation;
		}
	}
}
