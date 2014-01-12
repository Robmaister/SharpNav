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
		private Node[] m_nodes;
		private int[] m_first;
		private int[] m_next;
		private int m_maxNodes;
		private int m_hashSize;
		private int m_nodeCount;

		public const int NULL_IDX = ~0;

		public NodePool(int maxNodes, int hashSize)
		{
			m_maxNodes = maxNodes;
			m_hashSize = hashSize;

			m_nodes = new Node[m_maxNodes];
			m_next = new int[m_maxNodes];
			m_first = new int[hashSize];

			for (int i = 0; i < hashSize; i++)
				m_first[i] = 0xff;
			for (int i = 0; i < m_maxNodes; i++)
				m_next[i] = 0xff;
		}

		public int HashRef(int a)
		{
			a += ~(a << 15);
			a ^= a >> 10;
			a += a << 3;
			a ^= a >> 6;
			a += ~(a << 11);
			a ^= a >> 16;

			return a;
		}

		public void Clear()
		{
			for (int i = 0; i < m_hashSize; i++)
				m_first[i] = 0xff;

			m_nodeCount = 0;
		}

		public Node FindNode(int id)
		{
			int bucket = HashRef(id) & (m_hashSize - 1);
			int i = m_first[bucket];

			while (i != NULL_IDX)
			{
				if (m_nodes[i].id == id)
					return m_nodes[i];

				i = m_next[i];
			}

			return null;
		}

		public Node GetNode(int id)
		{
			int bucket = HashRef(id) & (m_hashSize - 1);
			int i = m_first[bucket];

			while (i != NULL_IDX)
			{
				if (m_nodes[i] == null)
					break;

				if (m_nodes[i].id == id)
					return m_nodes[i];

				i = m_next[i];
			}

			if (m_nodeCount >= m_maxNodes)
				return null;

			i = m_nodeCount;
			m_nodeCount++;

			m_nodes[i] = new Node();
			m_nodes[i].pidx = 0;
			m_nodes[i].cost = 0;
			m_nodes[i].total = 0;
			m_nodes[i].id = id;
			m_nodes[i].flags = 0;

			m_next[i] = m_first[bucket];
			m_first[bucket] = i;

			return m_nodes[i];
		}

		public int GetNodeIdx(Node node)
		{
			if (node == null)
				return 0;

			for (int i = 0; i < m_nodes.Length; i++)
			{
				if (m_nodes[i] == node)
					return i + 1;
			}

			return 0;
		}

		public Node GetNodeAtIdx(int idx)
		{
			if (idx <= 0 || idx > m_maxNodes)
				return null;

			return m_nodes[idx - 1];
		}

		public int MaxNodes { get { return m_maxNodes; } }
		public int HashSize { get { return m_hashSize; } }

		public int GetFirst(int bucket)
		{
			return m_first[bucket];
		}

		public int GetNext(int i)
		{
			return m_next[i];
		}
	}
}
