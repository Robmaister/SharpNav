// Copyright (c) 2014-2015 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
// Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE

using System;
using System.Collections.Generic;

using SharpNav.Geometry;

using NUnit.Framework;

using SharpNav;

#if MONOGAME
using Vector3 = Microsoft.Xna.Framework.Vector3;
#elif OPENTK
using Vector3 = OpenTK.Vector3;
#elif SHARPDX
using Vector3 = SharpDX.Vector3;
#endif

namespace SharpNav.Tests
{
	[TestFixture]
	public class CompactHeightfieldTests
	{
		[Test]
		public void ConvertSpans_OneCell()
		{
			Heightfield hf = new Heightfield(new BBox3(Vector3.Zero, Vector3.One), 0.5f, 0.02f);
			hf[0].AddSpan(new Span(10, 20, Area.Default));
			hf[0].AddSpan(new Span(25, 30, Area.Default));
			
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
			Heightfield hf = new Heightfield(new BBox3(Vector3.Zero, new Vector3(1, 1, 1)), 0.5f, 0.02f);
			hf[0].AddSpan(new Span(10, 20, Area.Default));
			hf[0].AddSpan(new Span(25, 30, Area.Default));
			hf[1].AddSpan(new Span(5, 15, Area.Default));
			hf[1].AddSpan(new Span(25, 30, Area.Default));
			hf[1].AddSpan(new Span(40, 55, Area.Default));
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
			Heightfield hf = new Heightfield(new BBox3(Vector3.Zero, Vector3.One), 0.5f, 0.02f);
			hf[0].AddSpan(new Span(10, 20, Area.Default));
			hf[0].AddSpan(new Span(25, 30, Area.Default));
			hf[1].AddSpan(new Span(10, 21, Area.Default));
			hf[1].AddSpan(new Span(25, 30, Area.Default));
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

		[Test]
		public void DistanceField_Simple_Success()
		{
			//Build a 3x3 heightfield
			Heightfield hf = new Heightfield(new BBox3(Vector3.Zero, Vector3.One), (float)(1.0f/3.0f), 0.02f);
			for (int i = 0; i < 9; i++)
			{
				hf[i].AddSpan(new Span(10, 20, Area.Default));
				hf[i].AddSpan(new Span(25, 30, Area.Default));
			}
			CompactHeightfield chf = new CompactHeightfield(hf, 2, 1);

			//make sure connections are set
			Assert.AreEqual(chf.Spans[0 * 2].ConnectionCount, 2); //corner
			Assert.AreEqual(chf.Spans[1 * 2].ConnectionCount, 3); //edge
			Assert.AreEqual(chf.Spans[2 * 2].ConnectionCount, 2); //corner
			Assert.AreEqual(chf.Spans[3 * 2].ConnectionCount, 3); //edge
			Assert.AreEqual(chf.Spans[4 * 2].ConnectionCount, 4); //center

			chf.BuildDistanceField();

			//check distance field values
			Assert.AreEqual(chf.MaxDistance, 2);

			//1st row
			Assert.AreEqual(chf.Distances[0 * 2], 0); //boundary
			Assert.AreEqual(chf.Distances[1 * 2], 0); //boundary
			Assert.AreEqual(chf.Distances[2 * 2], 0); //boundary
			
			//2nd row
			Assert.AreEqual(chf.Distances[3 * 2], 0); //boundary
			Assert.AreEqual(chf.Distances[4 * 2], 2); //center span
			Assert.AreEqual(chf.Distances[5 * 2], 0); //boundary
		}

		[Test]
		public void DistanceField_Medium_Success()
		{
			//Build a 5x5 heightfield
			Heightfield hf = new Heightfield(new BBox3(Vector3.Zero, Vector3.One), 0.2f, 0.02f);
			for (int i = 0; i < 25; i++)
			{
				hf[i].AddSpan(new Span(10, 20, Area.Default));
				hf[i].AddSpan(new Span(25, 30, Area.Default));
			}
			CompactHeightfield chf = new CompactHeightfield(hf, 2, 1);

			chf.BuildDistanceField();

			//Before box blur, MaxDistance is 4
			//After box blur, MaxDistance is 2  
			Assert.AreEqual(chf.MaxDistance, 2); 

			//1st row
			Assert.AreEqual(chf.Distances[0 * 2], 0); //boundary
			Assert.AreEqual(chf.Distances[4 * 2], 0); //boundary
			
			//2nd row
			Assert.AreEqual(chf.Distances[5 * 2], 0); //boundary
			Assert.AreEqual(chf.Distances[6 * 2], 2); //near boundary
			Assert.AreEqual(chf.Distances[7 * 2], 2); //near boundary
			Assert.AreEqual(chf.Distances[8 * 2], 2); //near boundary
			Assert.AreEqual(chf.Distances[9 * 2], 0); //boundary

			//3rd row
			Assert.AreEqual(chf.Distances[10 * 2], 0); //boundary
			Assert.AreEqual(chf.Distances[11 * 2], 2); //near boundary
			Assert.AreEqual(chf.Distances[12 * 2], 2); //center (box blurred distance is (2*8 + 5)/9)
			Assert.AreEqual(chf.Distances[13 * 2], 2); //near boundary
			Assert.AreEqual(chf.Distances[14 * 2], 0); //boundary
		}

		[Test]
		public void BuildRegions_Success()
		{
			//Build a 3x3 heightfield
			Heightfield hf = new Heightfield(new BBox3(Vector3.Zero, Vector3.One), (float)(1.0f / 3.0f), 0.02f);
			for (int i = 0; i < 9; i++)
			{
				hf[i].AddSpan(new Span(10, 20, Area.Default));
				hf[i].AddSpan(new Span(25, 30, Area.Default));
			}
			CompactHeightfield chf = new CompactHeightfield(hf, 2, 1);

			chf.BuildDistanceField();
			chf.BuildRegions(1, 2, 3);

			//Most spans do not have a region id because those spans are part of the border
			//Most region ids won't be assigned to any span

			//Total number of regions right now
			Assert.AreEqual(chf.MaxRegions, 7);
			
			//Center spans should have region id
			Assert.AreEqual((int)chf.Spans[4 * 2 + 0].Region, 5);
			Assert.AreEqual((int)chf.Spans[4 * 2 + 1].Region, 6);

			//Check that the rest of the region ids are not assigned to a span
			for (int i = 0; i < chf.Spans.Length; i++)
			{
				for (int j = 0; j <= 4; j++)
				{
					Assert.AreNotEqual((int)chf.Spans[i].Region, j);
				}
			}
		}
	}
}
