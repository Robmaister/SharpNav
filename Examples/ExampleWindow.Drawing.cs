#region License
/**
 * Copyright (c) 2013-2014 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
 * Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE
 */
#endregion

using System;

using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;

using SharpNav;
using SharpNav.Geometry;

//Prevents name collision under the Standalone configuration
#if !OPENTK
using Vector3 = OpenTK.Vector3;
using SVector3 = SharpNav.Vector3;
#else

#endif

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
			cam.Position = new Vector3(bounds.X, bounds.Y, bounds.Z);

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
					var cellLoc = new Vector3(j * cellSize.X + halfCellSize.X + heightfield.Bounds.Min.X, heightfield.Bounds.Min.Y, i * cellSize.Z + halfCellSize.Z + heightfield.Bounds.Min.Z);
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
			Vector3 squarePos;
			Matrix4.CreateScale(cellSize.X, 1, cellSize.Z, out squareScale);

			for (int i = 0; i < compactHeightfield.Length; i++)
			{
				for (int j = 0; j < compactHeightfield.Width; j++)
				{
					squarePos = new Vector3(j * cellSize.X + halfCellSize.X + heightfield.Bounds.Min.X, heightfield.Bounds.Min.Y, i * cellSize.Z + halfCellSize.Z + heightfield.Bounds.Min.Z);

					var cell = compactHeightfield[j, i];

					foreach (var span in cell)
					{
						GL.PushMatrix();

						int numCons = 0;
						for (var dir = Direction.West; dir <= Direction.South; dir++)
						{
							if (span.IsConnected(dir))
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

			int maxdist = compactHeightfield.MaxDistance;

			var cellSize = heightfield.CellSize;
			var halfCellSize = cellSize * 0.5f;

			Matrix4 squareScale, squareTrans;
			Vector3 squarePos;
			Matrix4.CreateScale(cellSize.X, 1, cellSize.Z, out squareScale);

			for (int i = 0; i < compactHeightfield.Length; i++)
			{
				for (int j = 0; j < compactHeightfield.Width; j++)
				{
					squarePos = new Vector3(j * cellSize.X + halfCellSize.X + heightfield.Bounds.Min.X, heightfield.Bounds.Min.Y, i * cellSize.Z + halfCellSize.Z + heightfield.Bounds.Min.Z);

					var cell = compactHeightfield.Cells[i * compactHeightfield.Width + j];

					for (int k = cell.StartIndex, kEnd = cell.StartIndex + cell.Count; k < kEnd; k++)
					{
						GL.PushMatrix();

						int dist = compactHeightfield.Distances[k];
						float val = (float)dist / (float)maxdist;
						GL.Color4(val, val, val, 1f);

						var span = compactHeightfield.Spans[k];
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

			int maxdist = compactHeightfield.MaxDistance;
			var cellSize = heightfield.CellSize;
			var halfCellSize = cellSize * 0.5f;

			Matrix4 squareScale, squareTrans;
			Vector3 squarePos;
			Matrix4.CreateScale(cellSize.X, 1, cellSize.Z, out squareScale);

			for (int i = 0; i < compactHeightfield.Length; i++)
			{
				for (int j = 0; j < compactHeightfield.Width; j++)
				{
					var cell = compactHeightfield.Cells[i * compactHeightfield.Width + j];
					squarePos = new Vector3(j * cellSize.X + halfCellSize.X + heightfield.Bounds.Min.X, heightfield.Bounds.Min.Y, i * cellSize.Z + halfCellSize.Z + heightfield.Bounds.Min.Z);

					for (int k = cell.StartIndex, kEnd = cell.StartIndex + cell.Count; k < kEnd; k++)
					{
						GL.PushMatrix();
						var span = compactHeightfield.Spans[k];

						int region = span.Region;
						if (Region.IsBorder(region))
							region = Region.RemoveBorderFlag(region);

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

			int maxdist = compactHeightfield.MaxDistance;
			var cellSize = heightfield.CellSize;
			var halfCellSize = cellSize * 0.5f;

			GL.PushMatrix();

			Matrix4 squareScale, squareTrans;

			Matrix4.CreateTranslation(heightfield.Bounds.Min.X + cellSize.X, heightfield.Bounds.Min.Y, heightfield.Bounds.Min.Z + cellSize.Z, out squareTrans);
			GL.MultMatrix(ref squareTrans);

			Matrix4.CreateScale(cellSize.X, cellSize.Y, cellSize.Z, out squareScale);
			GL.MultMatrix(ref squareScale);

			GL.LineWidth(5f);
			GL.Begin(BeginMode.Lines);
			
			foreach (var c in contourSet.Contours)
			{
				int region = c.RegionId;

				//skip border regions
				if (Region.IsBorder(region))
					continue;

				Color4 col = regionColors[region];
				GL.Color4(col);

				if (simplified)
				{
					for (int i = 0; i < c.Vertices.Length; i++)
					{
						int ni = (i + 1) % c.Vertices.Length;
						GL.Vertex3(c.Vertices[i].X, c.Vertices[i].Y, c.Vertices[i].Z);
						GL.Vertex3(c.Vertices[ni].X, c.Vertices[ni].Y, c.Vertices[ni].Z);
					}
				}
				else
				{
					for (int i = 0; i < c.RawVertices.Length; i++)
					{
						int ni = (i + 1) % c.RawVertices.Length;
						GL.Vertex3(c.RawVertices[i].X, c.RawVertices[i].Y, c.RawVertices[i].Z);
						GL.Vertex3(c.RawVertices[ni].X, c.RawVertices[ni].Y, c.RawVertices[ni].Z);
					}
				}
			}

			GL.End();

			GL.PopMatrix();

			GL.DisableClientState(ArrayCap.VertexArray);
		}

		private void DrawPolyMesh()
		{
			GL.PushMatrix();

			Matrix4 squareScale, squareTrans;

			Matrix4.CreateTranslation(polyMesh.Bounds.Min.X + polyMesh.CellSize * 0.5f, polyMesh.Bounds.Min.Y, polyMesh.Bounds.Min.Z + polyMesh.CellSize * 0.5f, out squareTrans);
			GL.MultMatrix(ref squareTrans);

			Matrix4.CreateScale(polyMesh.CellSize, polyMesh.CellHeight, polyMesh.CellSize, out squareScale);
			GL.MultMatrix(ref squareScale);

			Color4 color = Color4.DarkViolet;
			color.A = 0.5f;
			GL.Color4(color);

			GL.Begin(BeginMode.Triangles);

			for (int i = 0; i < polyMesh.NPolys; i++)
			{
				if (polyMesh.Areas[i] != AreaFlags.Walkable)
					continue;

				for (int j = 2; j < polyMesh.NumVertsPerPoly; j++)
				{
					if (polyMesh.Polys[i].Vertices[j] == PolyMesh.MESH_NULL_IDX)
						break;

					int vertIndex0 = polyMesh.Polys[i].Vertices[0];
					int vertIndex1 = polyMesh.Polys[i].Vertices[j - 1];
					int vertIndex2 = polyMesh.Polys[i].Vertices[j];
					
					var v = polyMesh.Verts[vertIndex0];
					GL.Vertex3(v.X, v.Y + 1, v.Z);

					v = polyMesh.Verts[vertIndex1];
					GL.Vertex3(v.X, v.Y + 1, v.Z);

					v = polyMesh.Verts[vertIndex2];
					GL.Vertex3(v.X, v.Y + 1, v.Z);
				}
			}

			GL.End();

			GL.DepthMask(false);

			//neighbor edges
			GL.Color4(Color4.Purple);

			GL.LineWidth(1.5f);
			GL.Begin(BeginMode.Lines);

			for (int i = 0; i < polyMesh.NPolys; i++)
			{
				for (int j = 0; j < polyMesh.NumVertsPerPoly; j++)
				{
					if (polyMesh.Polys[i].Vertices[j] == PolyMesh.MESH_NULL_IDX)
						break;
					if ((polyMesh.Polys[i].ExtraInfo[j] & 0x8000) != 0)
						continue;

					int nj = (j + 1 >= polyMesh.NumVertsPerPoly || polyMesh.Polys[i].Vertices[j + 1] == PolyMesh.MESH_NULL_IDX) ? 0 : j + 1;

					int vertIndex0 = polyMesh.Polys[i].Vertices[j];
					int vertIndex1 = polyMesh.Polys[i].Vertices[nj];
					
					var v = polyMesh.Verts[vertIndex0];
					GL.Vertex3(v.X, v.Y + 1, v.Z);

					v = polyMesh.Verts[vertIndex1];
					GL.Vertex3(v.X, v.Y + 1, v.Z);
				}
			}

			GL.End();

			//boundary edges
			GL.LineWidth(3.5f);
			GL.Begin(BeginMode.Lines);
			for (int i = 0; i < polyMesh.NPolys; i++)
			{
				for (int j = 0; j < polyMesh.NumVertsPerPoly; j++)
				{
					if (polyMesh.Polys[i].Vertices[j] == PolyMesh.MESH_NULL_IDX)
						break;

					if ((polyMesh.Polys[i].ExtraInfo[j] & 0x8000) == 0)
						continue;

					int nj = (j + 1 >= polyMesh.NumVertsPerPoly || polyMesh.Polys[i].Vertices[j + 1] == PolyMesh.MESH_NULL_IDX) ? 0 : j + 1;

					int vertIndex0 = polyMesh.Polys[i].Vertices[j];
					int vertIndex1 = polyMesh.Polys[i].Vertices[nj];
					
					var v = polyMesh.Verts[vertIndex0];
					GL.Vertex3(v.X, v.Y + 1, v.Z);

					v = polyMesh.Verts[vertIndex1];
					GL.Vertex3(v.X, v.Y + 1, v.Z);
				}
			}

			GL.End();

			GL.PointSize(4.8f);
			GL.Begin(BeginMode.Points);
			for (int i = 0; i < polyMesh.NVerts; i++)
			{
				var v = polyMesh.Verts[i];
				GL.Vertex3(v.X, v.Y + 1, v.Z);
			}

			GL.End();
			
			GL.DepthMask(true);

			GL.PopMatrix();
		}

		private void DrawPolyMeshDetail()
		{
			GL.PushMatrix();

			Color4 color = Color4.DarkViolet;
			color.A = 0.5f;
			GL.Color4(color);

			GL.Begin(BeginMode.Triangles);
			for (int i = 0; i < polyMeshDetail.NMeshes; i++)
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

			GL.PopMatrix();
		}

		private void DrawPathfinding()
		{
			GL.PushMatrix();

			Color4 color = Color4.Cyan;

			GL.Begin(BeginMode.Triangles);
			for (int i = 0; i < pathCount; i++)
			{
				if (i == 0)
					color = Color4.Cyan;
				else if (i == pathCount - 1)
					color = Color4.PaleVioletRed;
				else
					color = Color4.LightYellow;
				GL.Color4(color);

				uint polyRef = path[i];
				PathfinderCommon.MeshTile tile = null;
				PathfinderCommon.Poly poly = null;
				tiledNavMesh.GetTileAndPolyByRefUnsafe(polyRef, ref tile, ref poly);
			
				for (int j = 2; j < poly.vertCount; j++)
				{
					int vertIndex0 = poly.verts[0];
					int vertIndex1 = poly.verts[j - 1];
					int vertIndex2 = poly.verts[j];

					var v = tile.verts[vertIndex0];
					GL.Vertex3(v.X, v.Y, v.Z);

					v = tile.verts[vertIndex1];
					GL.Vertex3(v.X, v.Y, v.Z);

					v = tile.verts[vertIndex2];
					GL.Vertex3(v.X, v.Y, v.Z);
				}
			}
			GL.End();

			GL.DepthMask(false);

			//neighbor edges
			GL.LineWidth(1.5f);
			GL.Begin(BeginMode.Lines);
			for (int i = 0; i < pathCount; i++)
			{
				if (i == 0)
					color = Color4.Blue;
				else if (i == pathCount - 1)
					color = Color4.Red;
				else
					color = Color4.Yellow;
				GL.Color4(color);

				uint polyRef = path[i];
				PathfinderCommon.MeshTile tile = null;
				PathfinderCommon.Poly poly = null;
				tiledNavMesh.GetTileAndPolyByRefUnsafe(polyRef, ref tile, ref poly);

				for (int j = 0; j < poly.vertCount; j++)
				{
					int vertIndex0 = poly.verts[j];
					int vertIndex1 = poly.verts[(j + 1) % poly.vertCount];

					var v = tile.verts[vertIndex0];
					GL.Vertex3(v.X, v.Y, v.Z);

					v = tile.verts[vertIndex1];
					GL.Vertex3(v.X, v.Y, v.Z);
				}
			}
			GL.End();
			GL.DepthMask(true);

			GL.PopMatrix();
		}
	}
}
