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
    /// <summary>Кнопка «Продать всё»: приход по таймеру, резерв, накал и хвост партии.</summary>
    public sealed class BatchSaleTests
    {
        const float PauseMin = 10f;
        const float PauseMax = 20f;
        const int GravelReserve = 4;
        const int BoardReserve = 4;

        static readonly PriceSettings Prices = new(20, 1.04f, 1, 2, 2);

        MergeRules rules;
        StorageGrid storage;
        Wallet wallet;
        ScoreMultiplier multiplier;
        MergeSystem merges;
        DeliverySystem deliveries;
        GameState state;

        [SetUp]
        public void SetUp()
        {
            rules = ScriptableObject.CreateInstance<MergeRules>();
            storage = new StorageGrid(24);
            wallet = new Wallet(40);
            multiplier = new ScoreMultiplier(0f, 0f, 0f, 0.05f, 1f, 2.5f, 2f);
            merges = new MergeSystem(storage, wallet, rules, multiplier);
            deliveries = new DeliverySystem(1f);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(rules);

        /// <summary>Поле без ландшафта и без месторождений: партия на нём живёт, пока есть что открыть.</summary>
        static HexMap FlatMap(int rows = 3)
        {
            var tiles = new List<TileData>();
            foreach (var coord in HexMap.CoordsInFlare(rows))
                tiles.Add(new TileData(coord, coord == HexCoord.Zero, null, BiomeType.Meadow));

            return new HexMap(rows, tiles);
        }

        /// <summary>Партия идёт: плитки закрыты, очков хватает открыть следующую.</summary>
        BatchSale Live()
        {
            state = new GameState(FlatMap(), wallet, storage, Prices, multiplier);
            state.Begin();
            return NewSale();
        }

        /// <summary>Хвост партии: поле открыто целиком, брать на нём больше нечего.</summary>
        BatchSale Tail()
        {
            state = new GameState(FlatMap(), new Wallet(100000), storage, Prices, multiplier);
            state.Begin();

            bool opened;
            do
            {
                opened = false;
                foreach (var tile in state.Map.Tiles.Values)
                    opened |= state.TryRevealTile(tile.Coord);
            }
            while (opened);

            return NewSale();
        }

        BatchSale NewSale() => new(
            storage, merges, rules,
            new GameEndSystem(state, rules, deliveries, 10, 500, 500),
            PauseMin, PauseMax, GravelReserve, BoardReserve, seed: 1);

        void Fill(ResourceType type, int count)
        {
            for (var i = 0; i < count; i++)
                storage.TryStore(type);
        }

        [Test]
        public void Button_DoesNotComeBeforeThePause()
        {
            Fill(ResourceType.Ingot, 3);
            var sale = Live();

            Assert.IsFalse(sale.Ready, "кнопка приходит не сразу");

            sale.Tick(PauseMax);

            Assert.IsTrue(sale.Ready);
            Assert.IsTrue(sale.CanSell);
        }

        [Test]
        public void Button_HidesWhenThereIsNothingButTheReserve()
        {
            Fill(ResourceType.Gravel, GravelReserve);
            Fill(ResourceType.Board, BoardReserve);
            var sale = Live();
            sale.Tick(PauseMax);

            Assert.IsTrue(sale.Ready);
            Assert.IsFalse(sale.CanSell, "пустая кнопка не показывается");
        }

        [Test]
        public void Sale_KeepsTheReserveOfGravelAndBoards()
        {
            Fill(ResourceType.Gravel, 6);
            Fill(ResourceType.Board, 6);
            Fill(ResourceType.Ingot, 3);
            var sale = Live();
            sale.Tick(PauseMax);

            sale.Sell();

            Assert.AreEqual(GravelReserve, storage.CountOf(ResourceType.Gravel), "щебень нужен на дорогу");
            Assert.AreEqual(BoardReserve, storage.CountOf(ResourceType.Board), "доски нужны на мост");
            Assert.AreEqual(0, storage.CountOf(ResourceType.Ingot), "слиток не нужен ни на что, кроме очков");
        }

        [Test]
        public void Sale_LeavesTheBaseResourcesAlone()
        {
            Fill(ResourceType.Wood, 5);
            Fill(ResourceType.Ingot, 2);
            var sale = Live();
            sale.Tick(PauseMax);

            sale.Sell();

            Assert.AreEqual(5, storage.CountOf(ResourceType.Wood), "базовый ресурс кнопка не трогает: он не крафт");
            Assert.AreEqual(0, storage.CountOf(ResourceType.Ingot));
        }

        [Test]
        public void EveryUnitSold_RaisesTheHeat()
        {
            Fill(ResourceType.Ingot, 4);
            var sale = Live();
            sale.Tick(PauseMax);

            sale.Sell();

            Assert.AreEqual(0.20f, multiplier.Heat, 1e-5f, "накал шагает за каждую проданную единицу");
            // 15 + 15×1.05 + 15×1.10 + 15×1.15 — ступень достаётся следующей единице, как у клика.
            Assert.AreEqual(15 + 15 + 16 + 17, wallet.TotalEarned);
        }

        [Test]
        public void PressedButton_ComesBackOnlyAfterANewPause()
        {
            Fill(ResourceType.Ingot, 6);
            var sale = Live();
            sale.Tick(PauseMax);

            sale.Begin();
            sale.Sell();

            Assert.IsFalse(sale.Ready, "нажатие отсчитывает паузу заново");
            sale.Tick(PauseMax);
            Assert.IsTrue(sale.Ready);
        }

        [Test]
        public void AtTheEndOfTheGame_TheButtonComesWithoutTheTimerAndKeepsNoReserve()
        {
            Fill(ResourceType.Gravel, 6);
            Fill(ResourceType.Board, 6);
            var sale = Tail();

            Assert.IsTrue(sale.Everything, "на поле хода не осталось");
            Assert.IsTrue(sale.Ready, "в хвосте партии кнопка не ждёт таймера");

            sale.Sell();

            Assert.AreEqual(0, storage.Count, "хвост продаётся без остатка: беречь щебень уже не для чего");
        }

        [Test]
        public void AtTheEndOfTheGame_TheTailIsMergedBeforeItIsSold()
        {
            Fill(ResourceType.Wood, 3);
            var sale = Tail();

            sale.Sell();

            Assert.AreEqual(0, storage.Count, "три бревна становятся доской, доска — очками");
            Assert.Greater(wallet.TotalEarned, 0);
        }

        [Test]
        public void AtTheEndOfTheGame_DeadWeightStopsTheSale()
        {
            Fill(ResourceType.Wood, 2);
            var sale = Tail();

            Assert.IsFalse(sale.CanSell, "двух брёвен не хватает на доску, продавать нечего");
            Assert.IsFalse(sale.TrySellOne());
            Assert.AreEqual(2, storage.Count);
        }
    }
}
