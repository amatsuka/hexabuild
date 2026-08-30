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
                Assert.AreEqual(HexCoord.Size, Plane(vertices[i]).magnitude, 1e-4f);
        }

        /// <summary>Крышка гекса лежит на земле: высота берётся из трансформа плитки, не из меша.</summary>
        [Test]
        public void Mesh_LiesFlatOnTheGround()
        {
            foreach (var vertex in HexMeshBuilder.Shared.vertices)
                Assert.AreEqual(0f, vertex.y, 1e-4f);
        }

        [Test]
        public void Shape_IsPointyTop_WidthMatchesHexWidth()
        {
            var maxAcross = 0f;
            var maxAlong = 0f;

            foreach (var vertex in HexMeshBuilder.Shared.vertices)
            {
                maxAcross = Mathf.Max(maxAcross, Mathf.Abs(vertex.x));
                maxAlong = Mathf.Max(maxAlong, Mathf.Abs(vertex.z));
            }

            Assert.AreEqual(HexCoord.Width * 0.5f, maxAcross, 1e-4f);
            Assert.Greater(maxAlong, maxAcross, "вершина должна быть дальше грани: гекс pointy-top");
        }

        [Test]
        public void GroundMeshes_FaceUp()
        {
            AssertFacesUp(HexMeshBuilder.Shared);
            AssertFacesUp(ShapeMeshes.HexRing);
        }

        /// <summary>
        /// Модельки и декор стоят вертикально в плоскости XY и остаются обращёнными к камере:
        /// на землю переехали только крышка плитки, ободок и ленты дорог.
        /// </summary>
        [Test]
        public void UprightShapes_StillFaceTheCamera()
        {
            AssertFacesCamera(ShapeMeshes.Triangle);
            AssertFacesCamera(ShapeMeshes.Decor(DecorShape.Conifer));
        }

        [Test]
        public void HexRing_HugsTheTileBorderFromOutside()
        {
            var vertices = ShapeMeshes.HexRing.vertices;

            Assert.AreEqual(12, vertices.Length);

            for (var i = 0; i < 6; i++)
            {
                var inner = Plane(vertices[i]).magnitude;
                var outer = Plane(vertices[i + 6]).magnitude;

                Assert.AreEqual(HexCoord.Size, inner, 1e-4f, "внутренний край ободка лежит на границе плитки");
                Assert.Greater(outer, inner, "внешний край ободка выходит за плитку");
            }
        }

        /// <summary>Призма: крышка на нуле, юбка уходит вниз — иначе гора читалась бы как яма.</summary>
        [Test]
        public void Prism_KeepsTheCapAtZeroAndDropsTheSkirtDown()
        {
            const float skirt = 0.3f;
            var vertices = HexMeshBuilder.Prism(skirt).vertices;

            var highest = float.MinValue;
            var lowest = float.MaxValue;
            foreach (var vertex in vertices)
            {
                highest = Mathf.Max(highest, vertex.y);
                lowest = Mathf.Min(lowest, vertex.y);
            }

            Assert.AreEqual(0f, highest, 1e-4f, "крышка призмы обязана лежать там же, где плоский гекс");
            Assert.AreEqual(-skirt, lowest, 1e-4f, "юбка обязана уходить вниз, а не вверх");
        }

        [Test]
        public void Prism_Cap_FacesUp()
        {
            var mesh = HexMeshBuilder.Prism(0.3f);
            var vertices = mesh.vertices;
            var triangles = mesh.triangles;

            // Первые шесть треугольников — крышка, дальше идёт юбка.
            for (var i = 0; i < 18; i += 3)
            {
                var normal = Vector3.Cross(
                    vertices[triangles[i + 1]] - vertices[triangles[i]],
                    vertices[triangles[i + 2]] - vertices[triangles[i]]);

                Assert.Greater(normal.y, 0f, $"треугольник крышки {i / 3} смотрит вниз");
            }
        }

        /// <summary>Стенки юбки смотрят наружу: внутрь смотрящая стенка невидима и рвёт силуэт.</summary>
        [Test]
        public void Prism_Skirt_FacesOutward()
        {
            var mesh = HexMeshBuilder.Prism(0.3f);
            var vertices = mesh.vertices;
            var triangles = mesh.triangles;

            for (var i = 18; i < triangles.Length; i += 3)
            {
                var a = vertices[triangles[i]];
                var b = vertices[triangles[i + 1]];
                var c = vertices[triangles[i + 2]];
                var normal = Vector3.Cross(b - a, c - a);
                var outward = Plane((a + b + c) / 3f);

                Assert.Greater(
                    Vector2.Dot(Plane(normal).normalized, outward.normalized), 0f,
                    $"стенка юбки {i / 3} смотрит внутрь плитки");
            }
        }

        static void AssertFacesUp(Mesh mesh)
        {
            var vertices = mesh.vertices;
            var triangles = mesh.triangles;

            for (var i = 0; i < triangles.Length; i += 3)
            {
                var normal = Vector3.Cross(
                    vertices[triangles[i + 1]] - vertices[triangles[i]],
                    vertices[triangles[i + 2]] - vertices[triangles[i]]);

                Assert.Greater(normal.y, 0f,
                    $"{mesh.name}: треугольник {i / 3} смотрит вниз и будет срезан backface culling");
            }
        }

        static void AssertFacesCamera(Mesh mesh)
        {
            var vertices = mesh.vertices;
            var triangles = mesh.triangles;

            for (var i = 0; i < triangles.Length; i += 3)
            {
                var normal = Vector3.Cross(
                    vertices[triangles[i + 1]] - vertices[triangles[i]],
                    vertices[triangles[i + 2]] - vertices[triangles[i]]);

                Assert.Less(normal.z, 0f,
                    $"{mesh.name}: треугольник {i / 3} отвернулся от камеры и будет срезан");
            }
        }

        /// <summary>Плоская координата вершины: земля — это XZ.</summary>
        static Vector2 Plane(Vector3 vertex) => new(vertex.x, vertex.z);
    }
}
