#region License
/**
 * Copyright (c) 2013-2014 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
 * Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE
 */
#endregion

using System;
using System.Collections.Generic;

namespace SharpNav.Collections.Generic
{
	/// <summary>
	/// Use a priority queue (heap) to determine which node is more important.
	/// </summary>
	/// <typeparam name="T">
	/// A type that has a cost for each instance via the <see cref="IValueWithCost"/> interface.
	/// </typeparam>
	public class PriorityQueue<T> : ICollection<T>
		where T : class, IValueWithCost
	{
		private T[] heap;
		private int capacity;
		private int size;

		public PriorityQueue(int n)
		{
			capacity = n;
			size = 0;
			heap = new T[capacity + 1];
		}

		public int Count
		{
			get
			{
				return size;
			}
		}

		bool ICollection<T>.IsReadOnly
		{
			get
			{
				return false;
			}
		}

		public void Clear()
		{
			size = 0;
		}

		public T Top()
		{
			return heap[0];
		}

		public T Pop()
		{
			T result = heap[0];
			size--;
			TrickleDown(0, heap[size]);
			return result;
		}

		public void Push(T node)
		{
			size++;
			BubbleUp(size - 1, node);
		}

		public bool Contains(T item)
		{
			throw new NotImplementedException();
		}

		public void Modify(T node)
		{
			for (int i = 0; i < size; i++)
			{
				if (heap[i] == node)
				{
					BubbleUp(i, node);
					return;
				}
			}
		}

		public IEnumerator<T> GetEnumerator()
		{
			return ((IEnumerable<T>)heap).GetEnumerator();
		}

		void ICollection<T>.Add(T item)
		{
			Push(item);
		}

		bool ICollection<T>.Contains(T item)
		{
			throw new NotImplementedException();
		}

		void ICollection<T>.CopyTo(T[] array, int arrayIndex)
		{
			if (arrayIndex + heap.Length > array.Length)
				throw new ArgumentException("array not large enough to hold priority queue", "array");

			Array.Copy(heap, 0, array, arrayIndex, heap.Length);
		}

		bool ICollection<T>.Remove(T item)
		{
			throw new InvalidOperationException("This priority queue implementation only allows elements to be popped off the top, not removed.");
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

		private void BubbleUp(int i, T node)
		{
			int parent = (i - 1) / 2;

			while ((i > 0) && (heap[parent].Cost > node.Cost))
			{
				heap[i] = heap[parent];
				i = parent;
				parent = (i - 1) / 2;
			}

			heap[i] = node;
		}

		private void TrickleDown(int i, T node)
		{
			int child = (i * 2) + 1;

			while (child < size)
			{
				if (((child + 1) < size) && (heap[child].Cost > heap[child + 1].Cost))
				{
					child++;
				}

				heap[i] = heap[child];
				i = child;
				child = (i * 2) + 1;
			}

			BubbleUp(i, node);
		}
	}
}
