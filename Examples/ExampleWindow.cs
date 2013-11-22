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

using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;
using OpenTK.Input;

using SharpNav;
using SharpNav.Geometry;

namespace Examples
{
	public class ExampleWindow : GameWindow
	{
		private int levelVbo, levelNormVbo, heightfieldVoxelVbo, heightfieldVoxelIbo, squareVbo, squareIbo;
		private int levelNumVerts;

		private Camera cam;

		private Heightfield heightfield;
		private CompactHeightfield openHeightfield;
		private ObjModel model;

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

		private static readonly byte[] squareInds =
		{
			0, 1, 2, 0, 2, 3
		};

		private static readonly float[] squareVerts =
		{
			 0.5f, 0,  0.5f, 0, 1, 0,
			 0.5f, 0, -0.5f, 0, 1, 0,
			-0.5f, 0, -0.5f, 0, 1, 0,
			-0.5f, 0,  0.5f, 0, 1, 0
		};

		private static readonly byte[] voxelInds =
		{
			0,  2,  1,  0,  3,  2,  //-Z
			4,  6,  5,  4,  7,  6,  //+X
			8,  10, 9,  8,  11, 10, //+Z
			12, 14, 13, 12, 15, 14, //-X
			16, 18, 17, 16, 19, 18, //+Y
			20, 22, 21, 20, 23, 22  //-Y
		};

		private static readonly float[] voxelVerts =
		{
			//-Z face
			-0.5f,  0.5f, -0.5f,  0,  0, -1,
			-0.5f, -0.5f, -0.5f,  0,  0, -1,
			 0.5f, -0.5f, -0.5f,  0,  0, -1,
			 0.5f,  0.5f, -0.5f,  0,  0, -1,

			//+X face
			 0.5f,  0.5f, -0.5f,  1,  0,  0,
			 0.5f, -0.5f, -0.5f,  1,  0,  0,
			 0.5f, -0.5f,  0.5f,  1,  0,  0,
			 0.5f,  0.5f,  0.5f,  1,  0,  0,

			//+Z face
			 0.5f,  0.5f,  0.5f,  0,  0,  1,
			 0.5f, -0.5f,  0.5f,  0,  0,  1,
			-0.5f, -0.5f,  0.5f,  0,  0,  1,
			-0.5f,  0.5f,  0.5f,  0,  0,  1,

			//-X face
			-0.5f,  0.5f,  0.5f, -1,  0,  0,
			-0.5f, -0.5f,  0.5f, -1,  0,  0,
			-0.5f, -0.5f, -0.5f, -1,  0,  0,
			-0.5f,  0.5f, -0.5f, -1,  0,  0,

			//+Y face
			-0.5f,  0.5f,  0.5f,  0,  1,  0,
			-0.5f,  0.5f, -0.5f,  0,  1,  0,
			 0.5f,  0.5f, -0.5f,  0,  1,  0,
			 0.5f,  0.5f,  0.5f,  0,  1,  0,

			//-Y face
			-0.5f, -0.5f, -0.5f,  0, -1,  0,
			-0.5f, -0.5f,  0.5f,  0, -1,  0,
			 0.5f, -0.5f,  0.5f,  0, -1,  0,
			 0.5f, -0.5f, -0.5f,  0, -1,  0
		};

		public ExampleWindow()
			: base()
		{
			cam = new Camera();
		}

		protected override void OnLoad(EventArgs e)
		{
			base.OnLoad(e);

			Keyboard.KeyDown += OnKeyboardKeyDown;

			GL.Enable(EnableCap.DepthTest);
			GL.DepthMask(true);
			GL.DepthFunc(DepthFunction.Lequal);
			GL.Enable(EnableCap.CullFace);
			GL.FrontFace(FrontFaceDirection.Ccw);
			GL.Enable(EnableCap.Blend);
			GL.BlendFunc(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);
			GL.ClearColor(Color4.CornflowerBlue);

			GL.Enable(EnableCap.Lighting);
			GL.Enable(EnableCap.Light0);
			GL.Light(LightName.Light0, LightParameter.Ambient, new Vector4(0.6f, 0.6f, 0.6f, 1f));

			model = new ObjModel("nav_test.obj");
			BBox3 bounds = model.GetBounds();
			heightfield = new Heightfield(bounds.Min, bounds.Max, 0.5f, 0.1f);

			levelVbo = GL.GenBuffer();
			GL.BindBuffer(BufferTarget.ArrayBuffer, levelVbo);
			Triangle3[] modelTris = model.GetTriangles();
			levelNumVerts = modelTris.Length * 3;
			GL.BufferData(BufferTarget.ArrayBuffer, (IntPtr)(modelTris.Length * 3 * 3 * 4), modelTris, BufferUsageHint.StaticDraw);
			GL.BindBuffer(BufferTarget.ArrayBuffer, 0);

			levelNormVbo = GL.GenBuffer();
			GL.BindBuffer(BufferTarget.ArrayBuffer, levelNormVbo);
			var modelNorms = model.GetNormals();
			GL.BufferData(BufferTarget.ArrayBuffer, (IntPtr)(modelNorms.Length * 3 * 4), modelNorms, BufferUsageHint.StaticDraw);
			GL.BindBuffer(BufferTarget.ArrayBuffer, 0);

			heightfieldVoxelVbo = GL.GenBuffer();
			GL.BindBuffer(BufferTarget.ArrayBuffer, heightfieldVoxelVbo);
			GL.BufferData(BufferTarget.ArrayBuffer, (IntPtr)(voxelVerts.Length * 4), voxelVerts, BufferUsageHint.StaticDraw);
			GL.BindBuffer(BufferTarget.ArrayBuffer, 0);

			heightfieldVoxelIbo = GL.GenBuffer();
			GL.BindBuffer(BufferTarget.ElementArrayBuffer, heightfieldVoxelIbo);
			GL.BufferData(BufferTarget.ElementArrayBuffer, (IntPtr)voxelInds.Length, voxelInds, BufferUsageHint.StaticDraw);
			GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);

			squareVbo = GL.GenBuffer();
			GL.BindBuffer(BufferTarget.ArrayBuffer, squareVbo);
			GL.BufferData(BufferTarget.ArrayBuffer, (IntPtr)(squareVerts.Length * 4), squareVerts, BufferUsageHint.StaticDraw);
			GL.BindBuffer(BufferTarget.ArrayBuffer, 0);

			squareIbo = GL.GenBuffer();
			GL.BindBuffer(BufferTarget.ElementArrayBuffer, squareIbo);
			GL.BufferData(BufferTarget.ElementArrayBuffer, (IntPtr)squareInds.Length, squareInds, BufferUsageHint.StaticDraw);
			GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);

		}

		protected override void OnUpdateFrame(FrameEventArgs e)
		{
			base.OnUpdateFrame(e);

			KeyboardState k = OpenTK.Input.Keyboard.GetState();
			MouseState m = OpenTK.Input.Mouse.GetState();

			if (k[Key.W])
				cam.Move(-5f * (float)e.Time);
			if (k[Key.A])
				cam.Strafe(-5f * (float)e.Time);
			if (k[Key.S])
				cam.Move(5f * (float)e.Time);
			if (k[Key.D])
				cam.Strafe(5f * (float)e.Time);
			if (k[Key.Q])
				cam.Elevate(5f * (float)e.Time);
			if (k[Key.E])
				cam.Elevate(-5f * (float)e.Time);

			if (m[MouseButton.Left])
			{
				cam.RotatePitch((m.X - prevM.X) * (float)e.Time * 2f);
				cam.RotateHeading((prevM.Y - m.Y) * (float)e.Time * 2f);
			}

			GL.MatrixMode(MatrixMode.Modelview);
			cam.LoadView();

			prevK = k;
			prevM = m;
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
				if (!hasVoxelized)
				{
					heightfield.RasterizeTriangles(model.GetTriangles());
					hasVoxelized = true;
				}
				else if (!hasOpenHeightfield)
				{
					const int WalkableClimb = 15;
					const int WalkableHeight = 40;

					heightfield.FilterLedgeSpans(WalkableHeight, WalkableClimb);
					heightfield.FilterLowHangingWalkableObstacles(WalkableClimb);
					heightfield.FilterWalkableLowHeightSpans(WalkableHeight);

					openHeightfield = new CompactHeightfield(heightfield, WalkableHeight, WalkableClimb);
					hasOpenHeightfield = true;
				}
				else if (!hasDistanceField)
				{
					openHeightfield.BuildDistanceField();
					hasDistanceField = true;
				}
				else if (!hasRegions)
				{
					openHeightfield.BuildRegions(2, 8, 20);

					Random r = new Random();
					regionColors = new Color4[openHeightfield.MaxRegions];
					for (int i = 0; i < regionColors.Length; i++)
						regionColors[i] = new Color4((byte)r.Next(0, 255), (byte)r.Next(0, 255), (byte)r.Next(0, 255), 255);

					hasRegions = true;
				}
				else if (!hasContours)
				{
					contourSet = new ContourSet(openHeightfield, 1f, 5, 0);
					hasContours = true;
				}
				else if (!hasSimplifiedContours)
				{
					hasSimplifiedContours = true;
				}
				else if (!hasNavMesh)
				{
					navMesh = new NavMesh(contourSet, 6);
					hasNavMesh = true;
				}
				else if (!hasNavMeshDetail)
				{
					navMeshDetail = new NavMeshDetail(navMesh, openHeightfield, 6, 1);
					hasNavMeshDetail = true;
				}
			}

			base.OnKeyDown(e);
		}

		protected override void OnRenderFrame(FrameEventArgs e)
		{
			base.OnRenderFrame(e);

			GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
			GL.Color4(Color4.White);

			GL.Enable(EnableCap.Lighting);
			GL.Enable(EnableCap.Light0);
			GL.Light(LightName.Light0, LightParameter.Position, new Vector4(0f, 1, 0f, 0));

			GL.EnableClientState(ArrayCap.VertexArray);
			GL.EnableClientState(ArrayCap.NormalArray);
			GL.BindBuffer(BufferTarget.ArrayBuffer, levelVbo);
			GL.VertexPointer(3, VertexPointerType.Float, 0, 0);
			GL.BindBuffer(BufferTarget.ArrayBuffer, levelNormVbo);
			GL.NormalPointer(NormalPointerType.Float, 0, 0);
			GL.DrawArrays(BeginMode.Triangles, 0, levelNumVerts);

			GL.Disable(EnableCap.Light0);
			GL.Disable(EnableCap.Lighting);

			GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
			GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);
			GL.DisableClientState(ArrayCap.NormalArray);
			GL.DisableClientState(ArrayCap.VertexArray);

			if (hasNavMesh)
			{
				DrawNavMesh();
			}
			else if (hasSimplifiedContours)
			{
				DrawContours(true);
			}
			else if (hasContours)
			{
				DrawContours(false);
			}
			else if (hasRegions)
			{
				DrawRegions();
			}
			else if (hasDistanceField)
			{
				DrawDistanceField();
			}
			else if (hasOpenHeightfield)
			{
				DrawCompactHeightfield();
			}
			else if (hasVoxelized)
			{
				DrawHeightfield();
			}

			//GL.DepthMask(true);

			SwapBuffers();
		}

		protected override void OnResize(EventArgs e)
		{
			base.OnResize(e);

			GL.Viewport(0, 0, Width, Height);
			float aspect = Width / (float)Height;

			Matrix4 persp = Matrix4.CreatePerspectiveFieldOfView(MathHelper.PiOver4, aspect, 0.1f, 100f);
			GL.MatrixMode(MatrixMode.Projection);
			GL.LoadMatrix(ref persp);
			GL.MatrixMode(MatrixMode.Modelview);
			GL.LoadIdentity();
		}

		private void DrawHeightfield()
		{
			GL.EnableClientState(ArrayCap.VertexArray);
			GL.EnableClientState(ArrayCap.NormalArray);

			GL.BindBuffer(BufferTarget.ArrayBuffer, heightfieldVoxelVbo);
			GL.VertexPointer(3, VertexPointerType.Float, 6 * 4, 0);
			GL.NormalPointer(NormalPointerType.Float, 6 * 4, 3 * 4);

			GL.Color4(1f, 0f, 0f, 0.5f);
			GL.DepthMask(false);

			GL.BindBuffer(BufferTarget.ElementArrayBuffer, heightfieldVoxelIbo);

			var cellSize = heightfield.CellSize;
			var halfCellSize = cellSize * 0.5f;
			Matrix4 spanLoc, spanScale;
			for (int i = 0; i < heightfield.Length; i++)
			{
				for (int j = 0; j < heightfield.Width; j++)
				{
					SharpNav.Vector3 cellLoc = new SharpNav.Vector3(j * cellSize.X + halfCellSize.X + heightfield.Bounds.Min.X, heightfield.Bounds.Min.Y, i * cellSize.Z + halfCellSize.Z + heightfield.Bounds.Min.Z);
					var cell = heightfield[j, i];

					foreach (var span in cell.Spans)
					{
						GL.PushMatrix();
						
						Matrix4.CreateTranslation(cellLoc.X, ((span.Minimum + span.Maximum) * 0.5f) * cellSize.Y + cellLoc.Y, cellLoc.Z, out spanLoc);
						GL.MultMatrix(ref spanLoc);

						Matrix4.CreateScale(cellSize.X, cellSize.Y * span.Height, cellSize.Z, out spanScale);
						GL.MultMatrix(ref spanScale);

						GL.DrawElements(BeginMode.Triangles, voxelInds.Length, DrawElementsType.UnsignedByte, 0);
						GL.PopMatrix();
					}
				}
			}

			GL.DepthMask(true);

			GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);
			GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
			GL.DisableClientState(ArrayCap.VertexArray);
			GL.DisableClientState(ArrayCap.NormalArray);
		}

		private void DrawCompactHeightfield()
		{
			GL.EnableClientState(ArrayCap.VertexArray);
			GL.EnableClientState(ArrayCap.NormalArray);

			GL.BindBuffer(BufferTarget.ArrayBuffer, squareVbo);
			GL.VertexPointer(3, VertexPointerType.Float, 6 * 4, 0);
			GL.NormalPointer(NormalPointerType.Float, 6 * 4, 3 * 4);

			GL.BindBuffer(BufferTarget.ElementArrayBuffer, squareIbo);

			var cellSize = heightfield.CellSize;
			var halfCellSize = cellSize * 0.5f;

			Matrix4 squareScale, squareTrans;
			OpenTK.Vector3 squarePos;
			Matrix4.CreateScale(cellSize.X, 1, cellSize.Z, out squareScale);

			for (int i = 0; i < openHeightfield.Length; i++)
			{
				for (int j = 0; j < openHeightfield.Width; j++)
				{
					squarePos = new OpenTK.Vector3(j * cellSize.X + halfCellSize.X + heightfield.Bounds.Min.X, heightfield.Bounds.Min.Y, i * cellSize.Z + halfCellSize.Z + heightfield.Bounds.Min.Z);

					var cell = openHeightfield[j, i];

					foreach (var span in cell)
					{
						GL.PushMatrix();
						
						int numCons = 0;
						for (int dir = 0; dir < 4; dir++)
						{
							if (CompactSpan.GetConnection(dir, span) != 0xff)
								numCons++;
						}

						GL.Color4(numCons / 4f, numCons / 4f, numCons / 4f, 1f);

						var squarePosFinal = squarePos;
						squarePosFinal.Y += span.Minimum * cellSize.Y;
						Matrix4.CreateTranslation(ref squarePosFinal, out squareTrans);
						GL.MultMatrix(ref squareTrans);

						GL.MultMatrix(ref squareScale);

						GL.DrawElements(BeginMode.Triangles, squareInds.Length, DrawElementsType.UnsignedByte, 0);

						GL.PopMatrix();
					}
				}
			}

			GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);
			GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
			GL.DisableClientState(ArrayCap.VertexArray);
			GL.DisableClientState(ArrayCap.NormalArray);
		}

		private void DrawDistanceField()
		{
			GL.EnableClientState(ArrayCap.VertexArray);
			GL.EnableClientState(ArrayCap.NormalArray);

			GL.BindBuffer(BufferTarget.ArrayBuffer, squareVbo);
			GL.VertexPointer(3, VertexPointerType.Float, 6 * 4, 0);
			GL.NormalPointer(NormalPointerType.Float, 6 * 4, 3 * 4);

			GL.BindBuffer(BufferTarget.ElementArrayBuffer, squareIbo);

			int maxdist = openHeightfield.MaxDistance;

			var cellSize = heightfield.CellSize;
			var halfCellSize = cellSize * 0.5f;

			Matrix4 squareScale, squareTrans;
			OpenTK.Vector3 squarePos;
			Matrix4.CreateScale(cellSize.X, 1, cellSize.Z, out squareScale);

			for (int i = 0; i < openHeightfield.Length; i++)
			{
				for (int j = 0; j < openHeightfield.Width; j++)
				{
					squarePos = new OpenTK.Vector3(j * cellSize.X + halfCellSize.X + heightfield.Bounds.Min.X, heightfield.Bounds.Min.Y, i * cellSize.Z + halfCellSize.Z + heightfield.Bounds.Min.Z);

					var cell = openHeightfield.Cells[i * openHeightfield.Width + j];

					for (int k = cell.StartIndex, kEnd = cell.StartIndex + cell.Count; k < kEnd; k++)
					{
						GL.PushMatrix();

						int dist = openHeightfield.Distances[k];
						float val = (float)dist / (float)maxdist;
						GL.Color4(val, val, val, 1f);

						var span = openHeightfield.Spans[k];
						var squarePosFinal = squarePos;
						squarePosFinal.Y += span.Minimum * cellSize.Y;
						Matrix4.CreateTranslation(ref squarePosFinal, out squareTrans);
						GL.MultMatrix(ref squareTrans);

						GL.MultMatrix(ref squareScale);

						GL.DrawElements(BeginMode.Triangles, squareInds.Length, DrawElementsType.UnsignedByte, 0);

						GL.PopMatrix();
					}
				}
			}

			GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);
			GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
			GL.DisableClientState(ArrayCap.VertexArray);
			GL.DisableClientState(ArrayCap.NormalArray);
		}

		private void DrawRegions()
		{
			GL.EnableClientState(ArrayCap.VertexArray);
			GL.EnableClientState(ArrayCap.NormalArray);

			GL.BindBuffer(BufferTarget.ArrayBuffer, squareVbo);
			GL.VertexPointer(3, VertexPointerType.Float, 6 * 4, 0);
			GL.NormalPointer(NormalPointerType.Float, 6 * 4, 3 * 4);

			GL.BindBuffer(BufferTarget.ElementArrayBuffer, squareIbo);

			int maxdist = openHeightfield.MaxDistance;
			var cellSize = heightfield.CellSize;
			var halfCellSize = cellSize * 0.5f;

			Matrix4 squareScale, squareTrans;
			OpenTK.Vector3 squarePos;
			Matrix4.CreateScale(cellSize.X, 1, cellSize.Z, out squareScale);

			for (int i = 0; i < openHeightfield.Length; i++)
			{
				for (int j = 0; j < openHeightfield.Width; j++)
				{
					var cell = openHeightfield.Cells[i * openHeightfield.Width + j];
					squarePos = new OpenTK.Vector3(j * cellSize.X + halfCellSize.X + heightfield.Bounds.Min.X, heightfield.Bounds.Min.Y, i * cellSize.Z + halfCellSize.Z + heightfield.Bounds.Min.Z);

					for (int k = cell.StartIndex, kEnd = cell.StartIndex + cell.Count; k < kEnd; k++)
					{
						GL.PushMatrix();
						var span = openHeightfield.Spans[k];

						int region = span.Region;
						if ((region & 0x8000) == 0x8000)
							region &= 0x7fff;
						Color4 col = regionColors[region];
						GL.Color4(col);

						var squarePosFinal = squarePos;
						squarePosFinal.Y += span.Minimum * cellSize.Y;
						Matrix4.CreateTranslation(ref squarePosFinal, out squareTrans);
						GL.MultMatrix(ref squareTrans);

						GL.MultMatrix(ref squareScale);

						GL.DrawElements(BeginMode.Triangles, squareInds.Length, DrawElementsType.UnsignedByte, 0);

						GL.PopMatrix();
					}
				}
			}

			GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);
			GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
			GL.DisableClientState(ArrayCap.VertexArray);
			GL.DisableClientState(ArrayCap.NormalArray);
		}

		private void DrawContours(bool simplified)
		{
			GL.EnableClientState(ArrayCap.VertexArray);

			int maxdist = openHeightfield.MaxDistance;
			var cellSize = heightfield.CellSize;
			var halfCellSize = cellSize * 0.5f;

			GL.PushMatrix();

			Matrix4 squareScale, squareTrans;

			Matrix4.CreateTranslation(heightfield.Bounds.Min.X + 1, heightfield.Bounds.Min.Y, heightfield.Bounds.Min.Z + 1, out squareTrans);
			GL.MultMatrix(ref squareTrans);

			Matrix4.CreateScale(cellSize.X, cellSize.Y, cellSize.Z, out squareScale);
			GL.MultMatrix(ref squareScale);

			foreach (var c in contourSet.Contours)
			{
				GL.VertexPointer(3, VertexPointerType.Int, 16, simplified ? c.Vertices : c.RawVertices);

				int region = c.RegionId;
				if ((region & 0x8000) == 0x8000) //HACK properly display border regions
					region &= 0x7fff;

				Color4 col = regionColors[region];
				GL.Color4(col);

				GL.LineWidth(5f);
				GL.DrawArrays(BeginMode.LineLoop, 0, simplified ? c.NumVerts : c.NumRawVerts);
			}

			GL.PopMatrix();

			GL.DisableClientState(ArrayCap.VertexArray);
		}

		private void DrawNavMesh()
		{
			
		}
	}
}
