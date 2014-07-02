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

using SharpNavEditor.IO;

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

		private IModelData testModel;

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
			menuFile.Menu.AddItem("Open...").Clicked += MainMenuFileOpen;
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

			if (testModel != null)
			{
				GL.PushMatrix();

				if (testModel.Positions != null)
				{
					GL.EnableClientState(ArrayCap.VertexArray);
					GL.VertexPointer(testModel.PositionVertexSize, VertexPointerType.Float, 0, testModel.Positions);
				}
				if (testModel.Normals != null)
				{
					GL.EnableClientState(ArrayCap.NormalArray);
					GL.NormalPointer(NormalPointerType.Float, 0, testModel.Normals);
				}

				if (testModel.Indices != null)
					GL.DrawElements(PrimitiveType.Triangles, testModel.Indices.Length, DrawElementsType.UnsignedInt, testModel.Indices);
				else
					GL.DrawArrays(PrimitiveType.Triangles, 0, testModel.Positions.Length / testModel.PositionVertexSize);

				if (testModel.Positions != null)
					GL.DisableClientState(ArrayCap.VertexArray);
				if (testModel.Normals != null)
					GL.DisableClientState(ArrayCap.NormalArray);

				GL.PopMatrix();
			}

			GL.Disable(EnableCap.Lighting);

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

		private void MainMenuFileOpen(Base control, EventArgs e)
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
			var filters = allFilter + "|" + filterStrings.Aggregate((seq, next) => seq + "|" + next);

			//open a new dialog and load the model if successful
			Gwen.Platform.Neutral.FileOpen("Load Model", ".", filters,
				(s) =>
				{
					testModel = extFileTypes[Path.GetExtension(s)].LoadModel(s);
				});
		}

		private void MainMenuFileExit(Base control, EventArgs e)
		{
			//TODO fix messagebox in GWEN.NET
			MessageBox askSave = new MessageBox(gwenCanvas, "Are you sure you want to exit? All unsaved changes will be lost.","Exit");
			askSave.Dismissed = (c, ea) => Exit();
			//Exit();
		}
	}
}
