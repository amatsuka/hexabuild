using System.Collections.Generic;
using Game.Grid;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public sealed class HexMapTests
    {
        static HexMap Build(int rows)
        {
            var tiles = new List<TileData>();
            foreach (var coord in HexMap.CoordsInFlare(rows))
                tiles.Add(new TileData(coord, coord == HexCoord.Zero));

            return new HexMap(rows, tiles);
        }

        [Test]
        public void TenRows_Hold100Tiles()
        {
            Assert.AreEqual(100, Build(10).Count);
        }

        [TestCase(1, 1)]
        [TestCase(2, 4)]
        [TestCase(3, 9)]
        public void EachRow_AddsTwoTilesToTheFlare(int rows, int expected)
        {
            Assert.AreEqual(expected, Build(rows).Count);
        }

        [Test]
        public void RowWidth_GrowsUpwardFromTheMetropolis()
        {
            var map = Build(10);
            var perRow = new Dictionary<int, int>();

            foreach (var coord in map.Tiles.Keys)
                perRow[coord.R] = perRow.GetValueOrDefault(coord.R) + 1;

            for (var row = 0; row < 10; row++)
                Assert.AreEqual(2 * row + 1, perRow[row], $"ряд {row}");
        }

        /// <summary>
        /// Ряд шире нижнего на две плитки, поэтому крайние плитки ряда опираются только на соседа
        /// сбоку — это геометрия раструба. Важно другое: от Метрополии достижима каждая плитка.
        /// </summary>
        [Test]
        public void EveryTile_IsReachableFromTheMetropolis()
        {
            var map = Build(10);
            var visited = new HashSet<HexCoord> { HexCoord.Zero };
            var frontier = new Queue<HexCoord>();
            frontier.Enqueue(HexCoord.Zero);

            while (frontier.Count > 0)
                foreach (var neighbor in map.NeighborsOf(frontier.Dequeue()))
                    if (visited.Add(neighbor.Coord))
                        frontier.Enqueue(neighbor.Coord);

            Assert.AreEqual(map.Count, visited.Count, "до части плиток нельзя добраться по соседям");
        }

        [Test]
        public void OutermostTilesOfARow_LeanOnTheirRowNeighbour()
        {
            var map = Build(10);

            foreach (var coord in map.Tiles.Keys)
            {
                var neighbors = new List<TileData>(map.NeighborsOf(coord));
                Assert.IsNotEmpty(neighbors, $"плитка {coord} осталась без соседей");
            }
        }

        [Test]
        public void Metropolis_SitsAtOrigin()
        {
            var map = Build(10);

            Assert.AreEqual(HexCoord.Zero, map.Metropolis.Coord);
            Assert.IsTrue(map.Metropolis.IsMetropolis);
        }

        [Test]
        public void EveryTile_SitsOnOrAboveTheMetropolisRow()
        {
            foreach (var coord in Build(10).Tiles.Keys)
            {
                Assert.GreaterOrEqual(coord.R, 0);
                Assert.Less(coord.R, 10);
            }
        }

        [Test]
        public void TryGetTile_InsideField_ReturnsTileWithSameCoord()
        {
            var map = Build(10);

            Assert.IsTrue(map.TryGetTile(new HexCoord(-1, 2), out var tile));
            Assert.AreEqual(new HexCoord(-1, 2), tile.Coord);
        }

        [Test]
        public void TryGetTile_OutsideField_ReturnsFalse()
        {
            var map = Build(10);

            Assert.IsFalse(map.Contains(new HexCoord(0, -1)));
            Assert.IsFalse(map.TryGetTile(new HexCoord(5, 1), out _));
        }

        [Test]
        public void NeighborsOf_Metropolis_AreTheTwoTilesAboveIt()
        {
            var neighbors = new List<TileData>(Build(10).NeighborsOf(HexCoord.Zero));

            Assert.AreEqual(2, neighbors.Count);
            foreach (var neighbor in neighbors)
                Assert.AreEqual(1, neighbor.Coord.R);
        }

        [Test]
        public void NeighborsOf_InnerTile_AreSix()
        {
            var neighbors = new List<TileData>(Build(10).NeighborsOf(new HexCoord(-1, 2)));

            Assert.AreEqual(6, neighbors.Count);
        }
    }
}
