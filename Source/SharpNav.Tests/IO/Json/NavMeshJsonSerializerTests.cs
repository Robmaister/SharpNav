// Copyright (c) 2015 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
// Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE

using NUnit.Framework;

using SharpNav.IO.Json;

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
