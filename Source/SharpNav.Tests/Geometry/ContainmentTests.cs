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
	public class ContainmentTests
	{
		[Test]
		public void PointInPoly_InternalPoint_Success()
		{
			Vector3 pt = new Vector3(0.0f, 0.0f, 0.5f);

			Vector3[] poly = new Vector3[3];
			poly[0] = new Vector3(0.0f, 0.0f, 1.0f);
			poly[1] = new Vector3(-1.0f, 0.0f, 0.0f);
			poly[2] = new Vector3(1.0f, 0.0f, 0.0f);

			bool isInPoly = Containment.PointInPoly(pt, poly, poly.Length);

			Assert.IsTrue(isInPoly);
		}

		[Test]
		public void PointInPoly_BoundaryPoint_Success()
		{
			Vector3 pt = new Vector3(0.0f, 0.0f, 0.0f);

			Vector3[] poly = new Vector3[3];
			poly[0] = new Vector3(0.0f, 0.0f, 1.0f);
			poly[1] = new Vector3(-1.0f, 0.0f, 0.0f);
			poly[2] = new Vector3(1.0f, 0.0f, 0.0f);

			bool isInPoly = Containment.PointInPoly(pt, poly, poly.Length);

			Assert.IsTrue(isInPoly);
		}

		[Test]
		public void PointInPoly_ExternalPoint_Success()
		{
			Vector3 pt = new Vector3(-1.0f, 0.0f, -1.0f);

			Vector3[] poly = new Vector3[3];
			poly[0] = new Vector3(0.0f, 0.0f, 1.0f);
			poly[1] = new Vector3(-1.0f, 0.0f, 0.0f);
			poly[2] = new Vector3(1.0f, 0.0f, 0.0f);

			bool isInPoly = Containment.PointInPoly(pt, poly, poly.Length);

			Assert.IsFalse(isInPoly);
		}
	}
}
