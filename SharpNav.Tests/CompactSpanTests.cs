// Copyright (c) 2013-2014 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
// Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE

using System;
using System.Runtime.InteropServices;

using NUnit.Framework;

using SharpNav;

namespace SharpNav.Tests
{
    [TestFixture]
    public class CompactSpanTests
    {
	    [Test]
		public void AddCompactSpan_NoUpperLimit_Success()
		{
			CompactSpan cs = new CompactSpan(10, int.MaxValue);

			Assert.IsFalse(cs.HasUpperBound);
			Assert.AreEqual(cs.ConnectionCount, 0);
		}

		[Test]
		public void AddCompactSpan_SetConnection_Success()
		{
			CompactSpan cs = new CompactSpan(10, int.MaxValue);
			CompactSpan.SetConnection((Direction)1, 1, ref cs);

			Assert.AreEqual(cs.ConnectionCount, 1);
		}

		[Test]
		public void AddCompactSpan_GetConnection_Success()
		{
			CompactSpan cs = new CompactSpan(10, int.MaxValue);
			CompactSpan.SetConnection((Direction)1, 50, ref cs);

			Assert.AreEqual(CompactSpan.GetConnection(ref cs, (Direction)1), 50);
			Assert.AreEqual(cs.GetConnection((Direction)1), 50);
		}

		[Test]
		public void AddCompactSpan_UnsetConnection_Success()
		{
			CompactSpan cs = new CompactSpan(10, int.MaxValue);
			CompactSpan.SetConnection((Direction)1, 1, ref cs);
			CompactSpan.UnsetConnection((Direction)1, ref cs);
			
			Assert.AreEqual(cs.ConnectionCount, 0);
		}

		[Test]
		public void AddCompactSpan_IsConnected_Success()
		{
			CompactSpan cs = new CompactSpan(10, int.MaxValue);
			CompactSpan.SetConnection(Direction.East, 1, ref cs);

			Assert.IsTrue(cs.IsConnected(Direction.East));
			Assert.IsFalse(cs.IsConnected(Direction.West));
		}

		[Test]
		public void SetConnection_TooHigh_Success()
		{
			CompactSpan cs = new CompactSpan(10, int.MaxValue);
			Assert.Throws<ArgumentOutOfRangeException>(() => CompactSpan.SetConnection((Direction)2, 300, ref cs));
		}

		[Test]
		public void SetConnection_InvalidDirection_Success()
		{
			CompactSpan cs = new CompactSpan(10, int.MaxValue);
			Assert.Throws<ArgumentException>(() => CompactSpan.SetConnection((Direction)(-1), 1, ref cs));	
		}
	}
}
