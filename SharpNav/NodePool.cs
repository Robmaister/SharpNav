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
		private int hashSize;
		private List<Node> nodes;
		private Dictionary<int, Node> nodeDict;
		private int maxNodes;

		public NodePool(int maxNodes, int hashSize)
		{
			this.maxNodes = maxNodes;
			this.hashSize = hashSize;

			nodes = new List<Node>(maxNodes);
			nodeDict = new Dictionary<int, Node>(new IntNodeIdComparer(hashSize));
		}

		public void Clear()
		{
			nodes.Clear();
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

			if (nodes.Count >= maxNodes)
				return null;

			Node newNode = new Node();
			newNode.pidx = 0;
			newNode.cost = 0;
			newNode.total = 0;
			newNode.id = id;
			newNode.flags = 0;
			
			nodes.Add(newNode);
			nodeDict.Add(id, newNode);

			return newNode;
		}

		public int GetNodeIdx(Node node)
		{
			if (node == null)
				return 0;

			for (int i = 0; i < nodes.Count; i++)
			{
				if (nodes[i] == node)
					return i + 1;
			}

			return 0;
		}

		public Node GetNodeAtIdx(int idx)
		{
			if (idx <= 0 || idx > nodes.Count)
				return null;

			return nodes[idx - 1]; 
		}

		private class IntNodeIdComparer : IEqualityComparer<int>
		{
			private int hashSize;

			public IntNodeIdComparer(int hashSize)
			{
				this.hashSize = hashSize;
			}

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

				return obj & (hashSize - 1);
			}
		}
	}
}
