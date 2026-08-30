using System;
using System.Collections.Generic;
using Game.Economy;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public sealed class ResourceShapeTests
    {
        static IEnumerable<ResourceType> AllTypes()
        {
            foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
                yield return type;
        }

        [TestCaseSource(nameof(AllTypes))]
        public void EveryResource_HasADrawableIcon(ResourceType type)
        {
            var mesh = ResourceShape.Icon(type);

            MeshAssert.FacesCamera(mesh);
            MeshAssert.FitsUnitSquare(mesh);
        }

        [Test]
        public void EveryResource_LooksDifferent()
        {
            var seen = new HashSet<string>();

            foreach (var type in AllTypes())
            {
                var outline = ResourceShape.Outline(type);
                var signature = new System.Text.StringBuilder();
                foreach (var point in outline.Vertices)
                    signature.Append(point.x.ToString("F3")).Append(',').Append(point.y.ToString("F3")).Append('|');

                Assert.IsTrue(seen.Add(signature.ToString()), $"{type}: иконка повторяет чужую");
            }
        }

        /// <summary>
        /// На поле иконка едет мешем, на складе её рисует `ResourceIcon` по тем же вершинам.
        /// Разойдись они — едущий ресурс и приехавший выглядели бы разными предметами.
        /// </summary>
        [TestCaseSource(nameof(AllTypes))]
        public void FieldMeshAndCanvasOutline_ShareTheSamePolygon(ResourceType type)
        {
            var outline = ResourceShape.Outline(type);
            var mesh = ResourceShape.Icon(type);

            Assert.AreEqual(outline.Vertices.Count, mesh.vertexCount);
            Assert.AreEqual(outline.Triangles.Count, mesh.triangles.Length);

            var vertices = mesh.vertices;
            for (var i = 0; i < vertices.Length; i++)
            {
                Assert.AreEqual(outline.Vertices[i].x, vertices[i].x, 1e-5f);
                Assert.AreEqual(outline.Vertices[i].y, vertices[i].y, 1e-5f);
            }
        }

        [Test]
        public void SameResource_BakesItsMeshOnce()
        {
            Assert.AreSame(ResourceShape.Icon(ResourceType.Ingot), ResourceShape.Icon(ResourceType.Ingot));
        }

        /// <summary>Щебень — три отдельных осколка, а не цельный камень.</summary>
        [Test]
        public void Gravel_IsBrokenIntoPieces()
        {
            var gravel = ResourceShape.Outline(ResourceType.Gravel);
            var stone = ResourceShape.Outline(ResourceType.Stone);

            Assert.Greater(
                gravel.Vertices.Count, stone.Vertices.Count,
                "щебень должен состоять из нескольких кусков, а валун — из одного");
        }
    }
}
