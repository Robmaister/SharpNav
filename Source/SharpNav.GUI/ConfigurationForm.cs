using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.IO;

using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

using SharpNav.Geometry;

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

		public class Object
		{
			public string Path { get; set; }
			public float Scale { get; set; }
			public float[] Position { get; set; }
			public Vector3 vector { get; set; }
			//TODO: rotation;
		}

		public class ControlWriter : TextWriter
		{
			private Control textbox;
			public ControlWriter(Control textbox)
			{
				this.textbox = textbox;
			}

			public override void Write(char value)
			{
				textbox.Text += value;
			}

			public override void Write(string value)
			{
				textbox.Text += value;
			}

			public override Encoding Encoding
			{
				get { return Encoding.ASCII; }
			}
		}

		private void menuStrip1_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
		{

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

				Console.SetOut(new ControlWriter(textBox1));

				Console.WriteLine("Config:");
				Console.WriteLine();
				Console.WriteLine("cell-size: {0}", setting.Config.CellSize);
				Console.WriteLine("cell-height: {0}", setting.Config.CellHeight);
				Console.WriteLine("max-climb: {0}", setting.Config.MaxClimb);
				Console.WriteLine("agent-height: {0}", setting.Config.AgentHeight);
				Console.WriteLine("agent-radius: {0}", setting.Config.AgentRadius);
				Console.WriteLine("min-region-size: {0}", setting.Config.MinRegionSize);
				Console.WriteLine("merged-region-size: {0}", setting.Config.MergedRegionSize);
				Console.WriteLine("max-edge-len: {0}", setting.Config.MaxEdgeLength);
				Console.WriteLine("max-edge-error: {0}", setting.Config.MaxEdgeError);
				Console.WriteLine("verts-per-poly: {0}", setting.Config.VertsPerPoly);
				Console.WriteLine("sample-distance: {0}", setting.Config.SampleDistance);
				Console.WriteLine("max-sample-error: {0}", setting.Config.MaxSampleError);

				List<string> meshes = new List<string>();
				List<ObjModel> models = new List<ObjModel>();

				Console.WriteLine();
				Console.WriteLine("Export Path:");
				Console.WriteLine(setting.Export);
				Console.WriteLine();
				Console.WriteLine("Meshes:");
				foreach (var mesh in setting.Meshes)
				{
					Console.WriteLine("Path:{0}\nScale:{1}", mesh.Path, mesh.Scale);
					meshes.Add(mesh.Path);

					//Console.WriteLine("array: {0} {1} {2}", mesh.Position[0], mesh.Position[1], mesh.Position[2]);
					mesh.vector = new Vector3(mesh.Position[0], mesh.Position[1], mesh.Position[2]);
					//Console.WriteLine("vector: {0} {1} {2}", mesh.vector.X, mesh.vector.Y, mesh.vector.Z);

					if (File.Exists(mesh.Path))
					{
						ObjModel obj = new ObjModel(mesh.Path);
						float scale = mesh.Scale;
						//TODO SCALE THE OBJ FILE
						models.Add(obj);
						Console.WriteLine("Position vector: {0} {1} {2}", mesh.vector.X, mesh.vector.Y, mesh.vector.Z);
					}
					else
					{
						Console.WriteLine("Obj file not exists.");
					}

				}
			}
         
		}

		private void saveAsToolStripMenuItem_Click(object sender, EventArgs e)
		{
			SaveFileDialog save = new SaveFileDialog();
			if (save.ShowDialog() == System.Windows.Forms.DialogResult.OK)
			{
				StreamWriter write = new StreamWriter(File.Create(save.FileName));
			}
		}

		private void exitToolStripMenuItem_Click(object sender, EventArgs e)
		{
			return;
		}
	}
}
