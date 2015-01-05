// Copyright (c) 2014 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
// Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace SharpNav.Collections.Generic
{
	/// <summary>
	/// Typical LIFO generic queue container that stores data inside of
	/// a fixed-size internal buffer (array).
	/// </summary>
	/// <typeparam name="T">Type of element that given BufferedQueue object stores. </typeparam>
	public class BufferedQueue<T> : ICollection<T>
	{
        private const int SIZE = 100;   // Fixed internal size of the data array
		private T[] data;               // Internal data array
	    private int first;              // Index of first element in queue
        private int last;               // Index of last element in queue

		/// <summary>
		/// Initializes a new instance of the <see cref="BufferedQueue{T}"/> class.
		/// </summary>
		/// <param name="size">The maximum number of items that will be stored.</param>
		public BufferedQueue(int size)
		{
			this.data = new T[SIZE];
            this.first = this.last = -1;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="BufferedQueue{T}"/> class as a copy of an
		/// <see cref="ICollection{T}"/> of the same type.
		/// </summary>
		/// <param name="items">The collection to copy from.</param>
		public BufferedQueue(ICollection<T> items)
		{
			if (items.Count <= SIZE)
			{
                this.data = new T[SIZE];
				items.CopyTo(data, 0);
				this.first = 0;
			}
			else
			{
                this.data = items.Skip(items.Count - SIZE).ToArray();
				this.first = 0;
			}
		}

		/// <summary>
		/// Gets the number of elements in the queue.
		/// </summary>
		public int Count
		{
			get
			{
                return (last >= 0 && first >= 0) ? last - first : 0;
			}
		}

		/// <summary>
		/// Gets a value indicating whether the queue is read-only (False for now)
		/// </summary>
		bool ICollection<T>.IsReadOnly
		{
			get
			{
				return false;
			}
		}

		/// <summary>
		/// Gets the value at specified index (valid ranges are from 0 to size-1)
		/// </summary>
		/// <param name="index">Index value</param>
		/// <returns>The value at the index</returns>
		public T this[int index]
		{
			get
			{
				return data[index];
			}
		}

		/// <summary>
		/// Adds a new element to the front of the queue.
		/// </summary>
		/// <param name="item">The element to be added to the queue</param>
		/// <returns>True if element was added to queue, False otherwise</returns>
		public bool Enqueue(T item)
		{
			if (last == data.Length)
				return false;
            if (first < 0)
                first = 0; 
			data[++last] = item;
			return true;
		}

		/// <summary>
		/// Removes bottom element from queue and returns it (and updates "first" index)
		/// </summary>
		/// <returns>Bottom element</returns>
		public T Dequeue()
		{
			if (first < 0)
				throw new InvalidOperationException("The queue is empty.");
			return data[first++];
		}

		/// <summary>
		/// Returns last element in the queue
		/// </summary>
		/// <returns>size element</returns>
		public T Peek()
		{
			if (last == 0)
				throw new InvalidOperationException("The queue is empty.");
            return data[last]; 
		}

		/// <summary>
		/// Resets queue pointer back to default, essentially clearing the queue. 
		/// </summary>
		public void Clear()
		{
			first = last = -1;
		}

		/// <summary>
		/// Returns whether the queue contains a given item.
		/// </summary>
		/// <param name="item">Item to search for</param>
		/// <returns>True if item exists in queue, False if not</returns>
		public bool Contains(T item)
		{
			for (int i = 0; i <= last; i++)
				if (item.Equals(data[i]))
					return true;

			return false;
		}

		/// <summary>
		/// Copies the contents of the <see cref="BufferedQueue{T}"/> to an array.
		/// </summary>
		/// <param name="array">The array to copy to.</param>
		/// <param name="arrayIndex">The index within the array to start copying to.</param>
		public void CopyTo(T[] array, int arrayIndex)
		{
			throw new NotImplementedException();
		}

		/// <summary>
		/// Gets the <see cref="BufferedQueue"/>'s enumerator.
		/// </summary>
		/// <returns>The enumerator.</returns>
		public IEnumerator<T> GetEnumerator()
		{
			if (last < 0 || first < 0)
				yield break;

			//TODO handle wrap-arounds.
			for (int i = 0; i <= last; i++)
				yield return data[i];
		}

		/// <summary>
		/// Calls <see cref="Enqueue"/>.
		/// </summary>
		/// <param name="item">The item to add.</param>
		void ICollection<T>.Add(T item)
		{
			Enqueue(item);
		}

		/// <summary>
		/// Unsupported, but necessary to implement <see cref="ICollection{T}"/>.
		/// </summary>
		/// <param name="item">An item.</param>
		/// <returns>Nothing. This method will always throw <see cref="InvalidOperationException"/>.</returns>
		/// <exception cref="InvalidOperationException">Will always be thrown. This is not a valid operation.</exception>
		bool ICollection<T>.Remove(T item)
		{
			throw new InvalidOperationException("Cannot remove from an arbitrary index in a queue");
		}

		/// <summary>
		/// The non-generic version of <see cref="GetEnumerator"/>.
		/// </summary>
		/// <returns>A non-generic enumerator.</returns>
		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}
	}
}