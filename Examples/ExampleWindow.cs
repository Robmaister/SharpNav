#region License
/**
 * Copyright (c) 2013-2014 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
 * Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE
 */
#endregion

using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Collections.Generic;

using Gwen.Control;

using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;
using OpenTK.Input;

using SharpNav;
using SharpNav.Geometry;

using Key = OpenTK.Input.Key;

//Prevents name collision under the Standalone configuration
#if !OPENTK
using Vector3 = OpenTK.Vector3;
using SVector3 = SharpNav.Vector3;
#else
using SVector3 = OpenTK.Vector3;
#endif

namespace Examples
{
	public partial class ExampleWindow : GameWindow
	{
		private enum DisplayMode
		{
			None,
			Heightfield,
			CompactHeightfield,
			DistanceField,
			Regions,
			Contours,
			SimplifiedContours,
			PolyMesh,
			PolyMeshDetail,
			Pathfinding
		}

		private Camera cam;
		private float zoom = MathHelper.PiOver4;

		private Heightfield heightfield;
		private CompactHeightfield compactHeightfield;
		private Color4[] regionColors;
		private ContourSet contourSet;
		private PolyMesh polyMesh;
		private PolyMeshDetail polyMeshDetail;
		private NavMeshCreateParams parameters;
		private NavMeshBuilder buildData;
		private TiledNavMesh tiledNavMesh;
		private NavMeshQuery navMeshQuery;

		private SVector3 startPos;
		private SVector3 endPos;
		private List<int> path;
		private List<SVector3> smoothPath;

		private bool hasGenerated;
		private bool displayLevel;
		private DisplayMode displayMode;

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

			//Pre-JIT compile both SharpNav and this project for faster first-time runs.
			Assembly sharpNavAssebmly = Assembly.Load(Assembly.GetExecutingAssembly().GetReferencedAssemblies().First(n => n.Name == "SharpNav"));
			PreJITMethods(sharpNavAssebmly);
			PreJITMethods(Assembly.GetExecutingAssembly());
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

			if (displayLevel)
			{
				GL.Enable(EnableCap.Lighting);
				GL.Enable(EnableCap.Light0);
				GL.Light(LightName.Light0, LightParameter.Position, new Vector4(0f, 1, 0f, 0));

				DrawLevel();

				GL.Disable(EnableCap.Light0);
				GL.Disable(EnableCap.Lighting);
			}

			if (hasGenerated)
			{
				switch (displayMode)
				{
					case DisplayMode.Heightfield: 
						DrawHeightfield();
						break;
					case DisplayMode.CompactHeightfield:
						DrawCompactHeightfield();
						break;
					case DisplayMode.DistanceField:
						DrawDistanceField();
						break;
					case DisplayMode.Regions:
						DrawRegions();
						break;
					case DisplayMode.Contours:
						DrawContours(false);
						break;
					case DisplayMode.SimplifiedContours:
						DrawContours(true);
						break;
					case DisplayMode.PolyMesh:
						DrawPolyMesh();
						break;
					case DisplayMode.PolyMeshDetail:
						DrawPolyMeshDetail();
						break;
					case DisplayMode.Pathfinding:
						DrawPathfinding();
						break;
				}
			}

			DrawUI();

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

			UnloadLevel();
			UnloadDebugMeshes();

			base.OnUnload(e);
		}

		private void GenerateNavMesh()
		{
			Stopwatch sw = new Stopwatch();
			sw.Start();
			long prevMs = 0;
			try
			{
				int voxMaxHeight = (int)(settings.MaxHeight / settings.CellHeight);
				int voxMaxClimb = (int)(settings.MaxClimb / settings.CellHeight);
				int voxErodeRadius = (int)(settings.ErodeRadius / settings.CellSize);

				level.SetBoundingBoxOffset(new SVector3(settings.CellSize * 0.5f, settings.CellHeight * 0.5f, settings.CellSize * 0.5f));
				BBox3 bounds = level.GetBounds();

				heightfield = new Heightfield(bounds.Min, bounds.Max, settings.CellSize, settings.CellHeight);

				Console.WriteLine("Heightfield");
				Console.WriteLine(" + Ctor\t\t\t\t" + (sw.ElapsedMilliseconds - prevMs).ToString("D3") + " ms");
				prevMs = sw.ElapsedMilliseconds;

				AreaId[] areas = AreaIdGenerator.From(level.GetTriangles(), AreaId.Walkable)
					.MarkAboveHeight(settings.MaxLevelHeight, AreaId.Null)
					.MarkBelowHeight(settings.MinLevelHeight, AreaId.Null)
					.MarkAboveSlope(settings.MaxTriSlope, AreaId.Null)
					.ToArray();
				heightfield.RasterizeTrianglesWithAreas(level.GetTriangles(), areas);
				//heightfield.RasterizeTriangles(level.GetTriangles());

				Console.WriteLine(" + Rasterization\t\t" + (sw.ElapsedMilliseconds - prevMs).ToString("D3") + " ms");
				prevMs = sw.ElapsedMilliseconds;

				heightfield.FilterLedgeSpans(voxMaxHeight, voxMaxClimb);
				heightfield.FilterLowHangingWalkableObstacles(voxMaxClimb);
				heightfield.FilterWalkableLowHeightSpans(voxMaxHeight);

				Console.WriteLine(" + Filtering\t\t\t" + (sw.ElapsedMilliseconds - prevMs).ToString("D3") + " ms");
				prevMs = sw.ElapsedMilliseconds;

				compactHeightfield = new CompactHeightfield(heightfield, voxMaxHeight, voxMaxClimb);

				Console.WriteLine("CompactHeightfield");
				Console.WriteLine(" + Ctor\t\t\t\t" + (sw.ElapsedMilliseconds - prevMs).ToString("D3") + " ms");
				prevMs = sw.ElapsedMilliseconds;
				 
				compactHeightfield.Erode(voxErodeRadius);

				Console.WriteLine(" + Erosion\t\t\t" + (sw.ElapsedMilliseconds - prevMs).ToString("D3") + " ms");
				prevMs = sw.ElapsedMilliseconds;

				compactHeightfield.BuildDistanceField();

				Console.WriteLine(" + Distance Field\t" + (sw.ElapsedMilliseconds - prevMs).ToString("D3") + " ms");
				prevMs = sw.ElapsedMilliseconds;

				compactHeightfield.BuildRegions(2, settings.MinRegionSize, settings.MergedRegionSize);

				Console.WriteLine(" + Regions\t\t\t" + (sw.ElapsedMilliseconds - prevMs).ToString("D3") + " ms");
				prevMs = sw.ElapsedMilliseconds;

				Random r = new Random();
				regionColors = new Color4[compactHeightfield.MaxRegions];
				regionColors[0] = Color4.Black;
				for (int i = 1; i < regionColors.Length; i++)
					regionColors[i] = new Color4((byte)r.Next(0, 255), (byte)r.Next(0, 255), (byte)r.Next(0, 255), 255);

				Console.WriteLine(" + Colors\t\t\t\t" + (sw.ElapsedMilliseconds - prevMs).ToString("D3") + " ms");
				prevMs = sw.ElapsedMilliseconds;

				contourSet = new ContourSet(compactHeightfield, settings.MaxEdgeError, settings.MaxEdgeLength, 0);

				Console.WriteLine("ContourSet");
				Console.WriteLine(" + Ctor\t\t\t\t" + (sw.ElapsedMilliseconds - prevMs).ToString("D3") + " ms");
				prevMs = sw.ElapsedMilliseconds;

				polyMesh = new PolyMesh(contourSet, settings.VertsPerPoly);

				Console.WriteLine("PolyMesh");
				Console.WriteLine(" + Ctor\t\t\t\t" + (sw.ElapsedMilliseconds - prevMs).ToString("D3") + " ms");
				prevMs = sw.ElapsedMilliseconds;

				polyMeshDetail = new PolyMeshDetail(polyMesh, compactHeightfield, settings.SampleDistance, settings.MaxSmapleError);

				Console.WriteLine("PolyMeshDetail");
				Console.WriteLine(" + Ctor\t\t\t\t" + (sw.ElapsedMilliseconds - prevMs).ToString("D3") + " ms");
				prevMs = sw.ElapsedMilliseconds;

				hasGenerated = true;


			}
			catch (Exception e)
			{
				Console.WriteLine("Navmesh generation failed with exception:" + Environment.NewLine + e.ToString());
			}
			finally
			{
				sw.Stop();
			}

			if (hasGenerated)
			{
				GeneratePathfinding();

				Label l = (Label)statusBar.FindChildByName("GenTime");
				l.Text = "Generation Time: " + sw.ElapsedMilliseconds + "ms";

				Console.WriteLine("Navmesh generated successfully in " + sw.ElapsedMilliseconds + "ms.");
				Console.WriteLine("Rasterized " + level.GetTriangles().Length + " triangles.");
				Console.WriteLine("Generated " + contourSet.Count + " regions.");
				Console.WriteLine("PolyMesh contains " + polyMesh.VertCount + " vertices in " + polyMesh.PolyCount + " polys.");
				Console.WriteLine("PolyMeshDetail contains " + polyMeshDetail.VertCount + " vertices and " + polyMeshDetail.TrisCount + " tris in " + polyMeshDetail.MeshCount + " meshes.");
			}
		}

		private void GeneratePathfinding()
		{
			parameters = new NavMeshCreateParams();
			parameters.Verts = polyMesh.Verts;
			parameters.vertCount = polyMesh.VertCount;
			parameters.polys = polyMesh.Polys;
			parameters.polyCount = polyMesh.PolyCount;
			parameters.numVertsPerPoly = polyMesh.NumVertsPerPoly;
			parameters.detailMeshes = polyMeshDetail.Meshes;
			parameters.detailVerts = polyMeshDetail.Verts;
			parameters.detailVertsCount = polyMeshDetail.VertCount;
			parameters.detailTris = polyMeshDetail.Tris;
			parameters.detailTriCount = polyMeshDetail.TrisCount;
			//no support for offmesh connections
			parameters.offMeshConVerts = null;
			parameters.offMeshConRadii = null;
			parameters.offMeshConDir = null;
			parameters.offMeshConAreas = null;
			parameters.offMeshConFlags = null;
			parameters.offMeshConUserID = null;
			parameters.offMeshConCount = 0;
			parameters.walkableHeight = settings.MaxHeight;
			parameters.walkableRadius = settings.ErodeRadius;
			parameters.walkableClimb = settings.MaxClimb;
			parameters.bounds = polyMesh.Bounds;
			parameters.cellSize = polyMesh.CellSize;
			parameters.cellHeight = polyMesh.CellHeight;
			parameters.buildBvTree = true;

			buildData = new NavMeshBuilder(parameters);

			tiledNavMesh = new TiledNavMesh(buildData);
			navMeshQuery = new NavMeshQuery(tiledNavMesh, 2048);

			//Find random start and end points on the poly mesh
			/*int startRef;
			navMeshQuery.FindRandomPoint(out startRef, out startPos);*/

			int startRef;
			SVector3 c = new SVector3(10, 0, 0);
			SVector3 e = new SVector3(5, 5, 5);
			navMeshQuery.FindNearestPoly(ref c, ref e, out startRef, out startPos);

			int endRef;
			navMeshQuery.FindRandomPointAroundCircle(startRef, startPos, 1000, out endRef, out endPos);

			//calculate the overall path, which contains an array of polygon references
			int MAX_POLYS = 256;
			path = new List<int>(MAX_POLYS);
			navMeshQuery.FindPath(startRef, endRef, ref startPos, ref endPos, path);

			//find a smooth path over the mesh surface
			int npolys = path.Count;
			int[] polys = path.ToArray();
			SVector3 iterPos = new SVector3();
			SVector3 targetPos = new SVector3();
			navMeshQuery.ClosestPointOnPoly(startRef, startPos, ref iterPos);
			navMeshQuery.ClosestPointOnPoly(polys[npolys - 1], endPos, ref targetPos);

			smoothPath = new List<SVector3>(2048);
			smoothPath.Add(iterPos);

			float STEP_SIZE = 0.5f;
			float SLOP = 0.01f;
			while (npolys > 0 && smoothPath.Count < smoothPath.Capacity)
			{
				//find location to steer towards
				SVector3 steerPos = new SVector3();
				int steerPosFlag = 0;
				int steerPosRef = 0;

				if (!GetSteerTarget(navMeshQuery, iterPos, targetPos, SLOP, polys, npolys, ref steerPos, ref steerPosFlag, ref steerPosRef))
					break;

				bool endOfPath = (steerPosFlag & PathfinderCommon.STRAIGHTPATH_END) != 0 ? true : false;
				bool offMeshConnection = (steerPosFlag & PathfinderCommon.STRAIGHTPATH_OFFMESH_CONNECTION) != 0 ? true : false;

				//find movement delta
				SVector3 delta = steerPos - iterPos;
				float len = (float)Math.Sqrt(SVector3.Dot(delta, delta));

				//if steer target is at end of path or off-mesh link
				//don't move past location
				if ((endOfPath || offMeshConnection) && len < STEP_SIZE)
					len = 1;
				else
					len = STEP_SIZE / len;

				SVector3 moveTgt = new SVector3();
				VMad(ref moveTgt, iterPos, delta, len);

				//move
				SVector3 result = new SVector3();
				List<int> visited = new List<int>(16);
				navMeshQuery.MoveAlongSurface(polys[0], iterPos, moveTgt, ref result, visited);
				npolys = FixupCorridor(polys, npolys, MAX_POLYS, visited);
				float h = 0;
				navMeshQuery.GetPolyHeight(polys[0], result, ref h);
				result.Y = h;
				iterPos = result;

				//handle end of path when close enough
				if (endOfPath && InRange(iterPos, steerPos, SLOP, 1.0f))
				{
					//reached end of path
					iterPos = targetPos;
					if (smoothPath.Count < smoothPath.Capacity)
					{
						smoothPath.Add(iterPos);
					}
					break;
				}

				//store results
				if (smoothPath.Count < smoothPath.Capacity)
				{
					smoothPath.Add(iterPos);
				}
			}
		}

		/// <summary>
		/// Scaled vector addition
		/// </summary>
		/// <param name="dest">Result</param>
		/// <param name="v1">Vector 1</param>
		/// <param name="v2">Vector 2</param>
		/// <param name="s">Scalar</param>
		private void VMad(ref SVector3 dest, SVector3 v1, SVector3 v2, float s)
		{
			dest.X = v1.X + v2.X * s;
			dest.Y = v1.Y + v2.Y * s;
			dest.Z = v1.Z + v2.Z * s;
		}

		private bool GetSteerTarget(NavMeshQuery navMeshQuery, SVector3 startPos, SVector3 endPos, float minTargetDist, int[] path, int pathSize,
			ref SVector3 steerPos, ref int steerPosFlag, ref int steerPosRef)
		{
			int MAX_STEER_POINTS = 3;
			SVector3[] steerPath = new SVector3[MAX_STEER_POINTS];
			int[] steerPathFlags = new int[MAX_STEER_POINTS];
			int[] steerPathPolys = new int[MAX_STEER_POINTS];
			int nsteerPath = 0;
			navMeshQuery.FindStraightPath(startPos, endPos, path, pathSize,
				steerPath, steerPathFlags, steerPathPolys, ref nsteerPath, MAX_STEER_POINTS, 0);

			if (nsteerPath == 0)
				return false;

			//find vertex far enough to steer to
			int ns = 0;
			while (ns < nsteerPath)
			{
				if ((steerPathFlags[ns] & PathfinderCommon.STRAIGHTPATH_OFFMESH_CONNECTION) != 0 ||
					!InRange(steerPath[ns], startPos, minTargetDist, 1000.0f))
					break;

				ns++;
			}

			//failed to find good point to steer to
			if (ns >= nsteerPath)
				return false;

			steerPos = steerPath[ns];
			steerPos.Y = startPos.Y;
			steerPosFlag = steerPathFlags[ns];
			steerPosRef = steerPathPolys[ns];

			return true;
		}

		private bool InRange(SVector3 v1, SVector3 v2, float r, float h)
		{
			float dx = v2.X - v1.X;
			float dy = v2.Y - v1.Y;
			float dz = v2.Z - v1.Z;
			return (dx * dx + dz * dz) < (r * r) && Math.Abs(dy) < h;
		}

		private int FixupCorridor(int[] path, int npath, int maxPath, List<int> visited)
		{
			int furthestPath = -1;
			int furthestVisited = -1;

			//find furhtest common polygon
			for (int i = npath - 1; i >= 0; i--)
			{
				bool found = false;
				for (int j = visited.Count - 1; j >= 0; j--)
				{
					if (path[i] == visited[j])
					{
						furthestPath = i;
						furthestVisited = j;
						found = true;
					}
				}

				if (found)
					break;
			}

			//if no intersection found, return current path
			if (furthestPath == -1 || furthestVisited == -1)
				return npath;

			//concatenate paths
			//adjust beginning of the buffer to include the visited
			int req = visited.Count - furthestVisited;
			int orig = Math.Min(furthestPath + 1, npath);
			int size = Math.Max(0, npath - orig);
			if (req + size > maxPath)
				size = maxPath - req;
			for (int i = 0; i < size; i++)
				path[req + i] = path[orig + i];

			//store visited
			for (int i = 0; i < req; i++)
				path[i] = visited[(visited.Count - 1) - i];

			return req + size;
		}

		/// <summary>
		/// Pre-JIT compiles an assembly.
		/// </summary>
		/// <remarks>
		/// Taken from http://blog.liranchen.com/2010/08/forcing-jit-compilation-during-runtime.html
		/// </remarks>
		/// <param name="assembly">The assembly to JIT.</param>
		private static void PreJITMethods(Assembly assembly)
		{
			Type[] types = assembly.GetTypes();
			foreach (Type curType in types)
			{
				MethodInfo[] methods = curType.GetMethods(
						BindingFlags.DeclaredOnly |
						BindingFlags.NonPublic |
						BindingFlags.Public |
						BindingFlags.Instance |
						BindingFlags.Static);

				foreach (MethodInfo curMethod in methods)
				{
					if (curMethod.IsAbstract ||
						curMethod.ContainsGenericParameters)
						continue;

					RuntimeHelpers.PrepareMethod(curMethod.MethodHandle);
				}
			}
		}
	}
}
