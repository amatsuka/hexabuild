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
        public void FourteenRows_Hold83Tiles()
        {
            Assert.AreEqual(83, Build(Rows).Count);
        }

        /// <summary>У самой Метрополии кривая упирается в потолок роста: там ряд шире нижнего ровно на плитку.</summary>
        [TestCase(1, 1)]
        [TestCase(2, 3)]
        [TestCase(3, 6)]
        public void FirstRows_AddOneTileEach(int rows, int expected)
        {
            Assert.AreEqual(expected, Build(rows).Count);
        }

        /// <summary>
        /// Ряд не бывает шире нижнего больше чем на плитку и не бывает уже: иначе плитки повисли бы
        /// в воздухе, а поле пошло бы бочкой вместо раструба.
        /// </summary>
        [Test]
        public void RowWidth_GrowsUpwardByOneTileAtMost()
        {
            var perRow = WidthPerRow(Build(Rows));

            Assert.AreEqual(1, perRow[0], "нижний ряд — одна Метрополия");
            for (var row = 1; row < Rows; row++)
            {
                Assert.GreaterOrEqual(perRow[row], perRow[row - 1], $"ряд {row} уже нижнего");
                Assert.LessOrEqual(perRow[row], perRow[row - 1] + 1, $"ряд {row} шире нижнего больше чем на плитку");
            }
        }

        /// <summary>
        /// Раструб замедленный: он раскрывается у Метрополии и упирается в потолок ширины наверху,
        /// а не растёт линейно до самой кромки. Без этого поле не влезает в вертикальный экран.
        /// </summary>
        [Test]
        public void Flare_SlowsDownTowardsTheTopRow()
        {
            var perRow = WidthPerRow(Build(Rows));

            Assert.AreEqual(HexMap.TopRowTiles, perRow[Rows - 1], "верхний ряд шире потолка");
            Assert.Less(perRow[Rows - 1] - perRow[Rows / 2], perRow[Rows / 2] - perRow[0],
                "верхняя половина поля расширяется не медленнее нижней — раструб линейный");
        }

        /// <summary>Поле выше, чем шире: игра идёт в вертикальной ориентации.</summary>
        [Test]
        public void Field_IsTallerThanItIsWide()
        {
            var map = Build(Rows);
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);

            foreach (var coord in map.Tiles.Keys)
            {
                var plane = coord.ToPlane();
                min = Vector2.Min(min, plane);
                max = Vector2.Max(max, plane);
            }

            Assert.Less(max.x - min.x, max.y - min.y, "поле шире, чем выше");
        }

        static Dictionary<int, int> WidthPerRow(HexMap map)
        {
            var perRow = new Dictionary<int, int>();
            foreach (var coord in map.Tiles.Keys)
                perRow[coord.R] = perRow.GetValueOrDefault(coord.R) + 1;

            return perRow;
        }

        /// <summary>
        /// Раструб не уезжает вбок: ряд стоит по оси Метрополии или в полуплитке от неё — ряду
        /// такой же ширины, как нижний, на оси места нет вовсе, — и стороны ухода чередуются,
        /// так что поле в среднем остаётся на оси.
        /// </summary>
        [Test]
        public void EveryRow_StaysOnTheMetropolisAxis()
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

            var drift = 0f;
            foreach (var row in span)
            {
                var centre = (row.Value.Min + row.Value.Max) * 0.5f;
                Assert.LessOrEqual(Mathf.Abs(centre), 0.5f + 1e-4f, $"ряд {row.Key} уехал больше чем на полплитки");
                drift += centre;
            }

            Assert.LessOrEqual(Mathf.Abs(drift / span.Count), 0.15f, "поле в целом сползло с оси Метрополии");
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
        /// Ряд шире нижнего не больше чем на плитку и уходит вбок не больше чем на полплитки,
        /// поэтому каждая плитка стоит на плитке ряда ниже. Это и держит поле связным.
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
