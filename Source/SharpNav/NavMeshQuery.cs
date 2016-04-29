// Copyright (c) 2013-2016 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
// Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE

using System;
using System.Collections.Generic;

using SharpNav.Collections.Generic;
using SharpNav.Geometry;
using SharpNav.Pathfinding;

#if MONOGAME
using Vector3 = Microsoft.Xna.Framework.Vector3;
#elif OPENTK
using Vector3 = OpenTK.Vector3;
#elif SHARPDX
using Vector3 = SharpDX.Vector3;
#endif

namespace SharpNav
{
	/// <summary>
	/// Do pathfinding calculations on the TiledNavMesh
	/// </summary>
	public class NavMeshQuery
	{
		private const float HeuristicScale = 0.999f;

		private TiledNavMesh nav;
		private NodePool tinyNodePool;
		private NodePool nodePool;
		private PriorityQueue<NavNode> openList;
		private QueryData query;
		private Random rand;

		/// <summary>
		/// Gets the mesh that this query is using for data.
		/// </summary>
		public TiledNavMesh NavMesh
		{
			get
			{
				return nav;
			}
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="NavMeshQuery"/> class.
		/// </summary>
		/// <param name="nav">The navigation mesh to query.</param>
		/// <param name="maxNodes">The maximum number of nodes that can be queued in a query.</param>
		public NavMeshQuery(TiledNavMesh nav, int maxNodes)
			: this(nav, maxNodes, new Random())
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="NavMeshQuery"/> class.
		/// </summary>
		/// <param name="nav">The navigation mesh to query.</param>
		/// <param name="maxNodes">The maximum number of nodes that can be queued in a query.</param>
		/// <param name="rand">A random number generator for use in methods like <see cref="NavMeshQuery.FindRandomPoint()"/></param>
		public NavMeshQuery(TiledNavMesh nav, int maxNodes, Random rand)
		{
			this.nav = nav;

			nodePool = new NodePool(maxNodes/*, MathHelper.NextPowerOfTwo(maxNodes / 4)*/);
			tinyNodePool = new NodePool(64/*, 32*/);
			openList = new PriorityQueue<NavNode>(maxNodes);

			this.rand = rand;

			this.query = new QueryData();
		}

		/// <summary>
		/// Finds a random point on a polygon.
		/// </summary>
		/// <param name="poly">Polygon to find a random point on.</param>
		/// <returns>Resulting random point</returns>
		public Vector3 FindRandomPointOnPoly(NavPolyId poly)
		{
			Vector3 result;
			this.FindRandomPointOnPoly(poly, out result);
			return result;
		}

		/// <summary>
		/// Finds a random point on a polygon.
		/// </summary>
		/// <param name="polyId">Polygon to find a radom point on.</param>
		/// <param name="randomPt">Resulting random point.</param>
		public void FindRandomPointOnPoly(NavPolyId polyId, out Vector3 randomPt)
		{
			NavTile tile;
			NavPoly poly;
			if (!nav.TryGetTileAndPolyByRef(polyId, out tile, out poly))
				throw new ArgumentException("Invalid polygon ID", "polyId");

			Vector3[] verts = new Vector3[poly.VertCount];
			for (int j = 0; j < poly.VertCount; j++)
				verts[j] = tile.Verts[poly.Verts[j]];

			float s = (float)rand.NextDouble();
			float t = (float)rand.NextDouble();

			PathfindingCommon.RandomPointInConvexPoly(verts, s, t, out randomPt);

			//TODO bad state again.
			float h = 0.0f;
			if (!GetPolyHeight(polyId, randomPt, ref h))
				throw new InvalidOperationException("Outside bounds?");

			randomPt.Y = h;
		}

		/// <summary>
		/// Finds a random point somewhere in the navigation mesh.
		/// </summary>
		/// <returns>Resulting random point.</returns>
		public NavPoint FindRandomPoint()
		{
			NavPoint result;
			this.FindRandomPoint(out result);
			return result;
		}

		/// <summary>
		/// Finds a random point somewhere in the navigation mesh.
		/// </summary>
		/// <param name="randomPoint">Resulting random point.</param>
		public void FindRandomPoint(out NavPoint randomPoint)
		{
			//TODO we're object-oriented, can prevent this state from ever happening.
			if (nav == null)
				throw new InvalidOperationException("TODO prevent this state from ever occuring");

			//randomly pick one tile
			//assume all tiles cover roughly the same area
			NavTile tile = null;
			float tsum = 0.0f;
			
			for (int i = 0; i < nav.TileCount; i++)
			{
				NavTile t = nav[i];
				
				if (t == null)
					continue;

				//choose random tile using reservoir sampling
				float area = 1.0f;
				tsum += area;
				float u = (float)rand.NextDouble();
				if (u * tsum <= area)
					tile = t;
			}

			//TODO why?
			if (tile == null)
				throw new InvalidOperationException("No tiles?");

			//randomly pick one polygon weighted by polygon area
			NavPolyId polyRef = NavPolyId.Null;
			NavPolyId polyBase = nav.GetTileRef(tile);

			float areaSum = 0.0f;
			for (int i = 0; i < tile.PolyCount; i++)
			{
				NavPoly p = tile.Polys[i];

				//don't return off-mesh connection polygons
				if (p.PolyType != NavPolyType.Ground)
					continue;

				NavPolyId reference;
				nav.IdManager.SetPolyIndex(ref polyBase, i, out reference);

				//calculate area of polygon
				float polyArea = 0.0f;
				float area;
				for (int j = 2; j < p.VertCount; j++)
				{
					Triangle3.Area2D(ref tile.Verts[p.Verts[0]], ref tile.Verts[p.Verts[j - 1]], ref tile.Verts[p.Verts[j]], out area);
					polyArea += area;
				}

				//choose random polygon weighted by area, usig resevoir sampling
				areaSum += polyArea;
				float u = (float)rand.NextDouble();
				if (u * areaSum <= polyArea)
				{
					polyRef = reference;
				}
			}

			//TODO why?
			if (polyRef == NavPolyId.Null)
				throw new InvalidOperationException("No polys?");

			Vector3 randomPt;
			FindRandomPointOnPoly(polyRef, out randomPt);

			randomPoint = new NavPoint(polyRef, randomPt);
		}

		/// <summary>
		/// Finds a random point in a NavMesh connected to a specified point on the same mesh.
		/// </summary>
		/// <param name="connectedTo">The point that the random point will be connected to.</param>
		/// <returns>A random point connected to <c>connectedTo</c>.</returns>
		public NavPoint FindRandomConnectedPoint(NavPoint connectedTo)
		{
			NavPoint result;
			FindRandomConnectedPoint(ref connectedTo, out result);
			return result;
		}

		/// <summary>
		/// Finds a random point in a NavMesh connected to a specified point on the same mesh.
		/// </summary>
		/// <param name="connectedTo">The point that the random point will be connected to.</param>
		/// <param name="randomPoint">A random point connected to <c>connectedTo</c>.</param>
		public void FindRandomConnectedPoint(ref NavPoint connectedTo, out NavPoint randomPoint)
		{
			FindRandomPointAroundCircle(ref connectedTo, 0, out randomPoint);
		}

		/// <summary>
		/// Finds a random point in a NavMesh within a specified circle.
		/// </summary>
		/// <param name="center">The center point.</param>
		/// <param name="radius">The maximum distance away from the center that the random point can be. If 0, any point on the mesh can be returned.</param>
		/// <returns>A random point within the specified circle.</returns>
		public NavPoint FindRandomPointAroundCircle(NavPoint center, float radius)
		{
			NavPoint result;
			this.FindRandomPointAroundCircle(ref center, radius, out result);
			return result;
		}

		/// <summary>
		/// Finds a random point in a NavMesh within a specified circle.
		/// </summary>
		/// <param name="center">The center point.</param>
		/// <param name="radius">The maximum distance away from the center that the random point can be. If 0, any connected point on the mesh can be returned.</param>
		/// <param name="randomPoint">A random point within the specified circle.</param>
		public void FindRandomPointAroundCircle(ref NavPoint center, float radius, out NavPoint randomPoint)
		{
			//TODO fix state
			if (nav == null || nodePool == null || openList == null)
				throw new InvalidOperationException("Something null");

			//validate input
			if (center.Polygon == NavPolyId.Null)
				throw new ArgumentOutOfRangeException("startRef", "Null poly reference");

			if (!nav.IsValidPolyRef(center.Polygon))
				throw new ArgumentException("startRef", "Poly reference is not valid for this navmesh");

			NavTile startTile;
			NavPoly startPoly;
			nav.TryGetTileAndPolyByRefUnsafe(center.Polygon, out startTile, out startPoly);

			nodePool.Clear();
			openList.Clear();

			NavNode startNode = nodePool.GetNode(center.Polygon);
			startNode.Position = center.Position;
			startNode.ParentIndex = 0;
			startNode.PolyCost = 0;
			startNode.TotalCost = 0;
			startNode.Id = center.Polygon;
			startNode.Flags = NodeFlags.Open;
			openList.Push(startNode);

			bool doRadiusCheck = radius != 0;

			float radiusSqr = radius * radius;
			float areaSum = 0.0f;

			NavPolyId randomPolyRef = NavPolyId.Null;

			while (openList.Count > 0)
			{
				NavNode bestNode = openList.Pop();
				SetNodeFlagClosed(ref bestNode);

				//get poly and tile
				NavPolyId bestRef = bestNode.Id;
				NavTile bestTile;
				NavPoly bestPoly;
				nav.TryGetTileAndPolyByRefUnsafe(bestRef, out bestTile, out bestPoly);

				//place random locations on ground
				if (bestPoly.PolyType == NavPolyType.Ground)
				{
					//calculate area of polygon
					float polyArea = 0.0f;
					float area;
					for (int j = 2; j < bestPoly.VertCount; j++)
					{
						Triangle3.Area2D(ref bestTile.Verts[bestPoly.Verts[0]], ref bestTile.Verts[bestPoly.Verts[j - 1]], ref bestTile.Verts[bestPoly.Verts[j]], out area);
						polyArea += area;
					}

					//choose random polygon weighted by area using resevoir sampling
					areaSum += polyArea;
					float u = (float)rand.NextDouble();
					if (u * areaSum <= polyArea)
					{
						randomPolyRef = bestRef;
					}
				}

				//get parent poly and tile
				NavPolyId parentRef = NavPolyId.Null;
				if (bestNode.ParentIndex != 0)
					parentRef = nodePool.GetNodeAtIdx(bestNode.ParentIndex).Id;

				foreach (Link link in bestPoly.Links)
				{
					NavPolyId neighborRef = link.Reference;

					//skip invalid neighbors and do not follow back to parent
					if (neighborRef == NavPolyId.Null || neighborRef == parentRef)
						continue;

					//expand to neighbor
					NavTile neighborTile;
					NavPoly neighborPoly;
					nav.TryGetTileAndPolyByRefUnsafe(neighborRef, out neighborTile, out neighborPoly);

					//find edge and calculate distance to edge
					Vector3 va = new Vector3();
					Vector3 vb = new Vector3();
					if (!GetPortalPoints(bestRef, bestPoly, bestTile, neighborRef, neighborPoly, neighborTile, ref va, ref vb))
						continue;

					//if circle isn't touching next polygon, skip it
					if (doRadiusCheck)
					{
						float tseg;
						float distSqr = Distance.PointToSegment2DSquared(ref center.Position, ref va, ref vb, out tseg);
						if (distSqr > radiusSqr)
							continue;
					}

					NavNode neighborNode = nodePool.GetNode(neighborRef);
					if (neighborNode == null)
						continue;

					if (IsInClosedList(neighborNode))
						continue;

					//cost
					if (neighborNode.Flags == 0)
						neighborNode.Position = Vector3.Lerp(va, vb, 0.5f);

					float total = bestNode.TotalCost + (bestNode.Position - neighborNode.Position).Length();

					//node is already in open list and new result is worse, so skip
					if (IsInOpenList(neighborNode) && total >= neighborNode.TotalCost)
						continue;

					neighborNode.Id = neighborRef;
					neighborNode.Flags = RemoveNodeFlagClosed(neighborNode);
					neighborNode.ParentIndex = nodePool.GetNodeIdx(bestNode);
					neighborNode.TotalCost = total;

					if (IsInOpenList(neighborNode))
					{
						openList.Modify(neighborNode);
					}
					else
					{
						neighborNode.Flags = NodeFlags.Open;
						openList.Push(neighborNode);
					}
				}
			}

			//TODO invalid state.
			if (randomPolyRef == NavPolyId.Null)
				throw new InvalidOperationException("Poly null?");

			Vector3 randomPt;
			FindRandomPointOnPoly(randomPolyRef, out randomPt);

			randomPoint = new NavPoint(randomPolyRef, randomPt);
		}

		/// <summary>
		/// Find a path from the start polygon to the end polygon.
		/// -If the end polygon can't be reached, the last polygon will be nearest the end polygon
		/// -If the path array is too small, it will be filled as far as possible 
		/// -start and end positions are used to calculate traversal costs
		/// </summary>
		/// <param name="startPt">The start point.</param>
		/// <param name="endPt">The end point.</param>
		/// <param name="filter">A filter for the navmesh data.</param>
		/// <param name="path">The path of polygon references</param>
		/// <returns>True, if path found. False, if otherwise.</returns>
		public bool FindPath(ref NavPoint startPt, ref NavPoint endPt, NavQueryFilter filter, Path path)
		{
			//reset path of polygons
			path.Clear();

			NavPolyId startRef = startPt.Polygon;
			Vector3 startPos = startPt.Position;
			NavPolyId endRef = endPt.Polygon;
			Vector3 endPos = endPt.Position;

			if (startRef == NavPolyId.Null || endRef == NavPolyId.Null)
				return false;

			//validate input
			if (!nav.IsValidPolyRef(startRef) || !nav.IsValidPolyRef(endRef))
				return false;

			//special case: both start and end are in the same polygon
			if (startRef == endRef)
			{
				path.Add(startRef);
				return true;
			}

			nodePool.Clear();
			openList.Clear();

			//initial node is located at the starting position
			NavNode startNode = nodePool.GetNode(startRef);
			startNode.Position = startPos;
			startNode.ParentIndex = 0;
			startNode.PolyCost = 0;
			startNode.TotalCost = (startPos - endPos).Length() * HeuristicScale;
			startNode.Id = startRef;
			startNode.Flags = NodeFlags.Open;
			openList.Push(startNode);

			NavNode lastBestNode = startNode;
			float lastBestTotalCost = startNode.TotalCost;

			while (openList.Count > 0)
			{
				//remove node from open list and put it in closed list
				NavNode bestNode = openList.Pop();
				SetNodeFlagClosed(ref bestNode);

				//reached the goal. stop searching
				if (bestNode.Id == endRef)
				{
					lastBestNode = bestNode;
					break;
				}

				//get current poly and tile
				NavPolyId bestRef = bestNode.Id;
				NavTile bestTile;
				NavPoly bestPoly;
				nav.TryGetTileAndPolyByRefUnsafe(bestRef, out bestTile, out bestPoly);

				//get parent poly and tile
				NavPolyId parentRef = NavPolyId.Null;
				NavTile parentTile = null;
				NavPoly parentPoly = null;
				if (bestNode.ParentIndex != 0)
					parentRef = nodePool.GetNodeAtIdx(bestNode.ParentIndex).Id;
				if (parentRef != NavPolyId.Null)
					nav.TryGetTileAndPolyByRefUnsafe(parentRef, out parentTile, out parentPoly);

				//examine neighbors
				foreach (Link link in bestPoly.Links)
				{
					NavPolyId neighborRef = link.Reference;

					//skip invalid ids and do not expand back to where we came from
					if (neighborRef == NavPolyId.Null || neighborRef == parentRef)
						continue;

					//get neighbor poly and tile
					NavTile neighborTile;
					NavPoly neighborPoly;
					nav.TryGetTileAndPolyByRefUnsafe(neighborRef, out neighborTile, out neighborPoly);

					NavNode neighborNode = nodePool.GetNode(neighborRef);
					if (neighborNode == null)
						continue;

					//if node is visited the first time, calculate node position
					if (neighborNode.Flags == 0)
					{
						GetEdgeMidPoint(bestRef, bestPoly, bestTile, neighborRef, neighborPoly, neighborTile, ref neighborNode.Position);
					}

					//calculate cost and heuristic
					float cost = 0;
					float heuristic = 0;

					//special case for last node
					if (neighborRef == endRef)
					{
						//cost
						float curCost = filter.GetCost(bestNode.Position, neighborNode.Position,
							parentRef, parentTile, parentPoly,
							bestRef, bestTile, bestPoly,
							neighborRef, neighborTile, neighborPoly);

						float endCost = filter.GetCost(neighborNode.Position, endPos,
							bestRef, bestTile, bestPoly,
							neighborRef, neighborTile, neighborPoly,
							NavPolyId.Null, null, null);

						cost = bestNode.PolyCost + curCost + endCost;
						heuristic = 0;
					}
					else
					{
						//cost
						float curCost = filter.GetCost(bestNode.Position, neighborNode.Position,
							parentRef, parentTile, parentPoly,
							bestRef, bestTile, bestPoly,
							neighborRef, neighborTile, neighborPoly);

						cost = bestNode.PolyCost + curCost;
						heuristic = (neighborNode.Position - endPos).Length() * HeuristicScale; 
					}

					float total = cost + heuristic;

					//the node is already in open list and new result is worse, skip
					if (IsInOpenList(neighborNode) && total >= neighborNode.TotalCost)
						continue;

					//the node is already visited and processesd, and the new result is worse, skip
					if (IsInClosedList(neighborNode) && total >= neighborNode.TotalCost)
						continue;

					//add or update the node
					neighborNode.ParentIndex = nodePool.GetNodeIdx(bestNode);
					neighborNode.Id = neighborRef;
					neighborNode.Flags = RemoveNodeFlagClosed(neighborNode);
					neighborNode.PolyCost = cost;
					neighborNode.TotalCost = total;

					if (IsInOpenList(neighborNode))
					{
						//already in open, update node location
						openList.Modify(neighborNode);
					}
					else
					{
						//put the node in the open list
						SetNodeFlagOpen(ref neighborNode);
						openList.Push(neighborNode);
					}

					//update nearest node to target so far
					if (heuristic < lastBestTotalCost)
					{
						lastBestTotalCost = heuristic;
						lastBestNode = neighborNode;
					}
				}
			}

			//save path
			NavNode node = lastBestNode;
			do
			{
				path.Add(node.Id);
				node = nodePool.GetNodeAtIdx(node.ParentIndex);
			}
			while (node != null);
			
			//reverse the path since it's backwards
			path.Reverse();

			return true;
		}

		/// <summary>
		/// Add vertices and portals to a regular path computed from the method FindPath().
		/// </summary>
		/// <param name="startPos">Starting position</param>
		/// <param name="endPos">Ending position</param>
		/// <param name="path">Path of polygon references</param>
		/// <param name="pathSize">Length of path</param>
		/// <param name="straightPath">An array of points on the straight path</param>
		/// <param name="straightPathFlags">An array of flags</param>
		/// <param name="straightPathRefs">An array of polygon references</param>
		/// <param name="straightPathCount">The number of points on the path</param>
		/// <param name="maxStraightPath">The maximum length allowed for the straight path</param>
		/// <param name="options">Options flag</param>
		/// <returns>True, if path found. False, if otherwise.</returns>
		public bool FindStraightPath(Vector3 startPos, Vector3 endPos, Path path, StraightPath straightPath, PathBuildFlags options)
		{
			straightPath.Clear();

			if (path.Count == 0)
				return false;

			bool stat = false;

			Vector3 closestStartPos = new Vector3();
			ClosestPointOnPolyBoundary(path[0], startPos, ref closestStartPos);

			Vector3 closestEndPos = new Vector3();
			ClosestPointOnPolyBoundary(path[path.Count - 1], endPos, ref closestEndPos);

			stat = straightPath.AppendVertex(new StraightPathVertex(new NavPoint(path[0], closestStartPos), StraightPathFlags.Start));

			if (!stat)
				return true;

			if (path.Count > 1)
			{
				Vector3 portalApex = closestStartPos;
				Vector3 portalLeft = portalApex;
				Vector3 portalRight = portalApex;
				int apexIndex = 0;
				int leftIndex = 0;
				int rightIndex = 0;

				NavPolyType leftPolyType = 0;
				NavPolyType rightPolyType = 0;

				NavPolyId leftPolyRef = path[0];
				NavPolyId rightPolyRef = path[0];

				for (int i = 0; i < path.Count; i++)
				{
					Vector3 left = new Vector3();
					Vector3 right = new Vector3();
					NavPolyType fromType = 0, toType = 0;

					if (i + 1 < path.Count)
					{
						//next portal
						if (GetPortalPoints(path[i], path[i + 1], ref left, ref right, ref fromType, ref toType) == false)
						{
							//failed to get portal points means path[i + 1] is an invalid polygon
							//clamp end point to path[i] and return path so far
							if (ClosestPointOnPolyBoundary(path[i], endPos, ref closestEndPos) == false)
							{
								//first polygon is invalid
								return false;
							}

							if ((options & (PathBuildFlags.AreaCrossingVertices | PathBuildFlags.AllCrossingVertices)) != 0)
							{
								//append portals
								stat = AppendPortals(apexIndex, i, closestEndPos, path, straightPath, options);
							}

							stat = straightPath.AppendVertex(new StraightPathVertex(new NavPoint(path[i], closestEndPos), StraightPathFlags.None));

							return true;
						}

						//if starting really close to the portal, advance
						if (i == 0)
						{
							float t;
							if (Distance.PointToSegment2DSquared(ref portalApex, ref left, ref right, out t) < 0.001 * 0.001)
								continue;
						}
					}
					else
					{
						//end of the path
						left = closestEndPos;
						right = closestEndPos;

						fromType = toType = NavPolyType.Ground;
					}

					//right vertex
					float triArea2D;
					Triangle3.Area2D(ref portalApex, ref portalRight, ref right, out triArea2D);
					if (triArea2D <= 0.0)
					{
						Triangle3.Area2D(ref portalApex, ref portalLeft, ref right, out triArea2D);
						if (portalApex == portalRight || triArea2D > 0.0)
						{
							portalRight = right;
							rightPolyRef = (i + 1 < path.Count) ? path[i + 1] : NavPolyId.Null;
							rightPolyType = toType;
							rightIndex = i;
						}
						else
						{
							//append portals along current straight path segment
							if ((options & (PathBuildFlags.AreaCrossingVertices | PathBuildFlags.AllCrossingVertices)) != 0)
							{
								stat = AppendPortals(apexIndex, leftIndex, portalLeft, path, straightPath, options);

								if (stat != true)
									return true;
							}

							portalApex = portalLeft;
							apexIndex = leftIndex;

							StraightPathFlags flags = 0;
							if (leftPolyRef == NavPolyId.Null)
								flags = StraightPathFlags.End;
							else if (leftPolyType == NavPolyType.OffMeshConnection)
								flags = StraightPathFlags.OffMeshConnection;

							NavPolyId reference = leftPolyRef;

							//append or update vertex
							stat = straightPath.AppendVertex(new StraightPathVertex(new NavPoint(reference, portalApex), flags));

							if (stat != true)
								return true;

							portalLeft = portalApex;
							portalRight = portalApex;
							leftIndex = apexIndex;
							rightIndex = apexIndex;

							//restart
							i = apexIndex;

							continue;
						}
					}

					//left vertex
					Triangle3.Area2D(ref portalApex, ref portalLeft, ref left, out triArea2D);
					if (triArea2D >= 0.0)
					{
						Triangle3.Area2D(ref portalApex, ref portalRight, ref left, out triArea2D);
						if (portalApex == portalLeft || triArea2D < 0.0f)
						{
							portalLeft = left;
							leftPolyRef = (i + 1 < path.Count) ? path[i + 1] : NavPolyId.Null;
							leftPolyType = toType;
							leftIndex = i;
						}
						else
						{
							if ((options & (PathBuildFlags.AreaCrossingVertices | PathBuildFlags.AllCrossingVertices)) != 0)
							{
								stat = AppendPortals(apexIndex, rightIndex, portalRight, path, straightPath, options);

								if (stat != true)
									return true;
							}

							portalApex = portalRight;
							apexIndex = rightIndex;

							StraightPathFlags flags = 0;
							if (rightPolyRef == NavPolyId.Null)
								flags = StraightPathFlags.End;
							else if (rightPolyType == NavPolyType.OffMeshConnection)
								flags = StraightPathFlags.OffMeshConnection;

							NavPolyId reference = rightPolyRef;

							//append or update vertex
							stat = straightPath.AppendVertex(new StraightPathVertex(new NavPoint(reference, portalApex), flags));

							if (stat != true)
								return true;

							portalLeft = portalApex;
							portalRight = portalApex;
							leftIndex = apexIndex;
							rightIndex = apexIndex;

							//restart 
							i = apexIndex;

							continue;
						}
					}
				}

				//append portals along the current straight line segment
				if ((options & (PathBuildFlags.AreaCrossingVertices | PathBuildFlags.AllCrossingVertices)) != 0)
				{
					stat = AppendPortals(apexIndex, path.Count - 1, closestEndPos, path, straightPath, options);

					if (stat != true)
						return true;
				}
			}

			stat = straightPath.AppendVertex(new StraightPathVertex(new NavPoint(NavPolyId.Null, closestEndPos), StraightPathFlags.End));

			return true;
		}

		/// <summary>
		/// This method is optimized for small delta movement and a small number of polygons.
		/// If movement distance is too large, the result will form an incomplete path.
		/// </summary>
		/// <param name="startPoint">The start point.</param>
		/// <param name="endPos">End position</param>
		/// <param name="resultPos">Intermediate point</param>
		/// <param name="visited">Visited polygon references</param>
		/// <returns>True, if point found. False, if otherwise.</returns>
		public bool MoveAlongSurface(ref NavPoint startPoint, ref Vector3 endPos, out Vector3 resultPos, List<NavPolyId> visited)
		{
			resultPos = Vector3.Zero;

			if (nav == null)
				return false;
			if (tinyNodePool == null)
				return false;

			visited.Clear();

			//validate input
			if (startPoint.Polygon == NavPolyId.Null)
				return false;
			if (!nav.IsValidPolyRef(startPoint.Polygon))
				return false;

			int MAX_STACK = 48;
			Queue<NavNode> nodeQueue = new Queue<NavNode>(MAX_STACK);

			tinyNodePool.Clear();

			NavNode startNode = tinyNodePool.GetNode(startPoint.Polygon);
			startNode.ParentIndex = 0;
			startNode.PolyCost = 0;
			startNode.TotalCost = 0;
			startNode.Id = startPoint.Polygon;
			startNode.Flags = NodeFlags.Closed;
			nodeQueue.Enqueue(startNode);

			Vector3 bestPos = startPoint.Position;
			float bestDist = float.MaxValue;
			NavNode bestNode = null;

			//search constraints
			Vector3 searchPos = Vector3.Lerp(startPoint.Position, endPos, 0.5f);
			float searchRad = (startPoint.Position - endPos).Length() / 2.0f + 0.001f;
			float searchRadSqr = searchRad * searchRad;

			Vector3[] verts = new Vector3[PathfindingCommon.VERTS_PER_POLYGON];
			
			while (nodeQueue.Count > 0)
			{
				//pop front
				NavNode curNode = nodeQueue.Dequeue();

				//get poly and tile
				NavPolyId curRef = curNode.Id;
				NavTile curTile;
				NavPoly curPoly;
				nav.TryGetTileAndPolyByRefUnsafe(curRef, out curTile, out curPoly);

				//collect vertices
				int nverts = curPoly.VertCount;
				for (int i = 0; i < nverts; i++)
					verts[i] = curTile.Verts[curPoly.Verts[i]];

				//if target is inside poly, stop search
				if (Containment.PointInPoly(endPos, verts, nverts))
				{
					bestNode = curNode;
					bestPos = endPos;
					break;
				}

				//find wall edges and find nearest point inside walls
				for (int i = 0, j = curPoly.VertCount - 1; i < curPoly.VertCount; j = i++)
				{
					//find links to neighbors
					List<NavPolyId> neis = new List<NavPolyId>(8);

					if ((curPoly.Neis[j] & Link.External) != 0)
					{
						//tile border
						foreach (Link link in curPoly.Links)
						{
							if (link.Edge == j)
							{
								if (link.Reference != NavPolyId.Null)
								{
									NavTile neiTile;
									NavPoly neiPoly;
									nav.TryGetTileAndPolyByRefUnsafe(link.Reference, out neiTile, out neiPoly);
									
									if (neis.Count < neis.Capacity)
										neis.Add(link.Reference);
								}
							}
						}
					}
					else if (curPoly.Neis[j] != 0)
					{
						int idx = curPoly.Neis[j] - 1;
						NavPolyId reference = nav.GetTileRef(curTile);
						nav.IdManager.SetPolyIndex(ref reference, idx, out reference);
						neis.Add(reference); //internal edge, encode id
					}

					if (neis.Count == 0)
					{
						//wall edge, calculate distance
						float tseg = 0;
						float distSqr = Distance.PointToSegment2DSquared(ref endPos, ref verts[j], ref verts[i], out tseg);
						if (distSqr < bestDist)
						{
							//update nearest distance
							bestPos = Vector3.Lerp(verts[j], verts[i], tseg);
							bestDist = distSqr;
							bestNode = curNode;
						}
					}
					else
					{
						for (int k = 0; k < neis.Count; k++)
						{
							//skip if no node can be allocated
							NavNode neighborNode = tinyNodePool.GetNode(neis[k]);
							if (neighborNode == null)
								continue;
							
							//skip if already visited
							if ((neighborNode.Flags & NodeFlags.Closed) != 0)
								continue;

							//skip the link if too far from search constraint
							float distSqr = Distance.PointToSegment2DSquared(ref searchPos, ref verts[j], ref verts[i]);
							if (distSqr > searchRadSqr)
								continue;

							//mark the node as visited and push to queue
							if (nodeQueue.Count < MAX_STACK)
							{
								neighborNode.ParentIndex = tinyNodePool.GetNodeIdx(curNode);
								neighborNode.Flags |= NodeFlags.Closed;
								nodeQueue.Enqueue(neighborNode);
							}
						}
					}
				}
			}

			if ((endPos - bestPos).Length() > 1f)
				return false;

			if (bestNode != null)
			{
				//save the path
				NavNode node = bestNode;
				do
				{
					visited.Add(node.Id);
					if (visited.Count >= visited.Capacity)
						break;

					node = tinyNodePool.GetNodeAtIdx(node.ParentIndex);
				}
				while (node != null);

				//reverse the path since it's backwards
				visited.Reverse();
			}

			resultPos = bestPos;
			
			return true;
		}

		/// <summary>
		/// Initialize a sliced path, which is used mostly for crowd pathfinding.
		/// </summary>
		/// <param name="startPoint">The start point.</param>
		/// <param name="endPoint">The end point.</param>
		/// <param name="filter">A filter for the navigation mesh.</param>
		/// <param name="options">Options for how the path should be found.</param>
		/// <returns>True if path initialized, false otherwise</returns>
		public bool InitSlicedFindPath(ref NavPoint startPoint, ref NavPoint endPoint, NavQueryFilter filter, FindPathOptions options)
		{
			//validate input
			if (startPoint.Polygon == NavPolyId.Null || endPoint.Polygon == NavPolyId.Null)
				return false;

			if (!nav.IsValidPolyRef(startPoint.Polygon) || !nav.IsValidPolyRef(endPoint.Polygon))
				return false;

			if (startPoint.Polygon == endPoint.Polygon)
			{
				query.Status = true;
				return true;
			}

			//init path state
			query = new QueryData();
			query.Status = false;
			query.Start = startPoint;
			query.End = endPoint;

			nodePool.Clear();
			openList.Clear();

			NavNode startNode = nodePool.GetNode(startPoint.Polygon);
			startNode.Position = startPoint.Position;
			startNode.ParentIndex = 0;
			startNode.PolyCost = 0;
			startNode.TotalCost = (endPoint.Position - startPoint.Position).Length() * HeuristicScale;
			startNode.Id = startPoint.Polygon;
			startNode.Flags = NodeFlags.Open;
			openList.Push(startNode);

			query.Status = true;
			query.LastBestNode = startNode;
			query.LastBestNodeCost = startNode.TotalCost;

			return query.Status;
		}

		/// <summary>
		/// Update the sliced path as agents move across the path.
		/// </summary>
		/// <param name="maxIter">Maximum iterations</param>
		/// <param name="doneIters">Number of times iterated through</param>
		/// <returns>True if updated, false if not</returns>
		public bool UpdateSlicedFindPath(int maxIter, ref int doneIters)
		{
			if (query.Status != true)
				return query.Status;

			//make sure the request is still valid
			if (!nav.IsValidPolyRef(query.Start.Polygon) || !nav.IsValidPolyRef(query.End.Polygon))
			{
				query.Status = false;
				return false;
			}

			int iter = 0;
			while (iter < maxIter && !openList.Empty())
			{
				iter++;

				//remove node from open list and put it in closed list
				NavNode bestNode = openList.Pop();
				SetNodeFlagClosed(ref bestNode);

				//reached the goal, stop searching
				if (bestNode.Id == query.End.Polygon)
				{
					query.LastBestNode = bestNode;
					query.Status = true;
					doneIters = iter;
					return query.Status;
				}

				//get current poly and tile
				NavPolyId bestRef = bestNode.Id;
				NavTile bestTile;
				NavPoly bestPoly;
				if (!nav.TryGetTileAndPolyByRef(bestRef, out bestTile, out bestPoly))
				{
					//the polygon has disappeared during the sliced query, fail
					query.Status = false;
					doneIters = iter;
					return query.Status;
				}

				//get parent poly and tile
				NavPolyId parentRef = NavPolyId.Null;
				NavPolyId grandpaRef = NavPolyId.Null;
				NavTile parentTile = null;
				NavPoly parentPoly = null;
				NavNode parentNode = null;
				if (bestNode.ParentIndex != 0)
				{
					parentNode = nodePool.GetNodeAtIdx(bestNode.ParentIndex);
					parentRef = parentNode.Id;
					if (parentNode.ParentIndex != 0)
						grandpaRef = nodePool.GetNodeAtIdx(parentNode.ParentIndex).Id;
				}
				if (parentRef != NavPolyId.Null)
				{
					bool invalidParent = !nav.TryGetTileAndPolyByRef(parentRef, out parentTile, out parentPoly);
					if (invalidParent || (grandpaRef != NavPolyId.Null && !nav.IsValidPolyRef(grandpaRef)))
					{
						//the polygon has disappeared during the sliced query, fail
						query.Status = false;
						doneIters = iter;
						return query.Status;
					}
				}

				//decide whether to test raycast to previous nodes
				bool tryLOS = false;
				if ((query.Options & FindPathOptions.AnyAngle) != 0)
				{
					if ((parentRef != NavPolyId.Null) && (parentNode.Position - bestNode.Position).LengthSquared() < query.RaycastLimitSquared)
						tryLOS = true;
				}

				foreach (Link link in bestPoly.Links)
				{
					NavPolyId neighborRef = link.Reference;

					//skip invalid ids and do not expand back to where we came from
					if (neighborRef == NavPolyId.Null || neighborRef == parentRef)
						continue;

					//get neighbor poly and tile
					NavTile neighborTile;
					NavPoly neighborPoly;
					nav.TryGetTileAndPolyByRefUnsafe(neighborRef, out neighborTile, out neighborPoly);

					if (!query.Filter.PassFilter(neighborRef, neighborTile, neighborPoly))
						continue;

					NavNode neighborNode = nodePool.GetNode(neighborRef);
					if (neighborNode == null)
						continue;

					if (neighborNode.ParentIndex != 0 && neighborNode.ParentIndex == bestNode.ParentIndex)
						continue;

					if (neighborNode.Flags == 0)
					{
						GetEdgeMidPoint(bestRef, bestPoly, bestTile, neighborRef, neighborPoly, neighborTile, ref neighborNode.Position);
					}

					//calculate cost and heuristic
					float cost = 0;
					float heuristic = 0;

					bool foundShortCut = false;
					RaycastHit hit;
					Path hitPath = new Path();
					if (tryLOS)
					{
						NavPoint startPoint = new NavPoint(parentRef, parentNode.Position);
						Raycast(ref startPoint, ref neighborNode.Position, grandpaRef, RaycastOptions.UseCosts, out hit, hitPath);
						foundShortCut = hit.T >= 1.0f;
					}

					if (foundShortCut)
					{
						cost = parentNode.PolyCost + hitPath.Cost;
					}
					else
					{
						float curCost = query.Filter.GetCost(bestNode.Position, neighborNode.Position,
							parentRef, parentTile, parentPoly,
							bestRef, bestTile, bestPoly,
							neighborRef, neighborTile, neighborPoly);

						cost = bestNode.PolyCost + curCost;
					}

					//special case for last node
					if (neighborRef == query.End.Polygon)
					{
						//cost
						float endCost = query.Filter.GetCost(bestNode.Position, neighborNode.Position,
							bestRef, bestTile, bestPoly,
							neighborRef, neighborTile, neighborPoly,
							NavPolyId.Null, null, null);

						cost = cost + endCost;
						heuristic = 0;
					}
					else
					{
						heuristic = (neighborNode.Position - query.End.Position).Length() * HeuristicScale;
					}

					float total = cost + heuristic;

					//the node is already in open list and new result is worse, skip
					if (IsInOpenList(neighborNode) && total >= neighborNode.TotalCost)
						continue;

					//the node is already visited and processesd, and the new result is worse, skip
					if (IsInClosedList(neighborNode) && total >= neighborNode.TotalCost)
						continue;

					//add or update the node
					neighborNode.ParentIndex = nodePool.GetNodeIdx(bestNode);
					neighborNode.Id = neighborRef;
					neighborNode.Flags = RemoveNodeFlagClosed(neighborNode);
					neighborNode.PolyCost = cost;
					neighborNode.TotalCost = total;
					if (foundShortCut)
						neighborNode.Flags |= NodeFlags.ParentDetached;

					if (IsInOpenList(neighborNode))
					{
						//already in open, update node location
						openList.Modify(neighborNode);
					}
					else
					{
						//put the node in the open list
						SetNodeFlagOpen(ref neighborNode);
						openList.Push(neighborNode);
					}

					//update nearest node to target so far
					if (heuristic < query.LastBestNodeCost)
					{
						query.LastBestNodeCost = heuristic;
						query.LastBestNode = neighborNode;
					}
				}
			}

			//exhausted all nodes, but could not find path
			if (openList.Empty())
			{
				query.Status = true;
			}

			doneIters = iter;

			return query.Status;
		}

		/// <summary>
		/// Save the sliced path 
		/// </summary>
		/// <param name="path">The path in terms of polygon references</param>
		/// <param name="pathCount">The path length</param>
		/// <param name="maxPath">The maximum path length allowed</param>
		/// <returns>True if the path is saved, false if not</returns>
		public bool FinalizeSlicedFindPath(Path path)
		{
			path.Clear();

			if (query.Status == false)
			{
				query = new QueryData();
				return false;
			}

			int n = 0;

			if (query.Start.Polygon == query.End.Polygon)
			{
				//special case: the search starts and ends at the same poly
				path.Add(query.Start.Polygon);
			}
			else
			{
				//reverse the path
				NavNode prev = null;
				NavNode node = query.LastBestNode;
				NodeFlags prevRay = 0;
				do
				{
					NavNode next = nodePool.GetNodeAtIdx(node.ParentIndex);
					node.ParentIndex = nodePool.GetNodeIdx(prev);
					prev = node;
					NodeFlags nextRay = node.Flags & NodeFlags.ParentDetached;
					node.Flags = (node.Flags & ~NodeFlags.ParentDetached) | prevRay;
					prevRay = nextRay;
					node = next;
				}
				while (node != null);

				//store path
				node = prev;
				do
				{
					NavNode next = nodePool.GetNodeAtIdx(node.ParentIndex);
					if ((node.Flags & NodeFlags.ParentDetached) != 0)
					{
						RaycastHit hit;
						Path m = new Path();
						NavPoint startPoint = new NavPoint(node.Id, node.Position);
						bool result = Raycast(ref startPoint, ref next.Position, RaycastOptions.None, out hit, m);
						path.AppendPath(m);

						if (path[path.Count - 1] == next.Id)
							path.RemoveAt(path.Count - 1);
					}
					else
					{
						path.Add(node.Id);
					}

					node = next;
				}
				while (node != null);
			}

			//reset query
			query = new QueryData();

			return true;
		}

		/// <summary>
		/// Save a partial path
		/// </summary>
		/// <param name="existing">Existing path</param>
		/// <param name="existingSize">Existing path's length</param>
		/// <param name="path">New path</param>
		/// <param name="pathCount">New path's length</param>
		/// <param name="maxPath">Maximum path length allowed</param>
		/// <returns>True if path saved, false if not</returns>
		public bool FinalizedSlicedPathPartial(Path existing, Path path)
		{
			path.Clear();

			if (existing.Count == 0)
			{
				return false;
			}

			if (query.Status == false)
			{
				query = new QueryData();
				return false;
			}

			int n = 0;

			if (query.Start.Polygon == query.End.Polygon)
			{
				//special case: the search starts and ends at the same poly
				path.Add(query.Start.Polygon);
			}
			else
			{
				//find furthest existing node that was visited
				NavNode prev = null;
				NavNode node = null;
				for (int i = existing.Count - 1; i >= 0; i--)
				{
					node = nodePool.FindNode(existing[i]);
					if (node != null)
						break;
				}

				if (node == null)
				{
					node = query.LastBestNode;
				}

				//reverse the path
				NodeFlags prevRay = 0;
				do
				{
					NavNode next = nodePool.GetNodeAtIdx(node.ParentIndex);
					node.ParentIndex = nodePool.GetNodeIdx(prev);
					prev = node;
					NodeFlags nextRay = node.Flags & NodeFlags.ParentDetached;
					node.Flags = (node.Flags & ~NodeFlags.ParentDetached) | prevRay;
					prevRay = nextRay;
					node = next;
				}
				while (node != null);

				//store path
				node = prev;
				do
				{
					NavNode next = nodePool.GetNodeAtIdx(node.ParentIndex);
					if ((node.Flags & NodeFlags.ParentDetached) != 0)
					{
						RaycastHit hit;
						Path m = new Path();
						NavPoint startPoint = new NavPoint(node.Id, node.Position);
						bool result = Raycast(ref startPoint, ref next.Position, RaycastOptions.None, out hit, m);
						path.AppendPath(m);

						if (path[path.Count - 1] == next.Id)
							path.RemoveAt(path.Count - 1);
					}
					else
					{
						path.Add(node.Id);
					}

					node = next;
				}
				while (node != null);
			}

			//reset query
			query = new QueryData();

			return true;
		}

		public bool Raycast(ref NavPoint startPoint, ref Vector3 endPos, RaycastOptions options, out RaycastHit hit, Path hitPath)
		{
			return Raycast(ref startPoint, ref endPos, NavPolyId.Null, options, out hit, hitPath);
		}

		public bool Raycast(ref NavPoint startPoint, ref Vector3 endPos, NavPolyId prevRef, RaycastOptions options, out RaycastHit hit, Path hitPath)
		{
			hit = new RaycastHit();

			if (hitPath != null)
				hitPath.Clear();

			//validate input
			if (startPoint.Polygon == NavPolyId.Null || !nav.IsValidPolyRef(startPoint.Polygon))
				return false;

			if (prevRef != NavPolyId.Null && !nav.IsValidPolyRef(prevRef))
				return false;

			Vector3[] verts = new Vector3[PathfindingCommon.VERTS_PER_POLYGON];

			NavTile prevTile, curTile, nextTile;
			NavPoly prevPoly, curPoly, nextPoly;

			NavPolyId curRef = startPoint.Polygon;

			nav.TryGetTileAndPolyByRefUnsafe(curRef, out curTile, out curPoly);
			nextTile = prevTile = curTile;
			nextPoly = prevPoly = curPoly;

			if (prevRef != NavPolyId.Null)
				nav.TryGetTileAndPolyByRefUnsafe(prevRef, out prevTile, out prevPoly);

			while (curRef != NavPolyId.Null)
			{
				//collect vertices
				int nv = 0;
				for (int i = 0; i < curPoly.VertCount; i++)
				{
					verts[nv] = curTile.Verts[curPoly.Verts[i]];
					nv++;
				}

				float tmin, tmax;
				int segMin, segMax;
				if (!Intersection.SegmentPoly2D(startPoint.Position, endPos, verts, nv, out tmin, out tmax, out segMin, out segMax))
				{
					//could not hit the polygon, keep the old t and report hit
					return true;
				}

				hit.EdgeIndex = segMax;

				//keep track of furthest t so far
				if (tmax > hit.T)
					hit.T = tmax;

				//store visited polygons
				if (hitPath != null)
					hitPath.Add(curRef);

				//ray end is completely inside the polygon
				if (segMax == -1)
				{
					hit.T = float.MaxValue;


					return true;
				}

				//follow neighbors
				NavPolyId nextRef = NavPolyId.Null;

				foreach (Link link in curPoly.Links)
				{
					//find link which contains the edge
					if (link.Edge != segMax)
						continue;

					//get pointer to the next polygon
					nav.TryGetTileAndPolyByRefUnsafe(link.Reference, out nextTile, out nextPoly);

					//skip off-mesh connection
					if (nextPoly.PolyType == NavPolyType.OffMeshConnection)
						continue;

					//TODO QueryFilter

					//if the link is internal, just return the ref
					if (link.Side == BoundarySide.Internal)
					{
						nextRef = link.Reference;
						break;
					}

					//if the link is at the tile boundary

					//check if the link spans the whole edge and accept
					if (link.BMin == 0 && link.BMax == 255)
					{
						nextRef = link.Reference;
						break;
					}

					//check for partial edge links
					int v0 = curPoly.Verts[link.Edge];
					int v1 = curPoly.Verts[(link.Edge + 1) % curPoly.VertCount];
					Vector3 left = curTile.Verts[v0];
					Vector3 right = curTile.Verts[v1];

					//check that the intersection lies inside the link portal
					if (link.Side == BoundarySide.PlusX || link.Side == BoundarySide.MinusX)
					{
						//calculate link size
						float s = 1.0f / 255.0f;
						float lmin = left.Z + (right.Z - left.Z) * (link.BMin * s);
						float lmax = left.Z + (right.Z - left.Z) * (link.BMax * s);
						if (lmin > lmax)
						{
							//swap
							float temp = lmin;
							lmin = lmax;
							lmax = temp;
						}

						//find z intersection
						float z = startPoint.Position.Z + (endPos.Z - startPoint.Position.Z) * tmax;
						if (z >= lmin && z <= lmax)
						{
							nextRef = link.Reference;
							break;
						}
					}
					else if (link.Side == BoundarySide.PlusZ || link.Side == BoundarySide.MinusZ)
					{
						//calculate link size
						float s = 1.0f / 255.0f;
						float lmin = left.X + (right.X - left.X) * (link.BMin * s);
						float lmax = left.X + (right.X - left.X) * (link.BMax * s);
						if (lmin > lmax)
						{
							//swap
							float temp = lmin;
							lmin = lmax;
							lmax = temp;
						}

						//find x intersection
						float x = startPoint.Position.X + (endPos.X - startPoint.Position.X) * tmax;
						if (x >= lmin && x <= lmax)
						{
							nextRef = link.Reference;
							break;
						}
					}
				}

				if ((options & RaycastOptions.UseCosts) != 0)
				{
					//TODO add cost
				}

				if (nextRef == NavPolyId.Null)
				{
					//no neighbor, we hit a wall

					//calculate hit normal
					int a = segMax;
					int b = (segMax + 1) < nv ? segMax + 1 : 0;
					Vector3 va = verts[a];
					Vector3 vb = verts[b];
					float dx = vb.X - va.X;
					float dz = vb.Z - va.Z;
					hit.Normal = new Vector3(dz, 0, dx);
					hit.Normal.Normalize();
					return true;
				}

				//no hit, advance to neighbor polygon
				prevRef = curRef;
				curRef = nextRef;
				prevTile = curTile;
				curTile = nextTile;
				prevPoly = curPoly;
				curPoly = nextPoly;
			}

			return true;
		}

		/// <summary>
		/// Store polygons that are within a certain range from the current polygon
		/// </summary>
		/// <param name="centerPoint">Starting position</param>
		/// <param name="radius">Range to search within</param>
		/// <param name="resultRef">All the polygons within range</param>
		/// <param name="resultParent">Polygon's parents</param>
		/// <param name="resultCount">Number of polygons stored</param>
		/// <param name="maxResult">Maximum number of polygons allowed</param>
		/// <returns>True, unless input is invalid</returns>
		public bool FindLocalNeighborhood(ref NavPoint centerPoint, float radius, NavPolyId[] resultRef, NavPolyId[] resultParent, ref int resultCount, int maxResult)
		{
			resultCount = 0;

			//validate input
			if (centerPoint.Polygon == NavPolyId.Null || !nav.IsValidPolyRef(centerPoint.Polygon))
				return false;

			int MAX_STACK = 48;
			NavNode[] stack = new NavNode[MAX_STACK];
			int nstack = 0;

			tinyNodePool.Clear();

			NavNode startNode = tinyNodePool.GetNode(centerPoint.Polygon);
			startNode.ParentIndex = 0;
			startNode.Id = centerPoint.Polygon;
			startNode.Flags = NodeFlags.Closed;
			stack[nstack++] = startNode;

			float radiusSqr = radius * radius;

			Vector3[] pa = new Vector3[PathfindingCommon.VERTS_PER_POLYGON];
			Vector3[] pb = new Vector3[PathfindingCommon.VERTS_PER_POLYGON];

			int n = 0;
			if (n < maxResult)
			{
				resultRef[n] = startNode.Id;
				resultParent[n] = NavPolyId.Null;
				++n;
			}

			while (nstack > 0)
			{
				//pop front
				NavNode curNode = stack[0];
				for (int i = 0; i < nstack - 1; i++)
					stack[i] = stack[i + 1];
				nstack--;

				//get poly and tile
				NavPolyId curRef = curNode.Id;
				NavTile curTile;
				NavPoly curPoly;
				nav.TryGetTileAndPolyByRefUnsafe(curRef, out curTile, out curPoly);

				foreach (Link link in curPoly.Links)
				{
					NavPolyId neighborRef = link.Reference;

					//skip invalid neighbors
					if (neighborRef == NavPolyId.Null)
						continue;

					//skip if cannot allocate more nodes
					NavNode neighborNode = tinyNodePool.GetNode(neighborRef);
					if (neighborNode == null)
						continue;

					//skip visited
					if ((neighborNode.Flags & NodeFlags.Closed) != 0)
						continue;

					//expand to neighbor
					NavTile neighborTile;
					NavPoly neighborPoly;
					nav.TryGetTileAndPolyByRefUnsafe(neighborRef, out neighborTile, out neighborPoly);

					//skip off-mesh connections
					if (neighborPoly.PolyType == NavPolyType.OffMeshConnection)
						continue;

					//find edge and calculate distance to edge
					Vector3 va = new Vector3();
					Vector3 vb = new Vector3();
					if (!GetPortalPoints(curRef, curPoly, curTile, neighborRef, neighborPoly, neighborTile, ref va, ref vb))
						continue;

					//if the circle is not touching the next polygon, skip it
					float tseg;
					float distSqr = Distance.PointToSegment2DSquared(ref centerPoint.Position, ref va, ref vb, out tseg);
					if (distSqr > radiusSqr)
						continue;

					//mark node visited
					neighborNode.Flags |= NodeFlags.Closed;
					neighborNode.ParentIndex = tinyNodePool.GetNodeIdx(curNode);

					//check that the polygon doesn't collide with existing polygons

					//collect vertices of the neighbor poly
					int npa = neighborPoly.VertCount;
					for (int k = 0; k < npa; k++)
						pa[k] = neighborTile.Verts[neighborPoly.Verts[k]];

					bool overlap = false;
					for (int j = 0; j < n; j++)
					{
						NavPolyId pastRef = resultRef[j];

						//connected polys do not overlap
						bool connected = false;
						foreach (Link link2 in curPoly.Links)
						{
							if (link2.Reference == pastRef)
							{
								connected = true;
								break;
							}
						}

						if (connected)
							continue;

						//potentially overlapping
						NavTile pastTile;
						NavPoly pastPoly;
						nav.TryGetTileAndPolyByRefUnsafe(pastRef, out pastTile, out pastPoly);

						//get vertices and test overlap
						int npb = pastPoly.VertCount;
						for (int k = 0; k < npb; k++)
							pb[k] = pastTile.Verts[pastPoly.Verts[k]];

						if (Intersection.PolyPoly2D(pa, npa, pb, npb))
						{
							overlap = true;
							break;
						}
					}

					if (overlap)
						continue;

					//store poly
					if (n < maxResult)
					{
						resultRef[n] = neighborRef;
						resultParent[n] = curRef;
						++n;
					}

					if (nstack < MAX_STACK)
					{
						stack[nstack++] = neighborNode;
					}
				}
			}

			resultCount = n;

			return true;
		}

		/// <summary>
		/// Collect all the edges from a polygon.
		/// </summary>
		/// <param name="reference">The polygon reference</param>
		/// <param name="segmentVerts">Segment vertices</param>
		/// <param name="segmentRefs">The polygon reference containing the segment</param>
		/// <param name="segmentCount">The number of segments stored</param>
		/// <param name="maxSegments">The maximum number of segments allowed</param>
		/// <returns>True, unless the polygon reference is invalid</returns>
		public bool GetPolyWallSegments(NavPolyId reference, Crowds.LocalBoundary.Segment[] segmentVerts, NavPolyId[] segmentRefs, ref int segmentCount, int maxSegments)
		{
			segmentCount = 0;

			NavTile tile;
			NavPoly poly;
			if (nav.TryGetTileAndPolyByRef(reference, out tile, out poly) == false)
				return false;

			int n = 0;
			int MAX_INTERVAL = 16;
			SegInterval[] ints = new SegInterval[MAX_INTERVAL];
			int nints;

			bool storePortals = segmentRefs.Length != 0;

			for (int i = 0, j = poly.VertCount - 1; i < poly.VertCount; j = i++)
			{
				//skip non-solid edges
				nints = 0;
				if ((poly.Neis[j] & Link.External) != 0)
				{
					//tile border
					foreach (Link link in poly.Links)
					{
						if (link.Edge == j)
						{
							if (link.Reference != NavPolyId.Null)
							{
								NavTile neiTile;
								NavPoly neiPoly;
								nav.TryGetTileAndPolyByRefUnsafe(link.Reference, out neiTile, out neiPoly);
								InsertInterval(ints, ref nints, MAX_INTERVAL, link.BMin, link.BMax, link.Reference);
							}
						}
					}
				}
				else
				{
					//internal edge
					NavPolyId neiRef = NavPolyId.Null;
					if (poly.Neis[j] != 0)
					{
						int idx = poly.Neis[j] - 1;
						NavPolyId id = nav.GetTileRef(tile);
						nav.IdManager.SetPolyIndex(ref id, idx, out neiRef);
					}

					//if the edge leads to another polygon and portals are not stored, skip
					if (neiRef != NavPolyId.Null && !storePortals)
						continue;

					if (n < maxSegments)
					{
						Vector3 vj = tile.Verts[poly.Verts[j]];
						Vector3 vi = tile.Verts[poly.Verts[i]];
						segmentVerts[n].Start = vj;
						segmentVerts[n].End = vi;
						segmentRefs[n] = neiRef;
						n++; //could be n += 2, since segments have 2 vertices
					}

					continue;
				}

				//add sentinels
				InsertInterval(ints, ref nints, MAX_INTERVAL, -1, 0, NavPolyId.Null);
				InsertInterval(ints, ref nints, MAX_INTERVAL, 255, 256, NavPolyId.Null);

				//store segments
				Vector3 vj2 = tile.Verts[poly.Verts[j]];
				Vector3 vi2 = tile.Verts[poly.Verts[i]];
				for (int k = 1; k < nints; k++)
				{
					//portal segment
					if (storePortals && ints[k].Reference != NavPolyId.Null)
					{
						float tmin = ints[k].TMin / 255.0f;
						float tmax = ints[k].TMax / 255.0f;
						if (n < maxSegments)
						{
							Vector3.Lerp(ref vj2, ref vi2, tmin, out segmentVerts[n].Start);
							Vector3.Lerp(ref vj2, ref vi2, tmax, out segmentVerts[n].End);
							segmentRefs[n] = ints[k].Reference;
							n++;
						}
					}

					//wall segment
					int imin = ints[k - 1].TMax;
					int imax = ints[k].TMin;
					if (imin != imax)
					{
						float tmin = imin / 255.0f;
						float tmax = imax / 255.0f;
						if (n < maxSegments)
						{
							Vector3.Lerp(ref vj2, ref vi2, tmin, out segmentVerts[n].Start);
							Vector3.Lerp(ref vj2, ref vi2, tmax, out segmentVerts[n].End);
							segmentRefs[n] = NavPolyId.Null;
							n++; 
						}
					}
				}
			}

			segmentCount = n;

			return true;
		}

		/// <summary>
		/// Insert a segment into the array
		/// </summary>
		/// <param name="ints">The array of segments</param>
		/// <param name="nints">The number of segments</param>
		/// <param name="maxInts">The maximium number of segments allowed</param>
		/// <param name="tmin">Parameter t minimum</param>
		/// <param name="tmax">Parameter t maximum</param>
		/// <param name="reference">Polygon reference</param>
		public void InsertInterval(SegInterval[] ints, ref int nints, int maxInts, int tmin, int tmax, NavPolyId reference)
		{
			if (nints + 1 > maxInts)
				return;

			//find insertion point
			int idx = 0;
			while (idx < nints)
			{
				if (tmax <= ints[idx].TMin)
					break;
				idx++;
			}

			//move current results
			if (nints - idx > 0)
			{
				for (int i = 0; i < nints - idx; i++)
					ints[idx + 1 + i] = ints[idx + i];
			}

			//store
			ints[idx].Reference = reference;
			ints[idx].TMin = tmin;
			ints[idx].TMax = tmax;
			nints++;
		}

		/// <summary>
		/// Get edge midpoint between two prolygons
		/// </summary>
		/// <param name="from">"From" polygon reference</param>
		/// <param name="fromPoly">"From" polygon data</param>
		/// <param name="fromTile">"From" mesh tile</param>
		/// <param name="to">"To" polygon reference</param>
		/// <param name="toPoly">"To" polygon data</param>
		/// <param name="toTile">"To" mesh tile</param>
		/// <param name="mid">Edge midpoint</param>
		/// <returns>True, if midpoint found. False, if otherwise.</returns>
		public bool GetEdgeMidPoint(NavPolyId from, NavPoly fromPoly, NavTile fromTile, NavPolyId to, NavPoly toPoly, NavTile toTile, ref Vector3 mid)
		{
			Vector3 left = new Vector3();
			Vector3 right = new Vector3();
			if (!GetPortalPoints(from, fromPoly, fromTile, to, toPoly, toTile, ref left, ref right))
				return false;

			mid = (left + right) * 0.5f;

			return true;
		}

		/// <summary>
		/// Find points on the left and right side.
		/// </summary>
		/// <param name="from">"From" polygon reference</param>
		/// <param name="to">"To" polygon reference</param>
		/// <param name="left">Point on the left side</param>
		/// <param name="right">Point on the right side</param>
		/// <param name="fromType">Polygon type of "From" polygon</param>
		/// <param name="toType">Polygon type of "To" polygon</param>
		/// <returns>True, if points found. False, if otherwise.</returns>
		public bool GetPortalPoints(NavPolyId from, NavPolyId to, ref Vector3 left, ref Vector3 right, ref NavPolyType fromType, ref NavPolyType toType)
		{
			NavTile fromTile;
			NavPoly fromPoly;
			if (nav.TryGetTileAndPolyByRef(from, out fromTile, out fromPoly) == false)
				return false;
			fromType = fromPoly.PolyType;

			NavTile toTile;
			NavPoly toPoly;
			if (nav.TryGetTileAndPolyByRef(to, out toTile, out toPoly) == false)
				return false;
			toType = toPoly.PolyType;

			return GetPortalPoints(from, fromPoly, fromTile, to, toPoly, toTile, ref left, ref right);
		}

		/// <summary>
		/// Find points on the left and right side.
		/// </summary>
		/// <param name="from">"From" polygon reference</param>
		/// <param name="fromPoly">"From" polygon data</param>
		/// <param name="fromTile">"From" mesh tile</param>
		/// <param name="to">"To" polygon reference</param>
		/// <param name="toPoly">"To" polygon data</param>
		/// <param name="toTile">"To" mesh tile</param>
		/// <param name="left">Resulting point on the left side</param>
		/// <param name="right">Resulting point on the right side</param>
		/// <returns>True, if points found. False, if otherwise.</returns>
		public bool GetPortalPoints(NavPolyId from, NavPoly fromPoly, NavTile fromTile, NavPolyId to, NavPoly toPoly, NavTile toTile, ref Vector3 left, ref Vector3 right)
		{
			//find the link that points to the 'to' polygon
			Link link = null;
			foreach (Link fromLink in fromPoly.Links)
			{
				if (fromLink.Reference == to)
				{
					link = fromLink;
					break;
				}
			}

			if (link == null)
				return false;

			//handle off-mesh connections
			if (fromPoly.PolyType == NavPolyType.OffMeshConnection)
			{
				//find link that points to first vertex
				foreach (Link fromLink in fromPoly.Links)
				{
					if (fromLink.Reference == to)
					{
						int v = fromLink.Edge;
						left = fromTile.Verts[fromPoly.Verts[v]];
						right = fromTile.Verts[fromPoly.Verts[v]];
						return true;
					}
				}

				return false;
			}

			if (toPoly.PolyType == NavPolyType.OffMeshConnection)
			{
				//find link that points to first vertex
				foreach (Link toLink in toPoly.Links)
				{
					if (toLink.Reference == from)
					{
						int v = toLink.Edge;
						left = toTile.Verts[toPoly.Verts[v]];
						right = toTile.Verts[toPoly.Verts[v]];
						return true;
					}
				}

				return false;
			}

			//find portal vertices
			int v0 = fromPoly.Verts[link.Edge];
			int v1 = fromPoly.Verts[(link.Edge + 1) % fromPoly.VertCount];
			left = fromTile.Verts[v0];
			right = fromTile.Verts[v1];

			//if the link is at the tile boundary, clamp the vertices to tile width
			if (link.Side != BoundarySide.Internal)
			{
				//unpack portal limits
				if (link.BMin != 0 || link.BMax != 255)
				{
					float s = 1.0f / 255.0f;
					float tmin = link.BMin * s;
					float tmax = link.BMax * s;
					left = Vector3.Lerp(fromTile.Verts[v0], fromTile.Verts[v1], tmin);
					right = Vector3.Lerp(fromTile.Verts[v0], fromTile.Verts[v1], tmax);
				}
			}

			return true;
		}

		/// <summary>
		/// Given a point on the polygon, find the closest point
		/// </summary>
		/// <param name="reference">Polygon reference</param>
		/// <param name="pos">Given point</param>
		/// <param name="closest">Resulting closest point</param>
		/// <returns>True, if point found. False, if otherwise.</returns>
		public bool ClosestPointOnPoly(NavPolyId reference, Vector3 pos, ref Vector3 closest)
		{
			if (nav == null)
				return false;

			NavTile tile;
			NavPoly poly;

			if (nav.TryGetTileAndPolyByRef(reference, out tile, out poly) == false)
				return false;

			if (tile == null)
				return false;

			tile.ClosestPointOnPoly(poly, pos, ref closest);
			return true;
		}

		/// <summary>
		/// Given a point on the polygon, find the closest point
		/// </summary>
		/// <param name="reference">Polygon reference</param>
		/// <param name="pos">Current position</param>
		/// <param name="closest">Resulting closest position</param>
		/// <param name="posOverPoly">Determines whether the position can be found on the polygon</param>
		/// <returns>True, if the closest point is found. False, if otherwise.</returns>
		public bool ClosestPointOnPoly(NavPolyId reference, Vector3 pos, out Vector3 closest, out bool posOverPoly)
		{
			posOverPoly = false;
			closest = Vector3.Zero;

			NavTile tile;
			NavPoly poly;
			if (!nav.TryGetTileAndPolyByRef(reference, out tile, out poly))
				return false;
			if (tile == null)
				return false;

			if (poly.PolyType == NavPolyType.OffMeshConnection)
			{
				Vector3 v0 = tile.Verts[poly.Verts[0]];
				Vector3 v1 = tile.Verts[poly.Verts[1]];
				float d0 = (pos - v0).Length();
				float d1 = (pos - v1).Length();
				float u = d0 / (d0 + d1);
				closest = Vector3.Lerp(v0, v1, u);
				return true;
			}

			int indexPoly = 0;
			for (int i = 0; i < tile.Polys.Length; i++)
			{
				if (tile.Polys[i] == poly)
				{
					indexPoly = i;
					break;
				}
			}

			PolyMeshDetail.MeshData pd = tile.DetailMeshes[indexPoly];

			//Clamp point to be inside the polygon
			Vector3[] verts = new Vector3[PathfindingCommon.VERTS_PER_POLYGON];
			float[] edgeDistance = new float[PathfindingCommon.VERTS_PER_POLYGON];
			float[] edgeT = new float[PathfindingCommon.VERTS_PER_POLYGON];
			int numPolyVerts = poly.VertCount;
			for (int i = 0; i < numPolyVerts; i++)
				verts[i] = tile.Verts[poly.Verts[i]];

			closest = pos;
			if (!Distance.PointToPolygonEdgeSquared(pos, verts, numPolyVerts, edgeDistance, edgeT))
			{
				//Point is outside the polygon
				//Clamp to nearest edge
				float minDistance = float.MaxValue;
				int minIndex = -1;
				for (int i = 0; i < numPolyVerts; i++)
				{
					if (edgeDistance[i] < minDistance)
					{
						minDistance = edgeDistance[i];
						minIndex = i;
					}
				}

				Vector3 va = verts[minIndex];
				Vector3 vb = verts[(minIndex + 1) % numPolyVerts];
				closest = Vector3.Lerp(va, vb, edgeT[minIndex]);
			}
			else
			{
				posOverPoly = false;
			}

			//find height at the location
			for (int j = 0; j < tile.DetailMeshes[indexPoly].TriangleCount; j++)
			{
				PolyMeshDetail.TriangleData t = tile.DetailTris[pd.TriangleIndex + j];
				Vector3 va, vb, vc;

				if (t.VertexHash0 < poly.VertCount)
					va = tile.Verts[poly.Verts[t.VertexHash0]];
				else
					va = tile.DetailVerts[pd.VertexIndex + (t.VertexHash0 - poly.VertCount)];

				if (t.VertexHash1 < poly.VertCount)
					vb = tile.Verts[poly.Verts[t.VertexHash1]];
				else
					vb = tile.DetailVerts[pd.VertexIndex + (t.VertexHash1 - poly.VertCount)];

				if (t.VertexHash2 < poly.VertCount)
					vc = tile.Verts[poly.Verts[t.VertexHash2]];
				else
					vc = tile.DetailVerts[pd.VertexIndex + (t.VertexHash2 - poly.VertCount)];

				float h;
				if (Distance.PointToTriangle(pos, va, vb, vc, out h))
				{
					closest.Y = h;
					break;
				}
			}

			return true;
		}

		/// <summary>
		/// Given a point on a polygon, find the closest point which lies on the polygon boundary.
		/// </summary>
		/// <param name="reference">Polygon reference</param>
		/// <param name="pos">Current position</param>
		/// <param name="closest">Resulting closest point</param>
		/// <returns>True, if the closest point is found. False, if otherwise.</returns>
		public bool ClosestPointOnPolyBoundary(NavPolyId reference, Vector3 pos, ref Vector3 closest)
		{
			NavTile tile;
			NavPoly poly;
			if (nav.TryGetTileAndPolyByRef(reference, out tile, out poly) == false)
				return false;

			tile.ClosestPointOnPolyBoundary(poly, pos, out closest);
			return true;
		}

		/// <summary>
		/// Update the vertices on the straight path
		/// </summary>
		/// <param name="startIdx">Original path's starting index</param>
		/// <param name="endIdx">Original path's end index</param>
		/// <param name="endPos">The end position</param>
		/// <param name="path">The original path of polygon references</param>
		/// <param name="straightPath">An array of points on the straight path</param>
		/// <param name="straightPathFlags">An array of flags</param>
		/// <param name="straightPathRefs">An array of polygon references</param>
		/// <param name="straightPathCount">The number of points on the path</param>
		/// <param name="maxStraightPath">The maximum length allowed for the straight path</param>
		/// <param name="options">Options flag</param>
		/// <returns></returns>
		public bool AppendPortals(int startIdx, int endIdx, Vector3 endPos, Path path, StraightPath straightPath, PathBuildFlags options)
		{
			Vector3 startPos = straightPath[straightPath.Count - 1].Point.Position;

			//append or update last vertex
			bool stat = false;
			for (int i = startIdx; i < endIdx; i++)
			{
				//calculate portal
				NavPolyId from = path[i];
				NavTile fromTile;
				NavPoly fromPoly;
				if (nav.TryGetTileAndPolyByRef(from, out fromTile, out fromPoly) == false)
					return false;

				NavPolyId to = path[i + 1];
				NavTile toTile;
				NavPoly toPoly;
				if (nav.TryGetTileAndPolyByRef(to, out toTile, out toPoly) == false)
					return false;

				Vector3 left = new Vector3();
				Vector3 right = new Vector3();
				if (GetPortalPoints(from, fromPoly, fromTile, to, toPoly, toTile, ref left, ref right) == false)
					break;

				if ((options & PathBuildFlags.AreaCrossingVertices) != 0)
				{
					//skip intersection if only area crossings are requested
					if (fromPoly.Area == toPoly.Area)
						continue;
				}

				//append intersection
				float s, t;
				if (Intersection.SegmentSegment2D(ref startPos, ref endPos, ref left, ref right, out s, out t))
				{
					Vector3 pt = Vector3.Lerp(left, right, t);

					stat = straightPath.AppendVertex(new StraightPathVertex(new NavPoint(path[i + 1], pt), StraightPathFlags.None));

					if (stat != true)
						return true;
				}
			}

			return true;
		}

		/// <summary>
		/// Return false if the provided position is outside the xz-bounds.
		/// </summary>
		/// <param name="reference">Polygon reference</param>
		/// <param name="pos">Current position</param>
		/// <param name="height">Resulting polygon height</param>
		/// <returns>True, if height found. False, if otherwise.</returns>
		public bool GetPolyHeight(NavPolyId reference, Vector3 pos, ref float height)
		{
			if (nav == null)
				return false;

			NavTile tile;
			NavPoly poly;
			if (!nav.TryGetTileAndPolyByRef(reference, out tile, out poly))
				return false;

			//off-mesh connections don't have detail polygons
			if (poly.PolyType == NavPolyType.OffMeshConnection)
			{
				Vector3 closest;
				tile.ClosestPointOnPolyOffMeshConnection(poly, pos, out closest);
				height = closest.Y;
				return true;
			}
			else
			{
				int indexPoly = 0;
				for (int i = 0; i < tile.Polys.Length; i++)
				{
					if (tile.Polys[i] == poly)
					{
						indexPoly = i;
						break;
					}
				}

				float h = 0;
				if (tile.ClosestHeight(indexPoly, pos, out h))
				{
					height = h;
					return true;
				}
			}

			return false;
		}

		/// <summary>
		/// Find the nearest poly within a certain range.
		/// </summary>
		/// <param name="center">Center.</param>
		/// <param name="extents">Extents.</param>
		/// <returns>The neareast point.</returns>
		public NavPoint FindNearestPoly(Vector3 center, Vector3 extents)
		{
			NavPoint result;
			this.FindNearestPoly(ref center, ref extents, out result);
			return result;
		}

		/// <summary>
		/// Find the nearest poly within a certain range.
		/// </summary>
		/// <param name="center">Center.</param>
		/// <param name="extents">Extents.</param>
		/// <param name="nearestPt">The neareast point.</param>
		public void FindNearestPoly(ref Vector3 center, ref Vector3 extents, out NavPoint nearestPt)
		{
			nearestPt = NavPoint.Null;

			//TODO error state?

			// Get nearby polygons from proximity grid.
			List<NavPolyId> polys = new List<NavPolyId>(128);
			if (!QueryPolygons(ref center, ref extents, polys))
				throw new InvalidOperationException("no nearby polys?");

			float nearestDistanceSqr = float.MaxValue;
			for (int i = 0; i < polys.Count; i++) 
			{
				NavPolyId reference = polys[i];
				Vector3 closestPtPoly;
				bool posOverPoly;
				ClosestPointOnPoly(reference, center, out closestPtPoly, out posOverPoly);

				// If a point is directly over a polygon and closer than
				// climb height, favor that instead of straight line nearest point.
				Vector3 diff = center - closestPtPoly;
				float d = 0;
				if (posOverPoly)
				{
					NavTile tile;
					NavPoly poly;
					nav.TryGetTileAndPolyByRefUnsafe(polys[i], out tile, out poly);
					d = Math.Abs(diff.Y) - tile.WalkableClimb;
					d = d > 0 ? d * d : 0;
				}
				else
				{
					d = diff.LengthSquared();
				}

				if (d < nearestDistanceSqr)
				{
					nearestDistanceSqr = d;
					nearestPt = new NavPoint(reference, closestPtPoly);
				}
			}
		}

		/// <summary>
		/// Finds nearby polygons within a certain range.
		/// </summary>
		/// <param name="center">The starting point</param>
		/// <param name="extent">The range to search within</param>
		/// <param name="polys">A list of polygons</param>
		/// <returns>True, if successful. False, if otherwise.</returns>
		public bool QueryPolygons(ref Vector3 center, ref Vector3 extent, List<NavPolyId> polys)
		{
			Vector3 bmin = center - extent;
			Vector3 bmax = center + extent;

			int minx, miny, maxx, maxy;
			nav.CalcTileLoc(ref bmin, out minx, out miny);
			nav.CalcTileLoc(ref bmax, out maxx, out maxy);

			BBox3 bounds = new BBox3(bmin, bmax);
			int n = 0;
			for (int y = miny; y <= maxy; y++)
			{
				for (int x = minx; x <= maxx; x++)
				{
					foreach (NavTile neighborTile in nav.GetTilesAt(x, y))
					{
						n += neighborTile.QueryPolygons(bounds, polys);
						if (n >= polys.Capacity) 
						{
							return true;
						}
					}
				}
			}

			return polys.Count != 0;
		}

		public bool IsValidPolyRef(NavPolyId reference)
		{
			NavTile tile;
			NavPoly poly;
			bool status = nav.TryGetTileAndPolyByRef(reference, out tile, out poly);
			if (status == false)
				return false;
			return true;
		}

		public bool IsInOpenList(NavNode node)
		{
			return (node.Flags & NodeFlags.Open) != 0;
		}

		public bool IsInClosedList(NavNode node)
		{
			return (node.Flags & NodeFlags.Closed) != 0;
		}

		public void SetNodeFlagOpen(ref NavNode node)
		{
			node.Flags |= NodeFlags.Open;
		}

		public void SetNodeFlagClosed(ref NavNode node)
		{
			node.Flags &= ~NodeFlags.Open;
			node.Flags |= NodeFlags.Closed;
		}

		public NodeFlags RemoveNodeFlagClosed(NavNode node)
		{
			return node.Flags & ~(NodeFlags.Closed | NodeFlags.ParentDetached);
		}

		private class QueryData
		{
			public bool Status;
			public NavNode LastBestNode;
			public float LastBestNodeCost;
			public NavPoint Start, End;
			public FindPathOptions Options;
			public float RaycastLimitSquared;
			public NavQueryFilter Filter;

			public QueryData()
			{
				Filter = new NavQueryFilter();
			}
		}

		public struct SegInterval
		{
			public NavPolyId Reference;
			public int TMin, TMax;
		}
	}
}
