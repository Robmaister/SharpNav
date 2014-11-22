#region License
/**
 * Copyright (c) 2014 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
 * Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE
 */
#endregion

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

using OpenTK;
using OpenTK.Input;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;

using Gwen.Control;

using SharpNav;
using SharpNav.Geometry;

using SharpNavEditor.IO;
using Gwen.ControlInternal;

namespace SharpNavEditor
{
	public class EditorWindow : GameWindow
	{
		Camera cam;
		private float zoom = MathHelper.PiOver4;

		private KeyboardState prevK;
		private MouseState prevM;

		private Gwen.Input.OpenTK gwenInput;
		private Gwen.Renderer.OpenTK gwenRenderer;
		private Gwen.Skin.Base gwenSkin;
		private Gwen.Control.Canvas gwenCanvas;
		private Matrix4 gwenProjection;

		//TODO split off UI and other things into different systems/at least partial classes
		private StatusBar statusBar;
		private MenuStrip mainMenu;

		private List<Mesh> meshes;
		private ListBox meshListBox;

		private Mesh selectedMesh;
		private Base meshProperties;

		private PolyMesh polyMesh;
		private PolyMeshDetail polyMeshDetail;
		private NavMesh navMesh;

		public EditorWindow()
			: base(1024, 600, new GraphicsMode(32, 8, 0, 4))
		{
			Keyboard.KeyDown += OnKeyboardKeyDown;
			Keyboard.KeyUp += OnKeyboardKeyUp;
			Mouse.ButtonDown += OnMouseButtonDown;
			Mouse.ButtonUp += OnMouseButtonUp;
			Mouse.Move += OnMouseMove;
			Mouse.WheelChanged += OnMouseWheel;

			this.Title = "SharpNav Editor";

			meshes = new List<Mesh>();
		}

		protected override void OnLoad(EventArgs e)
		{
			base.OnLoad(e);

			cam = new Camera();

			gwenRenderer = new Gwen.Renderer.OpenTK();
			gwenSkin = new Gwen.Skin.TexturedBase(gwenRenderer, "GwenSkin.png");
			gwenCanvas = new Gwen.Control.Canvas(gwenSkin);
			gwenInput = new Gwen.Input.OpenTK(this);

			gwenInput.Initialize(gwenCanvas);
			gwenCanvas.SetSize(Width, Height);
			gwenCanvas.ShouldDrawBackground = false;

			gwenProjection = Matrix4.CreateOrthographicOffCenter(0, Width, Height, 0, -1, 1);

			statusBar = new StatusBar(gwenCanvas);
			mainMenu = new MenuStrip(gwenCanvas);

			MenuItem menuFile = mainMenu.AddItem("File");
			menuFile.Menu.AddItem("New");
			//menuFile.Menu.AddItem("Open...").Clicked += LoadModelBtn;
			menuFile.Menu.AddDivider();
			menuFile.Menu.AddItem("Save...");
			menuFile.Menu.AddItem("Save As...");
			menuFile.Menu.AddDivider();
			menuFile.Menu.AddItem("Exit").SetAction(MainMenuFileExit);

			MenuItem menuEdit = mainMenu.AddItem("Edit");
			menuEdit.Menu.AddItem("Undo");
			menuEdit.Menu.AddItem("Redo");
			menuEdit.Menu.AddDivider();
			menuEdit.Menu.AddItem("Preferences...");

			MenuItem menuView = mainMenu.AddItem("View");
			menuView.Menu.AddItem("Level");

			MenuItem menuHelp = mainMenu.AddItem("Help");
			menuHelp.Menu.AddItem("About...");

			Button statusLoadModelBtn = new Button(statusBar);
			statusLoadModelBtn.Dock = Gwen.Pos.Left;
			statusLoadModelBtn.Text = "Load model";
			statusLoadModelBtn.Clicked += LoadModelBtn;

			Button statusGenNavmesh = new Button(statusBar);
			statusGenNavmesh.Dock = Gwen.Pos.Left;
			statusGenNavmesh.Text = "Generate NavMesh";
			statusGenNavmesh.AutoSizeToContents = true;
			statusGenNavmesh.Clicked += GenNavMesh;

			TabControl sidePanel = new TabControl(gwenCanvas);
			sidePanel.Width = 300;
			sidePanel.Dock = Gwen.Pos.Right;

			Resizer sidePanelResizer = new Resizer(sidePanel);
			sidePanelResizer.Dock = Gwen.Pos.Left;
			sidePanelResizer.ResizeDir = Gwen.Pos.Left;
			sidePanelResizer.SetSize(4, 4);

			var generalTab = sidePanel.AddPage("General").Page;

			HorizontalSplitter generalSplitter = new HorizontalSplitter(generalTab);
			generalSplitter.SplitterSize = 5;
			generalSplitter.Dock = Gwen.Pos.Fill;
			//generalSplitter.SplittersVisible = true;

			meshListBox = new ListBox(generalSplitter);
			meshListBox.EnableScroll(false, false);
			//meshListBox.UpdateScrollBars();
			//meshListBox.Dock = Gwen.Pos.Top;
			//meshListBox.Height = 100;

			generalSplitter.SetPanel(0, meshListBox);

			ScrollControl generalScrollBase = new ScrollControl(generalSplitter);
			generalScrollBase.Dock = Gwen.Pos.Fill;
			generalScrollBase.EnableScroll(false, true);

			meshProperties = new Base(generalScrollBase);
			meshProperties.Dock = Gwen.Pos.Top;
			meshProperties.Height = 200;
			//meshProperties.Disable();
			meshProperties.Hide();

			Label nameLabel = new Label(meshProperties);
			nameLabel.Text = "Name";
			nameLabel.Dock = Gwen.Pos.Top;
			nameLabel.AutoSizeToContents = true;
			nameLabel.Padding = new Gwen.Padding(0, 2, 5, 0);

			TextBox nameTextbox = new TextBox(meshProperties);
			nameTextbox.Name = "Name";
			//nameTextbox.Width = 200;
			//nameTextbox.Height = 50;
			nameTextbox.Dock = Gwen.Pos.Top;
			nameTextbox.TextChanged += (s, se) =>
			{
				if (selectedMesh != null)
				{
					selectedMesh.Name = ((TextBox)s).Text;
					meshListBox.SelectedRow.Text = selectedMesh.Name;
				}
			};

			Label xLabel = new Label(meshProperties);
			xLabel.Text = "X";
			xLabel.Dock = Gwen.Pos.Top;

			TextBoxNumeric xTextBox = new TextBoxNumeric(meshProperties);
			xTextBox.Dock = Gwen.Pos.Top;
			xTextBox.TextChanged += (s, se) =>
			{
				if (selectedMesh != null)
				{
					Transform t = selectedMesh.Transform;
					t.Translation.X = ((TextBoxNumeric)s).Value;
					selectedMesh.Transform = t;
				}
			};

			Label yLabel = new Label(meshProperties);
			yLabel.Text = "Y";
			yLabel.Dock = Gwen.Pos.Top;

			TextBoxNumeric yTextBox = new TextBoxNumeric(meshProperties);
			yTextBox.Dock = Gwen.Pos.Top;
			yTextBox.TextChanged += (s, se) =>
			{
				if (selectedMesh != null)
				{
					Transform t = selectedMesh.Transform;
					t.Translation.Y = ((TextBoxNumeric)s).Value;
					selectedMesh.Transform = t;
				}
			};

			Label zLabel = new Label(meshProperties);
			zLabel.Text = "Z";
			zLabel.Dock = Gwen.Pos.Top;

			TextBoxNumeric zTextBox = new TextBoxNumeric(meshProperties);
			zTextBox.Dock = Gwen.Pos.Top;
			zTextBox.TextChanged += (s, se) =>
			{
				if (selectedMesh != null)
				{
					Transform t = selectedMesh.Transform;
					t.Translation.Z = ((TextBoxNumeric)s).Value;
					selectedMesh.Transform = t;
				}
			};

			meshListBox.RowSelected += (s, se) =>
			{
				var mesh = (Mesh)se.SelectedItem.UserData;
				selectedMesh = mesh;
				meshProperties.Show();
				nameTextbox.Text = selectedMesh.Name;
				xTextBox.Value = selectedMesh.Transform.Translation.X;
				yTextBox.Value = selectedMesh.Transform.Translation.Y;
				zTextBox.Value = selectedMesh.Transform.Translation.Z;
			};

			generalSplitter.SetPanel(1, generalScrollBase);

			var navMeshTab = sidePanel.AddPage("NavMesh").Page;
			var otherTab = sidePanel.AddPage("Other").Page;

			GL.ClearColor(Color4.CornflowerBlue);
			GL.Enable(EnableCap.Lighting);
			GL.Enable(EnableCap.Light0);
			GL.Enable(EnableCap.DepthTest);
			GL.DepthMask(true);
			GL.DepthFunc(DepthFunction.Lequal);
			GL.Enable(EnableCap.CullFace);
			GL.Enable(EnableCap.Blend);
			GL.BlendFunc(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);
		}

		protected override void OnUpdateFrame(FrameEventArgs e)
		{
			base.OnUpdateFrame(e);

			if (!Focused)
				return;

			KeyboardState k = OpenTK.Input.Keyboard.GetState();
			MouseState m = OpenTK.Input.Mouse.GetState();

            if(m[MouseButton.Left] && selectedMesh != null) {
                Transform t = selectedMesh.Transform;
                t.Translation.X += (m.X - prevM.X) * (float)e.Time * 2f;
                t.Translation.Y += (prevM.Y - m.Y) * (float)e.Time * 2f;
                selectedMesh.Transform = t;
            }

			bool isShiftDown = false;
			if (k[Key.LShift] || k[Key.RShift])
				isShiftDown = true;

			//TODO make cam speed/shift speedup controllable from GUI
			float camSpeed = 5f * (float)e.Time * (isShiftDown ? 3f : 1f);
			float zoomSpeed = (float)Math.PI * (float)e.Time * (isShiftDown ? 0.2f : 0.1f);

			if (k[Key.W])
				cam.Move(-camSpeed);
			if (k[Key.A])
				cam.Strafe(-camSpeed);
			if (k[Key.S])
				cam.Move(camSpeed);
			if (k[Key.D])
				cam.Strafe(camSpeed);
			if (k[Key.Q])
				cam.Elevate(camSpeed);
			if (k[Key.E])
				cam.Elevate(-camSpeed);
			if (k[Key.Z])
			{
				zoom += zoomSpeed;
				if (zoom > MathHelper.PiOver2)
					zoom = MathHelper.PiOver2;
			}
			if (k[Key.C])
			{
				zoom -= zoomSpeed;
				if (zoom < 0.002f)
					zoom = 0.002f;
			}

			if (m[MouseButton.Right])
			{
				cam.RotatePitch((m.X - prevM.X) * (float)e.Time * 2f);
				cam.RotateHeading((prevM.Y - m.Y) * (float)e.Time * 2f);
			}

			float aspect = Width / (float)Height;
			Matrix4 persp = Matrix4.CreatePerspectiveFieldOfView(zoom, aspect, 0.1f, 1000f);
			GL.MatrixMode(MatrixMode.Projection);
			GL.LoadMatrix(ref persp);
			GL.MatrixMode(MatrixMode.Modelview);
			cam.LoadView();

			prevK = k;
			prevM = m;

			if (gwenRenderer.TextCacheSize > 1000)
				gwenRenderer.FlushTextCache();
		}

		protected void OnKeyboardKeyDown(object sender, KeyboardKeyEventArgs e)
		{
			if (!Focused)
				return;

			if (e.Key == Key.Escape)
				Exit();
			else if (e.Key == Key.F11)
				WindowState = OpenTK.WindowState.Normal;
			else if (e.Key == Key.F12)
				WindowState = OpenTK.WindowState.Fullscreen;

			gwenInput.ProcessKeyDown(e);

			base.OnKeyDown(e);
		}

		protected void OnKeyboardKeyUp(object sender, KeyboardKeyEventArgs e)
		{
			if (!Focused)
				return;

			gwenInput.ProcessKeyUp(e);
		}

		protected void OnMouseButtonDown(object sender, MouseButtonEventArgs e)
		{
			if (!Focused)
				return;

			gwenInput.ProcessMouseMessage(e);
		}

		protected void OnMouseButtonUp(object sender, MouseButtonEventArgs e)
		{
			if (!Focused)
				return;

			gwenInput.ProcessMouseMessage(e);
		}

		protected void OnMouseMove(object sender, MouseMoveEventArgs e)
		{
			gwenInput.ProcessMouseMessage(e);
		}

		protected void OnMouseWheel(object sender, MouseWheelEventArgs e)
		{
			if (!Focused)
				return;

			gwenInput.ProcessMouseMessage(e);
		}

		protected override void OnRenderFrame(FrameEventArgs e)
		{
			base.OnRenderFrame(e);

			GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

			GL.Light(LightName.Light0, LightParameter.Position, new Vector4(0f, 1, 0f, 0));

			Matrix4 meshTrans;
			foreach (var mesh in meshes)
			{
				meshTrans = mesh.Transform.Matrix;
				var model = mesh.Model;

				if (model != null)
				{
					if (mesh == selectedMesh)
						GL.Light(LightName.Light0, LightParameter.Diffuse, Color4.Green);
					else
						GL.Light(LightName.Light0, LightParameter.Diffuse, Color4.White);

					GL.PushMatrix();
					GL.MultMatrix(ref meshTrans);

					if (model.Positions != null)
					{
						GL.EnableClientState(ArrayCap.VertexArray);
						GL.VertexPointer(model.PositionVertexSize, VertexPointerType.Float, 0, model.Positions);
					}
					if (model.Normals != null)
					{
						GL.EnableClientState(ArrayCap.NormalArray);
						GL.NormalPointer(NormalPointerType.Float, 0, model.Normals);
					}

					if (model.Indices != null)
						GL.DrawElements(PrimitiveType.Triangles, model.Indices.Length, DrawElementsType.UnsignedInt, model.Indices);
					else
						GL.DrawArrays(PrimitiveType.Triangles, 0, model.Positions.Length / model.PositionVertexSize);

					if (model.Positions != null)
						GL.DisableClientState(ArrayCap.VertexArray);
					if (model.Normals != null)
						GL.DisableClientState(ArrayCap.NormalArray);

					GL.PopMatrix();
				}
			}

			GL.Disable(EnableCap.Lighting);

			if (navMesh != null)
			{
				DrawPolyMeshDetail();
			}

			GL.PushMatrix();
			GL.LoadIdentity();
			GL.MatrixMode(MatrixMode.Projection);
			GL.PushMatrix();
			GL.LoadMatrix(ref gwenProjection);
			GL.FrontFace(FrontFaceDirection.Cw);
			gwenCanvas.RenderCanvas();
			GL.FrontFace(FrontFaceDirection.Ccw);
			GL.PopMatrix();
			GL.MatrixMode(MatrixMode.Modelview);
			GL.PopMatrix();

			GL.Enable(EnableCap.Lighting);

			SwapBuffers();
		}

		protected override void OnResize(EventArgs e)
		{
			base.OnResize(e);

			GL.Viewport(0, 0, Width, Height);
			float aspect = Width / (float)Height;

			Matrix4 persp = Matrix4.CreatePerspectiveFieldOfView(zoom, aspect, 0.1f, 1000f);
			GL.MatrixMode(MatrixMode.Projection);
			GL.LoadMatrix(ref persp);
			GL.MatrixMode(MatrixMode.Modelview);
			GL.LoadIdentity();

			gwenProjection = Matrix4.CreateOrthographicOffCenter(0, Width, Height, 0, 0, 1);
			gwenCanvas.SetSize(Width, Height);
		}

		protected override void OnUnload(EventArgs e)
		{
			gwenCanvas.Dispose();
			gwenSkin.Dispose();
			gwenRenderer.Dispose();

			base.OnUnload(e);
		}

		private void LoadModelBtn(Base control, EventArgs e)
		{
			//use reflection to get all model loaders and their file extensions.
			var extFileTypes = (from t in Assembly.GetExecutingAssembly().GetTypes()
				where t.IsClass && !t.IsAbstract && typeof(IModelLoader).IsAssignableFrom(t) && t.GetConstructor(Type.EmptyTypes) != null
				from attr in t.GetCustomAttributes(false)
				where attr is FileLoaderAttribute
				select new { Ext = (attr as FileLoaderAttribute).Extension, Loader = (IModelLoader)Activator.CreateInstance(t) })
				.ToDictionary(item => item.Ext, item => item.Loader);

			//create a filter string for the open file dialog
			var filterStrings = extFileTypes.Keys.Select(s => "*" + s);
			var allFilter = "All Model Formats(" + filterStrings.Aggregate((seq, next) => seq + ", " + next) + ")";
			var filters = allFilter + "|" + filterStrings.Aggregate((seq, next) => seq + ";" + next);

			//open a new dialog and load the model if successful
			Gwen.Platform.Neutral.FileOpen("Load Model", ".", filters,
				(s) =>
				{
					//HACK fix this in GWEN.NET
					if (s == "")
						return;

					Mesh m = new Mesh(Path.GetFileNameWithoutExtension(s), extFileTypes[Path.GetExtension(s)].LoadModel(s));
					meshes.Add(m);
					meshListBox.AddRow(m.Name, m.Name, m);
				});
		}

		private void MainMenuFileExit(Base control, EventArgs e)
		{
			//TODO fix messagebox in GWEN.NET
			MessageBox askSave = new MessageBox(gwenCanvas, "Are you sure you want to exit? All unsaved changes will be lost.","Exit");
			askSave.Dismissed = (c, ea) => Exit();
			//Exit();
		}

		private void GenNavMesh(Base control, EventArgs e)
		{
			IEnumerable<Triangle3> tris = Enumerable.Empty<Triangle3>();
			foreach (var mesh in meshes)
				tris = tris.Concat(mesh.GetTransformedTris());

			var settings = NavMeshGenerationSettings.Default;

			BBox3 bounds = tris.GetBoundingBox(settings.CellSize);
			var hf = new Heightfield(bounds, settings);
			hf.RasterizeTriangles(tris);
			hf.FilterLedgeSpans(settings.VoxelAgentHeight, settings.VoxelMaxClimb);
			hf.FilterLowHangingWalkableObstacles(settings.VoxelMaxClimb);
			hf.FilterWalkableLowHeightSpans(settings.VoxelAgentHeight);

			var chf = new CompactHeightfield(hf, settings);
			chf.Erode(settings.VoxelAgentWidth);
			chf.BuildDistanceField();
			chf.BuildRegions(2, settings.MinRegionSize, settings.MergedRegionSize);

			var cont = new ContourSet(chf, settings);

			polyMesh = new PolyMesh(cont, settings);

			polyMeshDetail = new PolyMeshDetail(polyMesh, chf, settings);

			var buildData = new NavMeshBuilder(polyMesh, polyMeshDetail, new SharpNav.Pathfinding.OffMeshConnection[0], settings);

			navMesh = new NavMesh(buildData);
		}

		private void DrawPolyMeshDetail()
		{
			GL.PushMatrix();

			Color4 color = Color4.DarkViolet;
			color.A = 0.5f;
			GL.Color4(color);

			GL.Begin(PrimitiveType.Triangles);
			for (int i = 0; i < polyMeshDetail.MeshCount; i++)
			{
				PolyMeshDetail.MeshData m = polyMeshDetail.Meshes[i];

				int vertIndex = m.VertexIndex;
				int triIndex = m.TriangleIndex;

				for (int j = 0; j < m.TriangleCount; j++)
				{
					var t = polyMeshDetail.Tris[triIndex + j];

					var v = polyMeshDetail.Verts[vertIndex + t.VertexHash0];
					GL.Vertex3(v.X, v.Y, v.Z);

					v = polyMeshDetail.Verts[vertIndex + t.VertexHash1];
					GL.Vertex3(v.X, v.Y, v.Z);

					v = polyMeshDetail.Verts[vertIndex + t.VertexHash2];
					GL.Vertex3(v.X, v.Y, v.Z);
				}
			}

			GL.End();

			GL.Color4(Color4.Purple);
			GL.LineWidth(1.5f);
			GL.Begin(PrimitiveType.Lines);
			for (int i = 0; i < polyMeshDetail.MeshCount; i++)
			{
				var m = polyMeshDetail.Meshes[i];

				int vertIndex = m.VertexIndex;
				int triIndex = m.TriangleIndex;

				for (int j = 0; j < m.TriangleCount; j++)
				{
					var t = polyMeshDetail.Tris[triIndex + j];
					for (int k = 0, kp = 2; k < 3; kp = k++)
					{
						if (((t.Flags >> (kp * 2)) & 0x3) == 0)
						{
							if (t[kp] < t[k])
							{
								var v = polyMeshDetail.Verts[vertIndex + t[kp]];
								GL.Vertex3(v.X, v.Y, v.Z);

								v = polyMeshDetail.Verts[vertIndex + t[k]];
								GL.Vertex3(v.X, v.Y, v.Z);
							}
						}
					}
				}
			}

			GL.End();

			GL.LineWidth(3.5f);
			GL.Begin(PrimitiveType.Lines);
			for (int i = 0; i < polyMeshDetail.MeshCount; i++)
			{
				var m = polyMeshDetail.Meshes[i];

				int vertIndex = m.VertexIndex;
				int triIndex = m.TriangleIndex;

				for (int j = 0; j < m.TriangleCount; j++)
				{
					var t = polyMeshDetail.Tris[triIndex + j];
					for (int k = 0, kp = 2; k < 3; kp = k++)
					{
						if (((t.Flags >> (kp * 2)) & 0x3) != 0)
						{
							var v = polyMeshDetail.Verts[vertIndex + t[kp]];
							GL.Vertex3(v.X, v.Y, v.Z);

							v = polyMeshDetail.Verts[vertIndex + t[k]];
							GL.Vertex3(v.X, v.Y, v.Z);
						}
					}
				}
			}

			GL.End();

			GL.PointSize(4.8f);
			GL.Begin(PrimitiveType.Points);
			for (int i = 0; i < polyMeshDetail.MeshCount; i++)
			{
				var m = polyMeshDetail.Meshes[i];

				for (int j = 0; j < m.VertexCount; j++)
				{
					var v = polyMeshDetail.Verts[m.VertexIndex + j];
					GL.Vertex3(v.X, v.Y, v.Z);
				}
			}

			GL.End();

			GL.PopMatrix();
		}
	}
}
