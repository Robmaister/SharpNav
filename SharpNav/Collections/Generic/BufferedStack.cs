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
    /// Typical FIFO generic stack container that stores data inside of
    /// a fixed-size internal buffer (array). 
    /// </summary>
    /// <typeparam name="T">Type of element that given BufferedStack object stores. </typeparam>
    /// 
    
	public class BufferedStack<T> : ICollection<T>
	{
		private T[] data;
		private int last;


        /// <summary>
        /// Initializes BufferedStack with empty array of given size
        /// </summary>
        /// <param name="size">Size of the internal buffer</param>
		public BufferedStack(int size)
		{
			data = new T[size];
			last = -1;
		}


		public BufferedStack(int size, Stack<T> items)
		{
			if (items.Count <= size)
			{
				data = new T[size];
				items.CopyTo(data, 0);
				last = items.Count - 1;
			}
			else
			{
				data = items.Skip(items.Count - size).ToArray();
				last = size - 1;
			}
		}


        /// <summary>
        /// Returns number of elements in the stack.
        /// </summary>
		public int Count
		{
			get
			{
				return last + 1;
			}
		}


        /// <summary>
        /// Returns whether the stack is read-only (False for now)
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
		/// Pushes a new element to the top of the stack.
		/// </summary>
		/// <param name="item">The element to be added to the stack</param>
		/// <returns>True if element was added to stack, False otherwise</returns>
		public bool Push(T item)
		{
            if (last < data.Length)
            {
                data[last++] = item;
                return true;
            }
            return false; 
		}


        /// <summary>
        /// Removes most recent (top) element from stack and returns it.
        /// </summary>
        /// <returns>Top element</returns>
		public T Pop()
		{
			if (last == 0)
				throw new InvalidOperationException("The stack is empty.");
			return data[--last];
		}


        /// <summary>
        /// Returns copy of the top element of the stack.
        /// </summary>
        /// <returns>Top element</returns>
		public T Peek()
		{
			if (last == 0)
				throw new InvalidOperationException("The stack is empty.");

			return data[last-1];
		}


        /// <summary>
        /// Resets stack pointer back to default, essentially clearing the stack. 
        /// </summary>
		public void Clear()
		{
			last = 0;
		}


        /// <summary>
        /// Returns whether the stack contains a given item.
        /// </summary>
        /// <param name="item">Item to search for</param>
        /// <returns>True if item exists in stack, False if not</returns>
		public bool Contains(T item)
		{
			for (int i = 0; i < last; i++)
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
			if (last == 0)
				yield break;

			//TODO handle wrap-arounds.
			for (int i = 0; i < last; i++)
				yield return data[i];
		}



        /// <summary>
        /// ICollection.Add() implementation for BufferedStack
        /// </summary>
        /// <param name="item"></param>
		void ICollection<T>.Add(T item)
		{
			Push(item);
		}


        /// <summary>
        /// Still in development. 
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
		bool ICollection<T>.Remove(T item)
		{
			throw new InvalidOperationException("Cannot remove from an arbitrary index in a stack");
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