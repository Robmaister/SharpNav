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

					if (!filter.PassFilter(neighbourPoly))
						continue;

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
					neighbourNode.flags = (neighbourNode.flags & ~NodeCommon.NODE_CLOSED);
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
				{
					break;
				}
				node = m_nodePool.GetNodeAtIdx(node.pidx);
			} while (node != null);

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
					PathfinderCommon.VectorLinearInterpolation(ref left, fromTile.verts[v0], fromTile.verts[v1], tmin);
					PathfinderCommon.VectorLinearInterpolation(ref right, fromTile.verts[v0], fromTile.verts[v1], tmax);
				}
			}

			return true;
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
