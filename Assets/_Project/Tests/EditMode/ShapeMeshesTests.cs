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
                MeshAssert.FacesCamera(mesh);
                MeshAssert.FitsUnitSquare(mesh);
            }
        }

        [Test]
        public void EveryDecorShape_HasItsOwnMesh()
        {
            var seen = new HashSet<Mesh>();

            foreach (DecorShape shape in Enum.GetValues(typeof(DecorShape)))
                Assert.IsTrue(seen.Add(ShapeMeshes.Decor(shape)), $"{shape}: меш повторяет чужой");
        }

        /// <summary>Кувшинка — круг с вырезанным клином, иначе она не отличается от валуна.</summary>
        [Test]
        public void Lily_HasANotch()
        {
            var covered = 0f;
            foreach (var vertex in ShapeMeshes.Decor(DecorShape.Lily).vertices)
                if (vertex != Vector3.zero)
                    covered = Mathf.Max(covered, Mathf.Atan2(vertex.y, vertex.x) * Mathf.Rad2Deg);

            Assert.Less(covered, 175f, "клин не вырезан: обод замкнулся в целый круг");
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
