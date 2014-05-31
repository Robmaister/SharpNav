#region License
/**
 * Copyright (c) 2013-2014 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
 * Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE
 */
#endregion

using System;
using System.Collections; 
using System.Collections.Generic; 

using NUnit.Framework;

using SharpNav;
using SharpNav.Geometry;

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
	public class JSONTests
	{
		[Test]
		public void WriteJSONTest()
        {
            TiledNavMesh mesh = new TiledNavMesh(null);
            mesh.SaveJson("mesh.json"); 
		}


        [Test]
        public void ReadJSONTest()
        {
            TiledNavMesh mesh = TiledNavMesh.LoadJson("mesh.json"); 
        }
	}
}
