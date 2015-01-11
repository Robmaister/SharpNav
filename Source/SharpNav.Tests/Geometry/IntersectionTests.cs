// Copyright (c) 2014-2015 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
// Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE

using System;

using NUnit.Framework;

using SharpNav;
using SharpNav.Geometry;

#if MONOGAME
using Vector3 = Microsoft.Xna.Framework.Vector3;
#elif OPENTK
using Vector3 = OpenTK.Vector3;
#elif SHARPDX
using Vector3 = SharpDX.Vector3;
#endif

namespace SharpNav.Tests.Geometry
{
	[TestFixture]
	public class IntersectionTests
	{
		[Test]
		public void SegmentSegment2D_without_float_success()
		{
			//the Segment 1
			Vector3 a = new Vector3(0, 0, 0);
			Vector3 b = new Vector3(1, 0, 1);

			//the segment 2
			Vector3 p = new Vector3(0, 0, 1);
			Vector3 q = new Vector3(1, 0, 0);
			bool f = Intersection.SegmentSegment2D(ref a, ref b, ref p, ref q);
			Assert.IsTrue(f);

		}

		[Test]
		public void SegmentSegment2D_without_float_false()
		{
			//the Segment 1
			Vector3 a = new Vector3(0, 0, 0);
			Vector3 b = new Vector3(1, 0, 1);

			//the segment 2
			Vector3 p = new Vector3(1, 0, 0);
			Vector3 q = new Vector3(2, 0, 1);
			bool f = Intersection.SegmentSegment2D(ref a, ref b, ref p, ref q);
			Assert.IsFalse(f);

		}

		[Test]
		public void SegmentSegment2D_with_float_success()
		{
			//the Segment 1
			Vector3 a = new Vector3(0, 0, 0);
			Vector3 b = new Vector3(1, 0, 1);

			//the segment 2
			Vector3 p = new Vector3(0, 0, 1);
			Vector3 q = new Vector3(1, 0, 0);
			float m;
			float n;
			bool f = Intersection.SegmentSegment2D(ref a, ref b, ref p, ref q, out m, out n);
			Assert.IsTrue(f);

		}

		[Test]
		public void SegmentSegment2D_with_float_false()
		{
			//the Segment 1
			Vector3 a = new Vector3(0, 0, 0);
			Vector3 b = new Vector3(1, 0, 1);

			//the segment 2
			Vector3 p = new Vector3(1, 0, 0);
			Vector3 q = new Vector3(2, 0, 1);
			float m;
			float n;
			bool f = Intersection.SegmentSegment2D(ref a, ref b, ref p, ref q, out m, out n);
			Assert.IsFalse(f);
		}
	}
}
