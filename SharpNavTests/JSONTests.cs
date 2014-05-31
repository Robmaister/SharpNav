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
			NavMeshGenerationSettings settings = NavMeshGenerationSettings.Default;
			CompactHeightfield heightField = new CompactHeightfield(new Heightfield(
                new BBox3(1, 1, 1, 5, 5, 5), settings), settings); 
            PolyMesh polyMesh = new PolyMesh(new ContourSet(heightField, settings), 8);
            PolyMeshDetail polyMeshDetail = new PolyMeshDetail(polyMesh, heightField, settings); 
            
            NavMeshBuilder buildData = new NavMeshBuilder(polyMesh, polyMeshDetail, new SharpNav.Pathfinding.OffMeshConnection[0], settings);
            TiledNavMesh mesh = new TiledNavMesh(buildData);
            mesh.SaveJson("mesh.json"); 
		}


        [Test]
        public void ReadJSONTest()
        {
            TiledNavMesh mesh = TiledNavMesh.LoadJson("mesh.json"); 
        }
	}
}
