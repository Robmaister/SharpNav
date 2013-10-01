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
		private int levelVbo, levelNormVbo, heightfieldVoxelVbo;
		private int levelNumVerts, heightfieldVoxelNumVerts;

		private Heightfield heightfield;
		private ObjModel model;

		private bool hasVoxelized;

		private OpenTK.Vector3 cameraPos;

		public ExampleWindow()
			: base()
		{
		}

		protected override void OnLoad(EventArgs e)
		{
			base.OnLoad(e);

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
			GL.Light(LightName.Light0, LightParameter.Position, new Vector4(0, 1, 0, 0));
			//GL.Light(LightName.Light0, LightParameter.LinearAttenuation, 0.001f);
			//GL.Light(LightName.Light0, LightParameter.QuadraticAttenuation, 0.0001f);
			//GL.Light(LightName.Light0, LightParameter.Ambient, 0.1f);
			//GL.Light(LightName.Light0, LightParameter.Diffuse, 0.5f);

			model = new ObjModel("nav_test.obj");
			BBox3 bounds = model.GetBounds();
			heightfield = new Heightfield(bounds.Min, bounds.Max, 0.5f, 0.2f);

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
		}

		protected override void OnUpdateFrame(FrameEventArgs e)
		{
			base.OnUpdateFrame(e);

			KeyboardState k = OpenTK.Input.Keyboard.GetState();
			MouseState m = OpenTK.Input.Mouse.GetState();

			if (k[Key.W])
				cameraPos.Z -= 0.1f;
			if (k[Key.A])
				cameraPos.X -= 0.1f;
			if (k[Key.S])
				cameraPos.Z += 0.1f;
			if (k[Key.D])
				cameraPos.X += 0.1f;
			if (k[Key.Q])
				cameraPos.Y += 0.1f;
			if (k[Key.E])
				cameraPos.Y -= 0.1f;

			if (k[Key.Escape])
				Exit();

			if (k[Key.F12])
				WindowState = OpenTK.WindowState.Fullscreen;
			if (k[Key.F11])
				WindowState = OpenTK.WindowState.Normal;

			if (k[Key.P] && !hasVoxelized)
			{
				heightfield.RasterizeTriangles(model.GetTriangles());
				hasVoxelized = true;
			}

			GL.MatrixMode(MatrixMode.Modelview);
			Matrix4 view = Matrix4.CreateTranslation(-cameraPos);
			GL.LoadMatrix(ref view);
		}

		protected override void OnRenderFrame(FrameEventArgs e)
		{
			base.OnRenderFrame(e);

			GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
			GL.Color4(Color4.White);

			GL.Enable(EnableCap.Lighting);
			GL.Light(LightName.Light0, LightParameter.Position, new Vector4(0.25f, 1, 0.15f, 0));

			GL.EnableClientState(ArrayCap.VertexArray);
			GL.EnableClientState(ArrayCap.NormalArray);
			GL.BindBuffer(BufferTarget.ArrayBuffer, levelVbo);
			GL.VertexPointer(3, VertexPointerType.Float, 0, 0);
			GL.BindBuffer(BufferTarget.ArrayBuffer, levelNormVbo);
			GL.NormalPointer(NormalPointerType.Float, 0, 0);
			GL.DrawArrays(BeginMode.Triangles, 0, levelNumVerts);
			GL.DisableClientState(ArrayCap.VertexArray);
			GL.DisableClientState(ArrayCap.NormalArray);

			GL.Disable(EnableCap.Lighting);
			GL.Begin(BeginMode.Lines);
			for (int i = 0; i < heightfield.Length; i++)
			{
				for (int j = 0; j < heightfield.Width; j++)
				{
					var cell = heightfield[j, i];

					foreach (var span in cell.Spans)
					{
						var min = new SharpNav.Vector3(j * heightfield.CellSize.X, span.Minimum * heightfield.CellSize.Y, i * heightfield.CellSize.Z);
						var max = new SharpNav.Vector3(j * heightfield.CellSize.X, span.Maximum * heightfield.CellSize.Y, i * heightfield.CellSize.Z);
						var halfCellSize = heightfield.CellSize * 0.5f;
						halfCellSize.Y = 0;

						min += heightfield.Bounds.Min + halfCellSize;
						max += heightfield.Bounds.Min + halfCellSize;

						GL.Color4(Color4.Red);
						GL.Vertex3(min.X, min.Y, min.Z);
						GL.Color4(Color4.Red);
						GL.Vertex3(max.X, max.Y, max.Z);
					}
				}
			}
			GL.End();

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
	}
}
