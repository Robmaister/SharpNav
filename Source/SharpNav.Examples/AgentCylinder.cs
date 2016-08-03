// Copyright (c) 2015 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
// Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE

using System;
using System.Collections.Generic;

using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;

//Prevents name collision under the Standalone configuration
#if STANDALONE
using Vector3 = OpenTK.Vector3;
using SVector3 = SharpNav.Geometry.Vector3;
#elif OPENTK
using SVector3 = OpenTK.Vector3;
#endif

//Doesn't compile if in an unsupported configuration
#if STANDALONE || OPENTK

namespace SharpNav.Examples
{
	public class AgentCylinder
	{
		private int segments;
		private float radius;
		private float height;

		private int vbo, ibo, numIndices;

		public AgentCylinder(int segments, float radius, float height)
		{
			this.segments = segments;
			this.radius = radius;
			this.height = height;

			this.Color = Color4.LightGray;

			Update();
		}

		public Color4 Color { get; set; }

		public int Segments
		{
			get { return segments; }
			set { segments = value; Update(); }
		}

		public float Radius
		{
			get { return radius; }
			set { radius = value; Update(); }
		}

		public float Height
		{
			get { return height; }
			set { height = value; Update(); }
		}

		private void Update()
		{
			List<Vector3> verts = new List<Vector3>();
			List<uint> inds = new List<uint>();

			//verts
			for (int i = 0; i < 2; i++)
			{
				for (int j = 0; j <= segments; j++)
				{
					float theta = ((float)j / (float)segments) * 2 * (float)Math.PI;
					float st = (float)Math.Sin(theta), ct = (float)Math.Cos(theta);

					verts.Add(new Vector3(radius * st, height * i, radius * ct));
					verts.Add(i == 0 ? -Vector3.UnitY : Vector3.UnitY);
				}
			}

			for (int i = 0; i < 2; i++)
			{
				for (int j = 0; j <= segments; j++)
				{
					float theta = ((float)j / (float)segments) * 2 * (float)Math.PI;
					float st = (float)Math.Sin(theta), ct = (float)Math.Cos(theta);

					verts.Add(new Vector3(radius * st, height * i, radius * ct));
					verts.Add(new Vector3(st, 0, ct));
				}
			}

			//inds
			int start = 0;

			//bottom cap
			for (int i = 1; i < segments - 1; i++)
			{
				inds.Add((uint)(start));
				inds.Add((uint)(start + i + 1));
				inds.Add((uint)(start + i));
			}

			start = segments + 1;

			//top cap
			for (int i = 1; i < segments - 1; i++)
			{
				inds.Add((uint)(start));
				inds.Add((uint)(start + i));
				inds.Add((uint)(start + i + 1));
			}

			start += segments + 1;

			//edge
			for (int i = 0; i <= segments; i++)
			{
				inds.Add((uint)(start + i));
				inds.Add((uint)(start + segments + i + 1));
				inds.Add((uint)(start + segments + i));
				inds.Add((uint)(start + i));
				inds.Add((uint)(start + i + 1));
				inds.Add((uint)(start + segments + i + 1));
			}

			if (vbo == 0)
				vbo = GL.GenBuffer();

			GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
			GL.BufferData(BufferTarget.ArrayBuffer, (IntPtr)(verts.Count * 3 * 4), verts.ToArray(), BufferUsageHint.StaticDraw);
			GL.BindBuffer(BufferTarget.ArrayBuffer, 0);

			if (ibo == 0)
				ibo = GL.GenBuffer();

			GL.BindBuffer(BufferTarget.ElementArrayBuffer, ibo);
			GL.BufferData(BufferTarget.ElementArrayBuffer, (IntPtr)(inds.Count * 4), inds.ToArray(), BufferUsageHint.StaticDraw);
			GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);

			numIndices = inds.Count;
		}

		public void Draw(Vector3 pos)
		{
			if (vbo == 0 || ibo == 0)
				return;

			Matrix4 trans;
			Matrix4.CreateTranslation(ref pos, out trans);

			GL.EnableClientState(ArrayCap.VertexArray);
			GL.EnableClientState(ArrayCap.NormalArray);

			GL.Enable(EnableCap.Lighting);
			GL.Enable(EnableCap.Light0);
			GL.Light(LightName.Light0, LightParameter.Position, new Vector4(0.5f, 1, 0.5f, 0));

			GL.PushMatrix();
			GL.MultMatrix(ref trans);

			GL.Color4(Color);

			GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
			GL.VertexPointer(3, VertexPointerType.Float, 6 * 4, 0);
			GL.NormalPointer(NormalPointerType.Float, 6 * 4, 3 * 4);

			GL.BindBuffer(BufferTarget.ElementArrayBuffer, ibo);

			GL.DrawElements(BeginMode.Triangles, numIndices, DrawElementsType.UnsignedInt, 0);

			GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);
			GL.BindBuffer(BufferTarget.ArrayBuffer, 0);

			GL.PopMatrix();

			GL.Disable(EnableCap.Light0);
			GL.Disable(EnableCap.Lighting);

			GL.DisableClientState(ArrayCap.NormalArray);
			GL.DisableClientState(ArrayCap.VertexArray);
		}
	}
}

#endif
