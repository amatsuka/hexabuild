using System.Collections.Generic;
using Game.Economy;
using Game.Grid;
using Game.Roads;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public sealed class ProductionSystemTests
    {
        const float Interval = 3f;

        static HexMap MapWithDeposits(params (HexCoord coord, ResourceType type, int reserve)[] deposits)
        {
            var contents = new Dictionary<HexCoord, List<Deposit>>();
            foreach (var (coord, type, reserve) in deposits)
            {
                if (!contents.TryGetValue(coord, out var list))
                    contents[coord] = list = new List<Deposit>();

                list.Add(new Deposit(type, reserve));
            }

            var tiles = new List<TileData>();
            foreach (var coord in HexMap.CoordsInFlare(10))
            {
                contents.TryGetValue(coord, out var list);
                var tile = new TileData(coord, coord == HexCoord.Zero, list);
                if (coord != HexCoord.Zero)
                    tile.Reveal();

                tiles.Add(tile);
            }

            return new HexMap(10, tiles);
        }

        [Test]
        public void ConnectedTile_ProducesOneUnitPerInterval()
        {
            var coord = new HexCoord(0, 1);
            var map = MapWithDeposits((coord, ResourceType.Wood, 5));
            var roads = new RoadNetwork(map);
            roads.Build(coord);
            var production = new ProductionSystem(map, roads, Interval);
            var produced = 0;
            production.Produced += (_, _) => produced++;

            production.Tick(2.9f);
            Assert.AreEqual(0, produced, "до интервала выдачи быть не должно");

            production.Tick(0.2f);
            Assert.AreEqual(1, produced);

            production.Tick(Interval);
            Assert.AreEqual(2, produced);
        }

        [Test]
        public void DisconnectedTile_ProducesNothing()
        {
            var coord = new HexCoord(0, 3);
            var map = MapWithDeposits((coord, ResourceType.Wood, 5));
            var roads = new RoadNetwork(map);
            roads.Build(coord);
            var production = new ProductionSystem(map, roads, Interval);
            var produced = 0;
            production.Produced += (_, _) => produced++;

            production.Tick(Interval * 3f);

            Assert.AreEqual(0, produced);
        }

        [Test]
        public void TileWithoutRoad_ProducesNothing()
        {
            var coord = new HexCoord(0, 1);
            var map = MapWithDeposits((coord, ResourceType.Wood, 5));
            var roads = new RoadNetwork(map);
            var production = new ProductionSystem(map, roads, Interval);
            var produced = 0;
            production.Produced += (_, _) => produced++;

            production.Tick(Interval * 3f);

            Assert.AreEqual(0, produced);
        }

        [Test]
        public void TwoDeposits_AreShippedInTurn()
        {
            var coord = new HexCoord(0, 1);
            var map = MapWithDeposits((coord, ResourceType.Wood, 5), (coord, ResourceType.Stone, 5));
            var roads = new RoadNetwork(map);
            roads.Build(coord);
            var production = new ProductionSystem(map, roads, Interval);
            var order = new List<ResourceType>();
            production.Produced += (_, type) => order.Add(type);

            for (var i = 0; i < 4; i++)
                production.Tick(Interval);

            Assert.AreEqual(
                new[] { ResourceType.Wood, ResourceType.Stone, ResourceType.Wood, ResourceType.Stone },
                order.ToArray());
        }

        [Test]
        public void ExhaustedTile_StopsProducingAndReportsDepletion()
        {
            var coord = new HexCoord(0, 1);
            var map = MapWithDeposits((coord, ResourceType.Wood, 2));
            var roads = new RoadNetwork(map);
            roads.Build(coord);
            var production = new ProductionSystem(map, roads, Interval);
            var produced = 0;
            var depleted = 0;
            production.Produced += (_, _) => produced++;
            production.TileDepleted += _ => depleted++;

            for (var i = 0; i < 5; i++)
                production.Tick(Interval);

            Assert.AreEqual(2, produced);
            Assert.AreEqual(1, depleted);
            map.TryGetTile(coord, out var tile);
            Assert.AreEqual(TileState.Depleted, tile.State);
        }

        [Test]
        public void EmptyTileWithRoad_NeverProduces()
        {
            var coord = new HexCoord(0, 1);
            var map = MapWithDeposits();
            var roads = new RoadNetwork(map);
            roads.Build(coord);
            var production = new ProductionSystem(map, roads, Interval);
            var produced = 0;
            production.Produced += (_, _) => produced++;

            production.Tick(Interval * 4f);

            Assert.AreEqual(0, produced);
        }
    }
}
