#region License
/**
 * Copyright (c) 2013-2014 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
 * Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE
 */
#endregion

using System;
using System.Collections.Generic;

using SharpNav.Collections.Generic;
using SharpNav.Geometry;

#if MONOGAME || XNA
using Microsoft.Xna.Framework;
#elif OPENTK
using OpenTK;
#elif SHARPDX
using SharpDX;
#endif

namespace SharpNav
{
	/// <summary>
	/// Do pathfinding calculations on the TiledNavMesh
	/// </summary>
	public class NavMeshQuery
	{
		private const float H_SCALE = 0.999f;

		private TiledNavMesh m_nav;
		private NodePool m_tinyNodePool;
		private NodePool m_nodePool;
		private PriorityQueue<Node> m_openList;
		private QueryData m_query;

		public NavMeshQuery(TiledNavMesh nav, int maxNodes)
		{
			m_nav = nav;

			m_nodePool = new NodePool(maxNodes, MathHelper.NextPowerOfTwo(maxNodes / 4));
			m_tinyNodePool = new NodePool(64, 32);
			m_openList = new PriorityQueue<Node>(maxNodes);
		}

		public bool FindRandomPoint(ref QueryFilter filter, ref int randomRef, ref Vector3 randomPt)
		{
			if (m_nav == null)
				return false;

			Random randObj = new Random();

			//randomly pick one tile
			//assume all tiles cover roughly the same area
			PathfinderCommon.MeshTile tile = null;
			float tsum = 0.0f;
			
			for (int i = 0; i < m_nav.GetMaxTiles(); i++)
			{
				PathfinderCommon.MeshTile t = m_nav.GetTile(i);
				
				if (t == null || t.header == null)
					continue;

				//choose random tile using reservoir sampling
				float area = 1.0f;
				tsum += area;
				float u = (float)randObj.NextDouble();
				if (u * tsum <= area)
					tile = t;
			}

			if (tile == null)
				return false;

			//randomly pick one polygon weighted by polygon area
			PathfinderCommon.Poly poly = null;
			int polyRef = 0;
			int polyBase = m_nav.GetPolyRefBase(tile);

			float areaSum = 0.0f;
			for (int i = 0; i < tile.header.polyCount; i++)
			{
				PathfinderCommon.Poly p = tile.polys[i];

				//don't return off-mesh connection polygons
				if (p.GetPolyType() != PolygonType.Ground)
					continue;

				//NOTE: polygon flags are never set so the filter will remove all polygons
				int reference = polyBase | i;
				//if (!filter.PassFilter(p))
				//	continue;

				//calculate area of polygon
				float polyArea = 0.0f;
				float area;
				for (int j = 2; j < p.vertCount; j++)
				{
					Triangle3.Area2D(ref tile.verts[p.verts[0]], ref tile.verts[p.verts[j - 1]], ref tile.verts[p.verts[j]], out area);
					polyArea += area;
				}

				//choose random polygon weighted by area, usig resevoir sampling
				areaSum += polyArea;
				float u = (float)randObj.NextDouble();
				if (u * areaSum <= polyArea)
				{
					poly = p;
					polyRef = reference;
				}
			}

			if (poly == null)
				return false;

			//randomly pick point on polygon
			Vector3[] verts = new Vector3[PathfinderCommon.VERTS_PER_POLYGON];
			float[] areas = new float[PathfinderCommon.VERTS_PER_POLYGON];
			for (int j = 0; j < poly.vertCount; j++)
			{
				verts[j] = tile.verts[poly.verts[j]];
			}

			float s = (float)randObj.NextDouble();
			float t1 = (float)randObj.NextDouble();

			Vector3 pt = new Vector3();
			PathfinderCommon.RandomPointInConvexPoly(verts, poly.vertCount, areas, s, t1, ref pt);

			float h = 0.0f;
			if (!GetPolyHeight(polyRef, pt, ref h))
				return false;

			pt.Y = h;

			randomPt = pt;
			randomRef = polyRef;

			return true;
		}

		public bool FindRandomPointAroundCircle(int startRef, Vector3 centerPos, float radius, ref QueryFilter filter, ref int randomRef, ref Vector3 randomPt)
		{
			if (m_nav == null)
				return false;

			if (m_nodePool == null)
				return false;

			if (m_openList == null)
				return false;

			//validate input
			if (startRef == 0 || !m_nav.IsValidPolyRef(startRef))
				return false;

			Random randObj = new Random();
			PathfinderCommon.MeshTile startTile = null;
			PathfinderCommon.Poly startPoly = null;
			m_nav.GetTileAndPolyByRefUnsafe(startRef, ref startTile, ref startPoly);
			//NOTE: DON'T USE FILTER!
			//if (!filter.PassFilter(startPoly))
			//	return false;

			m_nodePool.Clear();
			m_openList.Clear();

			Node startNode = m_nodePool.GetNode(startRef);
			startNode.pos = centerPos;
			startNode.pidx = 0;
			startNode.cost = 0;
			startNode.total = 0;
			startNode.id = startRef;
			startNode.flags = NodeFlags.Open;
			m_openList.Push(startNode);

			float radiusSqr = radius * radius;
			float areaSum = 0.0f;

			PathfinderCommon.MeshTile randomTile = null;
			PathfinderCommon.Poly randomPoly = null;
			int randomPolyRef = 0;

			while (m_openList.Count > 0)
			{
				Node bestNode = m_openList.Pop();
				SetNodeFlagClosed(ref bestNode);

				//get poly and tile
				int bestRef = bestNode.id;
				PathfinderCommon.MeshTile bestTile = null;
				PathfinderCommon.Poly bestPoly = null;
				m_nav.GetTileAndPolyByRefUnsafe(bestRef, ref bestTile, ref bestPoly);

				//place random locations on ground
				if (bestPoly.GetPolyType() == PolygonType.Ground)
				{
					//calculate area of polygon
					float polyArea = 0.0f;
					float area;
					for (int j = 2; j < bestPoly.vertCount; j++)
					{
						Triangle3.Area2D(ref bestTile.verts[bestPoly.verts[0]], ref bestTile.verts[bestPoly.verts[j - 1]], ref bestTile.verts[bestPoly.verts[j]], out area);
						polyArea += area;
					}

					//choose random polygon weighted by area using resevoir sampling
					areaSum += polyArea;
					float u = (float)randObj.NextDouble();
					if (u * areaSum <= polyArea)
					{
						randomTile = bestTile;
						randomPoly = bestPoly;
						randomPolyRef = bestRef;
					}
				}

				//get parent poly and tile
				int parentRef = 0;
				PathfinderCommon.MeshTile parentTile = null;
				PathfinderCommon.Poly parentPoly = null;
				if (bestNode.pidx != 0)
					parentRef = m_nodePool.GetNodeAtIdx(bestNode.pidx).id;
				if (parentRef != 0)
					m_nav.GetTileAndPolyByRefUnsafe(parentRef, ref parentTile, ref parentPoly);

				for (int i = bestPoly.firstLink; i != PathfinderCommon.NULL_LINK; i = bestTile.links[i].next)
				{
					PathfinderCommon.Link link = bestTile.links[i];
					int neighbourRef = link.reference;
					//skip invalid neighbours and do not follor back to parent
					if (neighbourRef == 0 || neighbourRef == parentRef)
						continue;

					//expand to neighbour
					PathfinderCommon.MeshTile neighbourTile = null;
					PathfinderCommon.Poly neighbourPoly = null;
					m_nav.GetTileAndPolyByRefUnsafe(neighbourRef, ref neighbourTile, ref neighbourPoly);

					//do not advance if polygon is excluded by filter
					//if (!filter.PassFilter(neighbourPoly))
					//	continue;

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

					Node neighbourNode = m_nodePool.GetNode(neighbourRef);
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
					neighbourNode.pidx = m_nodePool.GetNodeIdx(bestNode);
					neighbourNode.total = total;

					if (IsInOpenList(neighbourNode))
					{
						m_openList.Modify(neighbourNode);
					}
					else
					{
						neighbourNode.flags = NodeFlags.Open;
						m_openList.Push(neighbourNode);
					}
				}
			}

			if (randomPoly == null)
				return false;

			//randomly pick point on polygon
			Vector3[] verts = new Vector3[PathfinderCommon.VERTS_PER_POLYGON];
			float[] areas = new float[PathfinderCommon.VERTS_PER_POLYGON];
			for (int j = 0; j < randomPoly.vertCount; j++)
			{
				verts[j] = randomTile.verts[randomPoly.verts[j]];
			}

			float s = (float)randObj.NextDouble();
			float t = (float)randObj.NextDouble();

			Vector3 pt = new Vector3();
			PathfinderCommon.RandomPointInConvexPoly(verts, randomPoly.vertCount, areas, s, t, ref pt);

			float h = 0.0f;
			if (!GetPolyHeight(randomPolyRef, pt, ref h))
				return false;

			pt.Y = h;

			randomPt = pt;
			randomRef = randomPolyRef;
			
			return true;
		}

		/// <summary>
		/// Find a path from the start polygon to the end polygon.
		/// -If the end polygon can't be reached, the last polygon will be nearest the end polygon
		/// -If the path array is too small, it will be filled as far as possible 
		/// -start and end positions are used to calculate traversal costs
		/// </summary>
		public bool FindPath(int startRef, int endRef, ref Vector3 startPos, ref Vector3 endPos, ref QueryFilter filter, int[] path, ref int pathCount, int maxPath)
		{
			pathCount = 0;

			if (startRef == 0 || endRef == 0)
				return false;

			if (maxPath == 0)
				return false;

			//validate input
			if (!m_nav.IsValidPolyRef(startRef) || !m_nav.IsValidPolyRef(endRef))
				return false;

			if (startRef == endRef)
			{
				path[0] = startRef;
				pathCount = 1;
				return true;
			}

			m_nodePool.Clear();
			m_openList.Clear();

			Node startNode = m_nodePool.GetNode(startRef);
			startNode.pos = startPos;
			startNode.pidx = 0;
			startNode.cost = 0;
			startNode.total = (startPos - endPos).Length() * H_SCALE;
			startNode.id = startRef;
			startNode.flags = NodeFlags.Open;
			m_openList.Push(startNode);

			Node lastBestNode = startNode;
			float lastBestTotalCost = startNode.total;

			while (m_openList.Count > 0)
			{
				//remove node from open list and put it in closed list
				Node bestNode = m_openList.Pop();
				SetNodeFlagClosed(ref bestNode);

				//reached the goal. stop searching
				if (bestNode.id == endRef)
				{
					lastBestNode = bestNode;
					break;
				}

				//get current poly and tile
				int bestRef = bestNode.id;
				PathfinderCommon.MeshTile bestTile = null;
				PathfinderCommon.Poly bestPoly = null;
				m_nav.GetTileAndPolyByRefUnsafe(bestRef, ref bestTile, ref bestPoly);

				//get parent poly and tile
				int parentRef = 0;
				PathfinderCommon.MeshTile parentTile = null;
				PathfinderCommon.Poly parentPoly = null;
				if (bestNode.pidx != 0)
					parentRef = m_nodePool.GetNodeAtIdx(bestNode.pidx).id;
				if (parentRef != 0)
					m_nav.GetTileAndPolyByRefUnsafe(parentRef, ref parentTile, ref parentPoly);

				for (int i = bestPoly.firstLink; i != PathfinderCommon.NULL_LINK; i = bestTile.links[i].next)
				{
					int neighbourRef = bestTile.links[i].reference;

					//skip invalid ids and do not expand back to where we came from
					if (neighbourRef == 0 || neighbourRef == parentRef)
						continue;

					//get neighbour poly and tile
					PathfinderCommon.MeshTile neighbourTile = null;
					PathfinderCommon.Poly neighbourPoly = null;
					m_nav.GetTileAndPolyByRefUnsafe(neighbourRef, ref neighbourTile, ref neighbourPoly);

					//if (!filter.PassFilter(neighbourPoly))
					//	continue;

					Node neighbourNode = m_nodePool.GetNode(neighbourRef);
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
						float curCost = filter.GetCost(bestNode.pos, neighbourNode.pos, bestPoly);
						float endCost = filter.GetCost(neighbourNode.pos, endPos, neighbourPoly);

						cost = bestNode.cost + curCost + endCost;
						heuristic = 0;
					}
					else
					{
						//cost
						float curCost = filter.GetCost(bestNode.pos, neighbourNode.pos, bestPoly);
						
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
					neighbourNode.pidx = m_nodePool.GetNodeIdx(bestNode);
					neighbourNode.id = neighbourRef;
					neighbourNode.flags = RemoveNodeFlagClosed(neighbourNode);
					neighbourNode.cost = cost;
					neighbourNode.total = total;

					if (IsInOpenList(neighbourNode))
					{
						//already in open, update node location
						m_openList.Modify(neighbourNode);
					}
					else
					{
						//put the node in the open list
						SetNodeFlagOpen(ref neighbourNode);
						m_openList.Push(neighbourNode);
					}

					//update nearest node to target so far
					if (heuristic < lastBestTotalCost)
					{
						lastBestTotalCost = heuristic;
						lastBestNode = neighbourNode;
					}
				}
			}

			//reverse the path
			Node prev = null;
			Node node = lastBestNode;
			do
			{
				Node next = m_nodePool.GetNodeAtIdx(node.pidx);
				node.pidx = m_nodePool.GetNodeIdx(prev);
				prev = node;
				node = next;
			} while (node != null);
			
			//store path
			node = prev;
			int n = 0;
			do
			{
				path[n++] = node.id;
				if (n >= maxPath)
					break;

				node = m_nodePool.GetNodeAtIdx(node.pidx);
			}
			while (node != null);

			pathCount = n;

			return true;
		}

		/// <summary>
		/// Add vertices and portals to a regular path computed from the method FindPath().
		/// </summary>
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

			stat = AppendVertex(closestStartPos, PathfinderCommon.STRAIGHTPATH_START, path[0], 
				straightPath, straightPathFlags, straightPathRefs, ref straightPathCount, maxStraightPath);

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
								stat = AppendPortals(apexIndex, i, closestEndPos, path,
									straightPath, straightPathFlags, straightPathRefs, ref straightPathCount, maxStraightPath, options);
							}

							stat = AppendVertex(closestEndPos, 0, path[i],
								straightPath, straightPathFlags, straightPathRefs, ref straightPathCount, maxStraightPath);

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
								stat = AppendPortals(apexIndex, leftIndex, portalLeft, path,
									straightPath, straightPathFlags, straightPathRefs, ref straightPathCount, maxStraightPath, options);

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
							stat = AppendVertex(portalApex, flags, reference,
								straightPath, straightPathFlags, straightPathRefs, ref straightPathCount, maxStraightPath);

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
								stat = AppendPortals(apexIndex, rightIndex, portalRight, path,
									straightPath, straightPathFlags, straightPathRefs, ref straightPathCount, maxStraightPath, options);

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
							stat = AppendVertex(portalApex, flags, reference,
								straightPath, straightPathFlags, straightPathRefs, ref straightPathCount, maxStraightPath);

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
					stat = AppendPortals(apexIndex, pathSize - 1, closestEndPos, path,
						straightPath, straightPathFlags, straightPathRefs, ref straightPathCount, maxStraightPath, options);

					if (stat != true)
						return true;
				}
			}

			stat = AppendVertex(closestEndPos, PathfinderCommon.STRAIGHTPATH_END, 0,
				straightPath, straightPathFlags, straightPathRefs, ref straightPathCount, maxStraightPath);

			return true;
		}

		/// <summary>
		/// Get edge midpoint between two prolygons
		/// </summary>
		public bool GetEdgeMidPoint(int from, PathfinderCommon.Poly fromPoly, PathfinderCommon.MeshTile fromTile, int to, PathfinderCommon.Poly toPoly, PathfinderCommon.MeshTile toTile, ref Vector3 mid)
		{
			Vector3 left = new Vector3();
			Vector3 right = new Vector3();
			if (!GetPortalPoints(from, fromPoly, fromTile, to, toPoly, toTile, ref left, ref right))
				return false;

			mid.X = (left.X + right.X) * 0.5f;
			mid.Y = (left.Y + right.Y) * 0.5f;
			mid.Z = (left.Z + right.Z) * 0.5f;

			return true;
		}

		public bool GetPortalPoints(int from, int to, ref Vector3 left, ref Vector3 right, ref PolygonType fromType, ref PolygonType toType)
		{
			PathfinderCommon.MeshTile fromTile = null;
			PathfinderCommon.Poly fromPoly = null;
			if (m_nav.GetTileAndPolyByRef(from, ref fromTile, ref fromPoly) == false)
				return false;
			fromType = fromPoly.GetPolyType();

			PathfinderCommon.MeshTile toTile = null;
			PathfinderCommon.Poly toPoly = null;
			if (m_nav.GetTileAndPolyByRef(to, ref toTile, ref toPoly) == false)
				return false;
			toType = toPoly.GetPolyType();

			return GetPortalPoints(from, fromPoly, fromTile, to, toPoly, toTile, ref left, ref right);
		}

		public bool GetPortalPoints(int from, PathfinderCommon.Poly fromPoly, PathfinderCommon.MeshTile fromTile, int to, PathfinderCommon.Poly toPoly, PathfinderCommon.MeshTile toTile, ref Vector3 left, ref Vector3 right)
		{
			//find the link that points to the 'to' polygon
			PathfinderCommon.Link link = null;
			for (int i = fromPoly.firstLink; i != PathfinderCommon.NULL_LINK; i = fromTile.links[i].next)
			{
				if (fromTile.links[i].reference == to)
				{
					link = fromTile.links[i];
					break;
				}
			}

			if (link == null)
				return false;

			//handle off-mesh connections
			if (fromPoly.GetPolyType() == PolygonType.OffMeshConnection)
			{
				//find link that points to first vertex
				for (int i = fromPoly.firstLink; i != PathfinderCommon.NULL_LINK; i = fromTile.links[i].next)
				{
					if (fromTile.links[i].reference == to)
					{
						int v = fromTile.links[i].edge;
						left = fromTile.verts[fromPoly.verts[v]];
						right = fromTile.verts[fromPoly.verts[v]];
						return true;
					}
				}

				return false;
			}

			if (toPoly.GetPolyType() == PolygonType.OffMeshConnection)
			{
				//find link that points to first vertex
				for (int i = toPoly.firstLink; i != PathfinderCommon.NULL_LINK; i = toTile.links[i].next)
				{
					if (toTile.links[i].reference == from)
					{
						int v = toTile.links[i].edge;
						left = toTile.verts[toPoly.verts[v]];
						right = toTile.verts[toPoly.verts[v]];
						return true;
					}
				}

				return false;
			}

			//find portal vertices
			int v0 = fromPoly.verts[link.edge];
			int v1 = fromPoly.verts[(link.edge + 1) % fromPoly.vertCount];
			left = fromTile.verts[v0];
			right = fromTile.verts[v1];

			//if the link is at the tile boundary, clamp the vertices to tile width
			if (link.side != 0xff)
			{
				//unpack portal limits
				if (link.bmin != 0 || link.bmax != 255)
				{
					float s = 1.0f / 255.0f;
					float tmin = link.bmin * s;
					float tmax = link.bmax * s;
					left = Vector3.Lerp(fromTile.verts[v0], fromTile.verts[v1], tmin);
					right = Vector3.Lerp(fromTile.verts[v0], fromTile.verts[v1], tmax);
				}
			}

			return true;
		}

		public bool ClosestPointOnPolyBoundary(int reference, Vector3 pos, ref Vector3 closest)
		{
			PathfinderCommon.MeshTile tile = null;
			PathfinderCommon.Poly poly = null;
			if (m_nav.GetTileAndPolyByRef(reference, ref tile, ref poly) == false)
				return false;

			Vector3[] verts = new Vector3[PathfinderCommon.VERTS_PER_POLYGON];
			float[] edged = new float[PathfinderCommon.VERTS_PER_POLYGON];
			float[] edget = new float[PathfinderCommon.VERTS_PER_POLYGON];
			int nv = 0;
			for (int i = 0; i < poly.vertCount; i++)
			{
				verts[nv] = tile.verts[poly.verts[i]];
				nv++;
			}

			bool inside = PathfinderCommon.DistancePointPolyEdgesSquare(pos, verts, nv, edged, edget);
			if (inside)
			{
				//point is inside the polygon
				closest = pos;
			}
			else
			{
				//point is outside the polygon
				//clamp to nearest edge
				float dmin = float.MaxValue;
				int imin = -1;
				for (int i = 0; i < nv; i++)
				{
					if (edged[i] < dmin)
					{
						dmin = edged[i];
						imin = i;
					}
				}
				Vector3 va = verts[imin];
				Vector3 vb = verts[(imin + 1) % nv];
				closest = Vector3.Lerp(va, vb, edget[imin]);
			}

			return true;
		}

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
				PathfinderCommon.MeshTile fromTile = null;
				PathfinderCommon.Poly fromPoly = null;
				if (m_nav.GetTileAndPolyByRef(from, ref fromTile, ref fromPoly) == false)
					return false;

				int to = path[i + 1];
				PathfinderCommon.MeshTile toTile = null;
				PathfinderCommon.Poly toPoly = null;
				if (m_nav.GetTileAndPolyByRef(to, ref toTile, ref toPoly) == false)
					return false;

				Vector3 left = new Vector3();
				Vector3 right = new Vector3();
				if (GetPortalPoints(from, fromPoly, fromTile, to, toPoly, toTile, ref left, ref right) == false)
					break;

				if ((options & PathfinderCommon.STRAIGHTPATH_AREA_CROSSINGS) != 0)
				{
					//skip intersection if only area crossings are requested
					if (fromPoly.GetArea() == toPoly.GetArea())
						continue;
				}

				//append intersection
				float s, t;
				if (MathHelper.Intersection.SegmentSegment2D(ref startPos, ref endPos, ref left, ref right, out s, out t))
				{
					Vector3 pt = Vector3.Lerp(left, right, t);

					stat = AppendVertex(pt, 0, path[i + 1],
						straightPath, straightPathFlags, straightPathRefs, ref straightPathCount, maxStraightPath);

					if (stat != true)
						return true;
				}
			}

			return true;
		}

		/// <summary>
		/// Return false if the provided position is outside the xz-bounds
		/// </summary>
		public bool GetPolyHeight(int reference, Vector3 pos, ref float height)
		{
			if (m_nav == null)
				return false;

			PathfinderCommon.MeshTile tile = null;
			PathfinderCommon.Poly poly = null;
			if (!m_nav.GetTileAndPolyByRef(reference, ref tile, ref poly))
				return false;

			//off-mesh connections don't have detail polygons
			if (poly.GetPolyType() == PolygonType.OffMeshConnection)
			{
				Vector3 v0 = tile.verts[poly.verts[0]];
				Vector3 v1 = tile.verts[poly.verts[1]];
				float d0 = (pos - v0).Length();
				float d1 = (pos - v1).Length();
				float u = d0 / (d0 + d1);
				height = v0.Y + (v1.Y - v0.Y) * u;
				return true;
			}
			else
			{
				int indexPoly = 0;
				for (int i = 0; i < tile.polys.Length; i++)
				{
					if (tile.polys[i] == poly)
					{
						indexPoly = i;
						break;
					}
				}

				PathfinderCommon.PolyDetail pd = tile.detailMeshes[indexPoly];

				//find height at the location
				for (int j = 0; j < pd.triCount; j++)
				{
					PolyMeshDetail.TriangleData t = tile.detailTris[pd.triBase + j];
					Vector3[] v = new Vector3[3];

					for (int k = 0; k < 3; k++)
					{
						if (t[k] < poly.vertCount)
							v[k] = tile.verts[poly.verts[t[k]]];
						else
							v[k] = tile.detailVerts[pd.vertBase + (t[k] - poly.vertCount)];
					}

					float h = 0;
					if (PathfinderCommon.ClosestHeightPointTriangle(pos, v[0], v[1], v[2], ref h))
					{
						height = h;
						return true;
					}
				}
			}

			return false;
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

		public struct QueryData
		{
			public Node lastBestNode;
			public float lastBestNodeCost;
			public int startRef, endRef;
			public Vector3 startPos, endPos;
			public QueryFilter filter;
		}
	}
}
