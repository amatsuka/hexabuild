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

            network.Build(new HexCoord(1, 2));

            Assert.IsFalse(network.IsConnected(new HexCoord(1, 2)));
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
