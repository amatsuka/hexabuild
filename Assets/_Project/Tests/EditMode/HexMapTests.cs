using System.Collections.Generic;
using Game.Grid;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public sealed class HexMapTests
    {
        const int Rows = 14;

        static HexMap Build(int rows)
        {
            var tiles = new List<TileData>();
            foreach (var coord in HexMap.CoordsInFlare(rows))
                tiles.Add(new TileData(coord, coord == HexCoord.Zero));

            return new HexMap(rows, tiles);
        }

        [Test]
        public void FourteenRows_Hold105Tiles()
        {
            Assert.AreEqual(105, Build(Rows).Count);
        }

        [TestCase(1, 1)]
        [TestCase(2, 3)]
        [TestCase(3, 6)]
        public void EachRow_AddsOneTileToTheFlare(int rows, int expected)
        {
            Assert.AreEqual(expected, Build(rows).Count);
        }

        [Test]
        public void RowWidth_GrowsUpwardFromTheMetropolis()
        {
            var map = Build(Rows);
            var perRow = new Dictionary<int, int>();

            foreach (var coord in map.Tiles.Keys)
                perRow[coord.R] = perRow.GetValueOrDefault(coord.R) + 1;

            for (var row = 0; row < Rows; row++)
                Assert.AreEqual(row + 1, perRow[row], $"ряд {row}");
        }

        /// <summary>Раструб симметричен: ряд стоит по центру над Метрополией, а не уезжает вбок.</summary>
        [Test]
        public void EveryRow_IsCentredOverTheMetropolis()
        {
            var map = Build(Rows);
            var span = new Dictionary<int, (float Min, float Max)>();

            foreach (var coord in map.Tiles.Keys)
            {
                var x = coord.ToPlane().x;
                span[coord.R] = span.TryGetValue(coord.R, out var known)
                    ? (Mathf.Min(known.Min, x), Mathf.Max(known.Max, x))
                    : (x, x);
            }

            foreach (var row in span)
                Assert.AreEqual(0f, row.Value.Min + row.Value.Max, 1e-4f, $"ряд {row.Key} сместился вбок");
        }

        [Test]
        public void EveryTile_IsReachableFromTheMetropolis()
        {
            var map = Build(Rows);
            var visited = new HashSet<HexCoord> { HexCoord.Zero };
            var frontier = new Queue<HexCoord>();
            frontier.Enqueue(HexCoord.Zero);

            while (frontier.Count > 0)
                foreach (var neighbor in map.NeighborsOf(frontier.Dequeue()))
                    if (visited.Add(neighbor.Coord))
                        frontier.Enqueue(neighbor.Coord);

            Assert.AreEqual(map.Count, visited.Count, "до части плиток нельзя добраться по соседям");
        }

        /// <summary>
        /// Замедленный раструб шире нижнего ряда на одну плитку, поэтому каждая плитка стоит на
        /// плитке ряда ниже. Это и держит поле связным.
        /// </summary>
        [Test]
        public void EveryTile_LeansOnTheRowBelow()
        {
            var map = Build(Rows);

            foreach (var coord in map.Tiles.Keys)
            {
                if (coord.R == 0)
                    continue;

                var below = 0;
                foreach (var neighbor in map.NeighborsOf(coord))
                    if (neighbor.Coord.R == coord.R - 1)
                        below++;

                Assert.Greater(below, 0, $"плитка {coord} висит в воздухе");
            }
        }

        [Test]
        public void Metropolis_SitsAtOrigin()
        {
            var map = Build(Rows);

            Assert.AreEqual(HexCoord.Zero, map.Metropolis.Coord);
            Assert.IsTrue(map.Metropolis.IsMetropolis);
        }

        [Test]
        public void EveryTile_SitsOnOrAboveTheMetropolisRow()
        {
            foreach (var coord in Build(Rows).Tiles.Keys)
            {
                Assert.GreaterOrEqual(coord.R, 0);
                Assert.Less(coord.R, Rows);
            }
        }

        [Test]
        public void TryGetTile_InsideField_ReturnsTileWithSameCoord()
        {
            var map = Build(Rows);

            Assert.IsTrue(map.TryGetTile(new HexCoord(-1, 2), out var tile));
            Assert.AreEqual(new HexCoord(-1, 2), tile.Coord);
        }

        [Test]
        public void TryGetTile_OutsideField_ReturnsFalse()
        {
            var map = Build(Rows);

            Assert.IsFalse(map.Contains(new HexCoord(0, -1)));
            Assert.IsFalse(map.TryGetTile(new HexCoord(5, 1), out _));
        }

        [Test]
        public void NeighborsOf_Metropolis_AreTheTwoTilesAboveIt()
        {
            var neighbors = new List<TileData>(Build(Rows).NeighborsOf(HexCoord.Zero));

            Assert.AreEqual(2, neighbors.Count);
            foreach (var neighbor in neighbors)
                Assert.AreEqual(1, neighbor.Coord.R);
        }

        [Test]
        public void NeighborsOf_InnerTile_AreSix()
        {
            var neighbors = new List<TileData>(Build(Rows).NeighborsOf(new HexCoord(-1, 2)));

            Assert.AreEqual(6, neighbors.Count);
        }
    }
}
