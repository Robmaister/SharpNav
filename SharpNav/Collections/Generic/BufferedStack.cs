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
		private T[] data;       // Internal data array
		private int top;        // Index of the position "above" the top of the stack


		/// <summary>
		/// Default constructor, sets size to 1000 by default
		/// </summary>
		public BufferedStack()
		{
			data = new T[1000];
			top = 0; 
		}


		/// <summary>
		/// Initializes BufferedStack with empty array of given size
		/// </summary>
		/// <param name="size">Size of the internal buffer</param>
		public BufferedStack(int size)
		{
			data = new T[size];
			top = 0; 
		}


		/// <summary>
		/// Initialize BufferedStack as a copy of a Stack container object with "size" number of elements
		/// </summary>
		/// <param name="size">Number of elements in container</param>
		/// <param name="items">Stack container object containing elements to be copied</param>
		public BufferedStack(int size, Stack<T> items)
		{
			if (items.Count <= size)
			{
				data = new T[size];
				items.CopyTo(data, 0);
				top = items.Count;
			}
			else
			{
				data = items.Skip(items.Count - size).ToArray();
				top = size; 
			}
		}


		/// <summary>
		/// Returns number of elements in the stack.
		/// </summary>
		public int Count
		{
			get
			{
				return top; 
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
			if (top == data.Length)
				return false;
			data[top++] = item;
			return true; 
		}


		/// <summary>
		/// Removes most recent (top) element from stack and returns it.
		/// </summary>
		/// <returns>Top element</returns>
		public T Pop()
		{
			if (top == 0)
				throw new InvalidOperationException("The stack is empty.");
			return data[--top]; 
		}


		/// <summary>
		/// Returns copy of the top element of the stack.
		/// </summary>
		/// <returns>Top element</returns>
		public T Peek()
		{
			if (top == 0)
				throw new InvalidOperationException("The stack is empty.");
			return data[top-1];
		}


		/// <summary>
		/// Resets stack pointer back to default, essentially clearing the stack. 
		/// </summary>
		public void Clear()
		{
			top = 0;
		}


		/// <summary>
		/// Returns whether the stack contains a given item.
		/// </summary>
		/// <param name="item">Item to search for</param>
		/// <returns>True if item exists in stack, False if not</returns>
		public bool Contains(T item)
		{
			for (int i = 0; i < top; i++)
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
			if (top == 0)
				yield break;

			//TODO handle wrap-arounds.
			for (int i = 0; i < top; i++)
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
		/// ICollection.Remove() implementation, which is not supported for stack containers. 
		/// </summary>
		/// <param name="item">Item to be removed (irrelevant)</param>
		/// <returns>False</returns>
		bool ICollection<T>.Remove(T item)
		{
			throw new InvalidOperationException("Cannot remove from an arbitrary index in a stack");
			return false; 
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
