using System.Collections.Generic;
using Game.Grid;
using Game.Roads;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public sealed class RoadNetworkTests
    {
        static HexMap Map(int radius = 6)
        {
            var tiles = new List<TileData>();
            foreach (var coord in HexMap.CoordsInRadius(radius))
                tiles.Add(new TileData(coord, coord == HexCoord.Zero));

            return new HexMap(radius, tiles);
        }

        [Test]
        public void RoadNextToMetropolis_IsConnectedByItself()
        {
            var network = new RoadNetwork(Map());

            Assert.IsTrue(network.Build(new HexCoord(1, 0)));

            Assert.IsTrue(network.HasRoad(new HexCoord(1, 0)));
            Assert.IsTrue(network.IsConnected(new HexCoord(1, 0)));
        }

        [Test]
        public void ChainOfRoads_ConnectsEveryTileInIt()
        {
            var network = new RoadNetwork(Map());

            for (var q = 1; q <= 4; q++)
                network.Build(new HexCoord(q, 0));

            for (var q = 1; q <= 4; q++)
                Assert.IsTrue(network.IsConnected(new HexCoord(q, 0)), $"плитка ({q}, 0) должна быть подключена");
        }

        [Test]
        public void RoadWithoutChainToMetropolis_StaysDisconnected()
        {
            var network = new RoadNetwork(Map());

            network.Build(new HexCoord(3, 0));

            Assert.IsTrue(network.HasRoad(new HexCoord(3, 0)));
            Assert.IsFalse(network.IsConnected(new HexCoord(3, 0)));
        }

        [Test]
        public void ClosingTheGap_ConnectsTheWholeChainAtOnce()
        {
            var network = new RoadNetwork(Map());
            network.Build(new HexCoord(1, 0));
            network.Build(new HexCoord(3, 0));
            network.Build(new HexCoord(4, 0));

            Assert.IsFalse(network.IsConnected(new HexCoord(3, 0)));
            Assert.IsFalse(network.IsConnected(new HexCoord(4, 0)));

            network.Build(new HexCoord(2, 0));

            foreach (var q in new[] { 1, 2, 3, 4 })
                Assert.IsTrue(network.IsConnected(new HexCoord(q, 0)), $"плитка ({q}, 0) должна подключиться");
        }

        [Test]
        public void DiagonalNeighbourIsNotAdjacent_SoItDoesNotConnect()
        {
            var network = new RoadNetwork(Map());
            network.Build(new HexCoord(1, 0));

            network.Build(new HexCoord(2, 1));

            Assert.IsFalse(network.IsConnected(new HexCoord(2, 1)));
        }

        [Test]
        public void Build_OnTheSameTileTwice_ChangesNothing()
        {
            var network = new RoadNetwork(Map());
            network.Build(new HexCoord(1, 0));

            Assert.IsFalse(network.Build(new HexCoord(1, 0)));
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

        [Test]
        public void Changed_FiresOncePerBuiltRoad()
        {
            var network = new RoadNetwork(Map());
            var events = 0;
            network.Changed += () => events++;

            network.Build(new HexCoord(1, 0));
            network.Build(new HexCoord(1, 0));
            network.Build(new HexCoord(2, 0));

            Assert.AreEqual(2, events);
        }

        [Test]
        public void PathToMetropolis_RunsFromTheTileToTheCentre()
        {
            var network = new RoadNetwork(Map());
            for (var q = 1; q <= 3; q++)
                network.Build(new HexCoord(q, 0));
            var path = new List<HexCoord>();

            Assert.IsTrue(network.TryFindPathToMetropolis(new HexCoord(3, 0), path));

            Assert.AreEqual(
                new[] { new HexCoord(3, 0), new HexCoord(2, 0), new HexCoord(1, 0), HexCoord.Zero },
                path.ToArray());
        }

        [Test]
        public void PathToMetropolis_TakesTheShortWayWhenTwoExist()
        {
            var network = new RoadNetwork(Map());
            foreach (var coord in new[]
                     {
                         new HexCoord(1, 0), new HexCoord(1, 1), new HexCoord(0, 2),
                         new HexCoord(0, 1)
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
            network.Build(new HexCoord(3, 0));
            var path = new List<HexCoord>();

            Assert.IsFalse(network.TryFindPathToMetropolis(new HexCoord(3, 0), path));
            Assert.IsEmpty(path);
        }

        [Test]
        public void Connectivity_FollowsRoadsAroundACorner()
        {
            var network = new RoadNetwork(Map());
            var path = new[] { new HexCoord(0, 1), new HexCoord(0, 2), new HexCoord(1, 2), new HexCoord(2, 2) };

            foreach (var coord in path)
                network.Build(coord);

            foreach (var coord in path)
                Assert.IsTrue(network.IsConnected(coord), $"плитка {coord} должна быть подключена");
        }
    }
}
