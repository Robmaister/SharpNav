#region License
/**
 * Copyright (c) 2014 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
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
#elif UNITY3D
using UnityEngine;
#endif

using MathHelper = SharpNav.MathHelper;

namespace SharpNavTests
{
	[TestFixture]
	public class MathHelperTests
	{
		[Test]
		public void ClampTest_ValOutOfRange_Success()
		{
			int num = MathHelper.Clamp(3, 6, 10);
			Assert.AreEqual (num, 6);
		}

		[Test]
		public void ClampTest_ValInRange_Success()
		{
			int num = MathHelper.Clamp(7, 4, 10);
			Assert.AreEqual(num, 7);
		}

		[Test]
		public void ClampTest_ValOutOfRangeRef_Success()
		{
			int num_r = 3;
			MathHelper.Clamp(ref num_r, 6, 10);
			Assert.AreEqual(num_r, 6);
		}

		[Test]
		public void ClampTest_ValInRangeRef_Success()
		{
			int num_r = 7;
			MathHelper.Clamp(ref num_r, 6, 10);
			Assert.AreEqual(num_r, 7);
		}

		[Test]
		public void ClampTest_ValInRangeFloat_Success()
		{
			float num = MathHelper.Clamp(7.56f, 6.75f, 10.89f);
			Assert.AreEqual(num, 7.56f);
		}

		[Test]
		public void ClampTest_ValOutOfRangeFloat_Success()
		{
			float num = MathHelper.Clamp(3.89f, 6.75f, 10.89f);
			Assert.AreEqual(num, 6.75f);
		}

		public void ClampTest_ValInRangeFloatRef_Success()
		{
			float num_r = 7.56f;
			MathHelper.Clamp(ref num_r, 6.75f, 10.89f);
			Assert.AreEqual(num_r, 7.56f);
		}

		[Test]
		public void ClampTest_ValOutOfRangeFloatRef_Success()
		{
			float num_r = 3.89f;
			MathHelper.Clamp(ref num_r, 6.75f, 10.89f);
			Assert.AreEqual (num_r, 6.75f);
		}

		[Test]
		public void NextPowerOfTwo_PositiveIntegerInt_Sucess()
		{
			int num = MathHelper.NextPowerOfTwo(5);

			Assert.AreEqual(num, 8);
		}

		[Test]
		public void NextPowerOfTwo_PositiveIntegerUint_Sucess()
		{
			uint num = MathHelper.NextPowerOfTwo((uint)5);

			Assert.AreEqual(num, 8);
		}

		[Test]
		public void Log2_PositiveIntegerInt_Success()
		{
			int num = MathHelper.Log2(65);

			Assert.AreEqual(num, 6);
		}
		
		[Test]
		public void Log2_PositiveIntegerUint_Success()
		{
			uint num = MathHelper.Log2((uint)65);

			Assert.AreEqual(num, 6);
		}

		[Test]
		public void IsPointInPoly_InternalPoint_Success()
		{
			Vector3 pt = new Vector3(0.0f, 0.0f, 0.5f);

			Vector3[] poly = new Vector3[3];
			poly[0] = new Vector3(0.0f, 0.0f, 1.0f);
			poly[1] = new Vector3(-1.0f, 0.0f, 0.0f);
			poly[2] = new Vector3(1.0f, 0.0f, 0.0f);

			bool isInPoly = MathHelper.IsPointInPoly(pt, poly, poly.Length);

			Assert.IsTrue(isInPoly);
		}

		[Test]
		public void IsPointInPoly_BoundaryPoint_Success()
		{
			Vector3 pt = new Vector3(0.0f, 0.0f, 0.0f);

			Vector3[] poly = new Vector3[3];
			poly[0] = new Vector3(0.0f, 0.0f, 1.0f);
			poly[1] = new Vector3(-1.0f, 0.0f, 0.0f);
			poly[2] = new Vector3(1.0f, 0.0f, 0.0f);

			bool isInPoly = MathHelper.IsPointInPoly(pt, poly, poly.Length);

			Assert.IsTrue(isInPoly);
		}

		[Test]
		public void IsPointInPoly_ExternalPoint_Success()
		{
			Vector3 pt = new Vector3(-1.0f, 0.0f, -1.0f);

			Vector3[] poly = new Vector3[3];
			poly[0] = new Vector3(0.0f, 0.0f, 1.0f);
			poly[1] = new Vector3(-1.0f, 0.0f, 0.0f);
			poly[2] = new Vector3(1.0f, 0.0f, 0.0f);

			bool isInPoly = MathHelper.IsPointInPoly(pt, poly, poly.Length);

			Assert.IsFalse(isInPoly);
		}

		[Test]
		public void PointToSegment2DSquared_NoVectors_Success()
		{
			//point is (0, 0), segment is (0, 1) to (1,0)
			float dist = MathHelper.Distance.PointToSegment2DSquared(0, 0, 0, 1, 1, 0);

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

			float dist = MathHelper.Distance.PointToSegment2DSquared(ref pt, ref p, ref q);

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

			float dist = MathHelper.Distance.PointToSegment2DSquared(ref pt, ref p, ref q, out t);

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

			float dist = MathHelper.Distance.PointToTriangle(p, a, b, c);

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

			float dist = MathHelper.Distance.PointToTriangle(p, a, b, c);

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
			bool isInTriangle = MathHelper.Distance.PointToTriangle(p, a, b, c, out height);

			Assert.AreEqual(height, 0.0f);
			Assert.IsTrue(isInTriangle);
		}

		[Test]
		public void SegmentSegment2D_without_float_success()
		{
			//the Segment 1
			Vector3 a = new Vector3 (0, 0, 0);
			Vector3 b = new Vector3 (1, 0, 1);

			//the segment 2
			Vector3 p = new Vector3(0, 0, 1);
			Vector3 q = new Vector3(1, 0, 0);
			bool f = MathHelper.Intersection.SegmentSegment2D (ref a, ref b, ref p, ref q);
			Assert.IsTrue (f);

		}

		[Test]
		public void SegmentSegment2D_without_float_false()
		{
			//the Segment 1
			Vector3 a = new Vector3 (0, 0, 0);
			Vector3 b = new Vector3 (1, 0, 1);

			//the segment 2
			Vector3 p = new Vector3(1, 0, 0);
			Vector3 q = new Vector3(2, 0, 1);
			bool f = MathHelper.Intersection.SegmentSegment2D (ref a, ref b, ref p, ref q);
			Assert.IsFalse (f);

		}

		[Test]
		public void SegmentSegment2D_with_float_success()
		{
			//the Segment 1
			Vector3 a = new Vector3 (0, 0, 0);
			Vector3 b = new Vector3 (1, 0, 1);

			//the segment 2
			Vector3 p = new Vector3(0, 0, 1);
			Vector3 q = new Vector3(1, 0, 0);
			float m;
			float n;
			bool f = MathHelper.Intersection.SegmentSegment2D (ref a, ref b, ref p, ref q, out m, out n);
			Assert.IsTrue (f);

		}

		[Test]
		public void SegmentSegment2D_with_float_false()
		{
			//the Segment 1
			Vector3 a = new Vector3 (0, 0, 0);
			Vector3 b = new Vector3 (1, 0, 1);

			//the segment 2
			Vector3 p = new Vector3(1, 0, 0);
			Vector3 q = new Vector3(2, 0, 1);
			float m;
			float n;
			bool f = MathHelper.Intersection.SegmentSegment2D (ref a, ref b, ref p, ref q, out m, out n);
			Assert.IsFalse(f);
		}


	}
}
