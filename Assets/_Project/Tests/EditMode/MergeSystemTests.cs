using System.Collections.Generic;
using Game.Economy;
using Game.Merge;
using Game.Storage;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public sealed class MergeSystemTests
    {
        MergeRules rules;
        StorageGrid storage;
        Wallet wallet;
        MergeSystem merges;
        ScoreMultiplier multiplier;
        List<string> refusals;

        [SetUp]
        public void SetUp()
        {
            rules = ScriptableObject.CreateInstance<MergeRules>();
            storage = new StorageGrid(25);
            wallet = new Wallet(0);
            multiplier = new ScoreMultiplier(0.05f, 0.25f, 2f);
            merges = new MergeSystem(storage, wallet, rules, multiplier);
            refusals = new List<string>();
            merges.Refused += refusals.Add;
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(rules);

        void Fill(ResourceType type, int count)
        {
            for (var i = 0; i < count; i++)
                storage.TryStore(type);
        }

        [Test]
        public void FiveWood_BecomeTwoBoardsAndPoints()
        {
            Fill(ResourceType.Wood, 5);

            Assert.IsTrue(merges.TryMerge(ResourceType.Wood));

            Assert.AreEqual(0, storage.CountOf(ResourceType.Wood));
            Assert.AreEqual(2, storage.CountOf(ResourceType.Board));
            Assert.AreEqual(0, wallet.Points, "очки даёт не merge, а клик по крафту");
            Assert.AreEqual(2, storage.Count);
        }

        [Test]
        public void Result_GoesIntoTheCellsFreedByTheMerge()
        {
            Fill(ResourceType.Wood, 5);

            merges.TryMerge(ResourceType.Wood);

            Assert.AreEqual(ResourceType.Board, storage[0]);
            Assert.AreEqual(ResourceType.Board, storage[1]);
            Assert.IsFalse(storage[2].HasValue);
        }

        [Test]
        public void FourWood_MergeThreeAndLeaveTheRest()
        {
            Fill(ResourceType.Wood, 4);

            Assert.IsTrue(merges.TryMerge(ResourceType.Wood));

            Assert.AreEqual(1, storage.CountOf(ResourceType.Wood));
            Assert.AreEqual(1, storage.CountOf(ResourceType.Board));
            Assert.AreEqual(0, wallet.Points);
        }

        [Test]
        public void TwoWood_AreRefusedAndChangeNothing()
        {
            Fill(ResourceType.Wood, 2);

            Assert.IsFalse(merges.TryMerge(ResourceType.Wood));

            Assert.AreEqual(2, storage.CountOf(ResourceType.Wood));
            Assert.AreEqual(0, wallet.Points);
            Assert.AreEqual(1, refusals.Count);
        }

        [Test]
        public void CraftedResource_IsNotMerged()
        {
            Fill(ResourceType.Board, 5);

            Assert.IsFalse(merges.TryMerge(ResourceType.Board));

            Assert.AreEqual(5, storage.CountOf(ResourceType.Board));
            Assert.AreEqual(0, wallet.Points);
            Assert.AreEqual(1, refusals.Count);
        }

        [TestCase(ResourceType.Board)]
        [TestCase(ResourceType.Gravel)]
        [TestCase(ResourceType.Ingot)]
        public void ClickOnCrafted_TurnsOneUnitIntoPoints(ResourceType crafted)
        {
            Fill(crafted, 2);

            Assert.IsTrue(merges.TryConvert(0));

            Assert.AreEqual(15, wallet.Points);
            Assert.AreEqual(1, storage.CountOf(crafted));
            Assert.IsFalse(storage[0].HasValue);
        }

        [Test]
        public void ClickOnBaseResource_IsRefusedByConversion()
        {
            Fill(ResourceType.Wood, 5);

            Assert.IsFalse(merges.TryConvert(0));

            Assert.AreEqual(5, storage.CountOf(ResourceType.Wood));
            Assert.AreEqual(0, wallet.Points);
            Assert.AreEqual(1, refusals.Count);
        }

        [Test]
        public void ClickOnEmptyCell_DoesNothing()
        {
            Assert.IsFalse(merges.TryConvert(0));
            Assert.AreEqual(0, wallet.Points);
            Assert.IsEmpty(refusals);
        }

        [Test]
        public void MergeThenConvert_IsTheWayPointsAreEarned()
        {
            Fill(ResourceType.Ore, 5);

            merges.TryMerge(ResourceType.Ore);
            merges.TryConvert(0);
            merges.TryConvert(1);

            Assert.AreEqual(30, wallet.Points);
            Assert.AreEqual(0, storage.Count);
        }

        [Test]
        public void StoneMerge_ProducesGravelThatPaysForRoads()
        {
            Fill(ResourceType.Stone, 3);

            merges.TryMerge(ResourceType.Stone);

            Assert.AreEqual(1, storage.CountOf(ResourceType.Gravel));
            Assert.IsTrue(storage.TryRemove(ResourceType.Gravel, 1));
        }

        [Test]
        public void MergeOnFullStorage_FreesRoomAndLosesNothing()
        {
            Fill(ResourceType.Wood, 25);

            merges.TryMerge(ResourceType.Wood);

            Assert.AreEqual(20, storage.CountOf(ResourceType.Wood));
            Assert.AreEqual(2, storage.CountOf(ResourceType.Board));
            Assert.AreEqual(22, storage.Count);
            Assert.AreEqual(0, storage.LostCount);
        }

        [Test]
        public void Merged_ReportsWhatWasProduced()
        {
            Fill(ResourceType.Ore, 5);
            MergeReport reported = default;
            merges.Merged += report => reported = report;

            merges.TryMerge(ResourceType.Ore);

            Assert.AreEqual(ResourceType.Ore, reported.Outcome.Source);
            Assert.AreEqual(ResourceType.Ingot, reported.Outcome.Result);
            Assert.AreEqual(2, reported.Outcome.Produced);
        }

        [Test]
        public void Merged_ReportsTheCellsTheAnimationNeeds()
        {
            Fill(ResourceType.Wood, 5);
            MergeReport reported = default;
            merges.Merged += report => reported = report;

            merges.TryMerge(ResourceType.Wood);

            Assert.AreEqual(new[] { 0, 1, 2, 3, 4 }, reported.ConsumedCells);
            Assert.AreEqual(new[] { 0, 1 }, reported.ResultCells);
        }

        [Test]
        public void Merged_ReportsScatteredCellsInStorageOrder()
        {
            storage.TryStore(ResourceType.Gravel);
            Fill(ResourceType.Wood, 3);
            storage.TryStore(ResourceType.Gravel);
            MergeReport reported = default;
            merges.Merged += report => reported = report;

            merges.TryMerge(ResourceType.Wood);

            Assert.AreEqual(new[] { 1, 2, 3 }, reported.ConsumedCells, "списываются клетки слева направо");
            Assert.AreEqual(new[] { 1 }, reported.ResultCells, "крафт ложится в первую освободившуюся");
        }
    
        [Test]
        public void ClickOnCrafted_PaysTheCraftedPriceWithTheMultiplier_RoundedDown()
        {
            multiplier.TrackOpened(7);
            multiplier.ExtendStreak();
            Fill(ResourceType.Board, 1);

            var converted = new List<int>();
            merges.Converted += (_, _, points) => converted.Add(points);
            merges.TryConvert(0);

            // 15 × (1 + 0.05 × 7 + 0.25) = 15 × 1.6 = 24.
            Assert.AreEqual(24, wallet.Points);
            CollectionAssert.AreEqual(new[] { 24 }, converted, "плашка показывает то, что легло в кошелёк");
        }

        // --- автодоигрывание склада ---

        [Test]
        public void PlayOut_SellsCraftBeforeTouchingTheRest()
        {
            Fill(ResourceType.Wood, 3);
            Fill(ResourceType.Board, 1);

            Assert.IsTrue(merges.TryPlayOut());

            Assert.AreEqual(0, storage.CountOf(ResourceType.Board), "крафт уходит первым и освобождает клетку");
            Assert.AreEqual(3, storage.CountOf(ResourceType.Wood));
            Assert.AreEqual(15, wallet.Points);
        }

        [Test]
        public void PlayOut_MergesWhenThereIsNoCraftLeft()
        {
            Fill(ResourceType.Wood, 3);

            Assert.IsTrue(merges.TryPlayOut());

            Assert.AreEqual(0, storage.CountOf(ResourceType.Wood));
            Assert.AreEqual(1, storage.CountOf(ResourceType.Board));
        }

        [Test]
        public void PlayOut_PrefersTheFiveMerge()
        {
            Fill(ResourceType.Wood, 5);

            merges.TryPlayOut();

            Assert.AreEqual(2, storage.CountOf(ResourceType.Board), "пятёрка даёт две доски, тройка — одну");
        }

        [Test]
        public void PlayOut_TurnsTheWholeTailIntoPoints()
        {
            Fill(ResourceType.Wood, 5);
            Fill(ResourceType.Stone, 3);

            var steps = 0;
            while (merges.TryPlayOut())
                steps++;

            Assert.AreEqual(0, storage.Count, "доигрывание кончается пустым складом");
            Assert.AreEqual(45, wallet.Points, "две доски и щебень — три обмена по 15");
            Assert.AreEqual(5, steps, "два слияния и три продажи");
            CollectionAssert.IsEmpty(refusals, "автоход не бьётся в отказы");
        }

        [Test]
        public void PlayOut_StopsOnDeadWeight()
        {
            Fill(ResourceType.Wood, 2);

            Assert.IsFalse(merges.TryPlayOut(), "двух брёвен не хватает на доску");
            Assert.AreEqual(2, storage.CountOf(ResourceType.Wood));
        }

        [Test]
        public void PlayOut_StopsOnAnEmptyStorage() => Assert.IsFalse(merges.TryPlayOut());
    }
}
