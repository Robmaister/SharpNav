// Copyright (c) 2013-2015 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
// Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE

using System;

using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;

using SharpNav;
using SharpNav.Geometry;
using SharpNav.Pathfinding;
using SharpNav.Crowds;
using System.Collections.Generic;

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
	public partial class ExampleWindow
	{
		private int levelVbo, levelNormVbo, heightfieldVoxelVbo, heightfieldVoxelIbo, squareVbo, squareIbo;
		private int levelNumInds;
		private bool levelHasNorm;

		private ObjModel level;
		private AgentCylinder agentCylinder;

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
			GL.Disable(EnableCap.Light0);
			GL.Disable(EnableCap.Lighting);

			agentCylinder = new AgentCylinder(12, 0.5f, 2f);
		}

		private void LoadLevel()
		{
			level = new ObjModel("nav_test.obj");
			var levelTris = level.GetTriangles();
			var levelNorms = level.GetNormals();
			levelNumInds = levelTris.Length * 3;
			levelHasNorm = levelNorms != null && levelNorms.Length > 0;

			var bounds = TriangleEnumerable.FromTriangle(levelTris, 0, levelTris.Length).GetBoundingBox();
			cam.Position = new Vector3(bounds.Max.X, bounds.Max.Y, bounds.Max.Z) * 1.5f;
			cam.RotateHeadingTo(-25);
			cam.RotatePitchTo(315);

			//TODO fix camera, it breaks with lookat...
			//cam.LookAt(new Vector3(bounds.Center.X, bounds.Center.Y, bounds.Center.Z));

			levelVbo = GL.GenBuffer();
			GL.BindBuffer(BufferTarget.ArrayBuffer, levelVbo);
			GL.BufferData(BufferTarget.ArrayBuffer, (IntPtr)(levelNumInds * 3 * 4), levelTris, BufferUsageHint.StaticDraw);
			GL.BindBuffer(BufferTarget.ArrayBuffer, 0);

			if (levelHasNorm)
			{
				levelNormVbo = GL.GenBuffer();
				GL.BindBuffer(BufferTarget.ArrayBuffer, levelNormVbo);
				GL.BufferData(BufferTarget.ArrayBuffer, (IntPtr)(levelNorms.Length * 3 * 4), levelNorms, BufferUsageHint.StaticDraw);
				GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
			}
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

			if (levelHasNorm)
				GL.EnableClientState(ArrayCap.NormalArray);

			GL.BindBuffer(BufferTarget.ArrayBuffer, levelVbo);
			GL.VertexPointer(3, VertexPointerType.Float, 0, 0);

			if (levelHasNorm)
			{
				GL.BindBuffer(BufferTarget.ArrayBuffer, levelNormVbo);
				GL.NormalPointer(NormalPointerType.Float, 0, 0);
			}

			GL.DrawArrays(BeginMode.Triangles, 0, levelNumInds);

			GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
			GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);

			if (levelHasNorm)
				GL.DisableClientState(ArrayCap.NormalArray);

			GL.DisableClientState(ArrayCap.VertexArray);
		}

		private void DrawHeightfield()
		{
			if (heightfield == null)
				return;

			GL.EnableClientState(ArrayCap.VertexArray);
			GL.EnableClientState(ArrayCap.NormalArray);

			GL.Enable(EnableCap.Lighting);
			GL.Enable(EnableCap.Light0);
			GL.Light(LightName.Light0, LightParameter.Position, new Vector4(0.5f, 1, 0.5f, 0));

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
			if (compactHeightfield == null)
				return;

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

						var squarePosFinal = new OpenTK.Vector3(squarePos.X, squarePos.Y, squarePos.Z);
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
			if (compactHeightfield == null)
				return;

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
						var squarePosFinal = new OpenTK.Vector3(squarePos.X, squarePos.Y, squarePos.Z);
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
			if (compactHeightfield == null)
				return;

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

						int region = span.Region.Id;
						//if (Region.IsBorder(region))
							//region = Region.RemoveFlags(region);

						Color4 col = regionColors[region];
						GL.Color4(col);

						var squarePosFinal = new OpenTK.Vector3(squarePos.X, squarePos.Y, squarePos.Z);
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

		private void DrawContours()
		{
			if (contourSet == null)
				return;

			GL.EnableClientState(ArrayCap.VertexArray);

			int maxdist = compactHeightfield.MaxDistance;
			var cellSize = heightfield.CellSize;
			var halfCellSize = cellSize * 0.5f;

			GL.PushMatrix();

			Matrix4 squareScale, squareTrans;

			Matrix4.CreateTranslation(heightfield.Bounds.Min.X + cellSize.X * compactHeightfield.BorderSize, heightfield.Bounds.Min.Y, heightfield.Bounds.Min.Z + cellSize.Z * compactHeightfield.BorderSize, out squareTrans);
			GL.MultMatrix(ref squareTrans);

			Matrix4.CreateScale(cellSize.X, cellSize.Y, cellSize.Z, out squareScale);
			GL.MultMatrix(ref squareScale);

			GL.LineWidth(5f);
			GL.Begin(BeginMode.Lines);
			
			foreach (var c in contourSet)
			{
				RegionId region = c.RegionId;

				//skip border regions
				if (RegionId.HasFlags(region, RegionFlags.Border))
					continue;

				Color4 col = regionColors[(int)region];
				GL.Color4(col);

				for (int i = 0; i < c.Vertices.Length; i++)
				{
					int ni = (i + 1) % c.Vertices.Length;
					GL.Vertex3(c.Vertices[i].X, c.Vertices[i].Y, c.Vertices[i].Z);
					GL.Vertex3(c.Vertices[ni].X, c.Vertices[ni].Y, c.Vertices[ni].Z);
				}
			}

			GL.End();

			GL.PopMatrix();

			GL.DisableClientState(ArrayCap.VertexArray);
		}

		private void DrawPolyMesh()
		{
			if (polyMesh == null)
				return;

			GL.PushMatrix();

			Matrix4 squareScale, squareTrans;

			Matrix4.CreateTranslation(polyMesh.Bounds.Min.X, polyMesh.Bounds.Min.Y, polyMesh.Bounds.Min.Z, out squareTrans);
			GL.MultMatrix(ref squareTrans);

			Matrix4.CreateScale(polyMesh.CellSize, polyMesh.CellHeight, polyMesh.CellSize, out squareScale);
			GL.MultMatrix(ref squareScale);

			Color4 color = Color4.DarkViolet;
			color.A = 0.5f;
			GL.Color4(color);

			GL.Begin(BeginMode.Triangles);

			for (int i = 0; i < polyMesh.PolyCount; i++)
			{
				if (!polyMesh.Polys[i].Area.IsWalkable)
					continue;

				for (int j = 2; j < polyMesh.NumVertsPerPoly; j++)
				{
					if (polyMesh.Polys[i].Vertices[j] == PolyMesh.NullId)
						break;

					int vertIndex0 = polyMesh.Polys[i].Vertices[0];
					int vertIndex1 = polyMesh.Polys[i].Vertices[j - 1];
					int vertIndex2 = polyMesh.Polys[i].Vertices[j];
					
					var v = polyMesh.Verts[vertIndex0];
					GL.Vertex3(v.X, v.Y, v.Z);

					v = polyMesh.Verts[vertIndex1];
					GL.Vertex3(v.X, v.Y, v.Z);

					v = polyMesh.Verts[vertIndex2];
					GL.Vertex3(v.X, v.Y, v.Z);
				}
			}

			GL.End();

			GL.DepthMask(false);

			//neighbor edges
			GL.Color4(Color4.Purple);

			GL.LineWidth(1.5f);
			GL.Begin(BeginMode.Lines);

			for (int i = 0; i < polyMesh.PolyCount; i++)
			{
				for (int j = 0; j < polyMesh.NumVertsPerPoly; j++)
				{
					if (polyMesh.Polys[i].Vertices[j] == PolyMesh.NullId)
						break;
					if (PolyMesh.IsBoundaryEdge(polyMesh.Polys[i].NeighborEdges[j]))
						continue;

					int nj = (j + 1 >= polyMesh.NumVertsPerPoly || polyMesh.Polys[i].Vertices[j + 1] == PolyMesh.NullId) ? 0 : j + 1;

					int vertIndex0 = polyMesh.Polys[i].Vertices[j];
					int vertIndex1 = polyMesh.Polys[i].Vertices[nj];
					
					var v = polyMesh.Verts[vertIndex0];
					GL.Vertex3(v.X, v.Y, v.Z);

					v = polyMesh.Verts[vertIndex1];
					GL.Vertex3(v.X, v.Y, v.Z);
				}
			}

			GL.End();

			//boundary edges
			GL.LineWidth(3.5f);
			GL.Begin(BeginMode.Lines);
			for (int i = 0; i < polyMesh.PolyCount; i++)
			{
				for (int j = 0; j < polyMesh.NumVertsPerPoly; j++)
				{
					if (polyMesh.Polys[i].Vertices[j] == PolyMesh.NullId)
						break;

					if (PolyMesh.IsInteriorEdge(polyMesh.Polys[i].NeighborEdges[j]))
						continue;

					int nj = (j + 1 >= polyMesh.NumVertsPerPoly || polyMesh.Polys[i].Vertices[j + 1] == PolyMesh.NullId) ? 0 : j + 1;

					int vertIndex0 = polyMesh.Polys[i].Vertices[j];
					int vertIndex1 = polyMesh.Polys[i].Vertices[nj];
					
					var v = polyMesh.Verts[vertIndex0];
					GL.Vertex3(v.X, v.Y, v.Z);

					v = polyMesh.Verts[vertIndex1];
					GL.Vertex3(v.X, v.Y, v.Z);
				}
			}

			GL.End();

			GL.PointSize(4.8f);
			GL.Begin(BeginMode.Points);

			for (int i = 0; i < polyMesh.VertCount; i++)
			{
				var v = polyMesh.Verts[i];
				GL.Vertex3(v.X, v.Y, v.Z);
			}

			GL.End();
			
			GL.DepthMask(true);

			GL.PopMatrix();
		}

		private void DrawPolyMeshDetail()
		{
			if (polyMeshDetail == null)
				return;

			GL.PushMatrix();

			Matrix4 transMatrix = Matrix4.CreateTranslation(0, -polyMesh.CellHeight, 0);
			GL.MultMatrix(ref transMatrix);

			Color4 color = Color4.DarkViolet;
			color.A = 0.5f;
			GL.Color4(color);

			GL.Begin(BeginMode.Triangles);
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
			GL.Begin(BeginMode.Lines);
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
			GL.Begin(BeginMode.Lines);
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
			GL.Begin(BeginMode.Points);
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

		private void DrawNavMesh()
		{
			if (tiledNavMesh == null)
				return;

			var tile = tiledNavMesh.GetTileAt(0, 0, 0);

			GL.PushMatrix();

			Color4 color = Color4.DarkViolet;
			color.A = 0.5f;
			GL.Color4(color);

			GL.Begin(BeginMode.Triangles);

			for (int i = 0; i < tile.Polys.Length; i++)
			{
				//if (!tile.Polys[i].Area.IsWalkable)
					//continue;

				for (int j = 2; j < PathfindingCommon.VERTS_PER_POLYGON; j++)
				{
					if (tile.Polys[i].Verts[j] == 0)
						break;

					int vertIndex0 = tile.Polys[i].Verts[0];
					int vertIndex1 = tile.Polys[i].Verts[j - 1];
					int vertIndex2 = tile.Polys[i].Verts[j];

					var v = tile.Verts[vertIndex0];
					GL.Vertex3(v.X, v.Y, v.Z);

					v = tile.Verts[vertIndex1];
					GL.Vertex3(v.X, v.Y, v.Z);

					v = tile.Verts[vertIndex2];
					GL.Vertex3(v.X, v.Y, v.Z);
				}
			}

			GL.End();

			GL.DepthMask(false);

			//neighbor edges
			GL.Color4(Color4.Purple);

			GL.LineWidth(1.5f);
			GL.Begin(BeginMode.Lines);

			for (int i = 0; i < tile.Polys.Length; i++)
			{
				for (int j = 0; j < PathfindingCommon.VERTS_PER_POLYGON; j++)
				{
					if (tile.Polys[i].Verts[j] == 0)
						break;
					if (PolyMesh.IsBoundaryEdge(tile.Polys[i].Neis[j]))
						continue;

					int nj = (j + 1 >= PathfindingCommon.VERTS_PER_POLYGON || tile.Polys[i].Verts[j + 1] == 0) ? 0 : j + 1;

					int vertIndex0 = tile.Polys[i].Verts[j];
					int vertIndex1 = tile.Polys[i].Verts[nj];

					var v = tile.Verts[vertIndex0];
					GL.Vertex3(v.X, v.Y, v.Z);

					v = tile.Verts[vertIndex1];
					GL.Vertex3(v.X, v.Y, v.Z);
				}
			}

			GL.End();

			//boundary edges
			GL.LineWidth(3.5f);
			GL.Begin(BeginMode.Lines);
			for (int i = 0; i < tile.Polys.Length; i++)
			{
				for (int j = 0; j < PathfindingCommon.VERTS_PER_POLYGON; j++)
				{
					if (tile.Polys[i].Verts[j] == 0)
						break;

					if (PolyMesh.IsInteriorEdge(tile.Polys[i].Neis[j]))
						continue;

					int nj = (j + 1 >= PathfindingCommon.VERTS_PER_POLYGON || tile.Polys[i].Verts[j + 1] == 0) ? 0 : j + 1;

					int vertIndex0 = tile.Polys[i].Verts[j];
					int vertIndex1 = tile.Polys[i].Verts[nj];

					var v = tile.Verts[vertIndex0];
					GL.Vertex3(v.X, v.Y, v.Z);

					v = tile.Verts[vertIndex1];
					GL.Vertex3(v.X, v.Y, v.Z);
				}
			}

			GL.End();

			GL.PointSize(4.8f);
			GL.Begin(BeginMode.Points);

			for (int i = 0; i < tile.Verts.Length; i++)
			{
				var v = tile.Verts[i];
				GL.Vertex3(v.X, v.Y, v.Z);
			}

			GL.End();

			GL.DepthMask(true);

			GL.PopMatrix();
		}

		private void DrawPathfinding()
		{
			if (path == null)
				return;

			GL.PushMatrix();

			Color4 color = Color4.Cyan;

			GL.Begin(BeginMode.Triangles);
			for (int i = 0; i < path.Count; i++)
			{
				if (i == 0)
					color = Color4.Cyan;
				else if (i == path.Count - 1)
					color = Color4.PaleVioletRed;
				else
					color = Color4.LightYellow;
				GL.Color4(color);

				NavPolyId polyRef = path[i];
				NavTile tile;
				NavPoly poly;
				tiledNavMesh.TryGetTileAndPolyByRefUnsafe(polyRef, out tile, out poly);

				for (int j = 2; j < poly.VertCount; j++)
				{
					int vertIndex0 = poly.Verts[0];
					int vertIndex1 = poly.Verts[j - 1];
					int vertIndex2 = poly.Verts[j];

					var v = tile.Verts[vertIndex0];
					GL.Vertex3(v.X, v.Y, v.Z);

					v = tile.Verts[vertIndex1];
					GL.Vertex3(v.X, v.Y, v.Z);

					v = tile.Verts[vertIndex2];
					GL.Vertex3(v.X, v.Y, v.Z);
				}
			}
			GL.End();

			GL.DepthMask(false);

			//neighbor edges
			GL.LineWidth(1.5f);
			GL.Begin(BeginMode.Lines);
			for (int i = 0; i < path.Count; i++)
			{
				if (i == 0)
					color = Color4.Blue;
				else if (i == path.Count - 1)
					color = Color4.Red;
				else
					color = Color4.Yellow;
				GL.Color4(color);

				NavPolyId polyRef = path[i];
				NavTile tile;
				NavPoly poly;
				tiledNavMesh.TryGetTileAndPolyByRefUnsafe(polyRef, out tile, out poly);

				for (int j = 0; j < poly.VertCount; j++)
				{
					int vertIndex0 = poly.Verts[j];
					int vertIndex1 = poly.Verts[(j + 1) % poly.VertCount];

					var v = tile.Verts[vertIndex0];
					GL.Vertex3(v.X, v.Y, v.Z);

					v = tile.Verts[vertIndex1];
					GL.Vertex3(v.X, v.Y, v.Z);
				}
			}
			GL.End();

			//steering path
			GL.Color4(Color4.Black);
			GL.Begin(BeginMode.Lines);
			for (int i = 0; i < smoothPath.Count - 1; i++)
			{
				SVector3 v0 = smoothPath[i];
				GL.Vertex3(v0.X, v0.Y, v0.Z);

				SVector3 v1 = smoothPath[i + 1];
				GL.Vertex3(v1.X, v1.Y, v1.Z);
			}
			GL.End();

			GL.DepthMask(true);

			GL.PopMatrix();
		}

		private void DrawCrowd()
		{
			if (crowd == null)
				return;

			GL.PushMatrix();

			//The black line represents the actual path that the agent takes
			/*GL.Color4(Color4.Black);
			GL.Begin(BeginMode.Lines);
			for (int i = 0; i < numActiveAgents; i++)
			{
				for (int j = 0; j < numIterations - 1; j++)
				{
					SVector3 v0 = trails[i].Trail[j];
					GL.Vertex3(v0.X, v0.Y, v0.Z);

					SVector3 v1 = trails[i].Trail[j + 1];
					GL.Vertex3(v1.X, v1.Y, v1.Z);
				}
			}
			GL.End();

			//The yellow line represents the ideal path from the start to the target
			GL.Color4(Color4.Yellow);
			GL.LineWidth(1.5f);
			GL.Begin(BeginMode.Lines);
			for (int i = 0; i < numActiveAgents; i++)
			{
				SVector3 v0 = trails[i].Trail[0];
				GL.Vertex3(v0.X, v0.Y, v0.Z);

				SVector3 v1 = trails[i].Trail[AGENT_MAX_TRAIL - 1];
				GL.Vertex3(v1.X, v1.Y, v1.Z);
			}
			GL.End();

			//The cyan point represents the agent's starting location
			GL.PointSize(100.0f);
			GL.Color4(Color4.Cyan);
			GL.Begin(BeginMode.Points);
			for (int i = 0; i < numActiveAgents; i++)
			{
				SVector3 v0 = trails[i].Trail[0];
				GL.Vertex3(v0.X, v0.Y, v0.Z);
			}
			GL.End();

			//The red point represent's the agent's target location
			GL.Color4(Color4.PaleVioletRed);
			GL.Begin(BeginMode.Points);
			for (int i = 0; i < numActiveAgents; i++)
			{
				SVector3 v0 = trails[i].Trail[AGENT_MAX_TRAIL - 1];
				GL.Vertex3(v0.X, v0.Y, v0.Z);
			}
			GL.End();*/
			//GL.DepthMask(true);

			GL.Color4(Color4.PaleVioletRed);
			GL.PointSize(10);
			GL.Begin(BeginMode.Points);
			GL.Color4(Color4.Blue);
			for (int i = 0; i < numActiveAgents; i++)
			{
				SVector3 p = crowd.GetAgent(i).TargetPosition;

				GL.Vertex3(p.X, p.Y, p.Z);
			}
			GL.End();

			if (agentCylinder != null)
			{
				for (int i = 0; i < numActiveAgents; i++)
				{
					SVector3 p = crowd.GetAgent(i).Position;
					agentCylinder.Draw(new Vector3(p.X, p.Y, p.Z));
				}
			}

			GL.PopMatrix();
		}
	}
}

#endif
