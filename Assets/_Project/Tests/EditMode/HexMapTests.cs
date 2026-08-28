using System.Collections.Generic;
using Game.Grid;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public sealed class HexMapTests
    {
        static HexMap Build(int radius)
        {
            var tiles = new List<TileData>();
            foreach (var coord in HexMap.CoordsInRadius(radius))
                tiles.Add(new TileData(coord, coord == HexCoord.Zero));

            return new HexMap(radius, tiles);
        }

        [Test]
        public void RadiusSix_Holds127Tiles()
        {
            Assert.AreEqual(127, Build(6).Count);
        }

        [TestCase(0, 1)]
        [TestCase(1, 7)]
        [TestCase(2, 19)]
        public void SmallRadii_HoldHexNumberOfTiles(int radius, int expected)
        {
            Assert.AreEqual(expected, Build(radius).Count);
        }

        [Test]
        public void Metropolis_SitsAtOrigin()
        {
            var map = Build(6);

            Assert.AreEqual(HexCoord.Zero, map.Metropolis.Coord);
            Assert.IsTrue(map.Metropolis.IsMetropolis);
        }

        [Test]
        public void EveryTile_LiesWithinRadius()
        {
            foreach (var coord in Build(6).Tiles.Keys)
                Assert.LessOrEqual(HexCoord.Distance(HexCoord.Zero, coord), 6);
        }

        [Test]
        public void TryGetTile_InsideField_ReturnsTileWithSameCoord()
        {
            var map = Build(6);

            Assert.IsTrue(map.TryGetTile(new HexCoord(-3, 2), out var tile));
            Assert.AreEqual(new HexCoord(-3, 2), tile.Coord);
        }

        [Test]
        public void TryGetTile_OutsideField_ReturnsFalse()
        {
            var map = Build(6);

            Assert.IsFalse(map.Contains(new HexCoord(7, 0)));
            Assert.IsFalse(map.TryGetTile(new HexCoord(7, 0), out _));
        }

        [Test]
        public void NeighborsOf_CenterTile_AreSix()
        {
            var neighbors = new List<TileData>(Build(6).NeighborsOf(HexCoord.Zero));

            Assert.AreEqual(6, neighbors.Count);
        }

        [Test]
        public void NeighborsOf_EdgeTile_SkipsCoordsOutsideField()
        {
            var neighbors = new List<TileData>(Build(6).NeighborsOf(new HexCoord(6, 0)));

            Assert.AreEqual(3, neighbors.Count);
            foreach (var neighbor in neighbors)
                Assert.LessOrEqual(HexCoord.Distance(HexCoord.Zero, neighbor.Coord), 6);
        }
    }
}
