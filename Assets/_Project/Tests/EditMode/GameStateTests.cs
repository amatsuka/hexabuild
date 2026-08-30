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
        const int OpenStep = 2;
        const int OpenGroup = 5;
        const int RoadCost = 1;
        const int BridgeCost = 2;

        static readonly PriceSettings Prices = new(OpenCost, OpenStep, OpenGroup, RoadCost, BridgeCost);

        /// <summary>
        /// Поле без ландшафта: правила проверяются на ровном месте. Раньше здесь стояла карта
        /// от генератора, но с M9 биом влияет на правила — гора или река под нужной координатой
        /// ломала бы тест про очки, ничего не сообщая про очки.
        /// </summary>
        static HexMap FlatMap(
            int rows = 6,
            System.Func<HexCoord, BiomeType> biome = null,
            System.Func<HexCoord, int> rivers = null)
        {
            var tiles = new List<TileData>();
            foreach (var coord in HexMap.CoordsInFlare(rows))
                tiles.Add(new TileData(
                    coord,
                    coord == HexCoord.Zero,
                    null,
                    biome?.Invoke(coord) ?? BiomeType.Meadow,
                    0f,
                    rivers?.Invoke(coord) ?? 0));

            return new HexMap(rows, tiles);
        }

        static GameState NewGame(int points = 40, int gravel = 3, HexMap map = null)
        {
            var storage = new StorageGrid(25);
            for (var i = 0; i < gravel; i++)
                storage.TryStore(ResourceType.Gravel);

            return new GameState(map ?? FlatMap(), new Wallet(points), storage, Prices);
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

        // --- M10: растущая цена открытия ---

        /// <summary>Открыть любую доступную плитку: какую именно — для цены безразлично.</summary>
        static bool OpenAnyTile(GameState state)
        {
            foreach (var tile in state.Map.Tiles.Values)
                if (tile.State == TileState.Available && state.TryRevealTile(tile.Coord))
                    return true;

            return false;
        }

        [Test]
        public void NextTileCost_RisesByAStepEveryGroupOfOpenedTiles()
        {
            var state = NewGame(points: 100000);
            state.Begin();

            Assert.AreEqual(OpenCost, state.NextTileCost, "первая плитка стоит стартовую цену");

            for (var i = 0; i < OpenGroup; i++)
                OpenAnyTile(state);

            Assert.AreEqual(OpenGroup, state.OpenedTiles);
            Assert.AreEqual(OpenCost + OpenStep, state.NextTileCost);

            for (var i = 0; i < OpenGroup; i++)
                OpenAnyTile(state);

            Assert.AreEqual(OpenCost + OpenStep * 2, state.NextTileCost);
        }

        [Test]
        public void TryRevealTile_ChargesThePriceItShowedBeforeTheClick()
        {
            var state = NewGame(points: 100000);
            state.Begin();

            var spent = 0;
            for (var i = 0; i < OpenGroup * 2; i++)
            {
                spent += state.NextTileCost;
                OpenAnyTile(state);
            }

            Assert.AreEqual(OpenGroup * OpenCost + OpenGroup * (OpenCost + OpenStep), spent);
            Assert.AreEqual(100000 - spent, state.Wallet.Points);
        }

        [Test]
        public void RefusedOpening_NeitherCountsNorRaisesThePrice()
        {
            var state = NewGame(points: OpenCost);
            state.Begin();
            state.TryRevealTile(new HexCoord(0, 3));

            Assert.AreEqual(0, state.OpenedTiles);
            Assert.AreEqual(OpenCost, state.NextTileCost);
        }

        [Test]
        public void Refusal_NamesTheGrownPrice()
        {
            var state = NewGame(points: OpenGroup * OpenCost);
            state.Begin();
            for (var i = 0; i < OpenGroup; i++)
                OpenAnyTile(state);

            var refusals = new List<string>();
            state.ActionRefused += refusals.Add;
            Assert.IsFalse(OpenAnyTile(state));

            CollectionAssert.Contains(refusals, $"Не хватает очков: нужно {OpenCost + OpenStep}");
        }

        // --- M9: горы и реки ---

        [Test]
        public void TryRevealTile_OnMountain_IsRefusedAndCostsNothing()
        {
            var state = NewGame(map: FlatMap(biome: c => c == new HexCoord(0, 1) ? BiomeType.Mountains : BiomeType.Meadow));
            state.Begin();
            var refusals = new List<string>();
            state.ActionRefused += refusals.Add;

            Assert.IsFalse(state.TryRevealTile(new HexCoord(0, 1)));

            state.Map.TryGetTile(new HexCoord(0, 1), out var tile);
            Assert.AreEqual(TileState.Available, tile.State, "гора остаётся закрытой навсегда");
            Assert.AreEqual(40, state.Wallet.Points);
            CollectionAssert.Contains(refusals, "Гора непроходима");
        }

        [Test]
        public void TryBuildRoad_OnMountain_IsRefused()
        {
            var state = NewGame(map: FlatMap(biome: c => c == new HexCoord(0, 1) ? BiomeType.Mountains : BiomeType.Meadow));
            state.Begin();
            var refusals = new List<string>();
            state.ActionRefused += refusals.Add;

            Assert.IsFalse(state.TryBuildRoad(new HexCoord(0, 1)));

            Assert.IsFalse(state.Roads.HasRoad(new HexCoord(0, 1)));
            Assert.AreEqual(3, state.Storage.CountOf(ResourceType.Gravel));
            CollectionAssert.Contains(refusals, "Гора непроходима");
        }

        /// <summary>Гора никого не открывает: за ней поле остаётся закрытым, это и делает её стеной.</summary>
        [Test]
        public void Mountain_DoesNotOpenTheWayBehindIt()
        {
            var wall = new[] { new HexCoord(-1, 1), new HexCoord(0, 1) };
            var state = NewGame(points: 1000, map: FlatMap(
                biome: c => System.Array.IndexOf(wall, c) >= 0 ? BiomeType.Mountains : BiomeType.Meadow));
            state.Begin();

            foreach (var coord in wall)
                state.TryRevealTile(coord);

            foreach (var tile in state.Map.Tiles.Values)
                if (tile.Coord.R >= 2)
                    Assert.AreEqual(TileState.Hidden, tile.State, $"{tile.Coord} открылась сквозь гряду");
        }

        /// <summary>
        /// Река лежит на ребре между (0,1) и Метрополией: направление 2 у плитки, обратное — у
        /// Метрополии. Дорога на (0,1) цепляется именно за это ребро, значит нужен мост.
        /// </summary>
        [Test]
        public void RoadAcrossARiverEdge_CostsTheBridgeSurcharge()
        {
            var state = NewGame(map: FlatMap(rivers: c =>
                c == new HexCoord(0, 1) ? 1 << 2 : c == HexCoord.Zero ? 1 << 5 : 0));
            state.Begin();
            state.TryRevealTile(new HexCoord(0, 1));
            state.Map.TryGetTile(new HexCoord(0, 1), out var tile);

            Assert.AreEqual(RoadCost + BridgeCost, state.RoadPrice(tile));
            Assert.IsTrue(state.TryBuildRoad(new HexCoord(0, 1)));
            Assert.AreEqual(0, state.Storage.CountOf(ResourceType.Gravel), "три щебня ушли целиком");
        }

        [Test]
        public void RoadBesideARiver_CostsTheUsualPrice()
        {
            // река на дальнем ребре (0,1), к Метрополии она не выходит
            var state = NewGame(map: FlatMap(rivers: c => c == new HexCoord(0, 1) ? 1 << 5 : 0));
            state.Begin();
            state.TryRevealTile(new HexCoord(0, 1));
            state.Map.TryGetTile(new HexCoord(0, 1), out var tile);

            Assert.AreEqual(RoadCost, state.RoadPrice(tile), "река сбоку моста не требует");
        }

        [Test]
        public void BridgeRefusal_NamesTheFullPrice()
        {
            var state = NewGame(gravel: 2, map: FlatMap(rivers: c =>
                c == new HexCoord(0, 1) ? 1 << 2 : c == HexCoord.Zero ? 1 << 5 : 0));
            state.Begin();
            state.TryRevealTile(new HexCoord(0, 1));
            var refusals = new List<string>();
            state.ActionRefused += refusals.Add;

            Assert.IsFalse(state.TryBuildRoad(new HexCoord(0, 1)));

            Assert.AreEqual(2, state.Storage.CountOf(ResourceType.Gravel));
            CollectionAssert.Contains(refusals, $"Нужен мост: {RoadCost + BridgeCost} щебня");
        }

        /// <summary>
        /// Цену считают до постройки по ребру к будущему родителю, а назначает родителя BFS уже
        /// после. Предсказание обязано совпадать, иначе игрок платит за один мост, а получает
        /// дорогу через другое ребро.
        /// </summary>
        [Test]
        public void PreviewedParent_MatchesTheOneAssignedOnBuild()
        {
            var state = NewGame(points: 100000, gravel: 0, map: FlatMap());
            state.Begin();
            for (var i = 0; i < 200; i++)
                state.Storage.TryStore(ResourceType.Gravel);

            foreach (var coord in new[]
                     {
                         new HexCoord(0, 1), new HexCoord(-1, 1), new HexCoord(-1, 2), new HexCoord(0, 2),
                         new HexCoord(-2, 2), new HexCoord(-2, 3), new HexCoord(-1, 3), new HexCoord(0, 3),
                         new HexCoord(-3, 4), new HexCoord(-2, 4)
                     })
            {
                state.TryRevealTile(coord);
                var previewed = state.Roads.TryPreviewParent(coord, out var expected);

                state.TryBuildRoad(coord);

                var assigned = state.Roads.TryGetParent(coord, out var actual);
                Assert.AreEqual(previewed, assigned, $"{coord}: предсказание и назначение разошлись в самом факте родителя");
                if (previewed)
                    Assert.AreEqual(expected, actual, $"{coord}: предсказан родитель {expected}, назначен {actual}");
            }
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
