using System;
using System.Collections.Generic;
using Game.Core;
using Game.Economy;
using Game.Grid;
using Game.Merge;
using Game.Storage;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public sealed class GameEndSystemTests
    {
        const int LossPenalty = 10;
        const int FieldBonus = 500;
        const int DepositBonus = 500;

        static readonly PriceSettings Prices = new(20, 1.04f, 1, 2);

        MergeRules rules;
        DeliverySystem deliveries;

        [SetUp]
        public void SetUp()
        {
            rules = ScriptableObject.CreateInstance<MergeRules>();
            deliveries = new DeliverySystem(1f);
        }

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(rules);

        /// <summary>Поле без ландшафта, с месторождениями там, где их просит тест.</summary>
        static HexMap FlatMap(
            int rows = 3,
            Func<HexCoord, BiomeType> biome = null,
            Func<HexCoord, List<Deposit>> deposits = null)
        {
            var tiles = new List<TileData>();
            foreach (var coord in HexMap.CoordsInFlare(rows))
                tiles.Add(new TileData(
                    coord,
                    coord == HexCoord.Zero,
                    deposits?.Invoke(coord),
                    biome?.Invoke(coord) ?? BiomeType.Meadow));

            return new HexMap(rows, tiles);
        }

        GameState NewGame(HexMap map, int points = 40, int gravel = 3, int capacity = 24)
        {
            var storage = new StorageGrid(capacity);
            for (var i = 0; i < gravel; i++)
                storage.TryStore(ResourceType.Gravel);

            var state = new GameState(map, new Wallet(points), storage, Prices, new ScoreMultiplier(0f, 0f, 0f, 0f, 0f, 0f, 1f));
            state.Begin();
            return state;
        }

        GameEndSystem EndOf(GameState state) =>
            new(state, rules, deliveries, LossPenalty, FieldBonus, DepositBonus);

        static List<Deposit> Stone(int reserve) => new() { new Deposit(ResourceType.Stone, reserve) };

        // --- когда партия ещё жива ---

        [Test]
        public void FreshGame_IsNotOver()
        {
            var end = EndOf(NewGame(FlatMap()));

            end.Tick();

            Assert.IsFalse(end.HasEnded, "стартовый щебень сам по себе меняется на очки");
        }

        [Test]
        public void ProducingTile_KeepsTheGameAlive()
        {
            var state = NewGame(FlatMap(deposits: c => c == new HexCoord(0, 1) ? Stone(5) : null));
            state.TryRevealTile(new HexCoord(0, 1));
            state.TryBuildRoad(new HexCoord(0, 1));
            state.Storage.TryRemove(ResourceType.Gravel, state.Storage.CountOf(ResourceType.Gravel));
            var end = EndOf(state);

            end.Tick();

            Assert.IsFalse(end.HasEnded, "подключённая плитка с запасом ещё повезёт ресурс");
        }

        [Test]
        public void ResourceOnTheWay_KeepsTheGameAlive()
        {
            var state = NewGame(FlatMap(), gravel: 0);
            deliveries.Send(ResourceType.Stone, new[] { new HexCoord(0, 1), HexCoord.Zero });
            var end = EndOf(state);

            end.Tick();

            Assert.IsFalse(end.HasEnded, "ресурс в пути ещё станет очками");
        }

        [Test]
        public void ThreeOfABaseResource_KeepAliveBecauseTheyMergeIntoCraft()
        {
            var state = NewGame(FlatMap(), gravel: 0);
            for (var i = 0; i < 3; i++)
                state.Storage.TryStore(ResourceType.Wood);
            var end = EndOf(state);

            end.Tick();

            Assert.IsFalse(end.HasEnded);
        }

        [Test]
        public void TwoOfABaseResource_AreDeadWeight()
        {
            var state = NewGame(FlatMap(), gravel: 0);
            for (var i = 0; i < 2; i++)
                state.Storage.TryStore(ResourceType.Wood);
            var end = EndOf(state);

            end.Tick();

            Assert.IsTrue(end.HasEnded, "двух брёвен не хватает даже на одну доску");
        }

        // --- когда заработать больше нечем ---

        [Test]
        public void NoGravelAndNoRoads_EndTheGameWhateverThePoints()
        {
            var state = NewGame(FlatMap(deposits: c => c == new HexCoord(0, 1) ? Stone(5) : null), points: 10000, gravel: 0);
            var end = EndOf(state);
            var ended = 0;
            end.Ended += _ => ended++;

            end.Tick();
            end.Tick();

            Assert.IsTrue(end.HasEnded, "очки открывают плитку, но без дороги она молчит");
            Assert.AreEqual(1, ended, "финал наступает один раз");
        }

        [Test]
        public void EmptyField_EndsTheGameOnceTheStartingGravelIsSpent()
        {
            var state = NewGame(FlatMap(), gravel: 3);
            var end = EndOf(state);

            end.Tick();

            Assert.IsFalse(end.HasEnded, "щебень на складе сам по себе стоит очков");

            state.Storage.TryRemove(ResourceType.Gravel, 3);
            end.Tick();

            Assert.IsTrue(end.HasEnded, "месторождений на поле нет вовсе");
        }

        // --- доигрывание склада ---

        /// <summary>Открыть всё, до чего дотягивается доступность: плитки становятся смежными волнами.</summary>
        static void RevealEverything(GameState state)
        {
            bool opened;
            do
            {
                opened = false;
                foreach (var tile in state.Map.Tiles.Values)
                    opened |= state.TryRevealTile(tile.Coord);
            }
            while (opened);
        }

        [Test]
        public void NothingLeftOnField_IsFalseWhileATileIsStillClosed()
        {
            var end = EndOf(NewGame(FlatMap()));

            Assert.IsFalse(end.NothingLeftOnField, "неоткрытая плитка ещё может стать дорогой и добычей");
        }

        [Test]
        public void NothingLeftOnField_IsTrueWhenEveryTileIsOpenedAndMinedOut()
        {
            var state = NewGame(FlatMap(), points: 100000);
            RevealEverything(state);

            Assert.IsTrue(EndOf(state).NothingLeftOnField, "месторождений на этой карте нет вовсе");
        }

        [Test]
        public void NothingLeftOnField_IsFalseWhileATileKeepsItsReserve()
        {
            var state = NewGame(
                FlatMap(deposits: c => c == new HexCoord(0, 1) ? Stone(5) : null), points: 100000);
            RevealEverything(state);

            Assert.IsFalse(EndOf(state).NothingLeftOnField, "к плитке с запасом ещё можно дотянуть дорогу");
        }

        [Test]
        public void NothingLeftOnField_IsFalseWhileAResourceIsOnTheWay()
        {
            var state = NewGame(FlatMap(), points: 100000);
            RevealEverything(state);
            deliveries.Send(ResourceType.Stone, new[] { new HexCoord(0, 1), HexCoord.Zero });

            Assert.IsFalse(EndOf(state).NothingLeftOnField, "продавать рано, пока ресурс едет");
        }

        [Test]
        public void DeadEnd_LeavesTheFieldWithoutAMoveWhileTheStorageStillPays()
        {
            // Плитка с запасом открыта, но не подключена, а щебня нет и взяться ему неоткуда:
            // на поле хода не осталось. Партия при этом не кончена — на складе лежит доска,
            // и её ещё меняют на очки. Это и есть тупик, до M32 в коде не названный.
            var state = NewGame(
                FlatMap(deposits: c => c == new HexCoord(0, 1) ? Stone(5) : null),
                points: 100000,
                gravel: 0);
            RevealEverything(state);
            state.Storage.TryStore(ResourceType.Board);
            var end = EndOf(state);

            end.Tick();

            Assert.IsTrue(end.NothingLeftOnField, "к плитке с запасом больше не дотянуться");
            Assert.IsFalse(end.HasEnded, "доска на складе сама по себе стоит очков");
        }

        [Test]
        public void DeadEnd_IsNotDeclaredWhileStoneCanStillBecomeGravel()
        {
            var state = NewGame(
                FlatMap(deposits: c => c == new HexCoord(0, 1) ? Stone(5) : null),
                points: 100000,
                gravel: 0);
            RevealEverything(state);
            for (var i = 0; i < 3; i++)
                state.Storage.TryStore(ResourceType.Stone);

            Assert.IsFalse(EndOf(state).NothingLeftOnField, "три камня — это будущий щебень и будущая дорога");
        }

        [Test]
        public void ClosedTilesAndNoGravel_AreStillAMove()
        {
            // щебня почти нет, плитки закрыты: открывать ещё есть на что, и это решение игрока.
            var state = NewGame(FlatMap(deposits: c => c == new HexCoord(0, 1) ? Stone(5) : null), gravel: 1);
            var end = EndOf(state);

            end.Tick();

            Assert.IsFalse(end.NothingLeftOnField, "очков хватает открыть плитку");
            Assert.IsFalse(end.HasEnded, "щебень на складе сам по себе стоит очков");
        }

        // --- счёт ---

        [Test]
        public void Score_IsWhatWasEarnedMinusWhatTheStorageDestroyed()
        {
            var state = NewGame(
                FlatMap(deposits: c => c == new HexCoord(0, 1) ? Stone(5) : null), gravel: 0, capacity: 2);
            state.Wallet.AddPoints(300);
            for (var i = 0; i < 5; i++)
                state.Storage.TryStore(ResourceType.Wood);

            var score = EndOf(state).BuildScore();

            Assert.AreEqual(300, score.Earned, "стартовые 40 очков не заработаны");
            Assert.AreEqual(3, score.Lost);
            Assert.AreEqual(30, score.LostPenalty);
            Assert.AreEqual(270, score.Total);
        }

        [Test]
        public void Score_PaysBothBonusesOnlyForAFullyClearedField()
        {
            var half = new FinalScore(100, 0, LossPenalty, 2, 4, 3, 6, FieldBonus, DepositBonus);
            var opened = new FinalScore(100, 0, LossPenalty, 4, 4, 3, 6, FieldBonus, DepositBonus);
            var perfect = new FinalScore(100, 0, LossPenalty, 4, 4, 6, 6, FieldBonus, DepositBonus);

            Assert.AreEqual(100, half.Total);
            Assert.AreEqual(100 + FieldBonus, opened.Total);
            Assert.AreEqual(100 + FieldBonus + DepositBonus, perfect.Total);
            Assert.IsFalse(opened.IsPerfect);
            Assert.IsTrue(perfect.IsPerfect);
        }

        [Test]
        public void Score_CountsOnlyTheFieldThatCanBeReached()
        {
            // за грядой из обеих плиток первого ряда поле недостижимо и в счёт не идёт
            var wall = new[] { new HexCoord(-1, 1), new HexCoord(0, 1) };
            var state = NewGame(FlatMap(
                rows: 4,
                biome: c => Array.IndexOf(wall, c) >= 0 ? BiomeType.Mountains : BiomeType.Meadow,
                deposits: c => c.R >= 2 ? Stone(5) : null));

            var score = EndOf(state).BuildScore();

            Assert.AreEqual(0, score.FieldTiles, "кроме Метрополии дойти некуда");
            Assert.AreEqual(0, score.FieldDeposits, "месторождения за грядой не считаются");
            Assert.IsTrue(score.IsPerfect, "поле пройдено настолько, насколько его вообще можно пройти");
        }

        [Test]
        public void Score_CallsTheFieldClearedWhenEveryTileIsOpenAndEveryDepositIsSpent()
        {
            var state = NewGame(FlatMap(rows: 2, deposits: c => c == new HexCoord(0, 1) ? Stone(1) : null), points: 100);
            var end = EndOf(state);

            Assert.IsFalse(end.BuildScore().IsPerfect);

            state.TryRevealTile(new HexCoord(0, 1));
            state.TryRevealTile(new HexCoord(-1, 1));
            state.Map.TryGetTile(new HexCoord(0, 1), out var tile);
            tile.TryExtract(out _);

            var score = end.BuildScore();

            Assert.AreEqual(2, score.OpenedTiles);
            Assert.AreEqual(2, score.FieldTiles);
            Assert.AreEqual(1, score.ExhaustedDeposits);
            Assert.AreEqual(1, score.FieldDeposits);
            Assert.IsTrue(score.IsPerfect);
        }
    }
}
