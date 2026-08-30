using System.Collections.Generic;
using Game.Grid;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public sealed class HexCoordTests
    {
        [Test]
        public void Neighbors_AreSixDistinctCoordsAtDistanceOne()
        {
            var origin = new HexCoord(2, -1);

            var neighbors = origin.Neighbors();

            Assert.AreEqual(6, neighbors.Length);
            CollectionAssert.AllItemsAreUnique(neighbors);
            foreach (var neighbor in neighbors)
                Assert.AreEqual(1, HexCoord.Distance(origin, neighbor));
        }

        [Test]
        public void Distance_ToItself_IsZero()
        {
            Assert.AreEqual(0, HexCoord.Distance(new HexCoord(3, -2), new HexCoord(3, -2)));
        }

        [TestCase(0, 0, 3, 0, 3)]
        [TestCase(0, 0, 0, -4, 4)]
        [TestCase(0, 0, 2, -1, 2)]
        [TestCase(-2, 1, 2, -1, 4)]
        [TestCase(1, 1, -1, -1, 4)]
        public void Distance_MatchesAxialFormula(int aq, int ar, int bq, int br, int expected)
        {
            var a = new HexCoord(aq, ar);
            var b = new HexCoord(bq, br);

            Assert.AreEqual(expected, HexCoord.Distance(a, b));
            Assert.AreEqual(expected, HexCoord.Distance(b, a));
        }

        [Test]
        public void S_CompletesCubeCoordinates()
        {
            var coord = new HexCoord(2, -5);

            Assert.AreEqual(0, coord.Q + coord.R + coord.S);
        }

        [Test]
        public void ToWorld_Origin_IsWorldZero()
        {
            Assert.AreEqual(Vector3.zero, HexCoord.Zero.ToWorld());
        }

        /// <summary>Земля — плоскость XZ. Высота плитки живёт в её трансформе, а не в координате.</summary>
        [Test]
        public void ToWorld_LiesOnTheGroundPlane()
        {
            for (var q = -6; q <= 6; q++)
            for (var r = -6; r <= 6; r++)
                Assert.AreEqual(0f, new HexCoord(q, r).ToWorld().y, 1e-4f);
        }

        /// <summary>Мировая точка — это плоская, разложенная по земле: x вправо, y плоскости в z.</summary>
        [Test]
        public void ToWorld_IsToPlaneLaidOnTheGround()
        {
            var coord = new HexCoord(2, -3);
            var plane = coord.ToPlane();
            var world = coord.ToWorld();

            Assert.AreEqual(plane.x, world.x, 1e-4f);
            Assert.AreEqual(plane.y, world.z, 1e-4f);
        }

        [Test]
        public void ToWorld_NeighborCenters_AreOneWidthApart()
        {
            var origin = HexCoord.Zero.ToWorld();

            foreach (var neighbor in HexCoord.Zero.Neighbors())
                Assert.AreEqual(HexCoord.Width, Vector3.Distance(origin, neighbor.ToWorld()), 1e-4f);
        }

        [Test]
        public void ToWorld_IsPointyTop_RowStepIsSmallerThanColumnStep()
        {
            var columnStep = new HexCoord(1, 0).ToWorld().x;
            var rowStep = new HexCoord(0, 1).ToWorld().z;

            Assert.AreEqual(HexCoord.Width, columnStep, 1e-4f);
            Assert.Less(rowStep, columnStep);
        }

        [Test]
        public void FromWorld_RoundTripsEveryCoordOfRadiusSix()
        {
            for (var q = -6; q <= 6; q++)
            for (var r = Mathf.Max(-6, -q - 6); r <= Mathf.Min(6, -q + 6); r++)
            {
                var coord = new HexCoord(q, r);
                Assert.AreEqual(coord, HexCoord.FromWorld(coord.ToWorld()), $"round trip failed for {coord}");
            }
        }

        /// <summary>Высота точки роли не играет: под лучом та же плитка, что и на земле.</summary>
        [Test]
        public void FromWorld_IgnoresHeight()
        {
            var coord = new HexCoord(-2, 4);
            var above = coord.ToWorld() + new Vector3(0f, 3.5f, 0f);

            Assert.AreEqual(coord, HexCoord.FromWorld(above));
        }

        [Test]
        public void FromWorld_PointNearHexEdge_ResolvesToThatHex()
        {
            var coord = new HexCoord(1, -2);
            var almostEdge = coord.ToWorld() + new Vector3(HexCoord.Width * 0.45f, 0f, 0f);

            Assert.AreEqual(coord, HexCoord.FromWorld(almostEdge));
        }

        [Test]
        public void Operators_AddSubtractAndCompare()
        {
            var a = new HexCoord(1, 2);
            var b = new HexCoord(-3, 1);

            Assert.AreEqual(new HexCoord(-2, 3), a + b);
            Assert.AreEqual(new HexCoord(4, 1), a - b);
            Assert.IsTrue(new HexCoord(1, 2) == a);
            Assert.IsTrue(a != b);
        }

        /// <summary>
        /// Хеш координаты держит внешний вид карты: ширину дорог и раскладку декора. Он обязан
        /// быть одним и тем же от запуска к запуску, поэтому `GetHashCode` для этого не годится.
        /// </summary>
        [Test]
        public void Hash01_IsStableAndInsideTheUnitRange()
        {
            for (var q = -6; q <= 6; q++)
            for (var r = 0; r <= 12; r++)
            for (var salt = 0; salt < 6; salt++)
            {
                var value = new HexCoord(q, r).Hash01(salt);

                Assert.GreaterOrEqual(value, 0f, $"({q},{r}) соль {salt}");
                Assert.Less(value, 1f, $"({q},{r}) соль {salt}");
                Assert.AreEqual(value, new HexCoord(q, r).Hash01(salt), "повторный вызов дал другое значение");
            }
        }

        [Test]
        public void Hash01_TellsCoordinatesAndSaltsApart()
        {
            var values = new HashSet<float>();
            for (var q = -6; q <= 6; q++)
            for (var r = 0; r <= 12; r++)
                values.Add(new HexCoord(q, r).Hash01(0));

            // 91 координата: совпадений быть почти не должно, иначе карта выйдет полосатой
            Assert.Greater(values.Count, 80, "хеш склеивает разные координаты в одно значение");

            var coord = new HexCoord(2, 3);
            Assert.AreNotEqual(coord.Hash01(0), coord.Hash01(1), "разные соли должны давать разные потоки");
        }

        /// <summary>Ступени ширины дороги и количество декора берутся так — перекос сразу заметен.</summary>
        [Test]
        public void Hash01_SpreadsAcrossBucketsEvenly()
        {
            var buckets = new int[4];
            var total = 0;

            for (var q = -8; q <= 8; q++)
            for (var r = 0; r <= 16; r++)
            {
                buckets[(int)(new HexCoord(q, r).Hash01(7) * buckets.Length)]++;
                total++;
            }

            foreach (var bucket in buckets)
                Assert.Greater(bucket, total / buckets.Length / 2, "одна из корзин почти пустая: хеш перекошен");
        }
    }
}
