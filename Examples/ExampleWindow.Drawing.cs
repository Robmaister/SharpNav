using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;

using SharpNav;
using SharpNav.Geometry;

namespace Examples
{
	public partial class ExampleWindow
	{
		private int levelVbo, levelNormVbo, heightfieldVoxelVbo, heightfieldVoxelIbo, squareVbo, squareIbo;
		private int levelNumVerts;

		private ObjModel level;

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

		private void InitializeOpenGL()
		{
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
		}

		private void LoadLevel()
		{
			level = new ObjModel("nav_test.obj");

			var bounds = level.GetBounds().Center;
			cam.Position = new OpenTK.Vector3(bounds.X, bounds.Y, bounds.Z);

			levelVbo = GL.GenBuffer();
			GL.BindBuffer(BufferTarget.ArrayBuffer, levelVbo);
			Triangle3[] modelTris = level.GetTriangles();
			levelNumVerts = modelTris.Length * 3;
			GL.BufferData(BufferTarget.ArrayBuffer, (IntPtr)(modelTris.Length * 3 * 3 * 4), modelTris, BufferUsageHint.StaticDraw);
			GL.BindBuffer(BufferTarget.ArrayBuffer, 0);

			levelNormVbo = GL.GenBuffer();
			GL.BindBuffer(BufferTarget.ArrayBuffer, levelNormVbo);
			var modelNorms = level.GetNormals();
			GL.BufferData(BufferTarget.ArrayBuffer, (IntPtr)(modelNorms.Length * 3 * 4), modelNorms, BufferUsageHint.StaticDraw);
			GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
		}

		private void LoadDebugMeshes()
		{
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

		private void UnloadLevel()
		{
			GL.DeleteBuffer(levelVbo);
			GL.DeleteBuffer(levelNormVbo);
		}

		private void UnloadDebugMeshes()
		{
			GL.DeleteBuffer(heightfieldVoxelVbo);
			GL.DeleteBuffer(heightfieldVoxelIbo);
			GL.DeleteBuffer(squareVbo);
			GL.DeleteBuffer(squareIbo);
		}

		private void DrawUI()
		{
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
		}

		private void DrawLevel()
		{
			GL.EnableClientState(ArrayCap.VertexArray);
			GL.EnableClientState(ArrayCap.NormalArray);

			GL.BindBuffer(BufferTarget.ArrayBuffer, levelVbo);
			GL.VertexPointer(3, VertexPointerType.Float, 0, 0);
			GL.BindBuffer(BufferTarget.ArrayBuffer, levelNormVbo);
			GL.NormalPointer(NormalPointerType.Float, 0, 0);
			GL.DrawArrays(BeginMode.Triangles, 0, levelNumVerts);

			GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
			GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);

			GL.DisableClientState(ArrayCap.NormalArray);
			GL.DisableClientState(ArrayCap.VertexArray);
		}

		private void DrawHeightfield()
		{
			GL.EnableClientState(ArrayCap.VertexArray);
			GL.EnableClientState(ArrayCap.NormalArray);

			GL.Enable(EnableCap.Lighting);
			GL.Enable(EnableCap.Light0);
			GL.Light(LightName.Light0, LightParameter.Position, new Vector4(0f, 1, 0f, 0));

			GL.BindBuffer(BufferTarget.ArrayBuffer, heightfieldVoxelVbo);
			GL.VertexPointer(3, VertexPointerType.Float, 6 * 4, 0);
			GL.NormalPointer(NormalPointerType.Float, 6 * 4, 3 * 4);

			GL.Color4(0.5f, 0.5f, 0.5f, 1f);

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

			GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);
			GL.BindBuffer(BufferTarget.ArrayBuffer, 0);

			GL.Disable(EnableCap.Light0);
			GL.Disable(EnableCap.Lighting);

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
							if (span.GetConnection(dir) != 0xff)
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

			Matrix4.CreateTranslation(heightfield.Bounds.Min.X + cellSize.X, heightfield.Bounds.Min.Y, heightfield.Bounds.Min.Z + cellSize.Z, out squareTrans);
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
			GL.PushMatrix();

			Matrix4 squareScale, squareTrans;

			Matrix4.CreateTranslation(navMesh.Bounds.Min.X + navMesh.CellSize * 0.5f, navMesh.Bounds.Min.Y, navMesh.Bounds.Min.Z + navMesh.CellSize * 0.5f, out squareTrans);
			GL.MultMatrix(ref squareTrans);

			Matrix4.CreateScale(navMesh.CellSize, navMesh.CellHeight, navMesh.CellSize, out squareScale);
			GL.MultMatrix(ref squareScale);

			Color4 color = Color4.DarkViolet;
			color.A = 0.5f;
			GL.Color4(color);

			GL.Begin(BeginMode.Triangles);

			for (int i = 0; i < navMesh.NPolys; i++)
			{
				if (navMesh.Areas[i] != AreaFlags.Walkable)
					continue;

				for (int j = 2; j < navMesh.NumVertsPerPoly; j++)
				{
					if (navMesh.Polys[i].Vertices[j] == NavMesh.MESH_NULL_IDX)
						break;

					int vertIndex0 = navMesh.Polys[i].Vertices[0];
					int vertIndex1 = navMesh.Polys[i].Vertices[j - 1];
					int vertIndex2 = navMesh.Polys[i].Vertices[j];
					OpenTK.Vector3 v;

					v.X = navMesh.Verts[vertIndex0].X;
					v.Y = navMesh.Verts[vertIndex0].Y + 1;
					v.Z = navMesh.Verts[vertIndex0].Z;

					GL.Vertex3(v);

					v.X = navMesh.Verts[vertIndex1].X;
					v.Y = navMesh.Verts[vertIndex1].Y + 1;
					v.Z = navMesh.Verts[vertIndex1].Z;

					GL.Vertex3(v);

					v.X = navMesh.Verts[vertIndex2].X;
					v.Y = navMesh.Verts[vertIndex2].Y + 1;
					v.Z = navMesh.Verts[vertIndex2].Z;

					GL.Vertex3(v);
				}
			}

			GL.End();

			GL.DepthMask(false);

			//neighbor edges
			GL.Color4(Color4.Purple);

			GL.LineWidth(1.5f);
			GL.Begin(BeginMode.Lines);

			for (int i = 0; i < navMesh.NPolys; i++)
			{
				for (int j = 0; j < navMesh.NumVertsPerPoly; j++)
				{
					if (navMesh.Polys[i].Vertices[j] == NavMesh.MESH_NULL_IDX)
						break;
					if ((navMesh.Polys[i].ExtraInfo[j] & 0x8000) != 0)
						continue;

					int nj = (j + 1 >= navMesh.NumVertsPerPoly || navMesh.Polys[i].Vertices[j + 1] == NavMesh.MESH_NULL_IDX) ? 0 : j + 1;

					int vertIndex0 = navMesh.Polys[i].Vertices[j];
					int vertIndex1 = navMesh.Polys[i].Vertices[nj];
					OpenTK.Vector3 v;

					v.X = navMesh.Verts[vertIndex0].X;
					v.Y = navMesh.Verts[vertIndex0].Y + 1;
					v.Z = navMesh.Verts[vertIndex0].Z;

					GL.Vertex3(v);

					v.X = navMesh.Verts[vertIndex1].X;
					v.Y = navMesh.Verts[vertIndex1].Y + 1;
					v.Z = navMesh.Verts[vertIndex1].Z;

					GL.Vertex3(v);
				}
			}

			GL.End();

			//boundary edges
			GL.LineWidth(3.5f);
			GL.Begin(BeginMode.Lines);
			for (int i = 0; i < navMesh.NPolys; i++)
			{
				for (int j = 0; j < navMesh.NumVertsPerPoly; j++)
				{
					if (navMesh.Polys[i].Vertices[j] == NavMesh.MESH_NULL_IDX)
						break;

					if ((navMesh.Polys[i].ExtraInfo[j] & 0x8000) == 0)
						continue;

					int nj = (j + 1 >= navMesh.NumVertsPerPoly || navMesh.Polys[i].Vertices[j + 1] == NavMesh.MESH_NULL_IDX) ? 0 : j + 1;

					int vertIndex0 = navMesh.Polys[i].Vertices[j];
					int vertIndex1 = navMesh.Polys[i].Vertices[nj];
					OpenTK.Vector3 v;

					v.X = navMesh.Verts[vertIndex0].X;
					v.Y = navMesh.Verts[vertIndex0].Y + 1;
					v.Z = navMesh.Verts[vertIndex0].Z;

					GL.Vertex3(v);
					
					v.X = navMesh.Verts[vertIndex1].X;
					v.Y = navMesh.Verts[vertIndex1].Y + 1;
					v.Z = navMesh.Verts[vertIndex1].Z;

					GL.Vertex3(v);
				}
			}

			GL.End();

			GL.PointSize(4.8f);
			GL.Begin(BeginMode.Points);
			for (int i = 0; i < navMesh.NVerts; i++)
			{
				OpenTK.Vector3 v;

				v.X = navMesh.Verts[i].X;
				v.Y = navMesh.Verts[i].Y + 1;
				v.Z = navMesh.Verts[i].Z;

				GL.Vertex3(v);
			}

			GL.End();

			GL.DepthMask(true);

			GL.PopMatrix();
		}

		private void DrawNavMeshDetail()
		{
			GL.PushMatrix();

			Color4 color = Color4.DarkViolet;
			color.A = 0.5f;
			GL.Color4(color);

			GL.Begin(BeginMode.Triangles);
			for (int i = 0; i < navMeshDetail.NMeshes; i++)
			{
				NavMeshDetail.MeshInfo m = navMeshDetail.Meshes[i];

				int vertIndex = m.OldNumVerts;
				int triIndex = m.OldNumTris;

				for (int j = 0; j < m.NewNumTris; j++)
				{
					var t = navMeshDetail.Tris[triIndex + j];

					SharpNav.Vector3 v = navMeshDetail.Verts[vertIndex + t.Vertex1Hash];
					GL.Vertex3(v.X, v.Y, v.Z);

					v = navMeshDetail.Verts[vertIndex + t.Vertex2Hash];
					GL.Vertex3(v.X, v.Y, v.Z);

					v = navMeshDetail.Verts[vertIndex + t.Vertex3Hash];
					GL.Vertex3(v.X, v.Y, v.Z);
				}
			}

			GL.End();

			GL.PopMatrix();
		}
	}
}
