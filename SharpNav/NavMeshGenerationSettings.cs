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
		/// Prevents a default instance of the <see cref="NavMeshGenerationSettings"/> class from being created.
		/// Use <see cref="Default"/> instead.
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

		/// <summary>
		/// Gets or sets the size of a cell in the X and Z axes in world units.
		/// </summary>
		public float CellSize { get; set; }

		/// <summary>
		/// Gets or sets the height of a cell in world units.
		/// </summary>
		public float CellHeight { get; set; }

		public float MaxClimb { get; set; }

		/// <summary>
		/// Gets or sets the height of the agents traversing the <see cref="NavMesh"/>.
		/// </summary>
		public float AgentHeight { get; set; }

		/// <summary>
		/// Gets or sets the width (radius) of the agents traversing the <see cref="NavMesh"/>.
		/// </summary>
		public float AgentWidth { get; set; }

		/// <summary>
		/// Gets or sets the minimum number of spans that can form a region. Any less than this, and they will be
		/// merged with another region.
		/// </summary>
		public int MinRegionSize { get; set; }

		public int MergedRegionSize { get; set; }

		public int MaxEdgeLength { get; set; }

		public float MaxEdgeError { get; set; }

		/// <summary>
		/// Gets or sets the flags that determine how the <see cref="ContourSet"/> is generated.
		/// </summary>
		public ContourBuildFlags ContourFlags { get; set; }

		public int VertsPerPoly { get; set; }

		public int SampleDistance { get; set; }

		public int MaxSampleError { get; set; }

		/// <summary>
		/// Gets or sets a value indicating whether a bounding volume tree is generated for the mesh.
		/// </summary>
		public bool BuildBoundingVolumeTree { get; set; }

		/// <summary>
		/// Gets the height of the agents traversing the <see cref="NavMesh"/> in voxel (cell) units.
		/// </summary>
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

		/// <summary>
		/// Gets the width (radius) of the agents traversing the <see cref="NavMesh"/> in voxel (cell) units.
		/// </summary>
		public int VoxelAgentWidth
		{
			get
			{
				return (int)(AgentWidth / CellHeight);
			}
		}
	}
}
