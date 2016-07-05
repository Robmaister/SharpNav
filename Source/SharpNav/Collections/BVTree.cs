// Copyright (c) 2014-2015 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
// Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE

using System;
using System.Collections.Generic;
using System.Linq;

using SharpNav;
using SharpNav.Geometry;

#if MONOGAME
using Vector3 = Microsoft.Xna.Framework.Vector3;
#elif OPENTK
using Vector3 = OpenTK.Vector3;
#elif SHARPDX
using Vector3 = SharpDX.Vector3;
#endif

namespace SharpNav.Collections
{
	/// <summary>
	/// A tree of bounding volumes.
	/// </summary>
	public class BVTree
	{
		private static readonly Node.CompareX XComparer = new Node.CompareX();
		private static readonly Node.CompareY YComparer = new Node.CompareY();
		private static readonly Node.CompareZ ZComparer = new Node.CompareZ();

		/// <summary>
		/// Nodes in the tree
		/// </summary>
		private Node[] nodes;

		/// <summary>
		/// Initializes a new instance of the <see cref="BVTree"/> class.
		/// </summary>
		/// <param name="verts">A set of vertices.</param>
		/// <param name="polys">A set of polygons composed of the vertices in <c>verts</c>.</param>
		/// <param name="nvp">The maximum number of vertices per polygon.</param>
		/// <param name="cellSize">The size of a cell.</param>
		/// <param name="cellHeight">The height of a cell.</param>
		public BVTree(PolyVertex[] verts, PolyMesh.Polygon[] polys, int nvp, float cellSize, float cellHeight)
		{
			nodes = new Node[polys.Length * 2];
			var items = new List<Node>();

			for (int i = 0; i < polys.Length; i++)
			{
				PolyMesh.Polygon p = polys[i];

				Node temp;
				temp.Index = i;
				temp.Bounds.Min = temp.Bounds.Max = verts[p.Vertices[0]];

				for (int j = 1; j < nvp; j++)
				{
					int vi = p.Vertices[j];
					if (vi == PolyMesh.NullId)
						break;

					var v = verts[vi];
					PolyVertex.ComponentMin(ref temp.Bounds.Min, ref v, out temp.Bounds.Min);
					PolyVertex.ComponentMax(ref temp.Bounds.Max, ref v, out temp.Bounds.Max);
				}

				temp.Bounds.Min.Y = (int)Math.Floor((float)temp.Bounds.Min.Y * cellHeight / cellSize);
				temp.Bounds.Max.Y = (int)Math.Ceiling((float)temp.Bounds.Max.Y * cellHeight / cellSize);

				items.Add(temp);
			}

			Subdivide(items, 0, items.Count, 0);
		}

		/// <summary>
		/// Creates a copy of the tree from a group of enumerable nodes.
		/// </summary>
		/// <param name="nodes">The nodes to copy from.</param>
		public BVTree(IEnumerable<Node> nodes)
		{
			//TODO verify that the nodes passed in are a valid tree?
			this.nodes = nodes.ToArray();
		}

		/// <summary>
		/// Gets the number of nodes in the tree.
		/// </summary>
		public int Count
		{
			get
			{
				return nodes.Length;
			}
		}

		/// <summary>
		/// Gets the node at a specified index.
		/// </summary>
		/// <param name="index">The index.</param>
		/// <returns>The node at the index.</returns>
		public Node this[int index]
		{
			get
			{
				return nodes[index];
			}
		}

		/// <summary>
		/// Calculates the bounding box for a set of bounding boxes.
		/// </summary>
		/// <param name="items">The list of all the bounding boxes.</param>
		/// <param name="minIndex">The first bounding box in the list to get the extends of.</param>
		/// <param name="maxIndex">The last bounding box in the list to get the extends of.</param>
		/// <param name="bounds">The extends of all the bounding boxes.</param>
		private static void CalcExtends(List<Node> items, int minIndex, int maxIndex, out PolyBounds bounds)
		{
			bounds = items[minIndex].Bounds;

			for (int i = minIndex + 1; i < maxIndex; i++)
			{
				Node it = items[i];
				PolyVertex.ComponentMin(ref it.Bounds.Min, ref bounds.Min, out bounds.Min);
				PolyVertex.ComponentMax(ref it.Bounds.Max, ref bounds.Max, out bounds.Max);
			}
		}

		/// <summary>
		/// Determine whether the bounding x, y, or z axis contains the longest distance 
		/// </summary>
		/// <param name="x">Length of bounding x-axis</param>
		/// <param name="y">Length of bounding y-axis</param>
		/// <param name="z">Length of bounding z-axis</param>
		/// <returns>Returns the a specific axis (x, y, or z)</returns>
		private static int LongestAxis(int x, int y, int z)
		{
			int axis = 0;
			int max = x;

			if (y > max)
			{
				axis = 1;
				max = y;
			}

			if (z > max)
				axis = 2;

			return axis;
		}

		/// <summary>
		/// Subdivides a list of bounding boxes until it is a tree.
		/// </summary>
		/// <param name="items">A list of bounding boxes.</param>
		/// <param name="minIndex">The first index to consider (recursively).</param>
		/// <param name="maxIndex">The last index to consier (recursively).</param>
		/// <param name="curNode">The current node to look at.</param>
		/// <returns>The current node at the end of each method.</returns>
		private int Subdivide(List<Node> items, int minIndex, int maxIndex, int curNode)
		{
			int numIndex = maxIndex - minIndex;
			int curIndex = curNode;

			int oldNode = curNode;
			curNode++;

			//Check if the current node is a leaf node
			if (numIndex == 1)
				nodes[oldNode] = items[minIndex];
			else
			{
				PolyBounds bounds;
				CalcExtends(items, minIndex, maxIndex, out bounds);
				nodes[oldNode].Bounds = bounds;

				int axis = LongestAxis((int)(bounds.Max.X - bounds.Min.X), (int)(bounds.Max.Y - bounds.Min.Y), (int)(bounds.Max.Z - bounds.Min.Z));

				switch (axis)
				{
					case 0:
						items.Sort(minIndex, numIndex, XComparer);
						break;
					case 1:
						items.Sort(minIndex, numIndex, YComparer);
						break;
					case 2:
						items.Sort(minIndex, numIndex, ZComparer);
						break;
					default:
						break;
				}

				int splitIndex = minIndex + (numIndex / 2);

				curNode = Subdivide(items, minIndex, splitIndex, curNode);
				curNode = Subdivide(items, splitIndex, maxIndex, curNode);

				int escapeIndex = curNode - curIndex;
				nodes[oldNode].Index = -escapeIndex;
			}

			return curNode;
		}

		/// <summary>
		/// The data stored in a bounding volume node.
		/// </summary>
		public struct Node
		{
			/// <summary>
			/// The bounding box of the node.
			/// </summary>
			public PolyBounds Bounds;

			/// <summary>
			/// The index of this node in a <see cref="BVTree"/>.
			/// </summary>
			public int Index;

			/// <summary>
			/// An <see cref="IComparer{T}"/> implementation that only compares two <see cref="Node"/>s on the X axis.
			/// </summary>
			public class CompareX : IComparer<Node>
			{
				/// <summary>
				/// Compares two nodes's bounds on the X axis.
				/// </summary>
				/// <param name="x">A node.</param>
				/// <param name="y">Another node.</param>
				/// <returns>A negative value if a is less than b; 0 if they are equal; a positive value of a is greater than b.</returns>
				public int Compare(Node x, Node y)
				{
					if (x.Bounds.Min.X < y.Bounds.Min.X)
						return -1;

					if (x.Bounds.Min.X > y.Bounds.Min.X)
						return 1;

					return 0;
				}
			}

			/// <summary>
			/// An <see cref="IComparer{T}"/> implementation that only compares two <see cref="Node"/>s on the Y axis.
			/// </summary>
			public class CompareY : IComparer<Node>
			{
				/// <summary>
				/// Compares two nodes's bounds on the Y axis.
				/// </summary>
				/// <param name="x">A node.</param>
				/// <param name="y">Another node.</param>
				/// <returns>A negative value if a is less than b; 0 if they are equal; a positive value of a is greater than b.</returns>
				public int Compare(Node x, Node y)
				{
					if (x.Bounds.Min.Y < y.Bounds.Min.Y)
						return -1;

					if (x.Bounds.Min.Y > y.Bounds.Min.Y)
						return 1;

					return 0;
				}
			}

			/// <summary>
			/// An <see cref="IComparer{T}"/> implementation that only compares two <see cref="Node"/>s on the Z axis.
			/// </summary>
			public class CompareZ : IComparer<Node>
			{
				/// <summary>
				/// Compares two nodes's bounds on the Z axis.
				/// </summary>
				/// <param name="x">A node.</param>
				/// <param name="y">Another node.</param>
				/// <returns>A negative value if a is less than b; 0 if they are equal; a positive value of a is greater than b.</returns>
				public int Compare(Node x, Node y)
				{
					if (x.Bounds.Min.Z < y.Bounds.Min.Z)
						return -1;

					if (x.Bounds.Min.Z > y.Bounds.Min.Z)
						return 1;

					return 0;
				}
			}
		}
	}
}
