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

		/// <summary>
		/// Initializes a new instance of the <see cref="NodePool"/> class.
		/// </summary>
		/// <param name="maxNodes">The maximum number of nodes that can be stored</param>
		/// <param name="hashSize">The maximum number of elements in the hash table</param>
		public NodePool(int maxNodes, int hashSize)
		{
			this.maxNodes = maxNodes;
			this.hashSize = hashSize;

			nodes = new List<Node>(maxNodes);
			nodeDict = new Dictionary<int, Node>(new IntNodeIdComparer(hashSize));
		}

		/// <summary>
		/// Reset all the data.
		/// </summary>
		public void Clear()
		{
			nodes.Clear();
			nodeDict.Clear();
		}

		/// <summary>
		/// Try to find a node.
		/// </summary>
		/// <param name="id">Node's id</param>
		/// <returns>The node, if found. Null, if otherwise.</returns>
		public Node FindNode(int id)
		{
			Node node;
			if (nodeDict.TryGetValue(id, out node))
			{
				return node;
			}

			return null;
		}

		/// <summary>
		/// Try to find the node. If it doesn't exist, create a new node.
		/// </summary>
		/// <param name="id">Node's id</param>
		/// <returns>The node</returns>
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

		/// <summary>
		/// Gets the id of the node.
		/// </summary>
		/// <param name="node">The node</param>
		/// <returns>The id</returns>
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

		/// <summary>
		/// Return a node at a certain index. If index is out-of-bounds, return null.
		/// </summary>
		/// <param name="idx">Node index</param>
		/// <returns></returns>
		public Node GetNodeAtIdx(int idx)
		{
			if (idx <= 0 || idx > nodes.Count)
				return null;

			return nodes[idx - 1]; 
		}

		/// <summary>
		/// Determine whether two nodes are equal
		/// </summary>
		private class IntNodeIdComparer : IEqualityComparer<int>
		{
			private int hashSize;

			/// <summary>
			/// Initializes a new instance of the <see cref="IntNodeIdComparer" /> class.
			/// </summary>
			/// <param name="hashSize">The maximum number of elements in the hash table</param>
			public IntNodeIdComparer(int hashSize)
			{
				this.hashSize = hashSize;
			}

			/// <summary>
			/// Determines whether two objects or equal or now
			/// </summary>
			/// <param name="left">The first object</param>
			/// <param name="right">The second object</param>
			/// <returns>True if equal, false if not equal</returns>
			public bool Equals(int left, int right)
			{
				return left == right;
			}

			/// <summary>
			/// Gets the hash code for this object
			/// </summary>
			/// <param name="obj">The object</param>
			/// <returns>The hash code</returns>
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
