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

#if MONOGAME || XNA
using Microsoft.Xna.Framework;
#elif OPENTK
using OpenTK;
#elif SHARPDX
using SharpDX;
#elif UNITY3D
using UnityEngine;
#endif

namespace SharpNav.Tests
{
	[TestFixture]
	public class ContourTests
	{
		[Test]
		public void ContourConstructor()
		{
			//TODO directly build contours now that ContourSet allows it.
			//Build a 5x5 heightfield
			Heightfield hf = new Heightfield(new BBox3(Vector3.Zero, Vector3.One), (float)(1.0f / 5.0f), 0.02f);
			for (int i = 0; i < 5 * 5; i++)
			{
				hf[i].AddSpan(new Span(10, 20, Area.Default));
				hf[i].AddSpan(new Span(25, 30, Area.Default));
			}
			CompactHeightfield chf = new CompactHeightfield(hf, 2, 1);

			chf.BuildDistanceField();
			chf.BuildRegions(1, 1, 1);

			ContourSet contourSet = chf.BuildContourSet(1, 5, ContourBuildFlags.None);

			Assert.AreEqual(contourSet.Count, 2);
		}
	}
}
