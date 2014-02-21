#region License
/**
 * Copyright (c) 2013-2014 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
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
			var reg = new Region((RegionId)1);

			reg.AddUniqueFloorRegion((RegionId)10);
			reg.AddUniqueFloorRegion((RegionId)10);
			reg.AddUniqueFloorRegion((RegionId)20);

			Assert.AreEqual((int)reg.FloorRegions[0], 10);
			Assert.AreEqual((int)reg.FloorRegions[1], 20);
			Assert.AreEqual(reg.FloorRegions.Count, 2);
		}

		[Test]
		public void Region_RemoveAdjacentConnections_Success()
		{
			var reg = new Region((RegionId)1);

			reg.Connections.Add((RegionId)10);
			reg.Connections.Add((RegionId)20);
			reg.Connections.Add((RegionId)20);
			reg.RemoveAdjacentNeighbours();

			Assert.AreEqual((int)reg.Connections[1], 20);
		}

		[Test]
		public void Region_ConnectedBorder_Success()
		{
			var reg = new Region((RegionId)1);

			reg.Connections.Add((RegionId)10);
			reg.Connections.Add((RegionId)20);
			reg.Connections.Add((RegionId)30);

			Assert.AreEqual(reg.IsConnectedToBorder(), false);
		}

		[Test]
		public void Region_ReplaceNeighbor_Success()
		{
			var reg = new Region((RegionId)1);

			reg.Connections.Add((RegionId)10);
			reg.Connections.Add((RegionId)20);
			reg.Connections.Add((RegionId)30);

			reg.AddUniqueFloorRegion((RegionId)10);
			reg.AddUniqueFloorRegion((RegionId)20);
			reg.AddUniqueFloorRegion((RegionId)30);

			reg.ReplaceNeighbour((RegionId)10, 0);

			Assert.AreEqual((int)reg.Connections[0], 0);
			Assert.AreEqual((int)reg.FloorRegions[0], 0);
		}

		[Test]
		public void Region_MergeWithOther_Success()
		{
			var reg1 = new Region((RegionId)1);
			var reg2 = new Region((RegionId)2);

			reg1.Connections.Add((RegionId)2);
			reg1.AddUniqueFloorRegion((RegionId)1);
			reg1.AddUniqueFloorRegion((RegionId)3);

			Assert.True(reg1.CanMergeWith(reg2));
			Assert.True(reg2.CanMergeWith(reg1));
		}

	}
}