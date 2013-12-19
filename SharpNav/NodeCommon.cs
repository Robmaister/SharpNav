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
	/// Node class: Every polygon becomes a Node, which contains a position and cost.
	/// NodePool: Link all nodes together. Store indices in hash map.
	/// NodeQueue: Use a priority queue (heap) to determine which node is more important.
	/// </summary>
	public class NodeCommon
	{
		public const int NODE_OPEN = 0x01;
		public const int NODE_CLOSED = 0x02;

		public class Node
		{
			public Vector3 pos;
			public float cost;
			public float total;
			public int pidx = 30; //index to parent node
			public int flags = 2; //node flags 0/open/closed
			public uint id; //polygon ref the node corresponds to
		}

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
			
			public uint HashRef(uint a)
			{
				a += ~(a << 15);
				a ^= (a >> 10);
				a += (a << 3);
				a ^= (a >> 6);
				a += ~(a << 11);
				a ^= (a >> 16);

				return a;
			}

			public void Clear()
			{
				for (int i = 0; i < m_hashSize; i++)
					m_first[i] = 0xff;

				m_nodeCount = 0;
			}

			public Node FindNode(uint id)
			{
				uint bucket = HashRef(id) & ((uint)m_hashSize - 1);
				int i = m_first[bucket];
				
				while (i != NULL_IDX)
				{
					if (m_nodes[i].id == id)
						return m_nodes[i];

					i = m_next[i];
				}

				return null;
			}

			public Node GetNode(uint id)
			{
				uint bucket = HashRef(id) & ((uint)m_hashSize - 1);
				int i = m_first[bucket];
				Node node = null;

				while (i != NULL_IDX)
				{
					if (m_nodes[i].id == id)
						return m_nodes[i];

					i = m_next[i];
				}

				if (m_nodeCount >= m_maxNodes)
					return null;

				i = m_nodeCount;
				m_nodeCount++;

				node = m_nodes[i];
				node.pidx = 0;
				node.cost = 0;
				node.total = 0;
				node.id = id;
				node.flags = 0;
				m_nodes[i] = node;

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

		public class NodeQueue
		{
			private Node[] m_heap;
			private int m_capacity;
			private int m_size;

			public NodeQueue(int n)
			{
				m_capacity = n;
				m_size = 0;
				m_heap = new Node[m_capacity + 1];
			}

			public void Clear()
			{
				m_size = 0;
			}

			public Node Top()
			{
				return m_heap[0];
			}

			public Node Pop()
			{
				Node result = m_heap[0];
				m_size--;
				TrickleDown(0, m_heap[m_size]);
				return result;
			}

			public void Push(Node node)
			{
				m_size++;
				BubbleUp(m_size - 1, node);
			}

			public void Modify(Node node)
			{
				for (int i = 0; i < m_size; i++)
				{
					if (m_heap[i] == node)
					{
						BubbleUp(i, node);
						return;
					}
				}
			}

			public bool Empty()
			{
				return m_size == 0;
			}

			public int GetCapacity()
			{
				return m_capacity;
			}

			public void BubbleUp(int i, Node node)
			{
				int parent = (i - 1) / 2;
				
				while ((i > 0) && (m_heap[parent].total > node.total))
				{
					m_heap[i] = m_heap[parent];
					i = parent;
					parent = (i - 1) / 2;
				}
				
				m_heap[i] = node;
			}

			public void TrickleDown(int i, Node node)
			{
				int child = (i * 2) + 1;
				
				while (child < m_size)
				{
					if (((child + 1) < m_size) && (m_heap[child].total > m_heap[child + 1].total))
					{
						child++;
					}

					m_heap[i] = m_heap[child];
					i = child;
					child = (i * 2) + 1;
				}

				BubbleUp(i, node);
			}
		}
	}
}
