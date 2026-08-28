using Game.Economy;
using Game.Grid;
using Game.Merge;
using Game.Roads;
using Game.Storage;
using Game.Tutorial;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public sealed class TutorialSystemTests
    {
        static readonly TutorialTrigger[] Sequence =
        {
            TutorialTrigger.TileRevealed,
            TutorialTrigger.RoadBuilt,
            TutorialTrigger.ResourceLanded,
            TutorialTrigger.Merged,
            TutorialTrigger.Converted,
            TutorialTrigger.TileRevealed
        };

        HexMap map;
        StorageGrid storage;
        RoadNetwork roads;
        MergeRules rules;
        TutorialSystem tutorial;

        [SetUp]
        public void SetUp()
        {
            map = MapGenerator.Generate(new MapGenerationSettings(10, 11, 30f, 45f, 20f, 5f, 8, 20));
            storage = new StorageGrid(25);
            roads = new RoadNetwork(map);
            rules = ScriptableObject.CreateInstance<MergeRules>();
            tutorial = new TutorialSystem(map, storage, roads, rules);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(rules);

        [Test]
        public void Triggers_CloseStepsInOrder()
        {
            Assert.AreEqual(TutorialStep.OpenTile, tutorial.Step);

            tutorial.Notify(TutorialTrigger.TileRevealed);
            Assert.AreEqual(TutorialStep.BuildRoad, tutorial.Step);

            tutorial.Notify(TutorialTrigger.RoadBuilt);
            Assert.AreEqual(TutorialStep.WatchDelivery, tutorial.Step);

            tutorial.Notify(TutorialTrigger.ResourceLanded);
            Assert.AreEqual(TutorialStep.Merge, tutorial.Step);

            tutorial.Notify(TutorialTrigger.Merged);
            Assert.AreEqual(TutorialStep.Convert, tutorial.Step);

            tutorial.Notify(TutorialTrigger.Converted);
            Assert.AreEqual(TutorialStep.Loop, tutorial.Step);

            tutorial.Notify(TutorialTrigger.TileRevealed);
            Assert.AreEqual(TutorialStep.Done, tutorial.Step);
            Assert.IsFalse(tutorial.IsRunning);
        }

        [Test]
        public void ForeignTrigger_DoesNotCloseStep()
        {
            tutorial.Notify(TutorialTrigger.Merged);
            tutorial.Notify(TutorialTrigger.RoadBuilt);
            tutorial.Notify(TutorialTrigger.Converted);

            Assert.AreEqual(TutorialStep.OpenTile, tutorial.Step);
        }

        [Test]
        public void FirstStep_PointsAtStoneNeighborOfMetropolis()
        {
            MakeNeighborsAvailable();

            Assert.IsTrue(tutorial.TargetTile.HasValue);
            Assert.AreEqual(1, HexCoord.Distance(tutorial.TargetTile.Value, HexCoord.Zero));

            map.TryGetTile(tutorial.TargetTile.Value, out var tile);
            Assert.IsTrue(HasStone(tile), "Обучение обязано вести на плитку с гарантированным камнем");
        }

        [Test]
        public void RoadStep_MovesTargetToTileThePlayerOpened()
        {
            storage.TryStore(ResourceType.Gravel);
            MakeNeighborsAvailable();
            var chosen = OtherNeighborThanTarget();

            tutorial.Notify(TutorialTrigger.TileRevealed);
            Assert.AreEqual(TutorialStep.BuildRoad, tutorial.Step);
            Assert.IsNull(tutorial.TargetTile, "Открытых плиток ещё нет — указывать не на что");

            map.TryGetTile(chosen, out var tile);
            tile.Reveal();
            tutorial.Refresh();

            Assert.AreEqual(chosen, tutorial.TargetTile);
        }

        [Test]
        public void RoadStep_WithoutGravelAndRoads_ReportsStuck()
        {
            AdvanceTo(TutorialStep.BuildRoad);

            Assert.IsTrue(tutorial.IsStuck);
            Assert.IsNull(tutorial.TargetTile);

            storage.TryStore(ResourceType.Gravel);
            tutorial.Refresh();

            Assert.IsFalse(tutorial.IsStuck);
        }

        [Test]
        public void MergeStep_HighlightsOnlyTypeThatReachedThreshold()
        {
            storage.TryStore(ResourceType.Wood);
            storage.TryStore(ResourceType.Wood);
            storage.TryStore(ResourceType.Stone);
            storage.TryStore(ResourceType.Stone);
            AdvanceTo(TutorialStep.Merge);

            Assert.IsEmpty(tutorial.TargetCells, "Двух одинаковых на слияние не хватает");

            storage.TryStore(ResourceType.Stone);
            tutorial.Refresh();

            Assert.AreEqual(new[] { 2, 3, 4 }, tutorial.TargetCells);
        }

        [Test]
        public void ConvertStep_HighlightsCraftedResourcesOnly()
        {
            storage.TryStore(ResourceType.Wood);
            storage.TryStore(ResourceType.Gravel);
            storage.TryStore(ResourceType.Board);
            AdvanceTo(TutorialStep.Convert);

            Assert.AreEqual(new[] { 1, 2 }, tutorial.TargetCells);
        }

        [Test]
        public void Skip_StopsTutorialAndClearsTargets()
        {
            MakeNeighborsAvailable();
            Assert.IsTrue(tutorial.TargetTile.HasValue);

            tutorial.Skip();

            Assert.AreEqual(TutorialStep.Done, tutorial.Step);
            Assert.IsFalse(tutorial.IsRunning);
            Assert.IsNull(tutorial.TargetTile);
            Assert.IsEmpty(tutorial.TargetCells);
        }

        [Test]
        public void FinishedTutorial_IgnoresFurtherEvents()
        {
            tutorial.Skip();

            tutorial.Notify(TutorialTrigger.TileRevealed);
            tutorial.Notify(TutorialTrigger.Merged);

            Assert.AreEqual(TutorialStep.Done, tutorial.Step);
        }

        void AdvanceTo(TutorialStep step)
        {
            while (tutorial.Step != step)
                tutorial.Notify(Sequence[(int)tutorial.Step]);
        }

        void MakeNeighborsAvailable()
        {
            foreach (var neighbor in map.NeighborsOf(HexCoord.Zero))
                neighbor.MakeAvailable();

            tutorial.Refresh();
        }

        /// <summary>Сосед Метрополии, на который обучение не указывает: цель должна переехать на него.</summary>
        HexCoord OtherNeighborThanTarget()
        {
            foreach (var neighbor in map.NeighborsOf(HexCoord.Zero))
                if (neighbor.Coord != tutorial.TargetTile)
                    return neighbor.Coord;

            return HexCoord.Zero;
        }

        static bool HasStone(TileData tile)
        {
            foreach (var deposit in tile.Deposits)
                if (deposit.Type == ResourceType.Stone)
                    return true;

            return false;
        }
    }
}
