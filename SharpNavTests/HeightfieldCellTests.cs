using System;

using NUnit.Framework;

using SharpNav;

namespace SharpNavTests
{
	[TestFixture]
	public class HeightfieldCellTests
	{
		[Test]
		public void AddSpan_Valid_DoesNotThrow()
		{
			var cell = new Heightfield.Cell();
			Assert.DoesNotThrow(() => cell.AddSpan(new Heightfield.Span()));
		}
	}
}
