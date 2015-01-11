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

using MathHelper = SharpNav.MathHelper;

namespace SharpNav.Tests
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
	}
}
