using System.Collections.Generic;
using Game.Core;
using Game.Economy;
using Game.Grid;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public sealed class GameStateTests
    {
        const int OpenCost = 20;

        static GameState NewGame(int points = 40)
        {
            var map = MapGenerator.Generate(new MapGenerationSettings(6, 11, 30f, 45f, 20f, 5f, 8, 20));
            return new GameState(map, new Wallet(points, 3), OpenCost);
        }

        [Test]
        public void Begin_OpensMetropolisAndMakesItsNeighborsAvailable()
        {
            var state = NewGame();

            state.Begin();

            Assert.AreEqual(TileState.Revealed, state.Map.Metropolis.State);
            foreach (var neighbor in state.Map.NeighborsOf(HexCoord.Zero))
                Assert.AreEqual(TileState.Available, neighbor.State);

            state.Map.TryGetTile(new HexCoord(3, 0), out var far);
            Assert.AreEqual(TileState.Hidden, far.State);
        }

        [Test]
        public void TryRevealTile_AvailableTile_SpendsPointsAndOpensIt()
        {
            var state = NewGame();
            state.Begin();

            Assert.IsTrue(state.TryRevealTile(new HexCoord(1, 0)));

            state.Map.TryGetTile(new HexCoord(1, 0), out var tile);
            Assert.AreEqual(TileState.Revealed, tile.State);
            Assert.AreEqual(20, state.Wallet.Points);
        }

        [Test]
        public void TryRevealTile_OpensTheWayFurther()
        {
            var state = NewGame();
            state.Begin();

            state.TryRevealTile(new HexCoord(1, 0));

            state.Map.TryGetTile(new HexCoord(2, 0), out var next);
            Assert.AreEqual(TileState.Available, next.State);
        }

        [Test]
        public void TryRevealTile_HiddenTile_IsRefusedAndCostsNothing()
        {
            var state = NewGame();
            state.Begin();
            var refusals = new List<string>();
            state.ActionRefused += refusals.Add;

            Assert.IsFalse(state.TryRevealTile(new HexCoord(3, 0)));

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

            Assert.IsFalse(state.TryRevealTile(new HexCoord(1, 0)));

            state.Map.TryGetTile(new HexCoord(1, 0), out var tile);
            Assert.AreEqual(TileState.Available, tile.State);
            Assert.AreEqual(19, state.Wallet.Points);
            Assert.AreEqual(1, refusals.Count);
        }

        [Test]
        public void TryRevealTile_AlreadyRevealedTile_ChangesNothing()
        {
            var state = NewGame();
            state.Begin();
            state.TryRevealTile(new HexCoord(1, 0));

            Assert.IsFalse(state.TryRevealTile(new HexCoord(1, 0)));
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
        public void TileChanged_FiresForMetropolisAndItsSixNeighbors()
        {
            var state = NewGame();
            var changed = new List<TileData>();
            state.TileChanged += changed.Add;

            state.Begin();

            Assert.AreEqual(7, changed.Count);
        }
    }
}
