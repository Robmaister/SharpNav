#region License
/**
 * Copyright (c) 2013 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
 * Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE
 */
#endregion

using System;
using System.Collections.Generic;

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
		private NodeCommon.NodePool m_tinyNodePool;
		private NodeCommon.NodePool m_nodePool;
		private NodeCommon.NodeQueue m_openList;
		private QueryData m_query;

		public NavMeshQuery(TiledNavMesh nav, int maxNodes)
		{
			m_nav = nav;

			m_nodePool = new NodeCommon.NodePool(maxNodes, (int)PathfinderCommon.NextPow2((uint)maxNodes / 4));
			m_tinyNodePool = new NodeCommon.NodePool(64, 32);
			m_openList = new NodeCommon.NodeQueue(maxNodes);
		}

		public bool FindRandomPoint(ref QueryFilter filter, ref uint randomRef, ref Vector3 randomPt)
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
			uint polyRef = 0;
			uint polyBase = m_nav.GetPolyRefBase(tile);

			float areaSum = 0.0f;
			for (int i = 0; i < tile.header.polyCount; i++)
			{
				PathfinderCommon.Poly p = tile.polys[i];

				//don't return off-mesh connection polygons
				if (p.GetType() != PathfinderCommon.POLTYPE_GROUND)
					continue;

				//NOTE: polygon flags are never set so the filter will remove all polygons
				uint reference = polyBase | (uint)i;
				//if (filter.PassFilter(p) == false)
				//	continue;

				//calculate area of polygon
				float polyArea = 0.0f;
				for (int j = 2; j < p.vertCount; j++)
				{
					Vector3 va = tile.verts[p.verts[0]];
					Vector3 vb = tile.verts[p.verts[j - 1]];
					Vector3 vc = tile.verts[p.verts[j]];
					polyArea += PathfinderCommon.TriangleArea2D(va, vb, vc);
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
			if (GetPolyHeight(polyRef, pt, ref h) == false)
				return false;

			pt.Y = h;

			randomPt = pt;
			randomRef = polyRef;

			return true;
		}

		public bool FindRandomPointAroundCircle(uint startRef, Vector3 centerPos, float radius, ref QueryFilter filter, 
			ref uint randomRef, ref Vector3 randomPt)
		{
			if (m_nav == null)
				return false;

			if (m_nodePool == null)
				return false;

			if (m_openList == null)
				return false;

			//validate input
			if (startRef == 0 || m_nav.IsValidPolyRef(startRef) == false)
				return false;

			Random randObj = new Random();
			PathfinderCommon.MeshTile startTile = null;
			PathfinderCommon.Poly startPoly = null;
			m_nav.GetTileAndPolyByRefUnsafe(startRef, ref startTile, ref startPoly);
			//NOTE: DON'T USE FILTER!
			//if (filter.PassFilter(startPoly) == false)
			//	return false;

			m_nodePool.Clear();
			m_openList.Clear();

			NodeCommon.Node startNode = m_nodePool.GetNode(startRef);
			startNode.pos = centerPos;
			startNode.pidx = 0;
			startNode.cost = 0;
			startNode.total = 0;
			startNode.id = startRef;
			startNode.flags = NodeCommon.NODE_OPEN;
			m_openList.Push(startNode);

			float radiusSqr = radius * radius;
			float areaSum = 0.0f;

			PathfinderCommon.MeshTile randomTile = null;
			PathfinderCommon.Poly randomPoly = null;
			uint randomPolyRef = 0;

			while (m_openList.Empty() == false)
			{
				NodeCommon.Node bestNode = m_openList.Pop();
				bestNode.flags &= ~NodeCommon.NODE_OPEN;
				bestNode.flags |= NodeCommon.NODE_CLOSED;

				//get poly and tile
				uint bestRef = bestNode.id;
				PathfinderCommon.MeshTile bestTile = null;
				PathfinderCommon.Poly bestPoly = null;
				m_nav.GetTileAndPolyByRefUnsafe(bestRef, ref bestTile, ref bestPoly);

				//place random locations on ground
				if (bestPoly.GetType() == PathfinderCommon.POLTYPE_GROUND)
				{
					//calculate area of polygon
					float polyArea = 0.0f;
					for (int j = 2; j < bestPoly.vertCount; j++)
					{
						Vector3 va = bestTile.verts[bestPoly.verts[0]];
						Vector3 vb = bestTile.verts[bestPoly.verts[j - 1]];
						Vector3 vc = bestTile.verts[bestPoly.verts[j]];
						polyArea += PathfinderCommon.TriangleArea2D(va, vb, vc);
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
				uint parentRef = 0;
				PathfinderCommon.MeshTile parentTile = null;
				PathfinderCommon.Poly parentPoly = null;
				if (bestNode.pidx != 0)
					parentRef = m_nodePool.GetNodeAtIdx(bestNode.pidx).id;
				if (parentRef != 0)
					m_nav.GetTileAndPolyByRefUnsafe(parentRef, ref parentTile, ref parentPoly);

				for (uint i = bestPoly.firstLink; i != PathfinderCommon.NULL_LINK; i = bestTile.links[i].next)
				{
					PathfinderCommon.Link link = bestTile.links[i];
					uint neighbourRef = link.reference;
					//skip invalid neighbours and do not follor back to parent
					if (neighbourRef == 0 || neighbourRef == parentRef)
						continue;

					//expand to neighbour
					PathfinderCommon.MeshTile neighbourTile = null;
					PathfinderCommon.Poly neighbourPoly = null;
					m_nav.GetTileAndPolyByRefUnsafe(neighbourRef, ref neighbourTile, ref neighbourPoly);

					//do not advance if polygon is excluded by filter
					//if (filter.PassFilter(neighbourPoly) == false)
					//	continue;

					//find edge and calculate distance to edge
					Vector3 va = new Vector3();
					Vector3 vb = new Vector3();
					if (GetPortalPoints(bestRef, bestPoly, bestTile, neighbourRef, neighbourPoly, neighbourTile, ref va, ref vb) == false)
						continue;

				 	//if circle isn't touching next polygon, skip it
					float tseg = 0;
					float distSqr = PathfinderCommon.DistancePointSegmentSquare2D(centerPos, va, vb, ref tseg);
					if (distSqr > radiusSqr)
						continue;

					NodeCommon.Node neighbourNode = m_nodePool.GetNode(neighbourRef);
					if (neighbourNode == null)
						continue;

					if ((neighbourNode.flags & NodeCommon.NODE_CLOSED) != 0)
						continue;

					//cost
					if (neighbourNode.flags == 0)
						neighbourNode.pos = Vector3.Lerp(va, vb, 0.5f);

					float total = bestNode.total + (bestNode.pos - neighbourNode.pos).Length();

					//node is already in open list and new result is worse, so skip
					if ((neighbourNode.flags & NodeCommon.NODE_OPEN) != 0 && total >= neighbourNode.total)
						continue;

					neighbourNode.id = neighbourRef;
					neighbourNode.flags = neighbourNode.flags & ~NodeCommon.NODE_CLOSED;
					neighbourNode.pidx = m_nodePool.GetNodeIdx(bestNode);
					neighbourNode.total = total;

					if ((neighbourNode.flags & NodeCommon.NODE_OPEN) != 0)
					{
						m_openList.Modify(neighbourNode);
					}
					else
					{
						neighbourNode.flags = NodeCommon.NODE_OPEN;
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
			if (GetPolyHeight(randomPolyRef, pt, ref h) == false)
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
		public bool FindPath(uint startRef, uint endRef, ref Vector3 startPos, ref Vector3 endPos, 
			ref QueryFilter filter, uint[] path, ref int pathCount, int maxPath)
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

			NodeCommon.Node startNode = m_nodePool.GetNode(startRef);
			startNode.pos = startPos;
			startNode.pidx = 0;
			startNode.cost = 0;
			startNode.total = (startPos - endPos).Length() * H_SCALE;
			startNode.id = startRef;
			startNode.flags = NodeCommon.NODE_OPEN;
			m_openList.Push(startNode);

			NodeCommon.Node lastBestNode = startNode;
			float lastBestTotalCost = startNode.total;

			while (!m_openList.Empty())
			{
				//remove node from open list and put it in closed list
				NodeCommon.Node bestNode = m_openList.Pop();
				bestNode.flags &= ~NodeCommon.NODE_OPEN;
				bestNode.flags |= NodeCommon.NODE_CLOSED;

				//reached the goal. stop searching
				if (bestNode.id == endRef)
				{
					lastBestNode = bestNode;
					break;
				}

				//get current poly and tile
				uint bestRef = bestNode.id;
				PathfinderCommon.MeshTile bestTile = null;
				PathfinderCommon.Poly bestPoly = null;
				m_nav.GetTileAndPolyByRefUnsafe(bestRef, ref bestTile, ref bestPoly);

				//get parent poly and tile
				uint parentRef = 0;
				PathfinderCommon.MeshTile parentTile = null;
				PathfinderCommon.Poly parentPoly = null;
				if (bestNode.pidx != 0)
					parentRef = m_nodePool.GetNodeAtIdx(bestNode.pidx).id;
				if (parentRef != 0)
					m_nav.GetTileAndPolyByRefUnsafe(parentRef, ref parentTile, ref parentPoly);

				for (uint i = bestPoly.firstLink; i != PathfinderCommon.NULL_LINK; i = bestTile.links[i].next)
				{
					uint neighbourRef = bestTile.links[i].reference;

					//skip invalid ids and do not expand back to where we came from
					if (neighbourRef == 0 || neighbourRef == parentRef)
						continue;

					//get neighbour poly and tile
					PathfinderCommon.MeshTile neighbourTile = null;
					PathfinderCommon.Poly neighbourPoly = null;
					m_nav.GetTileAndPolyByRefUnsafe(neighbourRef, ref neighbourTile, ref neighbourPoly);

					//if (!filter.PassFilter(neighbourPoly))
					//	continue;

					NodeCommon.Node neighbourNode = m_nodePool.GetNode(neighbourRef);
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
					if ((neighbourNode.flags & NodeCommon.NODE_OPEN) != 0 && total >= neighbourNode.total)
						continue;

					//the node is already visited and processesd, and the new result is worse, skip
					if ((neighbourNode.flags & NodeCommon.NODE_CLOSED) != 0 && total >= neighbourNode.total)
						continue;

					//add or update the node
					neighbourNode.pidx = m_nodePool.GetNodeIdx(bestNode);
					neighbourNode.id = neighbourRef;
					neighbourNode.flags = neighbourNode.flags & ~NodeCommon.NODE_CLOSED;
					neighbourNode.cost = cost;
					neighbourNode.total = total;

					if ((neighbourNode.flags & NodeCommon.NODE_OPEN) != 0)
					{
						//already in open, update node location
						m_openList.Modify(neighbourNode);
					}
					else
					{
						//put the node in the open list
						neighbourNode.flags |= NodeCommon.NODE_OPEN;
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
			NodeCommon.Node prev = null;
			NodeCommon.Node node = lastBestNode;
			do
			{
				NodeCommon.Node next = m_nodePool.GetNodeAtIdx(node.pidx);
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
		/// Get edge midpoint between two prolygons
		/// </summary>
		public bool GetEdgeMidPoint(uint from, PathfinderCommon.Poly fromPoly, PathfinderCommon.MeshTile fromTile,
			uint to, PathfinderCommon.Poly toPoly, PathfinderCommon.MeshTile toTile, ref Vector3 mid)
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

		public bool GetPortalPoints(uint from, PathfinderCommon.Poly fromPoly, PathfinderCommon.MeshTile fromTile,
			uint to, PathfinderCommon.Poly toPoly, PathfinderCommon.MeshTile toTile, ref Vector3 left, ref Vector3 right)
		{
			//find the link that points to the 'to' polygon
			PathfinderCommon.Link link = null;
			for (uint i = fromPoly.firstLink; i != PathfinderCommon.NULL_LINK; i = fromTile.links[i].next)
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
			if (fromPoly.GetType() == PathfinderCommon.POLTYPE_OFFMESH_CONNECTION)
			{
				//find link that points to first vertex
				for (uint i = fromPoly.firstLink; i != PathfinderCommon.NULL_LINK; i = fromTile.links[i].next)
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

			if (toPoly.GetType() == PathfinderCommon.POLTYPE_OFFMESH_CONNECTION)
			{
				//find link that points to first vertex
				for (uint i = toPoly.firstLink; i != PathfinderCommon.NULL_LINK; i = toTile.links[i].next)
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

		/// <summary>
		/// Return false if the provided position is outside the xz-bounds
		/// </summary>
		public bool GetPolyHeight(uint reference, Vector3 pos, ref float height)
		{
			if (m_nav == null)
				return false;

			PathfinderCommon.MeshTile tile = null;
			PathfinderCommon.Poly poly = null;
			if (m_nav.GetTileAndPolyByRef(reference, ref tile, ref poly) == false)
				return false;

			//off-mesh connections don't have detail polygons
			if (poly.GetType() == PathfinderCommon.POLTYPE_OFFMESH_CONNECTION)
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
					NavMeshDetail.TrisInfo t = tile.detailTris[pd.triBase + j];
					Vector3[] v = new Vector3[3];

					for (int k = 0; k < 3; k++)
					{
						if (t.VertexHash[k] < poly.vertCount)
							v[k] = tile.verts[poly.verts[t.VertexHash[k]]];
						else
							v[k] = tile.detailVerts[pd.vertBase + (t.VertexHash[k] - poly.vertCount)];
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

		public struct QueryData
		{
			public NodeCommon.Node lastBestNode;
			public float lastBestNodeCost;
			public uint startRef, endRef;
			public Vector3 startPos, endPos;
			public QueryFilter filter;
		}
	}
}
