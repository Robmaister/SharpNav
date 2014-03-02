#region License
/**
 * Copyright (c) 2014 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
 * Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE
 */
#endregion

using System;
using System.Collections.Generic;

using SharpNav.Geometry;

using NUnit.Framework;

using SharpNav;

namespace SharpNavTests
{
	[TestFixture]
	public class CompactHeightfieldTests
	{
		[Test]
		public void ConvertSpans_OneCell()
		{
			Heightfield hf = new Heightfield(Vector3.Zero, Vector3.One, 0.5f, 0.02f);
			hf[0].AddSpan(new Span(10, 20, AreaId.Walkable));
			hf[0].AddSpan(new Span(25, 30, AreaId.Walkable));
			
			CompactHeightfield chf = new CompactHeightfield(hf, 2, 1);
			
			Assert.AreEqual(chf.Spans.Length, 2);
			
			Assert.AreEqual(chf.Spans[0].Minimum, 20);
			Assert.AreEqual(chf.Spans[0].Height, 5);

			Assert.AreEqual(chf.Spans[1].Minimum, 30);
			Assert.AreEqual(chf.Spans[1].Height, int.MaxValue);
		}

		[Test]
		public void ConvertSpans_TwoCells()
		{
			Heightfield hf = new Heightfield(Vector3.Zero, Vector3.One, 0.5f, 0.02f);
			hf[0].AddSpan(new Span(10, 20, AreaId.Walkable));
			hf[0].AddSpan(new Span(25, 30, AreaId.Walkable));
			hf[1].AddSpan(new Span(5, 15, AreaId.Walkable));
			hf[1].AddSpan(new Span(25, 30, AreaId.Walkable));
			hf[1].AddSpan(new Span(40, 55, AreaId.Walkable));
			CompactHeightfield chf = new CompactHeightfield(hf, 2, 1);

			Assert.AreEqual(chf.Cells.Length, 4);

			Assert.AreEqual(chf.Cells[0].StartIndex, 0);
			Assert.AreEqual(chf.Cells[0].Count, 2);
			Assert.AreEqual(chf.Cells[1].StartIndex, 2);
			Assert.AreEqual(chf.Cells[1].Count, 3);
		}

		[Test]
		public void SetConnection_TwoCells()
		{
			Heightfield hf = new Heightfield(Vector3.Zero, Vector3.One, 0.5f, 0.02f);
			hf[0].AddSpan(new Span(10, 20, AreaId.Walkable));
			hf[0].AddSpan(new Span(25, 30, AreaId.Walkable));
			hf[1].AddSpan(new Span(10, 21, AreaId.Walkable));
			hf[1].AddSpan(new Span(25, 30, AreaId.Walkable));
			CompactHeightfield chf = new CompactHeightfield(hf, 2, 1);

			Assert.IsTrue(chf.Spans[0].IsConnected(Direction.East));
			Assert.IsTrue(chf.Spans[1].IsConnected(Direction.East));
			Assert.IsTrue(chf.Spans[2].IsConnected(Direction.West));
			Assert.IsTrue(chf.Spans[3].IsConnected(Direction.West));

			Assert.AreEqual(chf.Spans[0].ConnectionEast, 0);
			Assert.AreEqual(chf.Spans[1].ConnectionEast, 1);
			Assert.AreEqual(chf.Spans[2].ConnectionWest, 0);
			Assert.AreEqual(chf.Spans[3].ConnectionWest, 1);
		}
	}
}
