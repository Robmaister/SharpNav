// Copyright (c) 2013-2014 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
// Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE

using System;

using Gwen;
using Gwen.Control;

using SharpNav;
using SharpNav.Geometry;

//Doesn't compile if in an unsupported configuration
#if STANDALONE || OPENTK

namespace SharpNav.Examples
{
	public partial class ExampleWindow
	{
		private NavMeshGenerationSettings settings;
		private AreaIdGenerationSettings areaSettings;

		private StatusBar statusBar;

		private void InitializeUI()
		{
			settings = NavMeshGenerationSettings.Default;
			areaSettings = new AreaIdGenerationSettings();

			DockBase dock = new DockBase(gwenCanvas);
			dock.Dock = Pos.Fill;
			dock.SetSize(Width, Height);
			dock.RightDock.Width = 280;
			dock.BottomDock.Height = 150;

			statusBar = new StatusBar(gwenCanvas);

			Label genTime = new Label(statusBar);
			genTime.Name = "GenTime";
			genTime.Text = "Generation Time: 0ms";
			genTime.Dock = Pos.Left;

			LabeledCheckBox catchCheckBox = new LabeledCheckBox(statusBar);
			catchCheckBox.Text = "Intercept and log exceptions";
			catchCheckBox.Dock = Pos.Right;
			catchCheckBox.CheckChanged += (s, e) => interceptExceptions = catchCheckBox.IsChecked;
			catchCheckBox.IsChecked = true;

			Base genBase = new Base(dock);
			dock.RightDock.TabControl.AddPage("NavMesh Generation", genBase);

			Button generateButton = new Button(genBase);
			generateButton.Text = "Generate!";
			generateButton.Height = 30;
			generateButton.Dock = Pos.Top;
			generateButton.Released += (s, e) => GenerateNavMesh();

			GroupBox displaySettings = new GroupBox(genBase);
			displaySettings.Text = "Display";
			displaySettings.Dock = Pos.Top;
			displaySettings.Height = 60;

			Base levelCheckBase = new Base(displaySettings);
			levelCheckBase.Dock = Pos.Top;

			Label levelCheckLabel = new Label(levelCheckBase);
			levelCheckLabel.Text = "Level";
			levelCheckLabel.Dock = Pos.Left;

			CheckBox levelCheckBox = new CheckBox(levelCheckBase);
			levelCheckBox.Dock = Pos.Right;
			levelCheckBox.Checked += (s, e) => displayLevel = true;
			levelCheckBox.UnChecked += (s, e) => displayLevel = false;
			levelCheckBox.IsChecked = true;

			levelCheckBase.SizeToChildren();

			Base displayModeBase = new Base(displaySettings);
			displayModeBase.Dock = Pos.Top;
			displayModeBase.Padding = new Padding(0, 4, 0, 0);

			Label displayModeLabel = new Label(displayModeBase);
			displayModeLabel.Text = "Generation Step";
			displayModeLabel.Dock = Pos.Left;
			displayModeLabel.Padding = new Padding(0, 0, 4, 0);

			ComboBox displayModes = new ComboBox(displayModeBase);
			displayModes.Dock = Pos.Top;
			displayModes.AddItem("None", "", DisplayMode.None);
			displayModes.AddItem("Heightfield", "", DisplayMode.Heightfield);
			displayModes.AddItem("Compact Heightfield", "", DisplayMode.CompactHeightfield);
			displayModes.AddItem("Distance Field", "", DisplayMode.DistanceField);
			displayModes.AddItem("Regions", "", DisplayMode.Regions);
			displayModes.AddItem("Contours", "", DisplayMode.Contours);
			displayModes.AddItem("Polygon Mesh", "", DisplayMode.PolyMesh);
			displayModes.AddItem("Polygon Mesh Detail", "", DisplayMode.PolyMeshDetail);
			displayModes.AddItem("NavMesh", "", DisplayMode.NavMesh);
			displayModes.AddItem("Pathfinding", "", DisplayMode.Pathfinding);
			displayModes.ItemSelected += (s, e) => displayMode = (DisplayMode)e.SelectedItem.UserData;

			displayModes.SelectByUserData(DisplayMode.PolyMeshDetail);

			displayModeBase.SizeToChildren();
			displayModeBase.Height += 4; //accounts for the padding, GWEN.NET should do this

			const int leftMax = 125;
			const int rightMax = 20;

			GroupBox areaSetting = new GroupBox(genBase);
			areaSetting.Text = "Area";
			areaSetting.Dock = Pos.Top;
			areaSetting.Height = 90;

			var levelTris = level.GetTriangles();
			BBox3 bounds = TriangleEnumerable.FromTriangle(levelTris, 0, levelTris.Length).GetBoundingBox();

			Base maxTriSlope = CreateSliderOption(areaSetting, "Max Tri Slope:", 0.0001f, 3.14f, 3.14f, "N2", leftMax, rightMax, v => areaSettings.MaxTriSlope = v);
			Base minLevelHeight = CreateSliderOption(areaSetting, "Min Height:", bounds.Min.Y, bounds.Max.Y, bounds.Min.Y, "N0", leftMax, rightMax, v => areaSettings.MinLevelHeight = v);
			Base maxLevelHeight = CreateSliderOption(areaSetting, "Max Height:", bounds.Min.Y, bounds.Max.Y, bounds.Max.Y, "N0", leftMax, rightMax, v => areaSettings.MaxLevelHeight = v);

			GroupBox rsSettings = new GroupBox(genBase);
			rsSettings.Text = "Rasterization";
			rsSettings.Dock = Pos.Top;
			rsSettings.Height = 90;

			Base cellSizeSetting = CreateSliderOption(rsSettings, "Cell Size:", 0.1f, 2.0f, 0.3f, "N2", leftMax, rightMax, v => settings.CellSize = v);
			Base cellHeightSetting = CreateSliderOption(rsSettings, "Cell Height:", 0.1f, 2f, 0.2f, "N2", leftMax, rightMax, v => settings.CellHeight = v);

			GroupBox agentSettings = new GroupBox(genBase);
			agentSettings.Text = "Agent";
			agentSettings.Dock = Pos.Top;
			agentSettings.Height = 115;

			Base maxSlopeSetting = CreateSliderOption(agentSettings, "Max Climb:", 0.1f, 5.0f, 0.9f, "N0", leftMax, rightMax, v => settings.MaxClimb = v);
			Base maxHeightSetting = CreateSliderOption(agentSettings, "Height:", 0.1f, 5.0f, 2.0f, "N0", leftMax, rightMax, v => { settings.AgentHeight = v; agentCylinder.Height = v; });
			Base erodeRadius = CreateSliderOption(agentSettings, "Radius:", 0.0f, 5.0f, 0.6f, "N1", leftMax, rightMax, v => { settings.AgentRadius = v; agentCylinder.Radius = v; });
			Base addRemoveAgent = CreateAddRemoveButton(agentSettings, "Count", leftMax, rightMax, 0, MAX_AGENTS, () => { numActiveAgents++; GenerateCrowd(); }, () => { numActiveAgents = Math.Max(0, numActiveAgents - 1); GenerateCrowd(); });

			GroupBox regionSettings = new GroupBox(genBase);
			regionSettings.Text = "Region";
			regionSettings.Dock = Pos.Top;
			regionSettings.Height = 65;

			Base minRegionSize = CreateSliderOption(regionSettings, "Min Region Size:", 0f, 150f, 8f, "N0", leftMax, rightMax, v => settings.MinRegionSize = (int)Math.Round(v));
			Base mrgRegionSize = CreateSliderOption(regionSettings, "Merged Region Size:", 0f, 150f, 20f, "N0", leftMax, rightMax, v => settings.MergedRegionSize = (int)Math.Round(v));

			GroupBox navMeshSettings = new GroupBox(genBase);
			navMeshSettings.Text = "NavMesh";
			navMeshSettings.Dock = Pos.Top;
			navMeshSettings.Height = 90;

			Base maxEdgeLength = CreateSliderOption(navMeshSettings, "Max Edge Length:", 0f, 50f, 12f, "N0", leftMax, rightMax, v => settings.MaxEdgeLength = (int)Math.Round(v));
			Base maxEdgeErr = CreateSliderOption(navMeshSettings, "Max Edge Error:", 0f, 3f, 1.8f, "N1", leftMax, rightMax, v => settings.MaxEdgeError = v);
			Base vertsPerPoly = CreateSliderOption(navMeshSettings, "Verts Per Poly:", 3f, 12f, 6f, "N0", leftMax, rightMax, v => settings.VertsPerPoly = (int)Math.Round(v));

			GroupBox navMeshDetailSettings = new GroupBox(genBase);
			navMeshDetailSettings.Text = "NavMeshDetail";
			navMeshDetailSettings.Dock = Pos.Top;
			navMeshDetailSettings.Height = 65;

			Base sampleDistance = CreateSliderOption(navMeshDetailSettings, "Sample Distance:", 0f, 16f, 6f, "N0", leftMax, rightMax, v => settings.SampleDistance = (int)Math.Round(v));
			Base maxSampleError = CreateSliderOption(navMeshDetailSettings, "Max Sample Error:", 0f, 16f, 1f, "N0", leftMax, rightMax, v => settings.MaxSampleError = (int)Math.Round(v));

			Base logBase = new Base(dock);
			dock.BottomDock.TabControl.AddPage("Log", logBase);

			ListBox logBox = new ListBox(logBase);
			logBox.Dock = Pos.Fill;
			logBox.AllowMultiSelect = false;
			logBox.EnableScroll(true, true);
			Console.SetOut(new GwenTextWriter(logBox));
		}

		private Base CreateAddRemoveButton(Base parent, string labelText, int labelMaxWidth, int valueLabelMaxWidth, int minValue, int maxValue, Action onAdd, Action onRemove)
		{
			Base b = new Base(parent);
			b.Dock = Pos.Top;
			b.Padding = new Padding(0, 0, 0, 4);

			//Base r = new Base(b);
			//r.Dock = Pos.Right;
			//r.Padding = new Padding(0, 0, 0, 0);

			//Base l = new Base(b);
			//l.Dock = Pos.Left;
			//l.Padding = new Padding(0, 0, 0, 0);


			Label label = new Label(b);
			label.Text = labelText;
			label.Dock = Pos.Left;
			label.Padding = new Padding(0, 0, Math.Max(0, labelMaxWidth - label.Width), 0);

			Label valueLabel = new Label(b);
			valueLabel.Dock = Pos.Left;
			valueLabel.Text = "0";
			valueLabel.Padding = new Padding(Math.Max(0, valueLabelMaxWidth - valueLabel.Width), 0, 0, 0);

			Button addButton = new Button(b);
			addButton.Text = "+";
			addButton.Height = 20;
			addButton.Width = 20;
			addButton.Dock = Pos.Right;
			addButton.Released += (s, e) => onAdd();
			addButton.Released += (s, e) => valueLabel.Text = Math.Min(maxValue, int.Parse(valueLabel.Text) + 1).ToString();

			Button removeButton = new Button(b);
			removeButton.Text = "-";
			removeButton.Height = 20;
			removeButton.Width = 20;
			removeButton.Dock = Pos.Right;
			removeButton.Released += (s, e) => onRemove();
			removeButton.Released += (s, e) => valueLabel.Text = Math.Max(minValue, int.Parse(valueLabel.Text) - 1).ToString();

			b.SizeToChildren();

			return b;
		}

		private Base CreateSliderOption(Base parent, string labelText, float min, float max, float value, string valueStringFormat, int labelMaxWidth, int valueLabelMaxWidth, Action<float> onChange)
		{
			Base b = new Base(parent);
			b.Dock = Pos.Top;
			b.Padding = new Padding(0, 0, 0, 4);

			Label label = new Label(b);
			label.Text = labelText;
			label.Dock = Pos.Left;
			label.Padding = new Padding(0, 0, Math.Max(0, labelMaxWidth - label.Width), 0);

			Label valueLabel = new Label(b);
			valueLabel.Dock = Pos.Right;

			HorizontalSlider slider = new HorizontalSlider(b);
			slider.Dock = Pos.Fill;
			slider.Height = 20;
			slider.Min = min;
			slider.Max = max;
			slider.Value = value;

			slider.ValueChanged += (s, e) =>
			{
				int prevWidth = valueLabel.Width;
				valueLabel.Text = slider.Value.ToString(valueStringFormat);
				valueLabel.Padding = new Padding(valueLabel.Padding.Left - (valueLabel.Width - prevWidth), 0, 0, 0);
			};
			slider.ValueChanged += (s, e) => onChange(slider.Value);

			valueLabel.Text = value.ToString(valueStringFormat);
			valueLabel.Padding = new Padding(Math.Max(0, valueLabelMaxWidth - valueLabel.Width), 0, 0, 0);
			onChange(value);

			b.SizeToChildren();

			return b;
		}

		private class AreaIdGenerationSettings
		{
			public float MaxTriSlope { get; set; }
			public float MinLevelHeight { get; set; }
			public float MaxLevelHeight { get; set; }
		}
	}
}

#endif
