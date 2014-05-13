using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SharpNav
{
	/// <summary>
	/// Contains all the settings necessary to convert a mesh to a navmesh.
	/// </summary>
	public class NavMeshGenerationSettings
	{
		/// <summary>
		/// This constructor is private temporarily, easier to make sure parameters are valid if static properties are used.
		/// </summary>
		private NavMeshGenerationSettings()
		{
		}

		/// <summary>
		/// Gets the "default" generation settings for a model where 1 unit represents 1 meter.
		/// </summary>
		public static NavMeshGenerationSettings Default
		{
			get
			{
				var settings = new NavMeshGenerationSettings();

				settings.CellSize = 0.3f;
				settings.CellHeight = 0.2f;
				settings.MaxClimb = 0.9f;
				settings.AgentHeight = 2.0f;
				settings.AgentWidth = 0.6f;
				settings.MinRegionSize = 8;
				settings.MergedRegionSize = 20;
				settings.MaxEdgeLength = 12;
				settings.MaxEdgeError = 1.8f;
				settings.VertsPerPoly = 6;
				settings.SampleDistance = 6;
				settings.MaxSampleError = 1;

				settings.BuildBoundingVolumeTree = true;

				return settings;
			}
		}

		public float CellSize { get; set; }
		public float CellHeight { get; set; }
		public float MaxClimb { get; set; }
		public float AgentHeight { get; set; }
		public float AgentWidth { get; set; }
		public int MinRegionSize { get; set; }
		public int MergedRegionSize { get; set; }
		public int MaxEdgeLength { get; set; }
		public float MaxEdgeError { get; set; }
		public ContourBuildFlags ContourFlags { get; set; }
		public int VertsPerPoly { get; set; }
		public int SampleDistance { get; set; }
		public int MaxSampleError { get; set; }

		public bool BuildBoundingVolumeTree { get; set; }

		public int VoxelAgentHeight
		{
			get
			{
				return (int)(AgentHeight / CellHeight);
			}
		}

		public int VoxelMaxClimb
		{
			get
			{
				return (int)(MaxClimb / CellHeight);
			}
		}

		public int VoxelAgentWidth
		{
			get
			{
				return (int)(AgentWidth / CellHeight);
			}
		}
	}
}
