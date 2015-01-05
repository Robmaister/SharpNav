// Copyright (c) 2013-2014 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
// Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE

using System;

using NUnit.Framework;

using SharpNav;

namespace SharpNav.Tests
{
	[TestFixture]
	public class CellTests
	{
		[Test]
		public void AddSpan_Flipped_Success()
		{
			var cell = new Cell(40);
			var span = new Span(20, 10);

			cell.AddSpan(span);

			Assert.AreEqual(cell.Spans[0].Minimum, span.Maximum);
			Assert.AreEqual(cell.Spans[0].Maximum, span.Minimum);
		}

		[Test]
		public void AddSpan_First_Success()
		{
			var cell = new Cell(40);
			var span = new Span(10, 20);

			cell.AddSpan(span);

			Assert.AreEqual(span.Minimum, cell.Spans[0].Minimum);
			Assert.AreEqual(span.Maximum, cell.Spans[0].Maximum);
		}

		[Test]
		public void AddSpan_Below_Success()
		{
			var cell = new Cell(40);
			var span = new Span(10, 20);
			var span2 = new Span(5, 8);

			cell.AddSpan(span);
			cell.AddSpan(span2);

			Assert.AreEqual(span2.Minimum, cell.Spans[0].Minimum);
			Assert.AreEqual(span2.Maximum, cell.Spans[0].Maximum);
			Assert.AreEqual(span.Minimum, cell.Spans[1].Minimum);
			Assert.AreEqual(span.Maximum, cell.Spans[1].Maximum);
		}

		[Test]
		public void AddSpan_Above_Success()
		{
			var cell = new Cell(40);
			var span = new Span(10, 20);
			var span2 = new Span(21, 25);

			cell.AddSpan(span);
			cell.AddSpan(span2);

			Assert.AreEqual(span.Minimum, cell.Spans[0].Minimum);
			Assert.AreEqual(span.Maximum, cell.Spans[0].Maximum);
			Assert.AreEqual(span2.Minimum, cell.Spans[1].Minimum);
			Assert.AreEqual(span2.Maximum, cell.Spans[1].Maximum);
		}

		[Test]
		public void AddSpan_ContainedMerge_Success()
		{
			var cell = new Cell(40);
			var span = new Span(10, 20);
			var span2 = new Span(5, 25);

			cell.AddSpan(span);
			cell.AddSpan(span2);

			Assert.AreEqual(span2.Minimum, cell.Spans[0].Minimum);
			Assert.AreEqual(span2.Maximum, cell.Spans[0].Maximum);
		}

		[Test]
		public void AddSpan_BelowMerge_Success()
		{
			var cell = new Cell(40);
			var span = new Span(10, 20);
			var span2 = new Span(5, 15);

			cell.AddSpan(span);
			cell.AddSpan(span2);

			Assert.AreEqual(span2.Minimum, cell.Spans[0].Minimum);
			Assert.AreEqual(span.Maximum, cell.Spans[0].Maximum);
		}

		[Test]
		public void AddSpan_AboveMerge_Success()
		{
			var cell = new Cell(40);
			var span = new Span(10, 20);
			var span2 = new Span(15, 25);

			cell.AddSpan(span);
			cell.AddSpan(span2);

			Assert.AreEqual(span.Minimum, cell.Spans[0].Minimum);
			Assert.AreEqual(span2.Maximum, cell.Spans[0].Maximum);
		}
	   

		[Test]
		public void Indexer_NoSpans_ReturnsNull()
		{
			var cell = new Cell(10);
			Assert.IsNull(cell[5]);
		}

		[Test]
		public void Indexer_BelowZero_Throws()
		{
			var cell = new Cell(10);
			Assert.Throws<ArgumentOutOfRangeException>(() => { var s = cell[-1]; });
		}

		[Test]
		public void Indexer_AboveMax_Throws()
		{
			var cell = new Cell(10);
			Assert.Throws<ArgumentOutOfRangeException>(() => { var s = cell[10]; });
		}

		[Test]
		public void Indexer_InSpan_Success()
		{
			var cell = new Cell(10);
			cell.AddSpan(new Span(2, 6));

			var span = cell[4];
			Assert.AreEqual(2, span.Value.Minimum);
			Assert.AreEqual(6, span.Value.Maximum);
		}

		[Test]
		public void Indexer_OutOfSpan_ReturnsNull()
		{
			var cell = new Cell(10);
			cell.AddSpan(new Span(2, 6));

			var span = cell[1];
			Assert.IsNull(span);
		}

		[Test]
		public void Indexer_FindSpan_Success()
		{
			var cell = new Cell(10);
			cell.AddSpan(new Span(2, 5));
			cell.AddSpan(new Span(7, 9));

			var span = cell[6];
			Assert.IsNull(span);
		}
	}
}
