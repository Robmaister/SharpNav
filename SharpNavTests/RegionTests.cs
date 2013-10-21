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
        public void Region_AvoidDuplicateFloors()
        {
            var reg = new Region(1);
            Assert.AreEqual(reg.getFloorRegions().Count, 0);
            reg.addUniqueFloorRegion(10);
            reg.addUniqueFloorRegion(10);
            reg.addUniqueFloorRegion(20);
            Assert.AreEqual(reg.getFloor(1), 20);
            Assert.AreEqual(reg.getFloorRegions().Count, 2);
        }
    }
}