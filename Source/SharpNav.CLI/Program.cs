using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Mono.Options;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using SharpNav;
using SharpNav.Geometry;


namespace SharpNav.CLI
{
	class Program
	{
		private static readonly string SharpNavVersion = typeof(NavMesh).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;
		private static readonly string ThisVersion = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;

		static int Main(string[] args)
		{
			bool help = false;
			bool version = false;
			Verbosity verbosity = Verbosity.Normal;
			List<string> files = new List<string>();

			var set = new OptionSet()
				.Add("verbosity=|v=", "Changes verbosity level. silent, minimal, normal, verbose, and debug are the only valid choices.", opt => { verbosity = Verbosity.Debug; })
				.Add("version", "Displays version information", opt => version = (opt != null))
				.Add("help|h", "Displays usage information", opt => help = (opt != null));

			try
			{
				files = set.Parse(args);
			}
			catch (OptionException)
			{
				Console.WriteLine("Options:");
				set.WriteOptionDescriptions(Console.Out);
				return 1;
			}

			if (help)
			{
				Console.WriteLine("Options:");
				set.WriteOptionDescriptions(Console.Out);
				return 0;
			}

			if (version)
			{
				Console.WriteLine("SharpNav     " + SharpNavVersion);
				Console.WriteLine("SharpNav.CLI " + ThisVersion);
				return 0;
			}

			if (verbosity > Verbosity.Normal)
				Console.WriteLine("Verbosity enabled (not really)");

			foreach (var f in files)
			{
				Console.WriteLine(f);
				var input = new StreamReader(f);

				var deserializer = new Deserializer(namingConvention: new HyphenatedNamingConvention());

				var setting = deserializer.Deserialize<Setting>(input);

				//string export_file_name = setting.Export;

				Console.WriteLine("Config:");
				Console.WriteLine();
				Console.WriteLine("cell-size: {0}", setting.Config.CellSize);
				Console.WriteLine("cell-height: {0}", setting.Config.CellHeight);
				Console.WriteLine("max-climb: {0}", setting.Config.MaxClimb);
				Console.WriteLine("agent-height: {0}", setting.Config.AgentHeight);
				Console.WriteLine("agent-radius: {0}", setting.Config.AgentRadius);
				Console.WriteLine("min-region-size: {0}", setting.Config.MinRegionSize);
				Console.WriteLine("merged-region-size: {0}", setting.Config.MergedRegionSize);
				Console.WriteLine("max-edge-len: {0}", setting.Config.MaxEdgeLength);
				Console.WriteLine("max-edge-error: {0}", setting.Config.MaxEdgeError);
				Console.WriteLine("verts-per-poly: {0}", setting.Config.VertsPerPoly);
				Console.WriteLine("sample-distance: {0}", setting.Config.SampleDistance);
				Console.WriteLine("max-sample-error: {0}", setting.Config.MaxSampleError);

				List<string> meshes = new List<string>();
				List<ObjModel> models = new List<ObjModel>();

				Console.WriteLine();
				Console.WriteLine("Export Path:");
				Console.WriteLine(setting.Export);
				Console.WriteLine();
				Console.WriteLine("Meshes:");
				foreach (var mesh in setting.Meshes)
				{
					Console.WriteLine("Path:{0}\nScale:{1}", mesh.Path, mesh.Scale);
					meshes.Add(mesh.Path);
					
					//Console.WriteLine("array: {0} {1} {2}", mesh.Position[0], mesh.Position[1], mesh.Position[2]);
					mesh.vector = new  Vector3(mesh.Position[0], mesh.Position[1], mesh.Position[2]);
					//Console.WriteLine("vector: {0} {1} {2}", mesh.vector.X, mesh.vector.Y, mesh.vector.Z);

					if (File.Exists(mesh.Path))
					{
						ObjModel obj = new ObjModel(mesh.Path);
						float scale = mesh.Scale;
						//TODO SCALE THE OBJ FILE
						models.Add(obj);
						Console.WriteLine("Position vector: {0} {1} {2}", mesh.vector.X, mesh.vector.Y, mesh.vector.Z);
					}
					else
					{
						Console.WriteLine("Obj file not exists.");
					}
					
				}
			}
			Console.ReadLine();
			return 0;
		}

		public class Setting
		{
			public NavMeshGenerationSettings Config { get; set; }
			public string Export { get; set; }
			public List<Object> Meshes { get; set; }

		}

		public class Export
		{
			public string Path { get; set; }
		}

		public class Object
		{
			public string Path { get; set; }
			public float Scale { get; set; }
			public float[] Position { get; set; }
			public Vector3 vector { get; set; }
			//TODO: rotation;
		}
	}
}
