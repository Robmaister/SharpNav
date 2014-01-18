#region License
/**
 * Copyright (c) 2013-2014 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
 * Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE
 */
#endregion

using System;
using System.Collections.Generic;

namespace SharpNav
{
	/// <summary>
	/// Link all nodes together. Store indices in hash map.
	/// </summary>
	public class NodePool
	{
		private List<Node> m_nodes;
		private Dictionary<int, Node> nodeDict;
		private int m_maxNodes;
		private static int m_hashSize;

		public NodePool(int maxNodes, int hashSize)
		{
			m_maxNodes = maxNodes;
			m_hashSize = hashSize;

			m_nodes = new List<Node>(m_maxNodes);
			nodeDict = new Dictionary<int, Node>(new IntNodeIdComparer());
		}

		public void Clear()
		{
			m_nodes.Clear();
			nodeDict.Clear();
		}

		public Node FindNode(int id)
		{
			Node node;
			if (nodeDict.TryGetValue(id, out node))
			{
				return node;
			}

			return null;
		}

		public Node GetNode(int id)
		{
			Node node;
			if (nodeDict.TryGetValue(id, out node))
			{
				return node;
			}

			if (m_nodes.Count >= m_maxNodes)
				return null;

			Node newNode = new Node();
			newNode.pidx = 0;
			newNode.cost = 0;
			newNode.total = 0;
			newNode.id = id;
			newNode.flags = 0;
			
			m_nodes.Add(newNode);
			nodeDict.Add(id, newNode);

			return newNode;
		}

		public int GetNodeIdx(Node node)
		{
			if (node == null)
				return 0;

			for (int i = 0; i < m_nodes.Count; i++)
			{
				if (m_nodes[i] == node)
					return i + 1;
			}

			return 0;
		}

		public Node GetNodeAtIdx(int idx)
		{
			if (idx <= 0 || idx > m_nodes.Count)
				return null;

			return m_nodes[idx - 1]; 
		}

		private class IntNodeIdComparer : IEqualityComparer<int>
		{
			public bool Equals(int left, int right)
			{
				return left == right;
			}

			public int GetHashCode(int obj)
			{
				obj += ~(obj << 15);
				obj ^= obj >> 10;
				obj += obj << 3;
				obj ^= obj >> 6;
				obj += ~(obj << 11);
				obj ^= obj >> 16;

				return obj & (m_hashSize - 1);
			}
		}
	}
}
