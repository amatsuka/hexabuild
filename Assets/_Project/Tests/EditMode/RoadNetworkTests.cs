using System.Collections.Generic;
using Game.Grid;
using Game.Roads;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public sealed class RoadNetworkTests
    {
        static HexMap Map(int rows = 10)
        {
            var tiles = new List<TileData>();
            foreach (var coord in HexMap.CoordsInFlare(rows))
                tiles.Add(new TileData(coord, coord == HexCoord.Zero));

            return new HexMap(rows, tiles);
        }

        [Test]
        public void RoadNextToMetropolis_IsConnectedByItself()
        {
            var network = new RoadNetwork(Map());

            Assert.IsTrue(network.Build(new HexCoord(0, 1)));

            Assert.IsTrue(network.HasRoad(new HexCoord(0, 1)));
            Assert.IsTrue(network.IsConnected(new HexCoord(0, 1)));
        }

        [Test]
        public void ChainOfRoads_ConnectsEveryTileInIt()
        {
            var network = new RoadNetwork(Map());

            for (var row = 1; row <= 4; row++)
                network.Build(new HexCoord(0, row));

            for (var row = 1; row <= 4; row++)
                Assert.IsTrue(network.IsConnected(new HexCoord(0, row)), $"плитка (0, {row}) должна быть подключена");
        }

        [Test]
        public void RoadWithoutChainToMetropolis_StaysDisconnected()
        {
            var network = new RoadNetwork(Map());

            network.Build(new HexCoord(0, 3));

            Assert.IsTrue(network.HasRoad(new HexCoord(0, 3)));
            Assert.IsFalse(network.IsConnected(new HexCoord(0, 3)));
        }

        [Test]
        public void ClosingTheGap_ConnectsTheWholeChainAtOnce()
        {
            var network = new RoadNetwork(Map());
            network.Build(new HexCoord(0, 1));
            network.Build(new HexCoord(0, 3));
            network.Build(new HexCoord(0, 4));

            Assert.IsFalse(network.IsConnected(new HexCoord(0, 3)));
            Assert.IsFalse(network.IsConnected(new HexCoord(0, 4)));

            network.Build(new HexCoord(0, 2));

            foreach (var row in new[] { 1, 2, 3, 4 })
                Assert.IsTrue(network.IsConnected(new HexCoord(0, row)), $"плитка (0, {row}) должна подключиться");
        }

        [Test]
        public void DiagonalNeighbourIsNotAdjacent_SoItDoesNotConnect()
        {
            var network = new RoadNetwork(Map());
            network.Build(new HexCoord(0, 1));

            network.Build(new HexCoord(-2, 2));

            Assert.AreEqual(2, HexCoord.Distance(new HexCoord(0, 1), new HexCoord(-2, 2)), "плитки должны быть по диагонали");
            Assert.IsFalse(network.IsConnected(new HexCoord(-2, 2)));
        }

        [Test]
        public void Build_OnTheSameTileTwice_ChangesNothing()
        {
            var network = new RoadNetwork(Map());
            network.Build(new HexCoord(0, 1));

            Assert.IsFalse(network.Build(new HexCoord(0, 1)));
            Assert.AreEqual(1, network.Roads.Count);
        }

        [Test]
        public void Build_OutsideFieldOrOnMetropolis_IsRejected()
        {
            var network = new RoadNetwork(Map());

            Assert.IsFalse(network.Build(new HexCoord(9, 9)));
            Assert.IsFalse(network.Build(HexCoord.Zero));
            Assert.IsEmpty(network.Roads);
        }

        /// <summary>Три взаимно смежные дороги не должны рисоваться треугольником.</summary>
        [Test]
        public void TriangleOfRoads_KeepsOnlyOneLinkPerTile()
        {
            var network = new RoadNetwork(Map());
            var left = new HexCoord(-1, 1);
            var right = new HexCoord(0, 1);
            var top = new HexCoord(-1, 2);

            network.Build(left);
            network.Build(right);
            network.Build(top);

            // обе нижние плитки цепляются за Метрополию, верхняя — ровно за одну из них
            Assert.IsTrue(network.IsRouteLink(left, HexCoord.Zero));
            Assert.IsTrue(network.IsRouteLink(right, HexCoord.Zero));
            Assert.IsFalse(network.IsRouteLink(left, right), "перемычка между соседями не нужна");

            var linksToTop = 0;
            if (network.IsRouteLink(top, left)) linksToTop++;
            if (network.IsRouteLink(top, right)) linksToTop++;
            Assert.AreEqual(1, linksToTop, "у верхней плитки должен быть один путь вниз");
        }

        /// <summary>
        /// Липкий родитель. `(-1,2)` смежна и с `(0,1)`, и с `(-1,1)`, а обе смежны с Метрополией:
        /// оба пути вниз одной длины. На полном пересчёте порядок очереди BFS отдал бы `(-1,2)`
        /// новому соседу, и уже построенная дорога перерисовалась бы у игрока на глазах.
        /// </summary>
        [Test]
        public void ParentOfAConnectedRoad_SurvivesANewNeighbour()
        {
            var network = new RoadNetwork(Map());
            network.Build(new HexCoord(0, 1));
            network.Build(new HexCoord(-1, 2));
            Assert.IsTrue(network.TryGetParent(new HexCoord(-1, 2), out var before));

            network.Build(new HexCoord(-1, 1));

            Assert.IsTrue(network.TryGetParent(new HexCoord(-1, 2), out var after));
            Assert.AreEqual(before, after, $"родитель {new HexCoord(-1, 2)} переехал с {before} на {after}");
        }

        /// <summary>Ни одна дорога не двигается — не только та, у которой появился второй путь.</summary>
        [Test]
        public void NoParentMoves_WhileTheNetworkGrows()
        {
            var network = new RoadNetwork(Map());
            var built = new List<HexCoord>();
            var seen = new Dictionary<HexCoord, HexCoord>();

            foreach (var coord in new[]
                     {
                         new HexCoord(0, 1), new HexCoord(0, 2), new HexCoord(-1, 2), new HexCoord(-1, 1),
                         new HexCoord(-2, 2), new HexCoord(-1, 3), new HexCoord(-2, 3), new HexCoord(0, 3)
                     })
            {
                network.Build(coord);
                built.Add(coord);
                Assert.IsTrue(network.IsConnected(coord), $"{coord} должна подключаться сразу: липкость проверяем на связной сети");

                foreach (var road in built)
                {
                    if (!network.TryGetParent(road, out var parent))
                        continue;

                    if (seen.TryGetValue(road, out var known))
                        Assert.AreEqual(known, parent, $"после постройки {coord} родитель {road} переехал");
                    else
                        seen[road] = parent;
                }
            }
        }

        /// <summary>
        /// Оторванный кусок — исключение из липкости: его дерево росло от собственного корня и
        /// смотрело бы не в ту сторону. При смыкании с Метрополией он перекладывается целиком.
        /// </summary>
        [Test]
        public void DisconnectedCluster_IsRebuiltFromTheMetropolis_WhenItReconnects()
        {
            var network = new RoadNetwork(Map());
            network.Build(new HexCoord(0, 3));
            network.Build(new HexCoord(0, 2));

            network.Build(new HexCoord(0, 1));

            Assert.IsTrue(network.TryGetParent(new HexCoord(0, 2), out var lower));
            Assert.AreEqual(new HexCoord(0, 1), lower, "нижняя дорога куска должна смотреть вниз");
            Assert.IsTrue(network.TryGetParent(new HexCoord(0, 3), out var upper));
            Assert.AreEqual(new HexCoord(0, 2), upper, "верхняя дорога куска должна смотреть вниз");

            foreach (var row in new[] { 1, 2, 3 })
                Assert.IsTrue(network.IsConnected(new HexCoord(0, row)));
        }

        /// <summary>
        /// Цена липкости, названная в спеке 3.4: маршрут иногда на шаг длиннее кратчайшего.
        /// `(-2,2)` вниз цепляется только через `(-1,2)`, пока `(-1,1)` пустая. Достроенная позже
        /// `(-1,1)` даёт путь на шаг короче, но маршрут не переезжает — и картинка не дёргается.
        /// </summary>
        [Test]
        public void PathToMetropolis_KeepsTheLongRoute_WhenAShortcutAppearsLater()
        {
            var network = new RoadNetwork(Map());
            foreach (var coord in new[] { new HexCoord(0, 1), new HexCoord(-1, 2), new HexCoord(-2, 2) })
                network.Build(coord);
            var path = new List<HexCoord>();
            network.TryFindPathToMetropolis(new HexCoord(-2, 2), path);
            Assert.AreEqual(4, path.Count, "путь в обход: (-2,2) → (-1,2) → (0,1) → Метрополия");

            network.Build(new HexCoord(-1, 1));

            network.TryFindPathToMetropolis(new HexCoord(-2, 2), path);

            Assert.AreEqual(4, path.Count, "короткий путь через (-1,1) появился, но маршрут остался прежним");
            Assert.AreEqual(new HexCoord(-1, 2), path[1]);
            Assert.AreEqual(1, HexCoord.Distance(new HexCoord(-2, 2), new HexCoord(-1, 1)),
                "условие теста: (-1,1) смежна с (-2,2) и с Метрополией, короткий путь и правда есть");
        }

        [Test]
        public void EveryConnectedRoad_HasExactlyOneParent()
        {
            var network = new RoadNetwork(Map());
            var roads = new[]
            {
                new HexCoord(0, 1), new HexCoord(-1, 1), new HexCoord(-1, 2),
                new HexCoord(0, 2), new HexCoord(-2, 2), new HexCoord(-2, 3)
            };

            foreach (var coord in roads)
                network.Build(coord);

            foreach (var coord in roads)
            {
                Assert.IsTrue(network.TryGetParent(coord, out var parent), $"у {coord} нет пути вниз");
                Assert.AreEqual(1, HexCoord.Distance(coord, parent), $"родитель {parent} не смежен с {coord}");
            }
        }

        [Test]
        public void RouteLinks_MatchTheDeliveryPath()
        {
            var network = new RoadNetwork(Map());
            foreach (var row in new[] { 1, 2, 3 })
                network.Build(new HexCoord(0, row));
            var path = new List<HexCoord>();

            network.TryFindPathToMetropolis(new HexCoord(0, 3), path);

            for (var i = 0; i < path.Count - 1; i++)
                Assert.IsTrue(network.IsRouteLink(path[i], path[i + 1]), $"участок {path[i]} → {path[i + 1]} не нарисован");
        }

        [Test]
        public void DisconnectedRing_IsDrawnWithoutAClosingLink()
        {
            var network = new RoadNetwork(Map());
            var ring = new[] { new HexCoord(0, 4), new HexCoord(-1, 5), new HexCoord(0, 5) };

            foreach (var coord in ring)
                network.Build(coord);

            var links = 0;
            for (var i = 0; i < ring.Length; i++)
            for (var j = i + 1; j < ring.Length; j++)
                if (network.IsRouteLink(ring[i], ring[j]))
                    links++;

            Assert.AreEqual(2, links, "три плитки соединяются двумя участками, а не тремя");
            foreach (var coord in ring)
                Assert.IsFalse(network.IsConnected(coord));
        }

        [Test]
        public void Changed_FiresOncePerBuiltRoad()
        {
            var network = new RoadNetwork(Map());
            var events = 0;
            network.Changed += () => events++;

            network.Build(new HexCoord(0, 1));
            network.Build(new HexCoord(0, 1));
            network.Build(new HexCoord(0, 2));

            Assert.AreEqual(2, events);
        }

        [Test]
        public void PathToMetropolis_RunsFromTheTileToTheCentre()
        {
            var network = new RoadNetwork(Map());
            for (var row = 1; row <= 3; row++)
                network.Build(new HexCoord(0, row));
            var path = new List<HexCoord>();

            Assert.IsTrue(network.TryFindPathToMetropolis(new HexCoord(0, 3), path));

            Assert.AreEqual(
                new[] { new HexCoord(0, 3), new HexCoord(0, 2), new HexCoord(0, 1), HexCoord.Zero },
                path.ToArray());
        }

        [Test]
        public void PathToMetropolis_TakesTheShortWayWhenTwoExist()
        {
            var network = new RoadNetwork(Map());
            foreach (var coord in new[]
                     {
                         new HexCoord(0, 1), new HexCoord(-1, 1), new HexCoord(-1, 2), new HexCoord(0, 2)
                     })
                network.Build(coord);
            var path = new List<HexCoord>();

            network.TryFindPathToMetropolis(new HexCoord(0, 2), path);

            Assert.AreEqual(3, path.Count, "короткий путь идёт через (0,1)");
            Assert.AreEqual(new HexCoord(0, 1), path[1]);
        }

        [Test]
        public void PathToMetropolis_ForDisconnectedRoad_IsNotFound()
        {
            var network = new RoadNetwork(Map());
            network.Build(new HexCoord(0, 3));
            var path = new List<HexCoord>();

            Assert.IsFalse(network.TryFindPathToMetropolis(new HexCoord(0, 3), path));
            Assert.IsEmpty(path);
        }

        [Test]
        public void Connectivity_FollowsRoadsAroundACorner()
        {
            var network = new RoadNetwork(Map());
            var path = new[] { new HexCoord(0, 1), new HexCoord(-1, 2), new HexCoord(-2, 3), new HexCoord(-2, 4) };

            foreach (var coord in path)
                network.Build(coord);

            foreach (var coord in path)
                Assert.IsTrue(network.IsConnected(coord), $"плитка {coord} должна быть подключена");
        }
    }
}
