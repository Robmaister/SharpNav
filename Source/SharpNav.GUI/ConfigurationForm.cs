// Copyright (c) 2015 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
// Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Forms;
using System.IO;

using SharpNav.Geometry;
using SharpNav.IO;

namespace SharpNav.GUI
{
	public partial class ConfigurationForm : Form
	{
		private string cwd { get; set; } //store the current working directory

		private bool changed { get; set; } //flag for file changes

		public ConfigurationForm()
		{
			InitializeComponent();
			changed = false;

			//change the way things are viewed without adding System.ComponentModel as a dependency to SharpNav
			TypeDescriptor.AddAttributes(typeof(NavMeshGenerationSettings), new TypeConverterAttribute(typeof(ExpandableObjectConverter)));
			propertyGrid1.BrowsableAttributes = new AttributeCollection(new Attribute[] {
				new BrowsableAttribute(true),
				new ReadOnlyAttribute(false)
			});
		}

		protected override bool ProcessDialogKey(Keys keyData)
		{
			changed = true;
			return base.ProcessDialogKey(keyData);
		}

		private void newToolStripMenuItem_Click(object sender, EventArgs e)
		{
			NavMeshConfigurationFile file = new NavMeshConfigurationFile();
			file.GenerationSettings = NavMeshGenerationSettings.Default;
			file.ExportPath = "default.snb";
			propertyGrid1.SelectedObject = file;
			changed = true;
		}

		private void openToolStripMenuItem_Click(object sender, EventArgs e)
		{
			if (openSettingsFileDialog.ShowDialog() == DialogResult.OK)
			{
				var input = new StreamReader(File.OpenRead(openSettingsFileDialog.FileName));

				var file = new NavMeshConfigurationFile(input);

				propertyGrid1.SelectedObject = file;

				cwd = openSettingsFileDialog.FileName;
			}
		}

		//TODO remove function now that it's a single line
		private void saveprocess(NavMeshConfigurationFile file, string location)
		{
			file.Save(location);
		}

		private void saveToolStripMenuItem_Click(object sender, EventArgs e)
		{
			NavMeshConfigurationFile setting = propertyGrid1.SelectedObject as NavMeshConfigurationFile;
			if (setting == null)
			{
				//MessageBox.Show("Nothing to save");
			}
			else
			{
				if (cwd == null)
					saveAsToolStripMenuItem_Click(sender, e);
				else
					saveprocess(setting, cwd);
			}
		}

		private void saveAsToolStripMenuItem_Click(object sender, EventArgs e)
		{
			NavMeshConfigurationFile file = propertyGrid1.SelectedObject as NavMeshConfigurationFile;
			if (file == null)
			{
				//MessageBox.Show("Nothing to save");
			}
			else
			{
				if (saveSettingsFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
				{
					saveprocess(file, saveSettingsFileDialog.FileName);
				}
			}
		}

		private void exitToolStripMenuItem_Click(object sender, EventArgs e)
		{
			if (changed == false)
				Application.Exit();
			else
			{
				DialogResult result = MessageBox.Show("Save before Exit?", "SharpNav GUI",
										MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);
				if (result == DialogResult.Yes)
				{
					if (cwd == null)
						saveAsToolStripMenuItem_Click(sender, e);
					else
					{
						NavMeshConfigurationFile setting = propertyGrid1.SelectedObject as NavMeshConfigurationFile;
						saveprocess(setting, cwd);
					}
					Application.Exit();
				}
				else if (result == DialogResult.No)
				{
					Application.Exit();
				}
			}
		}

		private void resetAllToolStripMenuItem_Click(object sender, EventArgs e)
		{
			NavMeshConfigurationFile setting = new NavMeshConfigurationFile();
			setting.GenerationSettings = NavMeshGenerationSettings.Default;
			setting.ExportPath = "default.snb";
			propertyGrid1.SelectedObject = setting;
			changed = true;
		}

		private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
		{
			//TODO different form with GitHub link
			if (MessageBox.Show("This is the GUI version of the SharpNav Configuration Tool. Visit the GitHub page?", "About", MessageBoxButtons.YesNo, MessageBoxIcon.Asterisk) == DialogResult.Yes)
			{
				System.Diagnostics.Process.Start("https://github.com/Robmaister/SharpNav");
			}
		}

		private void generateButton_Click(object sender, EventArgs e)
		{
			NavMeshConfigurationFile setting = propertyGrid1.SelectedObject as NavMeshConfigurationFile;
			if (setting == null)
			{
				return;
			}
			List<ObjModel> models = new List<ObjModel>();

			if (setting.InputMeshes.Count == 0)
			{
				MessageBox.Show("No Obj files included");
				return;
			}

			foreach (var mesh in setting.InputMeshes)
			{
				//mesh.vector = new Vector3(mesh.Position[0], mesh.Position[1], mesh.Position[2]);

				if (File.Exists(mesh.Path))
				{
					ObjModel obj = new ObjModel(mesh.Path);
					float scale = mesh.Scale;
					//TODO SCALE THE OBJ FILE
					models.Add(obj);
				}
				else
				{
					MessageBox.Show(mesh.Path + "\nObj file not exists.");
				}

			}
		}

		private void propertyGrid1_PropertyValueChanged(object s, PropertyValueChangedEventArgs e)
		{
			changed = true;
		}
	}
}
