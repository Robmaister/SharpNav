#region License
/**
 * Copyright (c) 2014 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
 * Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE
 */
#endregion

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
		private T[] data;       // Internal data array
		private int size;       // Size of the queue
		private int first;      // Index of first element in queue


		/// <summary>
		/// Default constructor, default size of 1000 for internal array
		/// </summary>
		public BufferedQueue()
		{
			data = new T[1000];
			size = first = 0;
		}


		/// <summary>
		/// Initializes BufferedQueue with empty array of given size
		/// </summary>
		/// <param name="size">Size of the internal buffer</param>
		public BufferedQueue(int _size)
		{
			data = new T[_size];
			size = _size; 
			first = 0; 
		}


		/// <summary>
		/// Initialize BufferedQueue as a copy of a Queue container object with "size" number of elements
		/// </summary>
		/// <param name="size">Number of elements in container</param>
		/// <param name="items">Queue container object containing elements to be copied</param>
		public BufferedQueue(int _size, Queue<T> items)
		{
			if (items.Count <= _size)
			{
				data = new T[_size];
				items.CopyTo(data, 0);
				size = items.Count;
				first = 0;
			}
			else
			{
				data = items.Skip(items.Count - _size).ToArray();
				size = _size; 
			}
		}


		/// <summary>
		/// Returns number of elements in the queue.
		/// </summary>
		public int Count
		{
			get
			{
				return size - first;
			}
		}


		/// <summary>
		/// Returns whether the queue is read-only (False for now)
		/// </summary>
		bool ICollection<T>.IsReadOnly
		{
			get
			{
				return false;
			}
		}


		/// <summary>
		/// Returns value at specified index (valid ranges are from 0 to size-1)
		/// </summary>
		/// <param name="index">Index value</param>
		/// <returns></returns>
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
			if (size == data.Length)
				return false;
			data[size++] = item;
			return true;
		}


		/// <summary>
		/// Removes bottom element from queue and returns it (and updates "first" index)
		/// </summary>
		/// <returns>Bottom element</returns>
		public T Dequeue()
		{
			if (first == size)
				throw new InvalidOperationException("The queue is empty.");
			return data[first++];
		}


		/// <summary>
		/// Returns copy of the size element of the queue.
		/// </summary>
		/// <returns>size element</returns>
		public T Peek()
		{
			if (size == 0)
				throw new InvalidOperationException("The queue is empty.");
			return data[size - 1];
		}


		/// <summary>
		/// Resets queue pointer back to default, essentially clearing the queue. 
		/// </summary>
		public void Clear()
		{
			size = 0;
		}


		/// <summary>
		/// Returns whether the queue contains a given item.
		/// </summary>
		/// <param name="item">Item to search for</param>
		/// <returns>True if item exists in queue, False if not</returns>
		public bool Contains(T item)
		{
			for (int i = 0; i < size; i++)
				if (item.Equals(data[i]))
					return true;

			return false;
		}


		/// <summary>
		/// Still in development. 
		/// </summary>
		/// <param name="array"></param>
		/// <param name="arrayIndex"></param>
		public void CopyTo(T[] array, int arrayIndex)
		{
			throw new NotImplementedException();
		}


		/// <summary>
		/// Returns generator.
		/// </summary>
		/// <returns>IEnumerator generator object.</returns>
		public IEnumerator<T> GetEnumerator()
		{
			if (size == 0)
				yield break;

			//TODO handle wrap-arounds.
			for (int i = 0; i < size; i++)
				yield return data[i];
		}


		/// <summary>
		/// ICollection.Add() implementation for Bufferedqueue
		/// </summary>
		/// <param name="item"></param>
		void ICollection<T>.Add(T item)
		{
			Enqueue(item);
		}


		/// <summary>
		/// ICollection.Remove() implementation, which is not supported for queue containers. 
		/// </summary>
		/// <param name="item">Item to be removed (irrelevant)</param>
		/// <returns>False</returns>
		bool ICollection<T>.Remove(T item)
		{
			throw new InvalidOperationException("Cannot remove from an arbitrary index in a queue");
		}

		/// <summary>
		/// Returns IEnumerable enumerator object. 
		/// </summary>
		/// <returns>IEnumerator object</returns>
		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}
	}
}