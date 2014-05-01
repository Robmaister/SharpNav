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

#if MONOGAME || XNA
using Microsoft.Xna.Framework;
#elif OPENTK
using OpenTK;
#elif SHARPDX
using SharpDX;
#elif UNITY3D
using UnityEngine;
#endif

namespace SharpNavTests
{
	[TestFixture]
	public class ContourTests
	{
		[Test]
		public void ContourConstructor()
		{
			//Build a 5x5 heightfield
			Heightfield hf = new Heightfield(Vector3.Zero, Vector3.One, (float)(1.0f / 5.0f), 0.02f);
			for (int i = 0; i < 5 * 5; i++)
			{
				hf[i].AddSpan(new Span(10, 20, AreaId.Walkable));
				hf[i].AddSpan(new Span(25, 30, AreaId.Walkable));
			}
			CompactHeightfield chf = new CompactHeightfield(hf, 2, 1);

			chf.BuildDistanceField();
			chf.BuildRegions(1, 1, 1);

			ContourSet contourSet = new ContourSet(chf, 1.0f, 10, 0);

			Assert.AreEqual(contourSet.Count, 2);
		}
	}
}
