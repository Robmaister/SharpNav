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

using NUnit.Framework;

using SharpNav.Collections.Generic;

namespace SharpNavTests.Collections.Generic
{
	[TestFixture]
	public class BufferedStackTests
	{
        [Test]
        public void Empty_Stack_Count()
        {
            BufferedStack<int> empty;
            Assert.AreEqual(empty.Count, 0);
        }

        [Test]
        public void Push_Test()
        {
            BufferedStack<int> stack;
            for (int c = 0; c < 100; c++)
            {
                stack.Push(c);
                Assert.AreEqual(stack.Count, c + 1);
            }
        }

        [Test]
        public void Pop_Test()
        {
            BufferedStack<int> stack;
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
            BufferedStack<int> stack;
            for (int c = 0; c < 100; c++)
                stack.Push(1);
            Assert.AreEqual(stack.Count, 100);
            stack.Clear();
            Assert.AreEqual(stack.Count, 0);
        }

        [Test]
        public void ReadOnly_Test()
        {
            BufferedStack<char> stack;
            ICollection<char> collection = stack;
            Assert.AreEqual(collection.IsReadOnly, false);
        }

        [Test]
        public void Index_Operator_Test()
        {
            BufferedStack<int> stack;
            for (int c = 0; c < 100; c++)
            {
                stack.Push(c);
                Assert.AreEqual(stack[c], c);
            }
        }

        [Test]
        public void Peek_Test()
        {
            BufferedStack<int> stack;
            for (int c = 0; c < 100; c++)
            {
                stack.Push(c);
                Assert.AreEqual(stack.Peek(), c);
            }
        }


        // TBD: Tests for ICollection interface functions
	}
}
