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
	public class BufferedStack<T> : ICollection<T>
	{
		private T[] data;
		private int last;

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

		public int Count { get { return last + 1; } }

		bool ICollection<T>.IsReadOnly { get { return false; } }

		public T this[int index]
		{
			get
			{
				return data[index];
			}
		}

		//HACK return bool when stack is filled and restarts is unclear and a hack
		public bool Push(T item)
		{
			last++;
			if (last == data.Length)
			{
				last = 0;
				data[last] = item;
				return true;
			}
			else
			{
				data[last] = item;
				return false;
			}
		}

		public T Pop()
		{
			if (last == -1)
				throw new InvalidOperationException("The stack is empty.");

			last--;
			return data[last + 1];
		}

		public T Peek()
		{
			if (last == -1)
				throw new InvalidOperationException("The stack is empty.");

			return data[last];
		}

		public void Clear()
		{
			last = -1;
		}

		public bool Contains(T item)
		{
			for (int i = 0; i <= last; i++)
				if (item.Equals(data[i]))
					return true;

			return false;
		}

		public void CopyTo(T[] array, int arrayIndex)
		{
			throw new NotImplementedException();
		}

		public IEnumerator<T> GetEnumerator()
		{
			if (last == -1)
				yield break;

			//TODO handle wrap-arounds.
			for (int i = 0; i <= last; i++)
				yield return data[i];
		}

		void ICollection<T>.Add(T item)
		{
			Push(item);
		}

		bool ICollection<T>.Remove(T item)
		{
			throw new InvalidOperationException("Cannot remove from an arbitrary index in a stack");
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}
	}
}
