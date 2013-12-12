#region License
/**
 * Copyright (c) 2013 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
 * Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE
 */
#endregion

using System;
using System.Collections.Generic;
using SharpNav.Geometry;

namespace SharpNav
{
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

		public void FindPath(uint startRef, uint endRef, ref Vector3 startPos, ref Vector3 endPos, 
			ref QueryFilter filter, uint[] path, ref int pathCount, int maxPath)
		{
			pathCount = 0;

			if (startRef == endRef)
			{
				path[0] = startRef;
				pathCount = 1;
				return;
			}

			m_nodePool.Clear();
			m_openList.Clear();

			NodeCommon.Node startNode = m_nodePool.GetNode(startRef);
			startNode.pos = startPos;
			startNode.pidx = 0;
			startNode.cost = 0;
			startNode.total = (new Vector3(startPos - endPos).Length) * H_SCALE;
			startNode.id = startRef;
			startNode.flags = NodeCommon.NODE_OPEN;
			m_openList.Push(startNode);

			NodeCommon.Node lastBestNode = startNode;
			float lastBestTotalCost = startNode.total;

			//do actual calculations
			//...
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
