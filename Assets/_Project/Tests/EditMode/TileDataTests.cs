using System.Collections.Generic;
using Game.Economy;
using Game.Grid;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public sealed class TileDataTests
    {
        static TileData Tile(params (ResourceType type, int reserve)[] deposits)
        {
            var list = new List<Deposit>();
            foreach (var (type, reserve) in deposits)
                list.Add(new Deposit(type, reserve));

            var tile = new TileData(new HexCoord(1, 0), false, list);
            tile.Reveal();
            return tile;
        }

        [Test]
        public void TryExtract_TwoDeposits_AlternatesBetweenThem()
        {
            var tile = Tile((ResourceType.Wood, 5), (ResourceType.Stone, 5));

            var order = new List<ResourceType>();
            for (var i = 0; i < 4; i++)
            {
                tile.TryExtract(out var type);
                order.Add(type);
            }

            Assert.AreEqual(
                new[] { ResourceType.Wood, ResourceType.Stone, ResourceType.Wood, ResourceType.Stone },
                order.ToArray());
        }

        [Test]
        public void TryExtract_SpendsOneUnitOfReservePerCall()
        {
            var tile = Tile((ResourceType.Ore, 3));

            tile.TryExtract(out _);

            Assert.AreEqual(2, tile.Deposits[0].Reserve);
        }

        [Test]
        public void TryExtract_SkipsExhaustedDeposit()
        {
            var tile = Tile((ResourceType.Wood, 1), (ResourceType.Stone, 5));

            tile.TryExtract(out var first);
            tile.TryExtract(out var second);
            tile.TryExtract(out var third);

            Assert.AreEqual(ResourceType.Wood, first);
            Assert.AreEqual(ResourceType.Stone, second);
            Assert.AreEqual(ResourceType.Stone, third, "исчерпанное дерево должно пропускаться");
        }

        [Test]
        public void TryExtract_ThreeDeposits_GoesRoundRobin()
        {
            var tile = Tile((ResourceType.Wood, 2), (ResourceType.Stone, 2), (ResourceType.Ore, 2));

            var order = new List<ResourceType>();
            for (var i = 0; i < 6; i++)
            {
                tile.TryExtract(out var type);
                order.Add(type);
            }

            Assert.AreEqual(
                new[]
                {
                    ResourceType.Wood, ResourceType.Stone, ResourceType.Ore,
                    ResourceType.Wood, ResourceType.Stone, ResourceType.Ore
                },
                order.ToArray());
        }

        [Test]
        public void Tile_BecomesDepletedWhenLastDepositRunsOut()
        {
            var tile = Tile((ResourceType.Wood, 1), (ResourceType.Stone, 1));

            tile.TryExtract(out _);
            Assert.AreEqual(TileState.Revealed, tile.State);

            tile.TryExtract(out _);
            Assert.AreEqual(TileState.Depleted, tile.State);
            Assert.IsTrue(tile.IsExhausted);
        }

        [Test]
        public void TryExtract_OnDepletedTile_ReturnsFalse()
        {
            var tile = Tile((ResourceType.Wood, 1));
            tile.TryExtract(out _);

            Assert.IsFalse(tile.TryExtract(out _));
        }

        [Test]
        public void TryExtract_OnTileWithoutDeposits_ReturnsFalseAndKeepsState()
        {
            var tile = Tile();

            Assert.IsFalse(tile.TryExtract(out _));
            Assert.AreEqual(TileState.Revealed, tile.State);
        }
    }
}
