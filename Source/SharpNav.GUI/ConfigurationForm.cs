// Copyright (c) 2015 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
// Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE

using System;
using System.Collections.Generic;
<<<<<<< HEAD
using System.IO;
=======
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Design;
using System.Linq;
>>>>>>> GUI new, open, save, saveas, about implemented
using System.Text;
using System.Windows.Forms;
<<<<<<< HEAD
=======
using System.Windows.Forms.Design;
using System.IO;
>>>>>>> GUI new, open, save, saveas, about implemented

using SharpNav.Geometry;

using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace SharpNav.GUI
{
	public partial class ConfigurationForm : Form
	{
		public ConfigurationForm()
		{
			InitializeComponent();
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

		[TypeConverter(typeof(ExpandableObjectConverter))]
		public class Object
		{
			public string Path { get; set; }
			public float Scale { get; set; }
			public float[] Position { get; set; }
			public Vector3 vector { get; set; }
			//TODO: rotation;
		}

		private void newToolStripMenuItem_Click(object sender, EventArgs e)
		{
			Setting setting = new Setting();
			setting.Config = NavMeshGenerationSettings.Default;
			propertyGrid1.SelectedObject = setting;
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

				/*List<string> meshes = new List<string>();
				List<ObjModel> models = new List<ObjModel>();

				foreach (var mesh in setting.Meshes)
				{
					meshes.Add(mesh.Path);

					mesh.vector = new Vector3(mesh.Position[0], mesh.Position[1], mesh.Position[2]);

					if (File.Exists(mesh.Path))
					{
						ObjModel obj = new ObjModel(mesh.Path);
						float scale = mesh.Scale;
						//TODO SCALE THE OBJ FILE
						models.Add(obj);
					}
					else
					{
						MessageBox.Show("Obj file not exists.");
					}

				}*/
			}

		}

		private void saveToolStripMenuItem_Click(object sender, EventArgs e)
		{
			Setting setting = propertyGrid1.SelectedObject as Setting;
			if (setting == null)
			{
				MessageBox.Show("Nothing to save");
			}
			else
			{

			}
<<<<<<< HEAD
		 
=======
>>>>>>> GUI new, open, save, saveas, about implemented
		}

		private void saveAsToolStripMenuItem_Click(object sender, EventArgs e)
		{
			Setting setting = propertyGrid1.SelectedObject as Setting;
			if (setting == null)
			{
				MessageBox.Show("Nothing to save");
			}
			else
			{
				SaveFileDialog save = new SaveFileDialog();
				save.Filter = "SharpNav Config Files (*.sncfg)|*.sncfg|  All Files (*.*)|*.*";
				if (save.ShowDialog() == System.Windows.Forms.DialogResult.OK)
				{
					StreamWriter write = new StreamWriter(File.Create(save.FileName));
				}
			}

		}

		private void exitToolStripMenuItem_Click(object sender, EventArgs e)
		{
			if(MessageBox.Show("Do you want to Exit?", "My Application",
				MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
			{
				Application.Exit();
			}
			
		}

		private void resetAllToolStripMenuItem_Click(object sender, EventArgs e)
		{
			Setting setting = new Setting();
			setting.Config = NavMeshGenerationSettings.Default;
			propertyGrid1.SelectedObject = setting;
		}

		private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
		{
			//MessageBox.Show("This is a GUI made for SharpNav");
			if (MessageBox.Show(
				"This is GUI made of SharpNav. \n\n Do you want to visit the github page?", "Visit", MessageBoxButtons.YesNo, MessageBoxIcon.Asterisk
					) == DialogResult.Yes)
			{
				System.Diagnostics.Process.Start("https://github.com/Robmaister/SharpNav");
			}
		}
	}
}
