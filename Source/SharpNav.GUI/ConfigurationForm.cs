// Copyright (c) 2015 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
// Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Design;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Windows.Forms.Design;
using System.IO;

using SharpNav.Geometry;

using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

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
		}

		protected override bool ProcessDialogKey(Keys keyData)
		{
			changed = true;
			return base.ProcessDialogKey(keyData);
		}

		public class Setting
		{
			public NavMeshGenerationSettings config { get; set; }
			public string export { get; set; }
			public List<Object> meshes { get; set; }
		}

		public class Export
		{
			public string path { get; set; }
		}

		[TypeConverter(typeof(ExpandableObjectConverter))]
		public class Object
		{
			public string Path { get; set; }
			public float Scale { get; set; }
			public float[] Position { get; set; }
			//public Vector3 vector { get; set; }
			//TODO: rotation;
		}

		private void newToolStripMenuItem_Click(object sender, EventArgs e)
		{
			Setting setting = new Setting();
			setting.config = NavMeshGenerationSettings.Default;
			setting.export = "default.snb";
			setting.meshes = new List<Object>();
			propertyGrid1.SelectedObject = setting;
			changed = true;
		}

		private void openToolStripMenuItem_Click(object sender, EventArgs e)
		{

			OpenFileDialog open = new OpenFileDialog();
			open.Filter = "SharpNav Config Files (*.sncfg)|*.sncfg|  All Files (*.*)|*.*";


			if (open.ShowDialog() == DialogResult.OK)
			{
				var input = new StreamReader(File.OpenRead(open.FileName));

				var deserializer = new Deserializer(namingConvention: new HyphenatedNamingConvention());

				var setting = deserializer.Deserialize<Setting>(input);

				propertyGrid1.SelectedObject = setting;

				cwd = open.FileName;
			}
		}

		private void saveprocess(Setting setting, string location)
		{
			var serializer = new Serializer(namingConvention: new HyphenatedNamingConvention());
			string temp_path = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
			string temp_file = "sharpnavconfig_temp.txt";
			string temp = System.IO.Path.Combine(temp_path, temp_file);
			
			StreamWriter write = new StreamWriter(temp);
			serializer.Serialize(write, setting);
			write.Dispose();

			string line = null;
			using (StreamReader reader = new StreamReader(temp))
			{
				using (StreamWriter writer = new StreamWriter(File.Create(location)))
				{
					while ((line = reader.ReadLine()) != null)
					{
						int i = line.IndexOf(":");
						if(i<=0)
							writer.WriteLine(line);
						else
						{
							string sub = line.Substring(0, i);
							if(string.Compare(sub, "  voxel-agent-height") == 0
								||string.Compare(sub, "  voxel-max-climb") == 0
								||string.Compare(sub, "  voxel-agent-radius") == 0)
							{
								continue;
							}
							else
								writer.WriteLine(line);
						}
					}
				}
			}
			if (System.IO.File.Exists(temp))
			{
				// Use a try block to catch IOExceptions, to 
				// handle the case of the file already being 
				// opened by another process. 
				try
				{
					System.IO.File.Delete(temp);
				}
				catch (System.IO.IOException e)
				{
					MessageBox.Show(e.Message);
					return;
				}
			}
			cwd = location;
			changed = false;
		}

		private void saveToolStripMenuItem_Click(object sender, EventArgs e)
		{
			Setting setting = propertyGrid1.SelectedObject as Setting;
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
			Setting setting = propertyGrid1.SelectedObject as Setting;
			if (setting == null)
			{
				//MessageBox.Show("Nothing to save");
			}
			else
			{
				SaveFileDialog save = new SaveFileDialog();
				save.Filter = "SharpNav Config Files (*.sncfg)|*.sncfg|  All Files (*.*)|*.*";
				if (save.ShowDialog() == System.Windows.Forms.DialogResult.OK)
				{
					saveprocess(setting, save.FileName);
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
						Setting setting = propertyGrid1.SelectedObject as Setting;
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
			Setting setting = new Setting();
			setting.config = NavMeshGenerationSettings.Default;
			setting.export = "default.snb";
			setting.meshes = new List<Object>();
			propertyGrid1.SelectedObject = setting;
			changed = true;
		}

		private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
		{
			//MessageBox.Show("This is a GUI made for SharpNav");
			if (MessageBox.Show(
				"This is GUI made of SharpNav. \n\nDo you want to visit the github page?", "About", MessageBoxButtons.YesNo, MessageBoxIcon.Asterisk
					) == DialogResult.Yes)
			{
				System.Diagnostics.Process.Start("https://github.com/Robmaister/SharpNav");
			}
		}

		private void generateButton_Click(object sender, EventArgs e)
		{
			Setting setting = propertyGrid1.SelectedObject as Setting;
			if (setting == null)
			{
				return;
			}
			List<ObjModel> models = new List<ObjModel>();

			if (setting.meshes.Count == 0)
			{
				MessageBox.Show("No Obj files included");
				return;
			}

			foreach (var mesh in setting.meshes)
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
