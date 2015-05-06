// Copyright (c) 2015 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
// Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

using Mono.Options;

using SharpNav.Geometry;

using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

//TODO differentiate between exit codes for errors
//TODO documentation should include a sample YAML file
//TODO move YAML parsing into SharpNav.IO
//TODO more logging

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
			List<string> files = new List<string>();

			var set = new OptionSet()
				.Add("verbosity=|v=", "Changes verbosity level. Valid options:\ns[ilent]\nm[inimal]\nn[ormal]\nv[erbose]\nd[ebug]", opt => { Log.Verbosity = Verbosity.Parse(opt); })
				.Add("version", "Displays version information", opt => version = (opt != null))
				.Add("help|h", "Displays usage information", opt => help = (opt != null));

			try
			{
				files = set.Parse(args);
			}
			catch (OptionException)
			{
				Log.Error("Invalid option.");
				Log.WriteLine("usage: sharpnav <OPTIONS> [FILES]");
				Log.WriteLine("Avaliable Options:");
				set.WriteOptionDescriptions(Console.Out);
				return 1;
			}

			if (help)
			{
				Log.WriteLine("usage: sharpnav <OPTIONS> [FILES]");
				Log.WriteLine("Available Options:");
				set.WriteOptionDescriptions(Console.Out);
				return 0;
			}

			if (version)
			{
				Log.WriteLine("SharpNav     " + SharpNavVersion);
				Log.WriteLine("SharpNav.CLI " + ThisVersion);
				return 0;
			}

			if (files.Count == 0)
			{
				Log.Error("No configuration files to process.");
				return 1;
			}

			Log.Info("Number of files to parse: " + files.Count);

			foreach (var f in files)
			{
				StreamReader input = null;
				Log.Info("Parsing file \"" + f + "\"");


				try
				{
					input = new StreamReader(f);
				}
				catch (Exception e)
				{
					Log.Error("Error opening file \"" + f + "\".");
					Log.Debug(e.GetType().ToString() + " thrown:");
					Log.Debug(e.StackTrace, 1);
					return 1;
				}

				var deserializer = new Deserializer(namingConvention: new HyphenatedNamingConvention());
				var setting = deserializer.Deserialize<Setting>(input);

				Log.Debug("Parsed configuration:");
				Log.Debug("Cell Size:          " + setting.Config.CellSize, 1);
				Log.Debug("Cell Height:        " + setting.Config.CellHeight, 1);
				Log.Debug("Max Climb:          " + setting.Config.MaxClimb, 1);
				Log.Debug("Agent Height:       " + setting.Config.AgentHeight, 1);
				Log.Debug("Agent Radius:       " + setting.Config.AgentRadius, 1);
				Log.Debug("Min Region Size:    " + setting.Config.MinRegionSize, 1);
				Log.Debug("Merged Region Size: " + setting.Config.MergedRegionSize, 1);
				Log.Debug("Max Edge Length:    " + setting.Config.MaxEdgeLength, 1);
				Log.Debug("Max Edge Error:     " + setting.Config.MaxEdgeError, 1);
				Log.Debug("Verts Per Poly:     " + setting.Config.VertsPerPoly, 1);
				Log.Debug("Sample Distance:    " + setting.Config.SampleDistance, 1);
				Log.Debug("Max Sample Error:   " + setting.Config.MaxSampleError, 1);
				Log.Debug("");
				Log.Debug("Output File: " + setting.Export, 1);
				Log.Debug("");
				Log.Debug("Meshes");

				List<string> meshes = new List<string>();
				List<ObjModel> models = new List<ObjModel>();

				foreach (var mesh in setting.Meshes)
				{
					Log.Debug("Path:  " + mesh.Path, 2);
					Log.Debug("Scale: " + mesh.Scale, 2);
					meshes.Add(mesh.Path);
					
					//Log.Debug("array: " + mesh.Position[0] + " " + mesh.Position[1] + " " + mesh.Position[2]);
					mesh.vector = new  Vector3(mesh.Position[0], mesh.Position[1], mesh.Position[2]);
					//Log.Debug("vector: " + mesh.vector.X + " " + mesh.vector.Y + " " + mesh.vector.Z);

					if (File.Exists(mesh.Path))
					{
						ObjModel obj = new ObjModel(mesh.Path);
						float scale = mesh.Scale;
						//TODO SCALE THE OBJ FILE
						models.Add(obj);
						Log.Debug("Position vector: " + mesh.vector.X + ", " + mesh.vector.Y + ", " + mesh.vector.Z);
					}
					else
					{
						Log.Error("Mesh file does not exist.");
						return 1;
					}
					
				}
			}

			Log.WriteLine("Done. " + files.Count + " files processed.");

			#if DEBUG
			Console.ReadLine();
			#endif

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
