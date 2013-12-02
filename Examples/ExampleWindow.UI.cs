using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Gwen;
using Gwen.Control;

namespace Examples
{
	public partial class ExampleWindow
	{
		private GenSettings settings;

		private void InitializeUI()
		{
			settings = new GenSettings();

			DockBase dock = new DockBase(gwenCanvas);
			dock.Dock = Pos.Fill;
			dock.SetSize(Width, Height);
			dock.RightDock.Width = 280;

			Base genBase = new Base(dock);
			dock.RightDock.TabControl.AddPage("NavMesh Generation", genBase);

			Button generateButton = new Button(genBase);
			generateButton.Text = "Generate!";
			generateButton.Height = 30;
			generateButton.Dock = Pos.Top;
			generateButton.Pressed += (s, e) => GenerateNavMesh();

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
			displayModes.AddItem("Simplified Contours", "", DisplayMode.SimplifiedContours);
			displayModes.AddItem("Navigation Mesh", "", DisplayMode.NavMesh);
			displayModes.AddItem("Navigation Mesh Detail", "", DisplayMode.NavMeshDetail);
			displayModes.ItemSelected += (s, e) => displayMode = (DisplayMode)e.SelectedItem.UserData;

			displayModes.SelectByUserData(DisplayMode.NavMesh);

			displayModeBase.SizeToChildren();
			displayModeBase.Height += 4; //accounts for the padding, GWEN.NET should do this

			const int leftMax = 125;
			const int rightMax = 20;

			GroupBox hfSettings = new GroupBox(genBase);
			hfSettings.Text = "Heightfield";
			hfSettings.Dock = Pos.Top;
			hfSettings.Height = 112;

			Base cellSizeSetting = CreateSliderOption(hfSettings, "Cell Size:", 0.1f, 2.0f, 0.2f, "N1", leftMax, rightMax, v => settings.CellSize = v);
			Base cellHeightSetting = CreateSliderOption(hfSettings, "Cell Height:", 0.1f, 2f, 0.1f, "N1", leftMax, rightMax, v => settings.CellHeight = v);
			Base maxSlopeSetting = CreateSliderOption(hfSettings, "Max Climb:", 1f, 20f, 15f, "N0", leftMax, rightMax, v => settings.MaxClimb = (int)Math.Round(v));
			Base maxHeightSetting = CreateSliderOption(hfSettings, "Max Height:", 1f, 50f, 40f, "N0", leftMax, rightMax, v => settings.MaxHeight = (int)Math.Round(v));

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
			Base maxEdgeErr = CreateSliderOption(navMeshSettings, "Max Edge Error:", 0f, 3f, 1.3f, "N1", leftMax, rightMax, v => settings.MaxEdgeError = v);
			Base vertsPerPoly = CreateSliderOption(navMeshSettings, "Verts Per Poly:", 3f, 12f, 6f, "N0", leftMax, rightMax, v => settings.VertsPerPoly = (int)Math.Round(v));

			GroupBox navMeshDetailSettings = new GroupBox(genBase);
			navMeshDetailSettings.Text = "NavMeshDetail";
			navMeshDetailSettings.Dock = Pos.Top;
			navMeshDetailSettings.Height = 65;

			Base sampleDistance = CreateSliderOption(navMeshDetailSettings, "Sample Distance:", 0f, 16f, 6f, "N0", leftMax, rightMax, v => settings.SampleDistance = (int)Math.Round(v));
			Base maxSampleError = CreateSliderOption(navMeshDetailSettings, "Max Sample Error:", 0f, 16f, 1f, "N0", leftMax, rightMax, v => settings.MaxSmapleError = (int)Math.Round(v));
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

		//TODO move this to SharpNav with better names/restrictions and create alternate constructors.
		private class GenSettings
		{
			public float CellSize { get; set; }
			public float CellHeight { get; set; }
			public int MaxClimb { get; set; }
			public int MaxHeight { get; set; }
			public int MinRegionSize { get; set; }
			public int MergedRegionSize { get; set; }
			public int MaxEdgeLength { get; set; }
			public float MaxEdgeError { get; set; }
			public int VertsPerPoly { get; set; }
			public int SampleDistance { get; set; }
			public int MaxSmapleError { get; set; }

			public override string ToString()
			{
				return "{" + CellSize + ", " + CellHeight + ", " + MaxClimb + ", " + MaxHeight + "}";
			}
		}
	}
}
