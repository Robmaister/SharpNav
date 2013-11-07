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
	public class RegionTests
	{
		[Test]
		public void Region_AvoidDuplicateFloors_Success()
		{
			var reg = new Region(1);

			reg.AddUniqueFloorRegion(10);
			reg.AddUniqueFloorRegion(10);
			reg.AddUniqueFloorRegion(20);
			
			Assert.AreEqual(reg.FloorRegions[1], 20);
			Assert.AreEqual(reg.FloorRegions.Count, 2);
		}

		[Test]
		public void Region_RemoveAdjacentConnections_Success()
		{
			var reg = new Region(1);

			reg.Connections.Add(10);
			reg.Connections.Add(20);
			reg.Connections.Add(20);
			reg.removeAdjacentNeighbours();

			Assert.AreEqual(reg.Connections[1], 20);
		}

		[Test]
		public void Region_ConnectedBorder_Success()
		{
			var reg = new Region(1);

			reg.Connections.Add(10);
			reg.Connections.Add(20);
			reg.Connections.Add(30);

			Assert.AreEqual(reg.isRegionConnectedToBorder(), false);
		}

		[Test]
		public void Region_ReplaceNeighbor_Success()
		{
			var reg = new Region(1);

			reg.Connections.Add(10);
			reg.Connections.Add(20);
			reg.Connections.Add(30);

			reg.AddUniqueFloorRegion(10);
			reg.AddUniqueFloorRegion(20);
			reg.AddUniqueFloorRegion(30);

			reg.replaceNeighbour(10, 0);

			Assert.AreEqual(reg.Connections[0], 0);
			Assert.AreEqual(reg.FloorRegions[0], 0);
		}

		[Test]
		public void Region_MergeWithOther_Success()
		{
			var reg1 = new Region(1);
			var reg2 = new Region(2);

			reg1.Connections.Add(2);
			reg1.AddUniqueFloorRegion(1);
			reg1.AddUniqueFloorRegion(3);

			Assert.True(reg1.CanMergeWithRegion(reg2));
			Assert.True(reg2.CanMergeWithRegion(reg1));
		}

	}
}