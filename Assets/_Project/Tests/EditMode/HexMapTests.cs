using Game.Grid;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public sealed class HexMapTests
    {
        [Test]
        public void RadiusSix_Holds127Tiles()
        {
            var map = new HexMap(6);

            Assert.AreEqual(127, map.Count);
        }

        [TestCase(0, 1)]
        [TestCase(1, 7)]
        [TestCase(2, 19)]
        public void SmallRadii_HoldHexNumberOfTiles(int radius, int expected)
        {
            Assert.AreEqual(expected, new HexMap(radius).Count);
        }

        [Test]
        public void Metropolis_SitsAtOrigin()
        {
            var map = new HexMap(6);

            Assert.AreEqual(HexCoord.Zero, map.Metropolis.Coord);
            Assert.IsTrue(map.Metropolis.IsMetropolis);
        }

        [Test]
        public void OnlyCenterTile_IsMetropolis()
        {
            var map = new HexMap(6);
            var metropolisCount = 0;

            foreach (var tile in map.Tiles.Values)
                if (tile.IsMetropolis)
                    metropolisCount++;

            Assert.AreEqual(1, metropolisCount);
        }

        [Test]
        public void EveryTile_LiesWithinRadius()
        {
            var map = new HexMap(6);

            foreach (var coord in map.Tiles.Keys)
                Assert.LessOrEqual(HexCoord.Distance(HexCoord.Zero, coord), 6);
        }

        [Test]
        public void TryGetTile_InsideField_ReturnsTileWithSameCoord()
        {
            var map = new HexMap(6);

            Assert.IsTrue(map.TryGetTile(new HexCoord(-3, 2), out var tile));
            Assert.AreEqual(new HexCoord(-3, 2), tile.Coord);
        }

        [Test]
        public void TryGetTile_OutsideField_ReturnsFalse()
        {
            var map = new HexMap(6);

            Assert.IsFalse(map.Contains(new HexCoord(7, 0)));
            Assert.IsFalse(map.TryGetTile(new HexCoord(7, 0), out _));
        }
    }
}
