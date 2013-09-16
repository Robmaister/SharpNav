#region License
/**
 * Copyright (c) 2013 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
 * Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE
 */
#endregion

using System;

using NUnit.Framework;

using SharpNav;

namespace SharpNavTests
{
	[TestFixture]
	public class HeightfieldTests
	{
		[Test]
		public void Indexer_Valid_ReturnsCell()
		{
			var hf = new Heightfield(Vector3.Zero, Vector3.One, 2, 2, 2);

			Assert.IsNotNull(hf[0, 1]);
		}

		[Test]
		public void Indexer_NegativeX_Throws()
		{
			var hf = new Heightfield(Vector3.Zero, Vector3.One, 2, 2, 2);

			Assert.Throws<IndexOutOfRangeException>(() => { var c = hf[-1, 1]; });
		}

		[Test]
		public void Indexer_NegativeY_Throws()
		{
			var hf = new Heightfield(Vector3.Zero, Vector3.One, 2, 2, 2);

			Assert.Throws<IndexOutOfRangeException>(() => { var c = hf[1, -1]; });
		}

		[Test]
		public void Indexer_NegativeBoth_Throws()
		{
			var hf = new Heightfield(Vector3.Zero, Vector3.One, 2, 2, 2);

			Assert.Throws<IndexOutOfRangeException>(() => { var c = hf[-1, -1]; });
		}

		[Test]
		public void Indexer_TooLargeX_Throws()
		{
			var hf = new Heightfield(Vector3.Zero, Vector3.One, 2, 2, 2);

			Assert.Throws<IndexOutOfRangeException>(() => { var c = hf[2, 0]; });
		}

		[Test]
		public void Indexer_TooLargeY_Throws()
		{
			var hf = new Heightfield(Vector3.Zero, Vector3.One, 2, 2, 2);

			Assert.Throws<IndexOutOfRangeException>(() => { var c = hf[0, 2]; });
		}

		[Test]
		public void Indexer_TooLargeBoth_Throws()
		{
			var hf = new Heightfield(Vector3.Zero, Vector3.One, 2, 2, 2);

			Assert.Throws<IndexOutOfRangeException>(() => { var c = hf[3, 3]; });
		}
	}
}
