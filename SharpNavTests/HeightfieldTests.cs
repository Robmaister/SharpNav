#region License
/**
 * Copyright (c) 2013 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
 * Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE
 */
#endregion

using System;

using NUnit.Framework;

using SharpNav;

#if MONOGAME || XNA
using Microsoft.Xna.Framework;
#elif OPENTK
using OpenTK;
#elif SHARPDX
using SharpDX;
#endif

namespace SharpNavTests
{
	[TestFixture]
	public class HeightfieldTests
	{
		[Test]
		public void Indexer_Valid_ReturnsCell()
		{
			var hf = new Heightfield(Vector3.Zero, Vector3.One, 0.5f, 0.5f);

			Assert.IsNotNull(hf[0, 1]);
		}

		[Test]
		public void Indexer_NegativeX_Throws()
		{
			var hf = new Heightfield(Vector3.Zero, Vector3.One, 0.5f, 0.5f);

			Assert.Throws<IndexOutOfRangeException>(() => { var c = hf[-1, 1]; });
		}

		[Test]
		public void Indexer_NegativeY_Throws()
		{
			var hf = new Heightfield(Vector3.Zero, Vector3.One, 0.5f, 0.5f);

			Assert.Throws<IndexOutOfRangeException>(() => { var c = hf[1, -1]; });
		}

		[Test]
		public void Indexer_NegativeBoth_Throws()
		{
			var hf = new Heightfield(Vector3.Zero, Vector3.One, 0.5f, 0.5f);

			Assert.Throws<IndexOutOfRangeException>(() => { var c = hf[-1, -1]; });
		}

		[Test]
		public void Indexer_TooLargeX_Throws()
		{
			var hf = new Heightfield(Vector3.Zero, Vector3.One, 0.5f, 0.5f);

			Assert.Throws<IndexOutOfRangeException>(() => { var c = hf[2, 0]; });
		}

		[Test]
		public void Indexer_TooLargeY_Throws()
		{
			var hf = new Heightfield(Vector3.Zero, Vector3.One, 0.5f, 0.5f);

			Assert.Throws<IndexOutOfRangeException>(() => { var c = hf[0, 2]; });
		}

		[Test]
		public void Indexer_TooLargeBoth_Throws()
		{
			var hf = new Heightfield(Vector3.Zero, Vector3.One, 0.5f, 0.5f);

			Assert.Throws<IndexOutOfRangeException>(() => { var c = hf[3, 3]; });
		}

		[Test]
		public void Indexer_CellOutOfRange_Throws()
		{
			var hf = new Heightfield(Vector3.Zero, Vector3.One, 0.5f, 0.5f);
			Assert.Throws<IndexOutOfRangeException>(() => { var c = hf[5]; });
		}

		[Test]
		public void Filter_LowHangingWalkable_Success()
		{
			var hf = new Heightfield(Vector3.Zero, Vector3.One, 0.5f, 0.02f);
			var span = new Span(10, 15, AreaFlags.Walkable);
			var span2 = new Span(16, 20, AreaFlags.Null);

			hf[0].AddSpan(span);
			hf[0].AddSpan(span2);

			hf.FilterLowHangingWalkableObstacles(20);

			Assert.AreEqual(hf[0].Spans[0].Area, hf[0].Spans[1].Area);
		}

		[Test]
		public void Filter_LowHangingWalkable_Fail()
		{
			var hf = new Heightfield(Vector3.Zero, Vector3.One, 0.5f, 0.02f);
			var span = new Span(1, 2, AreaFlags.Walkable);
			var span2 = new Span(10, 20, AreaFlags.Null);

			hf[2].AddSpan(span);
			hf[2].AddSpan(span2);

			//walkable step cannot cover the gap (difference between span2 maximum and span 1 maximum) so fail
			hf.FilterLowHangingWalkableObstacles(10);
			Assert.AreNotEqual(hf[0, 1].Spans[0].Area, hf[0, 1].Spans[1].Area);
		}

		[Test]
		public void Filter_WalkableLowHeight_Success()
		{
			var hf = new Heightfield(Vector3.Zero, Vector3.One, 0.5f, 0.02f);
			var span = new Span(10, 20, AreaFlags.Walkable);
			var span2 = new Span(25, 30, AreaFlags.Walkable);

			hf[0].AddSpan(span);
			hf[0].AddSpan(span2);

			//too low to walk through. there is only a gap of 5 units to walk through,
			//but at least 15 units is needed
			hf.FilterWalkableLowHeightSpans(15);

			//so one span is unwalkable and the other is fine
			Assert.AreEqual(hf[0].Spans[0].Area, AreaFlags.Null);
			Assert.AreEqual(hf[0].Spans[1].Area, AreaFlags.Walkable);
		}
	}
}
