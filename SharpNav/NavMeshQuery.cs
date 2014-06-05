﻿#region License
/**
 * Copyright (c) 2013-2014 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
 * Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE
 */
#endregion

using System;
using System.Collections.Generic;

using SharpNav.Collections.Generic;
using SharpNav.Geometry;
using SharpNav.Pathfinding;

#if MONOGAME || XNA
using Microsoft.Xna.Framework;
#elif OPENTK
using OpenTK;
#elif SHARPDX
using SharpDX;
#elif UNITY3D
using UnityEngine;
#endif

namespace SharpNav
{
	/// <summary>
	/// Do pathfinding calculations on the TiledNavMesh
	/// </summary>
	public class NavMeshQuery
	{
		private const float H_SCALE = 0.999f;

		private TiledNavMesh nav;
		private float[] areaCost; 
		private NodePool tinyNodePool;
		private NodePool nodePool;
		private PriorityQueue<Node> openList;
		private QueryData query;

		public NavMeshQuery(TiledNavMesh nav, int maxNodes)
		{
			this.nav = nav;

			areaCost = new float[byte.MaxValue + 1];
			for (int i = 0; i < areaCost.Length; i++)
				areaCost[i] = 1.0f;

			nodePool = new NodePool(maxNodes, MathHelper.NextPowerOfTwo(maxNodes / 4));
			tinyNodePool = new NodePool(64, 32);
			openList = new PriorityQueue<Node>(maxNodes);
		}

		/// <summary>
		/// The cost between two points may vary depending on the type of polygon.
		/// </summary>
		/// <param name="pa">Point A</param>
		/// <param name="pb">Point B</param>
		/// <param name="curPoly">Current polygon</param>
		/// <returns>Cost</returns>
		public float GetCost(Vector3 pa, Vector3 pb, Poly curPoly)
		{
			return (pa - pb).Length() * areaCost[(int)curPoly.Area];
		}

		public TiledNavMesh Nav
		{
			get
			{
				return nav;
			}
		}

		/// <summary>
		/// Find a random point on a polygon. Use the overload with a <c>Random</c> parameter if calling multiple times in a row.
		/// </summary>
		/// <param name="tile">The current mesh tile</param>
		/// <param name="poly">The current polygon</param>
		/// <param name="polyRef">Polygon reference</param>
		/// <param name="randomPt">Resulting random point</param>
		/// <returns>True, if point found. False, if otherwise</returns>
		public bool FindRandomPointOnPoly(MeshTile tile, Poly poly, int polyRef, out Vector3 randomPt)
		{
			return FindRandomPointOnPoly(tile, poly, polyRef, new System.Random(), out randomPt);
		}

		/// <summary>
		/// Find a random point on a polygon.
		/// </summary>
		/// <param name="tile">The current mesh tile</param>
		/// <param name="poly">The current polygon</param>
		/// <param name="polyRef">Polygon reference</param>
		/// <param name="randomPt">Resulting random point</param>
		/// <returns>True, if point found. False, if otherwise</returns>
		public bool FindRandomPointOnPoly(MeshTile tile, Poly poly, int polyRef, System.Random rand, out Vector3 randomPt)
		{
			Vector3[] verts = new Vector3[PathfinderCommon.VERTS_PER_POLYGON];
			float[] areas = new float[PathfinderCommon.VERTS_PER_POLYGON];
			for (int j = 0; j < poly.VertCount; j++)
				verts[j] = tile.Verts[poly.Verts[j]];

			float s = (float)rand.NextDouble();
			float t = (float)rand.NextDouble();

			PathfinderCommon.RandomPointInConvexPoly(verts, poly.VertCount, areas, s, t, out randomPt);

			float h = 0.0f;
			if (!GetPolyHeight(polyRef, randomPt, ref h))
				return false;

			randomPt.Y = h;
			return true;
		}

		/// <summary>
		/// Find a random point. Use the overload with a <c>Random</c> parameter if calling multiple times in a row.
		/// </summary>
		/// <param name="randomRef">Resulting polygon reference containing random point</param>
		/// <param name="randomPt">Resulting random point</param>
		/// <returns>True, if point found. False, if otherwise.</returns>
		public bool FindRandomPoint(out int randomRef, out Vector3 randomPt)
		{
			return FindRandomPoint(new System.Random(), out randomRef, out randomPt);
		}

		/// <summary>
		/// Find a random point.
		/// </summary>
		/// <param name="randomRef">Resulting polygon reference containing random point</param>
		/// <param name="randomPt">Resulting random point</param>
		/// <returns>True, if point found. False, if otherwise.</returns>
		public bool FindRandomPoint(System.Random rand, out int randomRef, out Vector3 randomPt)
		{
			randomRef = 0;
			randomPt = Vector3.Zero;

			if (nav == null)
				return false;

			//randomly pick one tile
			//assume all tiles cover roughly the same area
			MeshTile tile = null;
			float tsum = 0.0f;
			
			for (int i = 0; i < nav.TileCount; i++)
			{
				MeshTile t = nav[i];
				
				if (t == null || t.Header == null)
					continue;

				//choose random tile using reservoir sampling
				float area = 1.0f;
				tsum += area;
				float u = (float)rand.NextDouble();
				if (u * tsum <= area)
					tile = t;
			}

			if (tile == null)
				return false;

			//randomly pick one polygon weighted by polygon area
			Poly poly = null;
			int polyRef = 0;
			int polyBase = nav.GetPolyRefBase(tile);

			float areaSum = 0.0f;
			for (int i = 0; i < tile.Header.polyCount; i++)
			{
				Poly p = tile.Polys[i];

				//don't return off-mesh connection polygons
				if (p.PolyType != PolygonType.Ground)
					continue;

				int reference = polyBase | i;

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
					poly = p;
					polyRef = reference;
				}
			}

			if (poly == null)
				return false;

			randomRef = polyRef;
			return FindRandomPointOnPoly(tile, poly, polyRef, rand, out randomPt);
		}

		/// <summary>
		/// Find a random point that is a certain distance away from another point. Use the overload with a <c>Random</c> parameter if calling multiple times in a row.
		/// </summary>
		/// <param name="startRef">Starting point's polygon reference</param>
		/// <param name="centerPos">Starting point</param>
		/// <param name="radius">Circle's radius</param>
		/// <param name="randomRef">Resulting polygon reference of random point</param>
		/// <param name="randomPt">Resulting random point</param>
		/// <returns>True, if point found. False, if otherwise.</returns>
		public bool FindRandomPointAroundCircle(int startRef, Vector3 centerPos, float radius, out int randomRef, out Vector3 randomPt)
		{
			return FindRandomPointAroundCircle(startRef, centerPos, radius, new System.Random(), out randomRef, out randomPt);
		}

		/// <summary>
		/// Find a random point that is a certain distance away from another point.
		/// </summary>
		/// <param name="startRef">Starting point's polygon reference</param>
		/// <param name="centerPos">Starting point</param>
		/// <param name="radius">Circle's radius</param>
		/// <param name="randomRef">Resulting polygon reference of random point</param>
		/// <param name="randomPt">Resulting random point</param>
		/// <returns>True, if point found. False, if otherwise.</returns>
		public bool FindRandomPointAroundCircle(int startRef, Vector3 centerPos, float radius, System.Random rand, out int randomRef, out Vector3 randomPt)
		{
			randomRef = 0;
			randomPt = Vector3.Zero;

			if (nav == null)
				return false;

			if (nodePool == null)
				return false;

			if (openList == null)
				return false;

			//validate input
			if (startRef == 0 || !nav.IsValidPolyRef(startRef))
				return false;

			MeshTile startTile;
			Poly startPoly;
			nav.TryGetTileAndPolyByRefUnsafe(startRef, out startTile, out startPoly);

			nodePool.Clear();
			openList.Clear();

			Node startNode = nodePool.GetNode(startRef);
			startNode.pos = centerPos;
			startNode.pidx = 0;
			startNode.cost = 0;
			startNode.total = 0;
			startNode.id = startRef;
			startNode.flags = NodeFlags.Open;
			openList.Push(startNode);

			float radiusSqr = radius * radius;
			float areaSum = 0.0f;

			MeshTile randomTile = null;
			Poly randomPoly = null;
			int randomPolyRef = 0;

			while (openList.Count > 0)
			{
				Node bestNode = openList.Pop();
				SetNodeFlagClosed(ref bestNode);

				//get poly and tile
				int bestRef = bestNode.id;
				MeshTile bestTile;
				Poly bestPoly;
				nav.TryGetTileAndPolyByRefUnsafe(bestRef, out bestTile, out bestPoly);

				//place random locations on ground
				if (bestPoly.PolyType == PolygonType.Ground)
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
						randomTile = bestTile;
						randomPoly = bestPoly;
						randomPolyRef = bestRef;
					}
				}

				//get parent poly and tile
				int parentRef = 0;
				MeshTile parentTile;
				Poly parentPoly;
				if (bestNode.pidx != 0)
					parentRef = nodePool.GetNodeAtIdx(bestNode.pidx).id;
				if (parentRef != 0)
					nav.TryGetTileAndPolyByRefUnsafe(parentRef, out parentTile, out parentPoly);

				for (int i = bestPoly.FirstLink; i != PathfinderCommon.NULL_LINK; i = bestTile.Links[i].Next)
				{
					Link link = bestTile.Links[i];
					int neighbourRef = link.Reference;
					//skip invalid neighbours and do not follor back to parent
					if (neighbourRef == 0 || neighbourRef == parentRef)
						continue;

					//expand to neighbour
					MeshTile neighbourTile;
					Poly neighbourPoly;
					nav.TryGetTileAndPolyByRefUnsafe(neighbourRef, out neighbourTile, out neighbourPoly);

					//find edge and calculate distance to edge
					Vector3 va = new Vector3();
					Vector3 vb = new Vector3();
					if (!GetPortalPoints(bestRef, bestPoly, bestTile, neighbourRef, neighbourPoly, neighbourTile, ref va, ref vb))
						continue;

					//if circle isn't touching next polygon, skip it
					float tseg;
					float distSqr = MathHelper.Distance.PointToSegment2DSquared(ref centerPos, ref va, ref vb, out tseg);
					if (distSqr > radiusSqr)
						continue;

					Node neighbourNode = nodePool.GetNode(neighbourRef);
					if (neighbourNode == null)
						continue;

					if (IsInClosedList(neighbourNode))
						continue;

					//cost
					if (neighbourNode.flags == 0)
						neighbourNode.pos = Vector3.Lerp(va, vb, 0.5f);

					float total = bestNode.total + (bestNode.pos - neighbourNode.pos).Length();

					//node is already in open list and new result is worse, so skip
					if (IsInOpenList(neighbourNode) && total >= neighbourNode.total)
						continue;

					neighbourNode.id = neighbourRef;
					neighbourNode.flags = RemoveNodeFlagClosed(neighbourNode);
					neighbourNode.pidx = nodePool.GetNodeIdx(bestNode);
					neighbourNode.total = total;

					if (IsInOpenList(neighbourNode))
					{
						openList.Modify(neighbourNode);
					}
					else
					{
						neighbourNode.flags = NodeFlags.Open;
						openList.Push(neighbourNode);
					}
				}
			}

			if (randomPoly == null)
				return false;

			randomRef = randomPolyRef;
			return FindRandomPointOnPoly(randomTile, randomPoly, randomPolyRef, rand, out randomPt);
		}

		/// <summary>
		/// Find a path from the start polygon to the end polygon.
		/// -If the end polygon can't be reached, the last polygon will be nearest the end polygon
		/// -If the path array is too small, it will be filled as far as possible 
		/// -start and end positions are used to calculate traversal costs
		/// </summary>
		/// <param name="startRef">Starting point's polygon reference</param>
		/// <param name="endRef">Ending point's polygon reference</param>
		/// <param name="startPos">Starting point</param>
		/// <param name="endPos">Ending point</param>
		/// <param name="path">The path of polygon references</param>
		/// <returns>True, if path found. False, if otherwise.</returns>
		public bool FindPath(int startRef, int endRef, ref Vector3 startPos, ref Vector3 endPos, List<int> path)
		{
			//reset path of polygons
			path.Clear();

			if (startRef == 0 || endRef == 0)
				return false;

			//path can't store any elements
			if (path.Capacity == 0)
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
			Node startNode = nodePool.GetNode(startRef);
			startNode.pos = startPos;
			startNode.pidx = 0;
			startNode.cost = 0;
			startNode.total = (startPos - endPos).Length() * H_SCALE;
			startNode.id = startRef;
			startNode.flags = NodeFlags.Open;
			openList.Push(startNode);

			Node lastBestNode = startNode;
			float lastBestTotalCost = startNode.total;

			while (openList.Count > 0)
			{
				//remove node from open list and put it in closed list
				Node bestNode = openList.Pop();
				SetNodeFlagClosed(ref bestNode);

				//reached the goal. stop searching
				if (bestNode.id == endRef)
				{
					lastBestNode = bestNode;
					break;
				}

				//get current poly and tile
				int bestRef = bestNode.id;
				MeshTile bestTile;
				Poly bestPoly;
				nav.TryGetTileAndPolyByRefUnsafe(bestRef, out bestTile, out bestPoly);

				//get parent poly and tile
				int parentRef = 0;
				MeshTile parentTile;
				Poly parentPoly;
				if (bestNode.pidx != 0)
					parentRef = nodePool.GetNodeAtIdx(bestNode.pidx).id;
				if (parentRef != 0)
					nav.TryGetTileAndPolyByRefUnsafe(parentRef, out parentTile, out parentPoly);

				//examine neighbors
				for (int i = bestPoly.FirstLink; i != PathfinderCommon.NULL_LINK; i = bestTile.Links[i].Next)
				{
					int neighbourRef = bestTile.Links[i].Reference;

					//skip invalid ids and do not expand back to where we came from
					if (neighbourRef == 0 || neighbourRef == parentRef)
						continue;

					//get neighbour poly and tile
					MeshTile neighbourTile;
					Poly neighbourPoly;
					nav.TryGetTileAndPolyByRefUnsafe(neighbourRef, out neighbourTile, out neighbourPoly);

					Node neighbourNode = nodePool.GetNode(neighbourRef);
					if (neighbourNode == null)
						continue;

					//if node is visited the first time, calculate node position
					if (neighbourNode.flags == 0)
					{
						GetEdgeMidPoint(bestRef, bestPoly, bestTile, neighbourRef, neighbourPoly, neighbourTile, ref neighbourNode.pos);
					}

					//calculate cost and heuristic
					float cost = 0;
					float heuristic = 0;

					//special case for last node
					if (neighbourRef == endRef)
					{
						//cost
						float curCost = GetCost(bestNode.pos, neighbourNode.pos, bestPoly);
						float endCost = GetCost(neighbourNode.pos, endPos, neighbourPoly);

						cost = bestNode.cost + curCost + endCost;
						heuristic = 0;
					}
					else
					{
						//cost
						float curCost = GetCost(bestNode.pos, neighbourNode.pos, bestPoly);
						
						cost = bestNode.cost + curCost;
						heuristic = (neighbourNode.pos - endPos).Length() * H_SCALE; 
					}

					float total = cost + heuristic;

					//the node is already in open list and new result is worse, skip
					if (IsInOpenList(neighbourNode) && total >= neighbourNode.total)
						continue;

					//the node is already visited and processesd, and the new result is worse, skip
					if (IsInClosedList(neighbourNode) && total >= neighbourNode.total)
						continue;

					//add or update the node
					neighbourNode.pidx = nodePool.GetNodeIdx(bestNode);
					neighbourNode.id = neighbourRef;
					neighbourNode.flags = RemoveNodeFlagClosed(neighbourNode);
					neighbourNode.cost = cost;
					neighbourNode.total = total;

					if (IsInOpenList(neighbourNode))
					{
						//already in open, update node location
						openList.Modify(neighbourNode);
					}
					else
					{
						//put the node in the open list
						SetNodeFlagOpen(ref neighbourNode);
						openList.Push(neighbourNode);
					}

					//update nearest node to target so far
					if (heuristic < lastBestTotalCost)
					{
						lastBestTotalCost = heuristic;
						lastBestNode = neighbourNode;
					}
				}
			}

			//save path
			Node node = lastBestNode;
			do
			{
				path.Add(node.id);
				if (path.Count >= path.Capacity)
					break;
		
				node = nodePool.GetNodeAtIdx(node.pidx);
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
		public bool FindStraightPath(Vector3 startPos, Vector3 endPos, int[] path, int pathSize, 
			Vector3[] straightPath, int[] straightPathFlags, int[] straightPathRefs, ref int straightPathCount, int maxStraightPath, int options)
		{
			straightPathCount = 0;

			if (path.Length == 0)
				return false;

			bool stat = false;

			Vector3 closestStartPos = new Vector3();
			ClosestPointOnPolyBoundary(path[0], startPos, ref closestStartPos);

			Vector3 closestEndPos = new Vector3();
			ClosestPointOnPolyBoundary(path[pathSize - 1], endPos, ref closestEndPos);

			stat = AppendVertex(closestStartPos, PathfinderCommon.STRAIGHTPATH_START, path[0], straightPath, straightPathFlags, straightPathRefs, ref straightPathCount, maxStraightPath);

			if (!stat)
				return true;

			if (pathSize > 1)
			{
				Vector3 portalApex = closestStartPos;
				Vector3 portalLeft = portalApex;
				Vector3 portalRight = portalApex;
				int apexIndex = 0;
				int leftIndex = 0;
				int rightIndex = 0;

				PolygonType leftPolyType = 0;
				PolygonType rightPolyType = 0;

				int leftPolyRef = path[0];
				int rightPolyRef = path[0];

				for (int i = 0; i < pathSize; i++)
				{
					Vector3 left = new Vector3();
					Vector3 right = new Vector3();
					PolygonType fromType = 0, toType = 0;

					if (i + 1 < pathSize)
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

							if ((options & (PathfinderCommon.STRAIGHTPATH_AREA_CROSSINGS | PathfinderCommon.STRAIGHTPATH_ALL_CROSSINGS)) != 0)
							{
								//append portals
								stat = AppendPortals(apexIndex, i, closestEndPos, path, straightPath, straightPathFlags, straightPathRefs, ref straightPathCount, maxStraightPath, options);
							}

							stat = AppendVertex(closestEndPos, 0, path[i], straightPath, straightPathFlags, straightPathRefs, ref straightPathCount, maxStraightPath);

							return true;
						}

						//if starting really close to the portal, advance
						if (i == 0)
						{
							float t;
							if (MathHelper.Distance.PointToSegment2DSquared(ref portalApex, ref left, ref right, out t) < 0.001 * 0.001)
								continue;
						}
					}
					else
					{
						//end of the path
						left = closestEndPos;
						right = closestEndPos;

						fromType = toType = PolygonType.Ground;
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
							rightPolyRef = (i + 1 < pathSize) ? path[i + 1] : 0;
							rightPolyType = toType;
							rightIndex = i;
						}
						else
						{
							//append portals along current straight path segment
							if ((options & (PathfinderCommon.STRAIGHTPATH_AREA_CROSSINGS | PathfinderCommon.STRAIGHTPATH_ALL_CROSSINGS)) != 0)
							{
								stat = AppendPortals(apexIndex, leftIndex, portalLeft, path, straightPath, straightPathFlags, straightPathRefs, ref straightPathCount, maxStraightPath, options);

								if (stat != true)
									return true;
							}

							portalApex = portalLeft;
							apexIndex = leftIndex;

							int flags = 0;
							if (leftPolyRef == 0)
								flags = PathfinderCommon.STRAIGHTPATH_END;
							else if (leftPolyType == PolygonType.OffMeshConnection)
								flags = PathfinderCommon.STRAIGHTPATH_OFFMESH_CONNECTION;

							int reference = leftPolyRef;

							//append or update vertex
							stat = AppendVertex(portalApex, flags, reference, straightPath, straightPathFlags, straightPathRefs, ref straightPathCount, maxStraightPath);

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
							leftPolyRef = (i + 1 < pathSize) ? path[i + 1] : 0;
							leftPolyType = toType;
							leftIndex = i;
						}
						else
						{
							if ((options & (PathfinderCommon.STRAIGHTPATH_AREA_CROSSINGS | PathfinderCommon.STRAIGHTPATH_ALL_CROSSINGS)) != 0)
							{
								stat = AppendPortals(apexIndex, rightIndex, portalRight, path, straightPath, straightPathFlags, straightPathRefs, ref straightPathCount, maxStraightPath, options);

								if (stat != true)
									return true;
							}

							portalApex = portalRight;
							apexIndex = rightIndex;

							int flags = 0;
							if (rightPolyRef == 0)
								flags = PathfinderCommon.STRAIGHTPATH_END;
							else if (rightPolyType == PolygonType.OffMeshConnection)
								flags = PathfinderCommon.STRAIGHTPATH_OFFMESH_CONNECTION;

							int reference = rightPolyRef;

							//append or update vertex
							stat = AppendVertex(portalApex, flags, reference, straightPath, straightPathFlags, straightPathRefs, ref straightPathCount, maxStraightPath);

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
				if ((options & (PathfinderCommon.STRAIGHTPATH_AREA_CROSSINGS | PathfinderCommon.STRAIGHTPATH_ALL_CROSSINGS)) != 0)
				{
					stat = AppendPortals(apexIndex, pathSize - 1, closestEndPos, path, straightPath, straightPathFlags, straightPathRefs, ref straightPathCount, maxStraightPath, options);

					if (stat != true)
						return true;
				}
			}

			stat = AppendVertex(closestEndPos, PathfinderCommon.STRAIGHTPATH_END, 0, straightPath, straightPathFlags, straightPathRefs, ref straightPathCount, maxStraightPath);

			return true;
		}

		/// <summary>
		/// This method is optimized for small delta movement and a small number of polygons.
		/// If movement distance is too large, the result will form an incomplete path.
		/// </summary>
		/// <param name="startRef">Starting polygon reference</param>
		/// <param name="startPos">Start position</param>
		/// <param name="endPos">End position</param>
		/// <param name="resultPos">Intermediate point</param>
		/// <param name="visited">Visited polygon references</param>
		/// <returns>True, if point found. False, if otherwise.</returns>
		public bool MoveAlongSurface(int startRef, Vector3 startPos, Vector3 endPos, ref Vector3 resultPos, List<int> visited)
		{
			if (nav == null)
				return false;
			if (tinyNodePool == null)
				return false;

			visited.Clear();

			//validate input
			if (startRef == 0)
				return false;
			if (!nav.IsValidPolyRef(startRef))
				return false;

			int MAX_STACK = 48;
			Queue<Node> nodeQueue = new Queue<Node>(MAX_STACK);

			tinyNodePool.Clear();

			Node startNode = tinyNodePool.GetNode(startRef);
			startNode.pidx = 0;
			startNode.cost = 0;
			startNode.total = 0;
			startNode.id = startRef;
			startNode.flags = NodeFlags.Closed;
			nodeQueue.Enqueue(startNode);

			Vector3 bestPos = startPos;
			float bestDist = float.MaxValue;
			Node bestNode = null;

			//search constraints
			Vector3 searchPos = Vector3.Lerp(startPos, endPos, 0.5f);
			float searchRad = (startPos - endPos).Length() / 2.0f + 0.001f;
			float searchRadSqr = searchRad * searchRad;

			Vector3[] verts = new Vector3[PathfinderCommon.VERTS_PER_POLYGON];
			
			while (nodeQueue.Count > 0)
			{
				//pop front
				Node curNode = nodeQueue.Dequeue();

				//get poly and tile
				int curRef = curNode.id;
				MeshTile curTile;
				Poly curPoly;
				nav.TryGetTileAndPolyByRefUnsafe(curRef, out curTile, out curPoly);

				//collect vertices
				int nverts = curPoly.VertCount;
				for (int i = 0; i < nverts; i++)
					verts[i] = curTile.Verts[curPoly.Verts[i]];

				//if target is inside poly, stop search
				if (MathHelper.IsPointInPoly(endPos, verts, nverts))
				{
					bestNode = curNode;
					bestPos = endPos;
					break;
				}

				//find wall edges and find nearest point inside walls
				for (int i = 0, j = curPoly.VertCount - 1; i < curPoly.VertCount; j = i++)
				{
					//find links to neighbors
					List<int> neis = new List<int>(8);

					if ((curPoly.Neis[j] & PathfinderCommon.EXT_LINK) != 0)
					{
						//tile border
						for (int k = curPoly.FirstLink; k != PathfinderCommon.NULL_LINK; k = curTile.Links[k].Next)
						{
							Link link = curTile.Links[k];
							if (link.Edge == j)
							{
								if (link.Reference != 0)
								{
									MeshTile neiTile;
									Poly neiPoly;
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
						int reference = nav.GetPolyRefBase(curTile) | idx;
						neis.Add(reference); //internal edge, encode id
					}

					if (neis.Count == 0)
					{
						//wall edge, calculate distance
						float tseg = 0;
						float distSqr = MathHelper.Distance.PointToSegment2DSquared(ref endPos, ref verts[j], ref verts[i], out tseg);
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
							Node neighbourNode = tinyNodePool.GetNode(neis[k]);
							if (neighbourNode == null)
								continue;
							
							//skip if already visited
							if ((neighbourNode.flags & NodeFlags.Closed) != 0)
								continue;

							//skip the link if too far from search constraint
							float distSqr = MathHelper.Distance.PointToSegment2DSquared(ref searchPos, ref verts[j], ref verts[i]);
							if (distSqr > searchRadSqr)
								continue;

							//mark the node as visited and push to queue
							if (nodeQueue.Count < MAX_STACK)
							{
								neighbourNode.pidx = tinyNodePool.GetNodeIdx(curNode);
								neighbourNode.flags |= NodeFlags.Closed;
								nodeQueue.Enqueue(neighbourNode);
							}
						}
					}
				}
			}

			if (bestNode != null)
			{
				//save the path
				Node node = bestNode;
				do
				{
					visited.Add(node.id);
					if (visited.Count >= visited.Capacity)
						break;

					node = tinyNodePool.GetNodeAtIdx(node.pidx);
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
		/// <param name="startRef">Start polygon reference</param>
		/// <param name="endRef">End polygon reference</param>
		/// <param name="startPos">Start position's coordinates</param>
		/// <param name="endPos">End position's coordinates</param>
		/// <returns>True if path initialized, false if no</returns>
		public bool InitSlicedFindPath(int startRef, int endRef, Vector3 startPos, Vector3 endPos)
		{
			//init path state
			query = new QueryData();
			query.Status = false;
			query.StartRef = startRef;
			query.EndRef = endRef;
			query.StartPos = startPos;
			query.EndPos = endPos;

			if (query.StartRef == 0 || query.EndRef == 0)
				return false;

			//validate input
			if (!nav.IsValidPolyRef(startRef) || !nav.IsValidPolyRef(endRef))
				return false;

			if (startRef == endRef)
			{
				query.Status = true;
				return true;
			}

			nodePool.Clear();
			openList.Clear();

			Node startNode = nodePool.GetNode(startRef);
			startNode.pos = startPos;
			startNode.pidx = 0;
			startNode.cost = 0;
			startNode.total = (endPos - startPos).Length() * H_SCALE;
			startNode.id = startRef;
			startNode.flags = NodeFlags.Open;
			openList.Push(startNode);

			query.Status = true;
			query.LastBestNode = startNode;
			query.LastBestNodeCost = startNode.total;

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
			if (!nav.IsValidPolyRef(query.StartRef) || !nav.IsValidPolyRef(query.EndRef))
			{
				query.Status = false;
				return false;
			}

			int iter = 0;
			while (iter < maxIter && !openList.Empty())
			{
				iter++;

				//remove node from open list and put it in closed list
				Node bestNode = openList.Pop();
				SetNodeFlagClosed(ref bestNode);

				//reached the goal, stop searching
				if (bestNode.id == query.EndRef)
				{
					query.LastBestNode = bestNode;
					query.Status = true;
					doneIters = iter;
					return query.Status;
				}

				//get current poly and tile
				int bestRef = bestNode.id;
				MeshTile bestTile;
				Poly bestPoly;
				if (nav.TryGetTileAndPolyByRef(bestRef, out bestTile, out bestPoly) == false)
				{
					//the polygon has disappeared during the sliced query, fail
					query.Status = false;
					doneIters = iter;
					return query.Status;
				}

				//get parent poly and tile
				int parentRef = 0;
				MeshTile parentTile;
				Poly parentPoly;
				if (bestNode.pidx != 0)
					parentRef = nodePool.GetNodeAtIdx(bestNode.pidx).id;
				if (parentRef != 0)
				{
					if (nav.TryGetTileAndPolyByRef(parentRef, out parentTile, out parentPoly) == false)
					{
						//the polygon has disappeared during the sliced query, fail
						query.Status = false;
						doneIters = iter;
						return query.Status;
					}
				}

				for (int i = bestPoly.FirstLink; i != PathfinderCommon.NULL_LINK; i = bestTile.Links[i].Next)
				{
					int neighbourRef = bestTile.Links[i].Reference;

					//skip invalid ids and do not expand back to where we came from
					if (neighbourRef == 0 || neighbourRef == parentRef)
						continue;

					//get neighbour poly and tile
					MeshTile neighbourTile;
					Poly neighbourPoly;
					nav.TryGetTileAndPolyByRefUnsafe(neighbourRef, out neighbourTile, out neighbourPoly);

					Node neighbourNode = nodePool.GetNode(neighbourRef);
					if (neighbourNode == null)
						continue;

					if (neighbourNode.flags == 0)
					{
						GetEdgeMidPoint(bestRef, bestPoly, bestTile, neighbourRef, neighbourPoly, neighbourTile, ref neighbourNode.pos);
					}

					//calculate cost and heuristic
					float cost = 0;
					float heuristic = 0;

					//special case for last node
					if (neighbourRef == query.EndRef)
					{
						//cost
						float curCost = GetCost(bestNode.pos, neighbourNode.pos, bestPoly);
						float endCost = GetCost(neighbourNode.pos, query.EndPos, neighbourPoly);

						cost = bestNode.cost + curCost + endCost;
						heuristic = 0;
					}
					else
					{
						//cost
						float curCost = GetCost(bestNode.pos, neighbourNode.pos, bestPoly);

						cost = bestNode.cost + curCost;
						heuristic = (neighbourNode.pos - query.EndPos).Length() * H_SCALE;
					}

					float total = cost + heuristic;

					//the node is already in open list and new result is worse, skip
					if (IsInOpenList(neighbourNode) && total >= neighbourNode.total)
						continue;

					//the node is already visited and processesd, and the new result is worse, skip
					if (IsInClosedList(neighbourNode) && total >= neighbourNode.total)
						continue;

					//add or update the node
					neighbourNode.pidx = nodePool.GetNodeIdx(bestNode);
					neighbourNode.id = neighbourRef;
					neighbourNode.flags = RemoveNodeFlagClosed(neighbourNode);
					neighbourNode.cost = cost;
					neighbourNode.total = total;

					if (IsInOpenList(neighbourNode))
					{
						//already in open, update node location
						openList.Modify(neighbourNode);
					}
					else
					{
						//put the node in the open list
						SetNodeFlagOpen(ref neighbourNode);
						openList.Push(neighbourNode);
					}

					//update nearest node to target so far
					if (heuristic < query.LastBestNodeCost)
					{
						query.LastBestNodeCost = heuristic;
						query.LastBestNode = neighbourNode;
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
		public bool FinalizeSlicedFindPath(int[] path, ref int pathCount, int maxPath)
		{
			pathCount = 0;

			if (query.Status == false)
			{
				query = new QueryData();
				return false;
			}

			int n = 0;

			if (query.StartRef == query.EndRef)
			{
				//special case: the search starts and ends at the same poly
				path[n++] = query.StartRef;
			}
			else
			{
				//reverse the path
				Node prev = null;
				Node node = query.LastBestNode;
				do
				{
					Node next = nodePool.GetNodeAtIdx(node.pidx);
					node.pidx = nodePool.GetNodeIdx(prev);
					prev = node;
					node = next;
				}
				while (node != null);

				//store path
				node = prev;
				do
				{
					path[n++] = node.id;
					if (n >= maxPath)
					{
						break;
					}
					node = nodePool.GetNodeAtIdx(node.pidx);
				}
				while (node != null);
			}

			//reset query
			query = new QueryData();

			//remember to update the path length
			pathCount = n;

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
		public bool FinalizedSlicedPathPartial(int[] existing, int existingSize, int[] path, ref int pathCount, int maxPath)
		{
			pathCount = 0;

			if (existingSize == 0)
			{
				return false;
			}

			if (query.Status == false)
			{
				query = new QueryData();
				return false;
			}

			int n = 0;

			if (query.StartRef == query.EndRef)
			{
				//special case: the search starts and ends at the same poly
				path[n++] = query.StartRef;
			}
			else
			{
				//find furthest existing node that was visited
				Node prev = null;
				Node node = null;
				for (int i = existingSize - 1; i >= 0; i--)
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
				do
				{
					Node next = nodePool.GetNodeAtIdx(node.pidx);
					node.pidx = nodePool.GetNodeIdx(prev);
					prev = node;
					node = next;
				}
				while (node != null);

				//store path
				node = prev;
				do
				{
					path[n++] = node.id;
					if (n >= maxPath)
					{
						break;
					}
					node = nodePool.GetNodeAtIdx(node.pidx);
				}
				while (node != null);
			}

			//reset query
			query = new QueryData();

			//remember to update the path length
			pathCount = n;

			return true;
		}

		public bool Raycast(int startRef, Vector3 startPos, Vector3 endPos, ref float t, ref Vector3 hitNormal, int[] path, ref int pathCount, int maxPath)
		{
			t = 0;
			pathCount = 0;

			//validate input
			if (startRef == 0 || !nav.IsValidPolyRef(startRef))
				return false;

			int curRef = startRef;
			Vector3[] verts = new Vector3[PathfinderCommon.VERTS_PER_POLYGON];
			int n = 0;

			hitNormal = new Vector3(0, 0, 0);

			while (curRef != 0)
			{
				//cast ray against current polygon

				MeshTile tile;
				Poly poly;
				nav.TryGetTileAndPolyByRefUnsafe(curRef, out tile, out poly);

				//collect vertices
				int nv = 0;
				for (int i = 0; i < poly.VertCount; i++)
				{
					verts[nv] = tile.Verts[poly.Verts[i]];
					nv++;
				}

				float tmin, tmax;
				int segMin, segMax;
				if (!MathHelper.Intersection.SegmentPoly2D(startPos, endPos, verts, nv, out tmin, out tmax, out segMin, out segMax))
				{
					//could not hit the polygon, keep the old t and report hit
					pathCount = n;
					return true;
				}

				//keep track of furthest t so far
				if (tmax > t)
					t = tmax;

				//store visited polygons
				if (n < maxPath)
					path[n++] = curRef;

				//ray end is completely inside the polygon
				if (segMax == -1)
				{
					t = float.MaxValue;
					pathCount = n;
					return true;
				}

				//follow neighbours
				int nextRef = 0;

				for (int i = poly.FirstLink; i != PathfinderCommon.NULL_LINK; i = tile.Links[i].Next)
				{
					Link link = tile.Links[i];

					//find link which contains the edge
					if (link.Edge != segMax)
						continue;

					//get pointer to the next polygon
					MeshTile nextTile;
					Poly nextPoly;
					nav.TryGetTileAndPolyByRefUnsafe(link.Reference, out nextTile, out nextPoly);

					//skip off-mesh connection
					if (nextPoly.PolyType == PolygonType.OffMeshConnection)
						continue;

					//if the link is internal, just return the ref
					if (link.Side == 0xff)
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
					int v0 = poly.Verts[link.Edge];
					int v1 = poly.Verts[(link.Edge + 1) % poly.VertCount];
					Vector3 left = tile.Verts[v0];
					Vector3 right = tile.Verts[v1];

					//check that the intersection lies inside the link portal
					if (link.Side == 0 || link.Side == 4)
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
						float z = startPos.Z + (endPos.Z - startPos.Z) * tmax;
						if (z >= lmin && z <= lmax)
						{
							nextRef = link.Reference;
							break;
						}
					}
					else if (link.Side == 2 || link.Side == 6)
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
						float x = startPos.X + (endPos.X - startPos.X) * tmax;
						if (x >= lmin && x <= lmax)
						{
							nextRef = link.Reference;
							break;
						}
					}
				}

				if (nextRef == 0)
				{
					//no neighbour, we hit a wall

					//calculate hit normal
					int a = segMax;
					int b = (segMax + 1) < nv ? segMax + 1 : 0;
					Vector3 va = verts[a];
					Vector3 vb = verts[b];
					float dx = vb.X - va.X;
					float dz = vb.Z - va.Z;
					hitNormal.X = dz;
					hitNormal.Y = 0;
					hitNormal.Z = -dx;
					hitNormal.Normalize();

					pathCount = n;
					return true;
				}

				//no hit, advance to neighbour polygon
				curRef = nextRef;
			}

			pathCount = n;

			return true;
		}

		/// <summary>
		/// Store polygons that are within a certain range from the current polygon
		/// </summary>
		/// <param name="startRef">Starting polygon reference</param>
		/// <param name="centerPos">Starting position</param>
		/// <param name="radius">Range to search within</param>
		/// <param name="resultRef">All the polygons within range</param>
		/// <param name="resultParent">Polygon's parents</param>
		/// <param name="resultCount">Number of polygons stored</param>
		/// <param name="maxResult">Maximum number of polygons allowed</param>
		/// <returns>True, unless input is invalid</returns>
		public bool FindLocalNeighbourhood(int startRef, Vector3 centerPos, float radius, 
			int[] resultRef, int[] resultParent, ref int resultCount, int maxResult)
		{
			resultCount = 0;

			//validate input
			if (startRef == 0 || !nav.IsValidPolyRef(startRef))
				return false;

			int MAX_STACK = 48;
			Node[] stack = new Node[MAX_STACK];
			int nstack = 0;

			tinyNodePool.Clear();

			Node startNode = tinyNodePool.GetNode(startRef);
			startNode.pidx = 0;
			startNode.id = startRef;
			startNode.flags = NodeFlags.Closed;
			stack[nstack++] = startNode;

			float radiusSqr = radius * radius;

			Vector3[] pa = new Vector3[PathfinderCommon.VERTS_PER_POLYGON];
			Vector3[] pb = new Vector3[PathfinderCommon.VERTS_PER_POLYGON];

			int n = 0;
			if (n < maxResult)
			{
				resultRef[n] = startNode.id;
				resultParent[n] = 0;
				++n;
			}

			while (nstack > 0)
			{
				//pop front
				Node curNode = stack[0];
				for (int i = 0; i < nstack - 1; i++)
					stack[i] = stack[i + 1];
				nstack--;

				//get poly and tile
				int curRef = curNode.id;
				MeshTile curTile;
				Poly curPoly;
				nav.TryGetTileAndPolyByRefUnsafe(curRef, out curTile, out curPoly);

				for (int i = curPoly.FirstLink; i != PathfinderCommon.NULL_LINK; i = curTile.Links[i].Next)
				{
					Link link = curTile.Links[i];
					int neighbourRef = link.Reference;

					//skip invalid neighbours
					if (neighbourRef == 0)
						continue;

					//skip if cannot allocate more nodes
					Node neighbourNode = tinyNodePool.GetNode(neighbourRef);
					if (neighbourNode == null)
						continue;
					//skip visited
					if ((neighbourNode.flags & NodeFlags.Closed) != 0)
						continue;

					//expand to neighbour
					MeshTile neighbourTile;
					Poly neighbourPoly;
					nav.TryGetTileAndPolyByRefUnsafe(neighbourRef, out neighbourTile, out neighbourPoly);

					//skip off-mesh connections
					if (neighbourPoly.PolyType == PolygonType.OffMeshConnection)
						continue;

					//find edge and calculate distance to edge
					Vector3 va = new Vector3();
					Vector3 vb = new Vector3();
					if (!GetPortalPoints(curRef, curPoly, curTile, neighbourRef, neighbourPoly, neighbourTile, ref va, ref vb))
						continue;

					//if the circle is not touching the next polygon, skip it
					float tseg;
					float distSqr = MathHelper.Distance.PointToSegment2DSquared(ref centerPos, ref va, ref vb, out tseg);
					if (distSqr > radiusSqr)
						continue;

					//mark node visited
					neighbourNode.flags |= NodeFlags.Closed;
					neighbourNode.pidx = tinyNodePool.GetNodeIdx(curNode);

					//check that the polygon doesn't collide with existing polygons

					//collect vertices of the neighbour poly
					int npa = neighbourPoly.VertCount;
					for (int k = 0; k < npa; k++)
						pa[k] = neighbourTile.Verts[neighbourPoly.Verts[k]];

					bool overlap = false;
					for (int j = 0; j < n; j++)
					{
						int pastRef = resultRef[j];

						//connected polys do not overlap
						bool connected = false;
						for (int k = curPoly.FirstLink; k != PathfinderCommon.NULL_LINK; k = curTile.Links[k].Next)
						{
							if (curTile.Links[k].Reference == pastRef)
							{
								connected = true;
								break;
							}
						}
						if (connected)
							continue;

						//potentially overlapping
						MeshTile pastTile;
						Poly pastPoly;
						nav.TryGetTileAndPolyByRefUnsafe(pastRef, out pastTile, out pastPoly);

						//get vertices and test overlap
						int npb = pastPoly.VertCount;
						for (int k = 0; k < npb; k++)
							pb[k] = pastTile.Verts[pastPoly.Verts[k]];

						if (MathHelper.Intersection.PolyPoly2D(pa, npa, pb, npb))
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
						resultRef[n] = neighbourRef;
						resultParent[n] = curRef;
						++n;
					}

					if (nstack < MAX_STACK)
					{
						stack[nstack++] = neighbourNode;
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
		public bool GetPolyWallSegments(int reference, LocalBoundary.Segment[] segmentVerts, int[] segmentRefs, ref int segmentCount, int maxSegments)
		{
			segmentCount = 0;

			MeshTile tile;
			Poly poly;
			if (nav.TryGetTileAndPolyByRef(reference, out tile, out poly) == false)
				return false;

			int n = 0;
			int MAX_INTERVAL = 16;
			SegInterval[] ints = new SegInterval[MAX_INTERVAL];
			int nints;

			bool storePortals = (segmentRefs.Length != 0);

			for (int i = 0, j = poly.VertCount - 1; i < poly.VertCount; j = i++)
			{
				//skip non-solid edges
				nints = 0;
				if ((poly.Neis[j] & PathfinderCommon.EXT_LINK) != 0)
				{
					//tile border
					for (int k = poly.FirstLink; k != PathfinderCommon.NULL_LINK; k = tile.Links[k].Next)
					{
						Link link = tile.Links[k];
						if (link.Edge == j)
						{
							if (link.Reference != 0)
							{
								MeshTile neiTile;
								Poly neiPoly;
								nav.TryGetTileAndPolyByRefUnsafe(link.Reference, out neiTile, out neiPoly);
								InsertInterval(ints, ref nints, MAX_INTERVAL, link.BMin, link.BMax, link.Reference);
							}
						}
					}
				}
				else
				{
					//internal edge
					int neiRef = 0;
					if (poly.Neis[j] != 0)
					{
						int idx = poly.Neis[j] - 1;
						neiRef = nav.GetPolyRefBase(tile) | idx;
					}

					//if the edge leads to another polygon and portals are not stored, skip
					if (neiRef != 0 && !storePortals)
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
				InsertInterval(ints, ref nints, MAX_INTERVAL, -1, 0, 0);
				InsertInterval(ints, ref nints, MAX_INTERVAL, 255, 256, 0);

				//store segments
				Vector3 vj2 = tile.Verts[poly.Verts[j]];
				Vector3 vi2 = tile.Verts[poly.Verts[i]];
				for (int k = 1; k < nints; k++)
				{
					//portal segment
					if (storePortals && ints[k].Reference != 0)
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
							segmentRefs[n] = 0;
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
		public void InsertInterval(SegInterval[] ints, ref int nints, int maxInts, int tmin, int tmax, int reference)
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
		public bool GetEdgeMidPoint(int from, Poly fromPoly, MeshTile fromTile, int to, Poly toPoly, MeshTile toTile, ref Vector3 mid)
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
		public bool GetPortalPoints(int from, int to, ref Vector3 left, ref Vector3 right, ref PolygonType fromType, ref PolygonType toType)
		{
			MeshTile fromTile;
			Poly fromPoly;
			if (nav.TryGetTileAndPolyByRef(from, out fromTile, out fromPoly) == false)
				return false;
			fromType = fromPoly.PolyType;

			MeshTile toTile;
			Poly toPoly;
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
		public bool GetPortalPoints(int from, Poly fromPoly, MeshTile fromTile, int to, Poly toPoly, MeshTile toTile, ref Vector3 left, ref Vector3 right)
		{
			//find the link that points to the 'to' polygon
			Link link = null;
			for (int i = fromPoly.FirstLink; i != PathfinderCommon.NULL_LINK; i = fromTile.Links[i].Next)
			{
				if (fromTile.Links[i].Reference == to)
				{
					link = fromTile.Links[i];
					break;
				}
			}

			if (link == null)
				return false;

			//handle off-mesh connections
			if (fromPoly.PolyType == PolygonType.OffMeshConnection)
			{
				//find link that points to first vertex
				for (int i = fromPoly.FirstLink; i != PathfinderCommon.NULL_LINK; i = fromTile.Links[i].Next)
				{
					if (fromTile.Links[i].Reference == to)
					{
						int v = fromTile.Links[i].Edge;
						left = fromTile.Verts[fromPoly.Verts[v]];
						right = fromTile.Verts[fromPoly.Verts[v]];
						return true;
					}
				}

				return false;
			}

			if (toPoly.PolyType == PolygonType.OffMeshConnection)
			{
				//find link that points to first vertex
				for (int i = toPoly.FirstLink; i != PathfinderCommon.NULL_LINK; i = toTile.Links[i].Next)
				{
					if (toTile.Links[i].Reference == from)
					{
						int v = toTile.Links[i].Edge;
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
			if (link.Side != 0xff)
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
		public bool ClosestPointOnPoly(int reference, Vector3 pos, ref Vector3 closest)
		{
			if (nav == null)
				return false;

			MeshTile tile;
			Poly poly;

			if (nav.TryGetTileAndPolyByRef(reference, out tile, out poly) == false)
				return false;

			if (tile == null)
				return false;

			PathfinderCommon.ClosestPointOnPolyInTile(tile, poly, pos, ref closest);
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
		public bool ClosestPointOnPoly(int reference, Vector3 pos, out Vector3 closest, out bool posOverPoly)
		{
			posOverPoly = false;
			closest = Vector3.Zero;

			MeshTile tile;
			Poly poly;
			if (!nav.TryGetTileAndPolyByRef(reference, out tile, out poly))
				return false;
			if (tile == null)
				return false;

			if (poly.PolyType == PolygonType.OffMeshConnection)
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
			Vector3[] verts = new Vector3[PathfinderCommon.VERTS_PER_POLYGON];
			float[] edgeDistance = new float[PathfinderCommon.VERTS_PER_POLYGON];
			float[] edgeT = new float[PathfinderCommon.VERTS_PER_POLYGON];
			int numPolyVerts = poly.VertCount;
			for (int i = 0; i < numPolyVerts; i++)
				verts[i] = tile.Verts[poly.Verts[i]];

			closest = pos;
			if (!MathHelper.Distance.PointToPolygonEdgeSquared(pos, verts, numPolyVerts, edgeDistance, edgeT))
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
				if (MathHelper.Distance.PointToTriangle(pos, va, vb, vc, out h))
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
		public bool ClosestPointOnPolyBoundary(int reference, Vector3 pos, ref Vector3 closest)
		{
			MeshTile tile;
			Poly poly;
			if (nav.TryGetTileAndPolyByRef(reference, out tile, out poly) == false)
				return false;

			PathfinderCommon.ClosestPointOnPolyBoundary(tile, poly, pos, out closest);
			return true;
		}

		/// <summary>
		/// Add a vertex to the straight path.
		/// </summary>
		/// <param name="pos"></param>
		/// <param name="flags"></param>
		/// <param name="reference"></param>
		/// <param name="straightPath">An array of points on the straight path</param>
		/// <param name="straightPathFlags">An array of flags</param>
		/// <param name="straightPathRefs">An array of polygon references</param>
		/// <param name="straightPathCount">The number of points on the path</param>
		/// <param name="maxStraightPath">The maximum length allowed for the straight path</param>
		/// <returns>True, if end of path hasn't been reached yet and path isn't full. False, if otherwise.</returns>
		public bool AppendVertex(Vector3 pos, int flags, int reference, 
			Vector3[] straightPath, int[] straightPathFlags, int[] straightPathRefs, ref int straightPathCount, int maxStraightPath)
		{
			if (straightPathCount > 0 && straightPath[straightPathCount - 1] == pos)
			{
				//the vertices are equal
				//update flags and polys
				if (straightPathFlags.Length != 0)
					straightPathFlags[straightPathCount - 1] = flags;
				
				if (straightPathRefs.Length != 0)
					straightPathRefs[straightPathCount - 1] = reference;
			}
			else
			{
				//append new vertex
				straightPath[straightPathCount] = pos;
				
				if (straightPathFlags.Length != 0)
					straightPathFlags[straightPathCount] = flags;
				
				if (straightPathRefs.Length != 0)
					straightPathRefs[straightPathCount] = reference;
				
				straightPathCount++;

				if (flags == PathfinderCommon.STRAIGHTPATH_END || straightPathCount >= maxStraightPath)
				{
					return false;
				}
			}

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
		public bool AppendPortals(int startIdx, int endIdx, Vector3 endPos, int[] path, 
			Vector3[] straightPath, int[] straightPathFlags, int[] straightPathRefs, ref int straightPathCount, int maxStraightPath, int options)
		{
			Vector3 startPos = straightPath[straightPathCount - 1];

			//append or update last vertex
			bool stat = false;
			for (int i = startIdx; i < endIdx; i++)
			{
				//calculate portal
				int from = path[i];
				MeshTile fromTile;
				Poly fromPoly;
				if (nav.TryGetTileAndPolyByRef(from, out fromTile, out fromPoly) == false)
					return false;

				int to = path[i + 1];
				MeshTile toTile;
				Poly toPoly;
				if (nav.TryGetTileAndPolyByRef(to, out toTile, out toPoly) == false)
					return false;

				Vector3 left = new Vector3();
				Vector3 right = new Vector3();
				if (GetPortalPoints(from, fromPoly, fromTile, to, toPoly, toTile, ref left, ref right) == false)
					break;

				if ((options & PathfinderCommon.STRAIGHTPATH_AREA_CROSSINGS) != 0)
				{
					//skip intersection if only area crossings are requested
					if (fromPoly.Area == toPoly.Area)
						continue;
				}

				//append intersection
				float s, t;
				if (MathHelper.Intersection.SegmentSegment2D(ref startPos, ref endPos, ref left, ref right, out s, out t))
				{
					Vector3 pt = Vector3.Lerp(left, right, t);

					stat = AppendVertex(pt, 0, path[i + 1], straightPath, straightPathFlags, straightPathRefs, ref straightPathCount, maxStraightPath);

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
		public bool GetPolyHeight(int reference, Vector3 pos, ref float height)
		{
			if (nav == null)
				return false;

			MeshTile tile;
			Poly poly;
			if (!nav.TryGetTileAndPolyByRef(reference, out tile, out poly))
				return false;

			//off-mesh connections don't have detail polygons
			if (poly.PolyType == PolygonType.OffMeshConnection)
			{
				Vector3 closest;
				PathfinderCommon.ClosestPointOnPolyOffMeshConnection(tile, poly, pos, out closest);
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
				if (PathfinderCommon.ClosestHeight(tile, indexPoly, pos, out h))
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
		/// <param name="nearestRef">Nearest reference.</param>
		/// <param name="neareastPt">Neareast point.</param>
		/// <returns>True, if the nearest poly was found, False, if otherwise.</returns>
		public bool FindNearestPoly(ref Vector3 center, ref Vector3 extents, out int nearestRef, out Vector3 nearestPt)
		{
			nearestRef = 0;
			nearestPt = Vector3.Zero;

			// Get nearby polygons from proximity grid.
			List<int> polys = new List<int>(128);
			if (!QueryPolygons(ref center, ref extents, polys)) 
				return false;

			float nearestDistanceSqr = float.MaxValue;
			for (int i = 0; i < polys.Count; i++) 
			{
				int reference = polys[i];
				Vector3 closestPtPoly;
				bool posOverPoly;
				ClosestPointOnPoly(reference, center, out closestPtPoly, out posOverPoly);

				// If a point is directly over a polygon and closer than
				// climb height, favor that instead of straight line nearest point.
				Vector3 diff = center - closestPtPoly;
				float d = 0;
				if (posOverPoly)
				{
					MeshTile tile;
					Poly poly;
					nav.TryGetTileAndPolyByRefUnsafe(polys[i], out tile, out poly);
					d = Math.Abs(diff.Y) - tile.Header.walkableClimb;
					d = d > 0 ? d * d : 0;
				}
				else
				{
					d = diff.LengthSquared();
				}

				if (d < nearestDistanceSqr)
				{
					nearestPt = closestPtPoly;
					nearestDistanceSqr = d;
					nearestRef = reference;
				}
			}
		
			return true;
		}

		/// <summary>
		/// Finds nearby polygons within a certain range.
		/// </summary>
		/// <param name="center">The starting point</param>
		/// <param name="extent">The range to search within</param>
		/// <param name="polys">A list of polygons</param>
		/// <returns>True, if successful. False, if otherwise.</returns>
		public bool QueryPolygons(ref Vector3 center, ref Vector3 extent, List<int> polys)
		{
			Vector3 bmin = center - extent;
			Vector3 bmax = center + extent;

			int minx, miny, maxx, maxy;
			nav.CalcTileLoc(ref bmin, out minx, out miny);
			nav.CalcTileLoc(ref bmax, out maxx, out maxy);

			MeshTile[] neis = new MeshTile[32];
			
			BBox3 bounds = new BBox3 (bmin, bmax);
			int n = 0;
			for (int y = miny; y <= maxy; y++)
			{
				for (int x = minx; x <= maxx; x++)
				{
					int nneis = nav.GetTilesAt(x, y, neis);
					for (int j = 0; j < nneis; j++)
					{
						n += nav.QueryPolygonsInTile(neis[j], bounds, polys);
						if (n >= polys.Capacity) 
						{
							return true;
						}
					}
				}
			}

			return polys.Count != 0;
		}

		public bool IsValidPolyRef(int reference)
		{
			MeshTile tile;
			Poly poly;
			bool status = nav.TryGetTileAndPolyByRef(reference, out tile, out poly);
			if (status == false)
				return false;
			return true;
		}

		public bool IsInOpenList(Node node)
		{
			return (node.flags & NodeFlags.Open) != 0;
		}

		public bool IsInClosedList(Node node)
		{
			return (node.flags & NodeFlags.Closed) != 0;
		}

		public void SetNodeFlagOpen(ref Node node)
		{
			node.flags |= NodeFlags.Open;
		}

		public void SetNodeFlagClosed(ref Node node)
		{
			node.flags &= ~NodeFlags.Open;
			node.flags |= NodeFlags.Closed;
		}

		public NodeFlags RemoveNodeFlagClosed(Node node)
		{
			return node.flags & ~NodeFlags.Closed;
		}

		private struct QueryData
		{
			public bool Status;
			public Node LastBestNode;
			public float LastBestNodeCost;
			public int StartRef, EndRef;
			public Vector3 StartPos, EndPos;
		}

		public struct SegInterval
		{
			public int Reference;
			public int TMin, TMax;
		}
	}
}
