#region License
/**
 * Copyright (c) 2013 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
 * Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Gwen;
using Gwen.Control;

using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;
using OpenTK.Input;

using SharpNav;
using SharpNav.Geometry;

using Key = OpenTK.Input.Key;
using GwenKey = Gwen.Key;

namespace Examples
{
	public partial class ExampleWindow : GameWindow
	{
		private Camera cam;

		private Heightfield heightfield;
		private CompactHeightfield openHeightfield;

		private bool hasVoxelized;

		private bool hasOpenHeightfield;

		private bool hasDistanceField;

		private bool hasRegions;
		private Color4[] regionColors;

		private ContourSet contourSet;
		private bool hasContours;
		private bool hasSimplifiedContours;

		private NavMesh navMesh;
		private bool hasNavMesh;

		private NavMeshDetail navMeshDetail;
		private bool hasNavMeshDetail;

		private KeyboardState prevK;
		private MouseState prevM;

		private Gwen.Input.OpenTK gwenInput;
		private Gwen.Renderer.OpenTK gwenRenderer;
		private Gwen.Skin.Base gwenSkin;
		private Gwen.Control.Canvas gwenCanvas;
		private Matrix4 gwenProjection;

		public ExampleWindow()
			: base(800, 600, new GraphicsMode(32, 8, 0, 4))
		{
			cam = new Camera();

			Keyboard.KeyDown += OnKeyboardKeyDown;
			Keyboard.KeyUp += OnKeyboardKeyUp;
			Mouse.ButtonDown += OnMouseButtonDown;
			Mouse.ButtonUp += OnMouseButtonUp;
			Mouse.Move += OnMouseMove;
			Mouse.WheelChanged += OnMouseWheel;
		}

		protected override void OnLoad(EventArgs e)
		{
			base.OnLoad(e);

			InitializeOpenGL();

			LoadLevel();
			LoadDebugMeshes();

			gwenRenderer = new Gwen.Renderer.OpenTK();
			gwenSkin = new Gwen.Skin.TexturedBase(gwenRenderer, "GwenSkin.png");
			gwenCanvas = new Gwen.Control.Canvas(gwenSkin);
			gwenInput = new Gwen.Input.OpenTK(this);

			gwenInput.Initialize(gwenCanvas);
			gwenCanvas.SetSize(Width, Height);
			gwenCanvas.ShouldDrawBackground = false;

			gwenProjection = Matrix4.CreateOrthographicOffCenter(0, Width, Height, 0, -1, 1);

			InitializeUI();

			//settingsScrollParent.SizeToChildren(true, false);
		}

		protected override void OnUpdateFrame(FrameEventArgs e)
		{
			base.OnUpdateFrame(e);

			KeyboardState k = OpenTK.Input.Keyboard.GetState();
			MouseState m = OpenTK.Input.Mouse.GetState();

			bool isShiftDown = false;
			if (k[Key.LShift] || k[Key.RShift])
				isShiftDown = true;

			//TODO make cam speed/shift speedup controllable from GUI
			float camSpeed = 5f * (float)e.Time * (isShiftDown ? 3f : 1f);

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

			if (m[MouseButton.Right])
			{
				cam.RotatePitch((m.X - prevM.X) * (float)e.Time * 2f);
				cam.RotateHeading((prevM.Y - m.Y) * (float)e.Time * 2f);
			}

			GL.MatrixMode(MatrixMode.Modelview);
			cam.LoadView();

			prevK = k;
			prevM = m;

			if (gwenRenderer.TextCacheSize > 1000)
				gwenRenderer.FlushTextCache();
		}

		protected void OnKeyboardKeyDown(object sender, KeyboardKeyEventArgs e)
		{
			if (e.Key == Key.Escape)
				Exit();
			else if (e.Key == Key.F11)
				WindowState = OpenTK.WindowState.Normal;
			else if (e.Key == Key.F12)
				WindowState = OpenTK.WindowState.Fullscreen;
			else if (e.Key == Key.P)
			{
				
			}

			gwenInput.ProcessKeyDown(e);

			base.OnKeyDown(e);
		}

		protected void OnKeyboardKeyUp(object sender, KeyboardKeyEventArgs e)
		{
			gwenInput.ProcessKeyUp(e);
		}

		protected void OnMouseButtonDown(object sender, MouseButtonEventArgs e)
		{
			gwenInput.ProcessMouseMessage(e);
		}

		protected void OnMouseButtonUp(object sender, MouseButtonEventArgs e)
		{
			gwenInput.ProcessMouseMessage(e);
		}

		protected void OnMouseMove(object sender, MouseMoveEventArgs e)
		{
			gwenInput.ProcessMouseMessage(e);
		}

		protected void OnMouseWheel(object sender, MouseWheelEventArgs e)
		{
			gwenInput.ProcessMouseMessage(e);
		}

		protected override void OnRenderFrame(FrameEventArgs e)
		{
			base.OnRenderFrame(e);

			GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

			GL.Enable(EnableCap.Lighting);
			GL.Enable(EnableCap.Light0);
			GL.Light(LightName.Light0, LightParameter.Position, new Vector4(0f, 1, 0f, 0));

			DrawLevel();

			GL.Disable(EnableCap.Light0);
			GL.Disable(EnableCap.Lighting);

			/*if (hasNavMeshDetail)
				DrawNavMeshDetail();
			else*/ if (hasNavMesh)
				DrawNavMesh();
			else if (hasSimplifiedContours)
				DrawContours(true);
			else if (hasContours)
				DrawContours(false);
			else if (hasRegions)
				DrawRegions();
			else if (hasDistanceField)
				DrawDistanceField();
			else if (hasOpenHeightfield)
				DrawCompactHeightfield();
			else if (hasVoxelized)
				DrawHeightfield();

			DrawUI();

			SwapBuffers();
		}

		protected override void OnResize(EventArgs e)
		{
			base.OnResize(e);

			GL.Viewport(0, 0, Width, Height);
			float aspect = Width / (float)Height;

			Matrix4 persp = Matrix4.CreatePerspectiveFieldOfView(MathHelper.PiOver4, aspect, 0.1f, 1000f);
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

			UnloadLevel();
			UnloadDebugMeshes();

			base.OnUnload(e);
		}

		private void GenerateNavMesh()
		{
			BBox3 bounds = level.GetBounds();
			heightfield = new Heightfield(bounds.Min, bounds.Max, settings.CellSize, settings.CellHeight);
			heightfield.RasterizeTriangles(level.GetTriangles());
			hasVoxelized = true;

			heightfield.FilterLedgeSpans(settings.MaxHeight, settings.MaxClimb);
			heightfield.FilterLowHangingWalkableObstacles(settings.MaxClimb);
			heightfield.FilterWalkableLowHeightSpans(settings.MaxHeight);

			openHeightfield = new CompactHeightfield(heightfield, settings.MaxHeight, settings.MaxClimb);
			hasOpenHeightfield = true;

			openHeightfield.BuildDistanceField();
			hasDistanceField = true;

			openHeightfield.BuildRegions(2, settings.MinRegionSize, settings.MergedRegionSize);

			Random r = new Random();
			regionColors = new Color4[openHeightfield.MaxRegions];
			for (int i = 0; i < regionColors.Length; i++)
				regionColors[i] = new Color4((byte)r.Next(0, 255), (byte)r.Next(0, 255), (byte)r.Next(0, 255), 255);

			hasRegions = true;

			contourSet = new ContourSet(openHeightfield, settings.MaxEdgeError, settings.MaxEdgeLength, 0);
			hasContours = true;

			hasSimplifiedContours = true;

			navMesh = new NavMesh(contourSet, settings.VertsPerPoly);
			hasNavMesh = true;

			navMeshDetail = new NavMeshDetail(navMesh, openHeightfield, settings.SampleDistance, settings.MaxSmapleError);
			hasNavMeshDetail = true;
		}
	}
}
