using System.Collections.Generic;
using Game.Core;
using Game.Economy;
using Game.Grid;
using Game.Storage;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public sealed class GameStateTests
    {
        const int OpenCost = 20;
        const int RoadCost = 1;

        static GameState NewGame(int points = 40, int gravel = 3)
        {
            var map = MapGenerator.Generate(new MapGenerationSettings(10, 11, 30f, 45f, 20f, 5f, 8, 20));
            var storage = new StorageGrid(25);
            for (var i = 0; i < gravel; i++)
                storage.TryStore(ResourceType.Gravel);

            return new GameState(map, new Wallet(points), storage, OpenCost, RoadCost);
        }

        [Test]
        public void Begin_OpensMetropolisAndMakesItsNeighborsAvailable()
        {
            var state = NewGame();

            state.Begin();

            Assert.AreEqual(TileState.Revealed, state.Map.Metropolis.State);
            foreach (var neighbor in state.Map.NeighborsOf(HexCoord.Zero))
                Assert.AreEqual(TileState.Available, neighbor.State);

            state.Map.TryGetTile(new HexCoord(0, 3), out var far);
            Assert.AreEqual(TileState.Hidden, far.State);
        }

        [Test]
        public void TryRevealTile_AvailableTile_SpendsPointsAndOpensIt()
        {
            var state = NewGame();
            state.Begin();

            Assert.IsTrue(state.TryRevealTile(new HexCoord(0, 1)));

            state.Map.TryGetTile(new HexCoord(0, 1), out var tile);
            Assert.AreEqual(TileState.Revealed, tile.State);
            Assert.AreEqual(20, state.Wallet.Points);
        }

        [Test]
        public void TryRevealTile_OpensTheWayFurther()
        {
            var state = NewGame();
            state.Begin();

            state.TryRevealTile(new HexCoord(0, 1));

            state.Map.TryGetTile(new HexCoord(0, 2), out var next);
            Assert.AreEqual(TileState.Available, next.State);
        }

        [Test]
        public void TryRevealTile_HiddenTile_IsRefusedAndCostsNothing()
        {
            var state = NewGame();
            state.Begin();
            var refusals = new List<string>();
            state.ActionRefused += refusals.Add;

            Assert.IsFalse(state.TryRevealTile(new HexCoord(0, 3)));

            Assert.AreEqual(1, refusals.Count);
            Assert.AreEqual(40, state.Wallet.Points);
        }

        [Test]
        public void TryRevealTile_WithoutEnoughPoints_IsRefusedAndTileStaysAvailable()
        {
            var state = NewGame(19);
            state.Begin();
            var refusals = new List<string>();
            state.ActionRefused += refusals.Add;

            Assert.IsFalse(state.TryRevealTile(new HexCoord(0, 1)));

            state.Map.TryGetTile(new HexCoord(0, 1), out var tile);
            Assert.AreEqual(TileState.Available, tile.State);
            Assert.AreEqual(19, state.Wallet.Points);
            Assert.AreEqual(1, refusals.Count);
        }

        [Test]
        public void TryRevealTile_AlreadyRevealedTile_ChangesNothing()
        {
            var state = NewGame();
            state.Begin();
            state.TryRevealTile(new HexCoord(0, 1));

            Assert.IsFalse(state.TryRevealTile(new HexCoord(0, 1)));
            Assert.AreEqual(20, state.Wallet.Points);
        }

        [Test]
        public void TryRevealTile_OutsideField_ReturnsFalse()
        {
            var state = NewGame();
            state.Begin();

            Assert.IsFalse(state.TryRevealTile(new HexCoord(9, 9)));
            Assert.AreEqual(40, state.Wallet.Points);
        }

        [Test]
        public void TryBuildRoad_OnRevealedTile_SpendsGravelAndConnectsIt()
        {
            var state = NewGame();
            state.Begin();
            state.TryRevealTile(new HexCoord(0, 1));

            Assert.IsTrue(state.TryBuildRoad(new HexCoord(0, 1)));

            Assert.IsTrue(state.Roads.HasRoad(new HexCoord(0, 1)));
            Assert.IsTrue(state.Roads.IsConnected(new HexCoord(0, 1)));
            Assert.AreEqual(2, state.Storage.CountOf(ResourceType.Gravel));
        }

        [Test]
        public void TryBuildRoad_OnUnopenedTile_IsRefused()
        {
            var state = NewGame();
            state.Begin();
            var refusals = new List<string>();
            state.ActionRefused += refusals.Add;

            Assert.IsFalse(state.TryBuildRoad(new HexCoord(0, 1)));

            Assert.IsFalse(state.Roads.HasRoad(new HexCoord(0, 1)));
            Assert.AreEqual(3, state.Storage.CountOf(ResourceType.Gravel));
            Assert.AreEqual(1, refusals.Count);
        }

        [Test]
        public void TryBuildRoad_WithoutGravel_IsRefusedAndBuildsNothing()
        {
            var state = NewGame(gravel: 0);
            state.Begin();
            state.TryRevealTile(new HexCoord(0, 1));
            var refusals = new List<string>();
            state.ActionRefused += refusals.Add;

            Assert.IsFalse(state.TryBuildRoad(new HexCoord(0, 1)));

            Assert.IsFalse(state.Roads.HasRoad(new HexCoord(0, 1)));
            Assert.AreEqual(1, refusals.Count);
        }

        [Test]
        public void TryBuildRoad_TwiceOnTheSameTile_SpendsGravelOnlyOnce()
        {
            var state = NewGame();
            state.Begin();
            state.TryRevealTile(new HexCoord(0, 1));
            state.TryBuildRoad(new HexCoord(0, 1));

            Assert.IsFalse(state.TryBuildRoad(new HexCoord(0, 1)));
            Assert.AreEqual(2, state.Storage.CountOf(ResourceType.Gravel));
        }

        [Test]
        public void TryBuildRoad_OnMetropolis_IsRejected()
        {
            var state = NewGame();
            state.Begin();

            Assert.IsFalse(state.TryBuildRoad(HexCoord.Zero));
            Assert.AreEqual(3, state.Storage.CountOf(ResourceType.Gravel));
        }

        [Test]
        public void HandleTileClick_OpensAvailableTileThenBuildsRoadOnIt()
        {
            var state = NewGame();
            state.Begin();

            state.HandleTileClick(new HexCoord(0, 1));
            state.HandleTileClick(new HexCoord(0, 1));

            state.Map.TryGetTile(new HexCoord(0, 1), out var tile);
            Assert.AreEqual(TileState.Revealed, tile.State);
            Assert.IsTrue(state.Roads.HasRoad(new HexCoord(0, 1)));
            Assert.AreEqual(20, state.Wallet.Points);
            Assert.AreEqual(2, state.Storage.CountOf(ResourceType.Gravel));
        }

        [Test]
        public void TileChanged_FiresForMetropolisAndTheTilesAboveIt()
        {
            var state = NewGame();
            var changed = new List<TileData>();
            state.TileChanged += changed.Add;

            state.Begin();

            Assert.AreEqual(3, changed.Count, "Метрополия и две плитки над ней");
        }
    }
}
