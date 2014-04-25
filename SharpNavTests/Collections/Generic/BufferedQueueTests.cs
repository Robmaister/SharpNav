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
    public class BufferedQueueTests
    {
        [Test]
        public void Empty_Queue_Count()
        {
            var empty = new BufferedQueue<int>();
            Assert.AreEqual(empty.Count, 0);
        }

        [Test]
        public void Enqueue_Test()
        {
            var queue = new BufferedQueue<int>();
            for (int c = 0; c < 100; c++)
            {
                queue.Enqueue(c);
                Assert.AreEqual(queue.Count, c + 1);
            }
        }

        [Test]
        public void Dequeue_Test()
        {
            var queue = new BufferedQueue<int>();
            for (int c = 0; c < 100; c++)
                queue.Enqueue(c);

            for (int c = 0; c < 100; c++)
            {
                int n = queue.Dequeue();
                Assert.AreEqual(n, c);
                Assert.AreEqual(queue.Count, c);
            }
        }

        [Test]
        public void Empty_Test()
        {
            var queue = new BufferedQueue<int>();
            for (int c = 0; c < 100; c++)
                queue.Enqueue(1);
            Assert.AreEqual(queue.Count, 100);
            queue.Clear();
            Assert.AreEqual(queue.Count, 0);
        }

        [Test]
        public void ReadOnly_Test()
        {
            var queue = new BufferedQueue<char>();
            ICollection<char> collection = queue;
            Assert.AreEqual(collection.IsReadOnly, false);
        }

        [Test]
        public void Peek_Test()
        {
            var queue = new BufferedQueue<int>();
            for (int c = 0; c < 100; c++)
            {
                queue.Enqueue(c);
                Assert.AreEqual(queue.Peek(), c);
            }
        }

        // TODO: ICollection
    }
}
