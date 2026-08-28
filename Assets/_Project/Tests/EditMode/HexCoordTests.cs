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
            Assert.AreEqual(Vector2.zero, HexCoord.Zero.ToWorld());
        }

        [Test]
        public void ToWorld_NeighborCenters_AreOneWidthApart()
        {
            var origin = HexCoord.Zero.ToWorld();

            foreach (var neighbor in HexCoord.Zero.Neighbors())
                Assert.AreEqual(HexCoord.Width, Vector2.Distance(origin, neighbor.ToWorld()), 1e-4f);
        }

        [Test]
        public void ToWorld_IsPointyTop_RowStepIsSmallerThanColumnStep()
        {
            var columnStep = new HexCoord(1, 0).ToWorld().x;
            var rowStep = new HexCoord(0, 1).ToWorld().y;

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

        [Test]
        public void FromWorld_PointNearHexEdge_ResolvesToThatHex()
        {
            var coord = new HexCoord(1, -2);
            var almostEdge = coord.ToWorld() + new Vector2(HexCoord.Width * 0.45f, 0f);

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
    }
}
