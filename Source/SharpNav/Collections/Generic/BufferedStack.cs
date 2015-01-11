// Copyright (c) 2014 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
// Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE

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
	public class BufferedStack<T> : ICollection<T>
	{
		private T[] data;       // Internal data array
		private int top;        // Index of the position "above" the top of the stack

		/// <summary>
		/// Initializes a new instance of the <see cref="BufferedStack{T}"/> class.
		/// </summary>
		/// <param name="size">The maximum number of items that will be stored.</param>
		public BufferedStack(int size)
		{
			data = new T[size];
			top = 0; 
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="BufferedStack{T}"/> class as a copy of an
		/// <see cref="ICollection{T}"/> of the same type.
		/// </summary>
		/// <param name="size">The number of elements to copy from the collection.</param>
		/// <param name="items">The collection to copy from.</param>
		public BufferedStack(int size, ICollection<T> items)
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
		/// Gets the number of elements in the stack.
		/// </summary>
		public int Count
		{
			get
			{
				return top; 
			}
		}

		/// <summary>
		/// Gets a value indicating whether the stack is read-only (False for now)
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

			return data[top - 1];
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
		/// Copies the contents of the <see cref="BufferedStack{T}"/> to an array.
		/// </summary>
		/// <param name="array">The array to copy to.</param>
		/// <param name="arrayIndex">The index within the array to start copying to.</param>
		public void CopyTo(T[] array, int arrayIndex)
		{
			throw new NotImplementedException();
		}

		/// <summary>
		/// Gets the <see cref="BufferedStack"/>'s enumerator.
		/// </summary>
		/// <returns>The enumerator.</returns>
		public IEnumerator<T> GetEnumerator()
		{
			if (top == 0)
				yield break;

			//TODO handle wrap-arounds.
			for (int i = 0; i < top; i++)
				yield return data[i];
		}

		/// <summary>
		/// Calls <see cref="Push"/>.
		/// </summary>
		/// <param name="item">The item to add.</param>
		void ICollection<T>.Add(T item)
		{
			Push(item);
		}

		/// <summary>
		/// Unsupported, but necessary to implement <see cref="ICollection{T}"/>.
		/// </summary>
		/// <param name="item">An item.</param>
		/// <returns>Nothing. This method will always throw <see cref="InvalidOperationException"/>.</returns>
		/// <exception cref="InvalidOperationException">Will always be thrown. This is not a valid operation.</exception>
		bool ICollection<T>.Remove(T item)
		{
			throw new InvalidOperationException("Cannot remove from an arbitrary index in a stack");
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
