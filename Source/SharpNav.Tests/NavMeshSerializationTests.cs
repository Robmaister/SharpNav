using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using SharpNav.Geometry;
using SharpNav.IO;

namespace SharpNav.Tests
{
    [TestFixture]
    class NavMeshSerializationTests
    {
        [Test]
        public void JsonSerializationTest()
        {
            var objModel = new ObjModel("nav_test.obj");
            TiledNavMesh mesh = NavMesh.Generate(objModel.GetTriangles(), NavMeshGenerationSettings.Default);
            new NavMeshJsonSerializer().Serialize("mesh.snj", mesh);

            TiledNavMesh deserializedMesh = new NavMeshJsonSerializer().Deserialize("mesh.snj");
        }
    }
}
