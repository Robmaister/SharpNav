// Copyright (c) 2014 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
// Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE

using System.ComponentModel;
using System.Drawing.Design;
using System.Windows.Forms;
using System.Windows.Forms.Design;
using System;

namespace SharpNav
{
	/// <summary>
	/// The following two classes are made for the GUI application purpose
	/// </summary>
	class NavMeshGenerationSettingsForm : Form
	{
		private PropertyGrid p;
		private Button okButton;
		public NavMeshGenerationSettingsForm()
		{
			p = new PropertyGrid();
			Controls.Add(p);
			p.Dock = DockStyle.Fill;
			okButton = new Button();
			okButton.Text = "OK";
			okButton.Dock = DockStyle.Bottom;
			okButton.DialogResult = DialogResult.OK;
			Controls.Add(okButton);
		}
		public NavMeshGenerationSettings NavSetting
		{
			get { return p.SelectedObject as NavMeshGenerationSettings; }
			set { p.SelectedObject = value; }
		}
	}
	class NavMeshGenerationSettingsEditor : UITypeEditor
	{
		public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext context)
		{
			return UITypeEditorEditStyle.Modal;
		}
		public override object EditValue(ITypeDescriptorContext context, System.IServiceProvider provider, object value)
		{
			IWindowsFormsEditorService svc = provider.GetService(typeof(IWindowsFormsEditorService)) as IWindowsFormsEditorService;

			NavMeshGenerationSettings config = value as NavMeshGenerationSettings;

			using (NavMeshGenerationSettingsForm form = new NavMeshGenerationSettingsForm())
			{
				form.NavSetting = config;
				if (svc.ShowDialog(form) == DialogResult.OK)
				{
					config = form.NavSetting; // update object
				}
			}

			return value; // can also replace the wrapper object here
		}
	}

	/// <summary>
	/// Contains all the settings necessary to convert a mesh to a navmesh.
	/// </summary>
	[Editor(typeof(NavMeshGenerationSettingsEditor), typeof(UITypeEditor))]
	[TypeConverter(typeof(ExpandableObjectConverter))]
	public class NavMeshGenerationSettings
	{
		/// <summary>
		/// Prevents a default instance of the <see cref="NavMeshGenerationSettings"/> class from being created.
		/// Use <see cref="Default"/> instead.
		/// </summary>
		public NavMeshGenerationSettings()
		{
			//TODO now that this is public set reasonable defaults.
		}

		/// <summary>
		/// Gets the "default" generation settings for a model where 1 unit represents 1 meter.
		/// </summary>
		public static NavMeshGenerationSettings Default
		{
			get
			{
				//TODO rename this property to something more descriptive.
				var settings = new NavMeshGenerationSettings();

				settings.CellSize = 0.3f;
				settings.CellHeight = 0.2f;
				settings.MaxClimb = 0.9f;
				settings.AgentHeight = 2.0f;
				settings.AgentRadius = 0.6f;
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

		/// <summary>
		/// Gets or sets the maximum climb height.
		/// </summary>
		public float MaxClimb { get; set; }

		/// <summary>
		/// Gets or sets the height of the agents traversing the <see cref="NavMesh"/>.
		/// </summary>
		public float AgentHeight { get; set; }

		/// <summary>
		/// Gets or sets the radius of the agents traversing the <see cref="NavMesh"/>.
		/// </summary>
		public float AgentRadius { get; set; }

		/// <summary>
		/// Gets or sets the minimum number of spans that can form a region. Any less than this, and they will be
		/// merged with another region.
		/// </summary>
		public int MinRegionSize { get; set; }

		/// <summary>
		/// Gets or sets the size of the merged regions
		/// </summary>
		public int MergedRegionSize { get; set; }

		/// <summary>
		/// Gets or sets the maximum edge length allowed
		/// </summary>
		public int MaxEdgeLength { get; set; }

		/// <summary>
		/// Gets or sets the maximum error allowed
		/// </summary>
		public float MaxEdgeError { get; set; }

		/// <summary>
		/// Gets or sets the flags that determine how the <see cref="ContourSet"/> is generated.
		/// </summary>
		public ContourBuildFlags ContourFlags { get; set; }

		/// <summary>
		/// Gets or sets the number of vertices a polygon can have.
		/// </summary>
		public int VertsPerPoly { get; set; }

		/// <summary>
		/// Gets or sets the sampling distance for the PolyMeshDetail
		/// </summary>
		public int SampleDistance { get; set; }

		/// <summary>
		/// Gets or sets the maximium error allowed in sampling for the PolyMeshDetail
		/// </summary>
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

		/// <summary>
		/// Gets the maximum clim height in voxel (cell) units.
		/// </summary>
		public int VoxelMaxClimb
		{
			get
			{
				return (int)(MaxClimb / CellHeight);
			}
		}

		/// <summary>
		/// Gets the radius of the agents traversing the <see cref="NavMesh"/> in voxel (cell) units.
		/// </summary>
		public int VoxelAgentRadius
		{
			get
			{
				return (int)(AgentRadius / CellHeight);
			}
		}
	}
}
