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
	public class DistanceTests
	{
		[Test]
		public void PointToSegment2DSquared_NoVectors_Success()
		{
			//point is (0, 0), segment is (0, 1) to (1,0)
			float dist = Distance.PointToSegment2DSquared(0, 0, 0, 1, 1, 0);

			//safe floating value comparison
			Assert.AreEqual(dist + float.Epsilon, 0.5f);
		}

		[Test]
		public void PointToSegment2DSquared_Vectors_Success()
		{
			//the point
			Vector3 pt = new Vector3(0, 0, 0);

			//the segment
			Vector3 p = new Vector3(0, 0, 1);
			Vector3 q = new Vector3(1, 0, 0);

			float dist = Distance.PointToSegment2DSquared(ref pt, ref p, ref q);

			//safe floating value comparison
			Assert.AreEqual(dist + float.Epsilon, 0.5f);
		}

		[Test]
		public void PointToSegment2DSquared_VectorsWithParameters_Success()
		{
			//the point
			Vector3 pt = new Vector3(0, 0, 0);

			//the segment
			Vector3 p = new Vector3(0, 0, 1);
			Vector3 q = new Vector3(1, 0, 0);

			//the paramater t
			float t;

			float dist = Distance.PointToSegment2DSquared(ref pt, ref p, ref q, out t);

			//safe floating value comparison
			Assert.AreEqual(dist + float.Epsilon, 0.5f);
			Assert.AreEqual(t + float.Epsilon, 0.5f);
		}

		[Test]
		public void PointToTriangle_CenterPointDist_Success()
		{
			//Point
			Vector3 p = new Vector3(0.5f, 0.5f, 0.5f);

			//Triangle
			Vector3 a = new Vector3(0, 0, 1);
			Vector3 b = new Vector3(-1, 0, 0);
			Vector3 c = new Vector3(1, 0, 0);

			float dist = Distance.PointToTriangle(p, a, b, c);

			Assert.AreEqual(dist + float.Epsilon, 0.5f);
		}

		[Test]
		public void PointToTriangle_EdgePointDist_Success()
		{
			//Point
			Vector3 p = new Vector3(0.0f, 0.0f, 0.0f);

			//Triangle
			Vector3 a = new Vector3(0, 0, 1);
			Vector3 b = new Vector3(-1, 0, 0);
			Vector3 c = new Vector3(1, 0, 0);

			float dist = Distance.PointToTriangle(p, a, b, c);

			Assert.AreEqual(dist, 0.0f);
		}

		[Test]
		public void PointToTriangle_CenterPointBool_Success()
		{
			//Point
			Vector3 p = new Vector3(0.5f, 0.5f, 0.5f);

			//Triangle
			Vector3 a = new Vector3(0, 0, 1);
			Vector3 b = new Vector3(-1, 0, 0);
			Vector3 c = new Vector3(1, 0, 0);

			float height;
			bool isInTriangle = Distance.PointToTriangle(p, a, b, c, out height);

			Assert.AreEqual(height, 0.0f);
			Assert.IsTrue(isInTriangle);
		}
	}
}
