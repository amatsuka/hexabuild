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
