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

            reg.addUniqueFloorRegion(10);
            reg.addUniqueFloorRegion(10);
            reg.addUniqueFloorRegion(20);
            
            Assert.AreEqual(reg.getFloorRegions()[1], 20);
            Assert.AreEqual(reg.getFloorRegions().Count, 2);
        }

        [Test]
        public void Region_RemoveAdjacentConnections_Success()
        {
            var reg = new Region(1);

            reg.getConnections().Add(10);
            reg.getConnections().Add(20);
            reg.getConnections().Add(20);
            reg.removeAdjacentNeighbours();

            Assert.AreEqual(reg.getConnections()[1], 20);
        }

        [Test]
        public void Region_ConnectedBorder_Success()
        {
            var reg = new Region(1);

            reg.getConnections().Add(10);
            reg.getConnections().Add(20);
            reg.getConnections().Add(30);

            Assert.AreEqual(reg.isRegionConnectedToBorder(), false);
        }

        [Test]
        public void Region_ReplaceNeighbor_Success()
        {
            var reg = new Region(1);

            reg.getConnections().Add(10);
            reg.getConnections().Add(20);
            reg.getConnections().Add(30);

            reg.addUniqueFloorRegion(10);
            reg.addUniqueFloorRegion(20);
            reg.addUniqueFloorRegion(30);

            reg.replaceNeighbour(10, 0);

            Assert.AreEqual(reg.getConnections()[0], 0);
            Assert.AreEqual(reg.getFloorRegions()[0], 0);
        }

        [Test]
        public void Region_MergeWithOther_Success()
        {
            var reg1 = new Region(1);
            var reg2 = new Region(2);

            reg1.getConnections().Add(2);
            reg1.addUniqueFloorRegion(1);
            reg1.addUniqueFloorRegion(3);

            Assert.True(reg1.canMergeWithRegion(reg2));
            Assert.True(reg2.canMergeWithRegion(reg1));
        }

    }
}