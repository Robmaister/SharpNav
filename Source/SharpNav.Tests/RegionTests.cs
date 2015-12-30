// Copyright (c) 2013-2014 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
// Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE

using System;

using NUnit.Framework;

using SharpNav;

namespace SharpNav.Tests
{
	[TestFixture]
	public class RegionTests
	{
		[Test]
		public void Region_AvoidDuplicateFloors_Success()
		{
			var reg = new Region(1);

			reg.AddUniqueFloorRegion(new RegionId(10));
			reg.AddUniqueFloorRegion(new RegionId(10));
			reg.AddUniqueFloorRegion(new RegionId(20));

			Assert.AreEqual((int)reg.FloorRegions[0], 10);
			Assert.AreEqual((int)reg.FloorRegions[1], 20);
			Assert.AreEqual(reg.FloorRegions.Count, 2);
		}

		[Test]
		public void Region_RemoveAdjacentConnections_Success()
		{
			var reg = new Region(1);

			reg.Connections.Add(new RegionId(10));
			reg.Connections.Add(new RegionId(20));
			reg.Connections.Add(new RegionId(20));
			reg.RemoveAdjacentNeighbors();

			Assert.AreEqual((int)reg.Connections[1], 20);
		}

		[Test]
		public void Region_ConnectedBorder_Success()
		{
			var reg = new Region(1);

			reg.Connections.Add(new RegionId(10));
			reg.Connections.Add(new RegionId(20));
			reg.Connections.Add(new RegionId(30));

			Assert.AreEqual(reg.IsConnectedToBorder(), false);
		}

		[Test]
		public void Region_ReplaceNeighbor_Success()
		{
			var reg = new Region(1);

			reg.Connections.Add(new RegionId(10));
			reg.Connections.Add(new RegionId(20));
			reg.Connections.Add(new RegionId(30));

			reg.AddUniqueFloorRegion(new RegionId(10));
			reg.AddUniqueFloorRegion(new RegionId(20));
			reg.AddUniqueFloorRegion(new RegionId(30));

			reg.ReplaceNeighbor(new RegionId(10), RegionId.Null);

			Assert.AreEqual((int)reg.Connections[0], 0);
			Assert.AreEqual((int)reg.FloorRegions[0], 0);
		}

		[Test]
		public void Region_MergeWithOther_Success()
		{
			var reg1 = new Region(1);
			var reg2 = new Region(2);

			reg1.Connections.Add(new RegionId(2));
			reg1.AddUniqueFloorRegion(new RegionId(1));
			reg1.AddUniqueFloorRegion(new RegionId(3));

			Assert.True(reg1.CanMergeWith(reg2));
			Assert.True(reg2.CanMergeWith(reg1));
		}

	}
}
