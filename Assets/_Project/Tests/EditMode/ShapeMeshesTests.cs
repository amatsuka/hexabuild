using System;
using System.Collections.Generic;
using Game.Economy;
using Game.Grid;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public sealed class ShapeMeshesTests
    {
        static readonly ResourceType[] DepositTypes = { ResourceType.Wood, ResourceType.Stone, ResourceType.Ore };

        [TestCaseSource(nameof(DepositTypes))]
        public void DepositModels_FaceTheCamera(ResourceType type)
        {
            foreach (var exhausted in new[] { false, true })
            foreach (var accent in new[] { false, true })
                MeshAssert.FacesCamera(ShapeMeshes.Deposit(type, exhausted, accent));
        }

        [TestCaseSource(nameof(DepositTypes))]
        public void DepositModels_FitTheUnitSquare(ResourceType type)
        {
            foreach (var exhausted in new[] { false, true })
            foreach (var accent in new[] { false, true })
                MeshAssert.FitsUnitSquare(ShapeMeshes.Deposit(type, exhausted, accent));
        }

        /// <summary>
        /// Истощение меняет форму, а не только альфу: пенёк, осевший валун, обломанные кристаллы.
        /// Одинаковый меш означал бы, что игрок отличает выработанную плитку только по прозрачности.
        /// </summary>
        [TestCaseSource(nameof(DepositTypes))]
        public void SpentModel_HasItsOwnShape(ResourceType type)
        {
            var full = ShapeMeshes.Deposit(type, false, false);
            var spent = ShapeMeshes.Deposit(type, true, false);

            Assert.AreNotSame(full, spent);
            Assert.Less(
                spent.bounds.size.y, full.bounds.size.y,
                $"{type}: выработанная моделька должна быть ниже целой");
        }

        [Test]
        public void SameModel_IsBuiltOnce()
        {
            Assert.AreSame(
                ShapeMeshes.Deposit(ResourceType.Ore, false, true),
                ShapeMeshes.Deposit(ResourceType.Ore, false, true));
        }

        [Test]
        public void BodyAndAccent_AreDifferentMeshes()
        {
            foreach (var type in DepositTypes)
                Assert.AreNotSame(
                    ShapeMeshes.Deposit(type, false, false),
                    ShapeMeshes.Deposit(type, false, true),
                    $"{type}: огранка совпала с телом, второй цвет негде показать");
        }

        [Test]
        public void EveryDecorShape_IsDrawableAndFitsTheTile()
        {
            foreach (DecorShape shape in Enum.GetValues(typeof(DecorShape)))
            {
                var mesh = ShapeMeshes.Decor(shape);

                Assert.Greater(mesh.triangles.Length, 0, $"{shape}: пустой меш");

                if (ShapeMeshes.StandsOnGround(shape))
                {
                    MeshAssert.StandsOnGround(mesh);
                    MeshAssert.FitsUnitFootprint(mesh);
                }
                else
                {
                    MeshAssert.FacesCamera(mesh);
                    MeshAssert.FitsUnitSquare(mesh);
                }
            }
        }

        /// <summary>
        /// Трава и дюны — объёмные холмики, а не плоские фигуры. Пока они лежали в XY, под
        /// наклоном камеры они были укорочены и с обратной стороны невидимы, а рядом с
        /// объёмными моделями леса и скал читались наклейкой.
        /// </summary>
        [Test]
        public void GrassAndDunes_AreVolumes_NotFlatCutouts()
        {
            foreach (var shape in new[] { DecorShape.Tussock, DecorShape.Dune })
            {
                Assert.IsTrue(ShapeMeshes.StandsOnGround(shape), $"{shape}: фигура обязана стоять на земле");

                var size = ShapeMeshes.Decor(shape).bounds.size;
                Assert.Greater(size.x, 0.1f, $"{shape}: нет ширины");
                Assert.Greater(size.y, 0.1f, $"{shape}: нет высоты");
                Assert.Greater(size.z, 0.1f, $"{shape}: фигура плоская — это снова наклейка");
            }
        }

        [Test]
        public void EveryDecorShape_HasItsOwnMesh()
        {
            var seen = new HashSet<Mesh>();

            foreach (DecorShape shape in Enum.GetValues(typeof(DecorShape)))
                Assert.IsTrue(seen.Add(ShapeMeshes.Decor(shape)), $"{shape}: меш повторяет чужой");
        }
    }

    /// <summary>Проверки, общие для всех процедурных мешей проекта.</summary>
    static class MeshAssert
    {
        /// <summary>Обход по часовой стрелке: против часовой грань срежет backface culling.</summary>
        public static void FacesCamera(Mesh mesh)
        {
            var vertices = mesh.vertices;
            var triangles = mesh.triangles;

            Assert.Greater(triangles.Length, 0, $"{mesh.name}: меш пустой");
            for (var i = 0; i < triangles.Length; i += 3)
            {
                var a = vertices[triangles[i]];
                var b = vertices[triangles[i + 1]];
                var c = vertices[triangles[i + 2]];
                var signedArea = (b.x - a.x) * (c.y - a.y) - (c.x - a.x) * (b.y - a.y);

                Assert.Less(signedArea, 0f, $"{mesh.name}: треугольник {i / 3} обходится против часовой");
            }
        }

        /// <summary>
        /// Объёмная фигура стоит основанием на земле: вью ставит её в `y = 0` и не поднимает
        /// на половину роста, как плоскую. Утопленная в землю половина холмика — это дыра.
        /// </summary>
        public static void StandsOnGround(Mesh mesh)
        {
            var bounds = mesh.bounds;

            Assert.AreEqual(0f, bounds.min.y, 1e-4f, $"{mesh.name}: основание не на земле");
            Assert.Greater(bounds.max.y, 0f, $"{mesh.name}: фигура не имеет высоты");
        }

        /// <summary>След объёмной фигуры на земле умещается в квадрат 1×1: масштабирует её вью.</summary>
        public static void FitsUnitFootprint(Mesh mesh)
        {
            foreach (var vertex in mesh.vertices)
            {
                Assert.LessOrEqual(Mathf.Abs(vertex.x), 0.5f + 1e-4f, $"{mesh.name}: вершина вылезла по ширине");
                Assert.LessOrEqual(Mathf.Abs(vertex.z), 0.5f + 1e-4f, $"{mesh.name}: вершина вылезла по глубине");
                Assert.LessOrEqual(vertex.y, 1f + 1e-4f, $"{mesh.name}: вершина вылезла по высоте");
            }
        }

        /// <summary>Фигура живёт в квадрате 1×1 с центром в нуле: масштабирует её уже вью.</summary>
        public static void FitsUnitSquare(Mesh mesh)
        {
            foreach (var vertex in mesh.vertices)
            {
                Assert.LessOrEqual(Mathf.Abs(vertex.x), 0.5f + 1e-4f, $"{mesh.name}: вершина вылезла по ширине");
                Assert.LessOrEqual(Mathf.Abs(vertex.y), 0.5f + 1e-4f, $"{mesh.name}: вершина вылезла по высоте");
            }
        }
    }
}
