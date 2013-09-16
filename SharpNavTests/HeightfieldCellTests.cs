using System;

using NUnit.Framework;

using SharpNav;

namespace SharpNavTests
{
	[TestFixture]
	public class HeightfieldCellTests
	{
		[Test]
		public void AddSpan_NoThickness_Throws()
		{
			var cell = new Heightfield.Cell(40);
			var span = new Heightfield.Span(10,10);

			Assert.Throws<ArgumentException>(() => cell.AddSpan(span));
		}

		[Test]
		public void AddSpan_Flipped_Throws()
		{
			var cell = new Heightfield.Cell(40);
			var span = new Heightfield.Span(20, 10);

			Assert.Throws<ArgumentException>(() => cell.AddSpan(span));
		}

		[Test]
		public void AddSpan_First_Success()
		{
			var cell = new Heightfield.Cell(40);
			var span = new Heightfield.Span(10, 20);

			cell.AddSpan(span);

			Assert.AreEqual(span.Minimum, cell.Spans[0].Minimum);
			Assert.AreEqual(span.Maximum, cell.Spans[0].Maximum);
		}

		[Test]
		public void AddSpan_Below_Success()
		{
			var cell = new Heightfield.Cell(40);
			var span = new Heightfield.Span(10, 20);
			var span2 = new Heightfield.Span(5, 8);

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
			var cell = new Heightfield.Cell(40);
			var span = new Heightfield.Span(10, 20);
			var span2 = new Heightfield.Span(21, 25);

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
			var cell = new Heightfield.Cell(40);
			var span = new Heightfield.Span(10, 20);
			var span2 = new Heightfield.Span(5, 25);

			cell.AddSpan(span);
			cell.AddSpan(span2);

			Assert.AreEqual(span2.Minimum, cell.Spans[0].Minimum);
			Assert.AreEqual(span2.Maximum, cell.Spans[0].Maximum);
		}

		[Test]
		public void AddSpan_BelowMerge_Success()
		{
			var cell = new Heightfield.Cell(40);
			var span = new Heightfield.Span(10, 20);
			var span2 = new Heightfield.Span(5, 15);

			cell.AddSpan(span);
			cell.AddSpan(span2);

			Assert.AreEqual(span2.Minimum, cell.Spans[0].Minimum);
			Assert.AreEqual(span.Maximum, cell.Spans[0].Maximum);
		}

		[Test]
		public void AddSpan_AboveMerge_Success()
		{
			var cell = new Heightfield.Cell(40);
			var span = new Heightfield.Span(10, 20);
			var span2 = new Heightfield.Span(15, 20);

			cell.AddSpan(span);
			cell.AddSpan(span2);

			Assert.AreEqual(span.Minimum, cell.Spans[0].Minimum);
			Assert.AreEqual(span2.Maximum, cell.Spans[0].Maximum);
		}

		[Test]
		public void Indexer_NoSpans_ReturnsNull()
		{
			var cell = new Heightfield.Cell(10);
			Assert.IsNull(cell[5]);
		}

		[Test]
		public void Indexer_BelowZero_Throws()
		{
			var cell = new Heightfield.Cell(10);
			Assert.Throws<IndexOutOfRangeException>(() => { var s = cell[-1]; });
		}

		[Test]
		public void Indexer_AboveMax_Throws()
		{
			var cell = new Heightfield.Cell(10);
			Assert.Throws<IndexOutOfRangeException>(() => { var s = cell[10]; });
		}

		[Test]
		public void Indexer_InSpan_Success()
		{
			var cell = new Heightfield.Cell(10);
			cell.AddSpan(new Heightfield.Span(2, 6));

			var span = cell[4];
			Assert.AreEqual(2, span.Value.Minimum);
			Assert.AreEqual(6, span.Value.Maximum);
		}

		[Test]
		public void Indexer_OutOfSpan_ReturnsNull()
		{
			var cell = new Heightfield.Cell(10);
			cell.AddSpan(new Heightfield.Span(2, 6));

			var span = cell[1];
			Assert.IsNull(span);
		}
	}
}
