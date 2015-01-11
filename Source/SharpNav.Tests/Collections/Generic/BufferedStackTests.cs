// Copyright (c) 2014 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
// Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using NUnit.Framework;

using SharpNav.Collections.Generic;

namespace SharpNav.Tests.Collections.Generic
{
	[TestFixture]
	public class BufferedStackTests
	{
		[Test]
		public void Empty_Stack_Count()
		{
			var empty = new BufferedStack<int>(1000);
			Assert.AreEqual(empty.Count, 0);
		}

		[Test]
		public void Push_Test()
		{
			var stack = new BufferedStack<int>(1000);
			for (int c = 0; c < 100; c++)
			{
				stack.Push(c);
				Assert.AreEqual(stack.Count, c + 1);
			}
		}

		[Test]
		public void Pop_Test()
		{
			var stack = new BufferedStack<int>(1000);
			for (int c = 0; c < 100; c++)
				stack.Push(c);

			for (int c = 99; c >= 0; c--)
			{
				int n = stack.Pop();
				Assert.AreEqual(n, c);
				Assert.AreEqual(stack.Count, c);
			}
		}

		[Test]
		public void Empty_Test()
		{
			var stack = new BufferedStack<int>(1000);
			for (int c = 0; c < 100; c++)
				stack.Push(1);
			Assert.AreEqual(stack.Count, 100);
			stack.Clear();
			Assert.AreEqual(stack.Count, 0);
		}

		[Test]
		public void ReadOnly_Test()
		{
			var stack = new BufferedStack<char>(1000);
			ICollection<char> collection = stack;
			Assert.AreEqual(collection.IsReadOnly, false);
		}

		[Test]
		public void Index_Operator_Test()
		{
			var stack = new BufferedStack<int>(1000);
			for (int c = 0; c < 100; c++)
			{
				stack.Push(c);
				Assert.AreEqual(stack[c], c);
			}
		}

		[Test]
		public void Peek_Test()
		{
			var stack = new BufferedStack<int>(1000);
			for (int c = 0; c < 100; c++)
			{
				stack.Push(c);
				Assert.AreEqual(stack.Peek(), c);
			}
		}


		// TBD: Tests for ICollection interface functions
	}
}
