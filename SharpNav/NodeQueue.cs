#region License
/**
 * Copyright (c) 2013 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
 * Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE
 */
#endregion

using System;
using System.Collections.Generic;

namespace SharpNav
{
	/// <summary>
	/// Use a priority queue (heap) to determine which node is more important.
	/// </summary>
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
