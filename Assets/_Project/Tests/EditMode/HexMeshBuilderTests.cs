using Game.Grid;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public sealed class HexMeshBuilderTests
    {
        [Test]
        public void Mesh_IsSixTrianglesAroundCenter()
        {
            var mesh = HexMeshBuilder.Shared;

            Assert.AreEqual(7, mesh.vertexCount);
            Assert.AreEqual(18, mesh.triangles.Length);
        }

        [Test]
        public void Corners_LieOnCircumscribedCircle()
        {
            var vertices = HexMeshBuilder.Shared.vertices;

            Assert.AreEqual(Vector3.zero, vertices[0]);
            for (var i = 1; i < vertices.Length; i++)
                Assert.AreEqual(HexCoord.Size, new Vector2(vertices[i].x, vertices[i].y).magnitude, 1e-4f);
        }

        [Test]
        public void Shape_IsPointyTop_WidthMatchesHexWidth()
        {
            var vertices = HexMeshBuilder.Shared.vertices;
            var maxX = 0f;
            var maxY = 0f;

            foreach (var vertex in vertices)
            {
                maxX = Mathf.Max(maxX, Mathf.Abs(vertex.x));
                maxY = Mathf.Max(maxY, Mathf.Abs(vertex.y));
            }

            Assert.AreEqual(HexCoord.Width * 0.5f, maxX, 1e-4f);
            Assert.Greater(maxY, maxX, "вершина должна быть выше грани: гекс pointy-top");
        }

        [Test]
        public void Triangles_FaceTheCamera_LookingAlongPositiveZ()
        {
            AssertFacesCamera(HexMeshBuilder.Shared);
        }

        [Test]
        public void DecorShapes_FaceTheCameraToo()
        {
            AssertFacesCamera(ShapeMeshes.Triangle);
            AssertFacesCamera(ShapeMeshes.Bar);
        }

        static void AssertFacesCamera(Mesh mesh)
        {
            var vertices = mesh.vertices;
            var triangles = mesh.triangles;

            for (var i = 0; i < triangles.Length; i += 3)
            {
                var a = vertices[triangles[i]];
                var b = vertices[triangles[i + 1]];
                var c = vertices[triangles[i + 2]];
                var signedArea = (b.x - a.x) * (c.y - a.y) - (c.x - a.x) * (b.y - a.y);

                Assert.Less(signedArea, 0f,
                    $"{mesh.name}: треугольник {i / 3} обходится против часовой и будет срезан backface culling");
            }
        }
    }
}
