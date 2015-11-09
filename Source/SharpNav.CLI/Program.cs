// Copyright (c) 2015 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
// Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

using Mono.Options;

using SharpNav.Geometry;
using SharpNav.IO;
using SharpNav.IO.Json;



//TODO differentiate between exit codes for errors
//TODO documentation should include a sample YAML file
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

				NavMeshConfigurationFile file = new NavMeshConfigurationFile(input);

				Log.Debug("Parsed configuration:");
				Log.Debug("Cell Size:          " + file.GenerationSettings.CellSize, 1);
				Log.Debug("Cell Height:        " + file.GenerationSettings.CellHeight, 1);
				Log.Debug("Max Climb:          " + file.GenerationSettings.MaxClimb, 1);
				Log.Debug("Agent Height:       " + file.GenerationSettings.AgentHeight, 1);
				Log.Debug("Agent Radius:       " + file.GenerationSettings.AgentRadius, 1);
				Log.Debug("Min Region Size:    " + file.GenerationSettings.MinRegionSize, 1);
				Log.Debug("Merged Region Size: " + file.GenerationSettings.MergedRegionSize, 1);
				Log.Debug("Max Edge Length:    " + file.GenerationSettings.MaxEdgeLength, 1);
				Log.Debug("Max Edge Error:     " + file.GenerationSettings.MaxEdgeError, 1);
				Log.Debug("Verts Per Poly:     " + file.GenerationSettings.VertsPerPoly, 1);
				Log.Debug("Sample Distance:    " + file.GenerationSettings.SampleDistance, 1);
				Log.Debug("Max Sample Error:   " + file.GenerationSettings.MaxSampleError, 1);
				Log.Debug("");
				Log.Debug("Output File: " + file.ExportPath, 1);
				Log.Debug("");
				Log.Debug("Meshes");

				List<string> meshes = new List<string>();
				List<ObjModel> models = new List<ObjModel>();

				foreach (var mesh in file.InputMeshes)
				{
					Log.Debug("Path:  " + mesh.Path, 2);
					Log.Debug("Scale: " + mesh.Scale, 2);
					Log.Debug("Position: " + mesh.Position.ToString(), 2);
					meshes.Add(mesh.Path);
					
					Vector3 position = new Vector3(mesh.Position[0], mesh.Position[1], mesh.Position[2]);

					if (File.Exists(mesh.Path))
					{
						ObjModel obj = new ObjModel(mesh.Path);
						float scale = mesh.Scale;
						//TODO SCALE THE OBJ FILE
						models.Add(obj);
					}
					else
					{
						Log.Error("Mesh file does not exist.");
						return 1;
					}
					
				}

				var tris = Enumerable.Empty<Triangle3>();
				foreach (var model in models)
					tris = tris.Concat(model.GetTriangles());

				TiledNavMesh navmesh = NavMesh.Generate(tris, file.GenerationSettings);
				new NavMeshJsonSerializer().Serialize(file.ExportPath, navmesh);
			}

			Log.WriteLine("Done. " + files.Count + " files processed.");

			#if DEBUG
			Console.ReadLine();
			#endif

			return 0;
		}

		
	}
}
