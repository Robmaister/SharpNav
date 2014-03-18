#region License
/**
 * Copyright (c) 2014 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
 * Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE
 */
#endregion

using System;

using NUnit.Framework;

using SharpNav;

namespace SharpNavTests
{
	[TestFixture]
	public class MathHelperTests
	{
		[Test]
		public void Clamp_Test_val_out_range()
		{
			int num = MathHelper.Clamp (3, 6, 10);
			Assert.AreEqual (num, 6);
		}

		[Test]
		public void Clamp_Test_val_in_range()
		{
			int num = MathHelper.Clamp (7, 4, 10);
			Assert.AreEqual (num, 7);
		}

		[Test]
		public void Clamp_Test_val_out_range_ref()
		{
			int num_r = 3;
			MathHelper.Clamp (ref num_r, 6, 10);
			Assert.AreEqual (num_r, 6);
		}

		[Test]
		public void Clamp_Test_val_in_range_ref()
		{
			int num_r = 7;
			MathHelper.Clamp (ref num_r, 6, 10);
			Assert.AreEqual (num_r, 7);
		}

		[Test]
		public void Clamp_Test_val_in_range_float()
		{
			float num = MathHelper.Clamp (7.56f, 6.75f, 10.89f);
			Assert.AreEqual (num, 7.56f);
		}

		[Test]
		public void Clamp_Test_val_out_range_float()
		{
			float num = MathHelper.Clamp (3.89f, 6.75f, 10.89f);
			Assert.AreEqual (num, 6.75f);
		}

		public void Clamp_Test_val_in_range_float_ref()
		{
			float num_r = 7.56f;
			MathHelper.Clamp (ref num_r, 6.75f, 10.89f);
			Assert.AreEqual (num_r, 7.56f);
		}

		[Test]
		public void Clamp_Test_val_out_range_float_ref()
		{
			float num_r = 3.89f;
			MathHelper.Clamp (ref num_r, 6.75f, 10.89f);
			Assert.AreEqual (num_r, 6.75f);
		}

		[Test]
		public void NextPowerOfTwo_PositiveInteger_Int()
		{
			int num = MathHelper.NextPowerOfTwo(5);

			Assert.AreEqual(num, 8);
		}

		[Test]
		public void NextPowerOfTwo_PositiveInteger_Uint()
		{
			uint num = MathHelper.NextPowerOfTwo((uint)5);

			Assert.AreEqual(num, 8);
		}

		[Test]
		public void NextPowerOfTwo_Zero_Int()
		{
			int num = MathHelper.NextPowerOfTwo(0);

			//the integer powers of 2 are positive, so the result should be 1
			//but num = 0
			Assert.AreEqual(num, 1);
		}

		[Test]
		public void NextPowerOfTwo_Zero_Uint()
		{
			uint num = MathHelper.NextPowerOfTwo((uint)0);

			//the integer powers of 2 are positive, so the result should be 1
			//but num = 0
			Assert.AreEqual(num, 1);
		}

		[Test]
		public void Log2_PositiveInteger_Int()
		{
			int num = MathHelper.Log2(65);

			Assert.AreEqual(num, 6);
		}
		
		[Test]
		public void Log2_PositiveInteger_Uint()
		{
			uint num = MathHelper.Log2((uint)65);

			Assert.AreEqual(num, 6);
		}

		[Test]
		public void Log2_Zero_Int()
		{
			int num = MathHelper.Log2(0);

			//This test expects 0, even though the result is undefined
			Assert.AreEqual(num, float.NaN);
		}

		[Test]
		public void Log2_Zero_Uint()
		{
			uint num = MathHelper.Log2((uint)0);

			//This test expects 0, even though the result is undefined
			Assert.AreEqual(num, float.NaN);
		}

		[Test]
		public void Log2_NegativeInteger_Int()
		{
			int num = MathHelper.Log2(-65);

			//This test expects -33, even though the result is undefined
			Assert.AreEqual(num, float.NaN);
		}
	}
}
