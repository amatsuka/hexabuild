using Game.UI;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// Корона финального экрана — единственный меш проекта, который никуда не встаёт: он живёт
    /// только в кадре пекаря иконок. Проверяем поэтому не место, а то, что фигура вообще
    /// смотрит наружу: вывернутая грань на снимке даёт дыру, и заметить её негде.
    /// </summary>
    public sealed class CrownMeshTests
    {
        [Test]
        public void Crown_StandsInItsOwnSquare()
        {
            var bounds = CrownMesh.Build().bounds;

            Assert.LessOrEqual(bounds.size.x, 1f, "корона шире своего квадрата");
            Assert.LessOrEqual(bounds.size.y, 1.2f, "корона выше своего квадрата");
            Assert.Greater(bounds.size.z, 0f, "у короны нет толщины, и стенкам света не поймать");
        }

        [Test]
        public void Crown_HasPeaksOverTheBand()
        {
            var vertices = CrownMesh.Build().vertices;
            var top = 0f;

            foreach (var vertex in vertices)
                top = Mathf.Max(top, vertex.y);

            var peaks = 0;
            foreach (var vertex in vertices)
                if (vertex.y > 0.3f && Mathf.Abs(vertex.x) > 0.4f)
                    peaks++;

            Assert.Greater(top, 0.5f, "среднего зубца нет");
            Assert.Greater(peaks, 0, "боковых зубцов нет, силуэт не читается короной");
        }

        /// <summary>
        /// Каждый треугольник обойдён так, что его нормаль по обходу смотрит туда же, куда
        /// записанная в меш. Разойдись они — половина фигуры на снимке пропадёт.
        /// </summary>
        [Test]
        public void Crown_EveryTriangle_FacesTheWayItsNormalPoints()
        {
            var mesh = CrownMesh.Build();
            var vertices = mesh.vertices;
            var normals = mesh.normals;
            var triangles = mesh.triangles;

            for (var i = 0; i < triangles.Length; i += 3)
            {
                var a = vertices[triangles[i]];
                var b = vertices[triangles[i + 1]];
                var c = vertices[triangles[i + 2]];
                var winding = Vector3.Cross(b - a, c - a).normalized;

                Assert.Greater(
                    Vector3.Dot(winding, normals[triangles[i]]), 0.5f,
                    $"треугольник {i / 3} вывернут: обход спорит с нормалью");
            }
        }

        /// <summary>
        /// Лицевая грань смотрит на зрителя, задняя — от него, а фигура в целом вывернута
        /// наружу, а не внутрь. По впадине между зубцами «прочь от центра» не проверить —
        /// там наружу как раз к центру, — поэтому стенки считаются суммой по всей фигуре.
        /// </summary>
        [Test]
        public void Crown_Faces_LookOutward()
        {
            var mesh = CrownMesh.Build();
            var vertices = mesh.vertices;
            var normals = mesh.normals;
            var triangles = mesh.triangles;
            var centre = mesh.bounds.center;

            for (var i = 0; i < vertices.Length; i++)
            {
                if (Mathf.Abs(normals[i].z) < 0.9f)
                    continue;

                Assert.Greater(
                    (vertices[i].z - centre.z) * normals[i].z, 0f, $"грань {i} смотрит внутрь короны");
            }

            var outward = 0f;
            for (var i = 0; i < triangles.Length; i += 3)
            {
                var a = vertices[triangles[i]];
                var b = vertices[triangles[i + 1]];
                var c = vertices[triangles[i + 2]];
                outward += Vector3.Dot((a + b + c) / 3f - centre, Vector3.Cross(b - a, c - a));
            }

            Assert.Greater(outward, 0f, "фигура вывернута наизнанку: нормали смотрят внутрь");
        }
    }
}
