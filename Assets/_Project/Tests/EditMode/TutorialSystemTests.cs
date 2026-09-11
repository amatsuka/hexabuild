using Game.Core;
using Game.Economy;
using Game.Grid;
using Game.Merge;
using Game.Roads;
using Game.Storage;
using Game.Tutorial;
using NUnit.Framework;
using UnityEditor;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// Обучение идёт по рукотворной карте первого уровня и закрывается либо единственным
    /// разрешённым действием, либо кнопкой «Дальше». Тесты стерегут три вещи: порядок шагов,
    /// саму раскладку карты и то, что на каждом шаге открыто ровно нужное, — сломается любая,
    /// и игрок упрётся в подсказку, которая никогда не закроется.
    /// </summary>
    public sealed class TutorialSystemTests
    {
        const string LevelPath = "Assets/_Project/ScriptableObjects/Campaign/Level 01.asset";

        /// <summary>
        /// Сценарий целиком: пятнадцать шагов и то, чем закрывается каждый. Таблица повторена
        /// здесь намеренно — она и есть проверяемый контракт, а не деталь реализации.
        /// </summary>
        static readonly (TutorialStep Step, TutorialTrigger Trigger)[] Scenario =
        {
            (TutorialStep.OpenStone, TutorialTrigger.TileRevealed),
            (TutorialStep.BuildRoad, TutorialTrigger.RoadBuilt),
            (TutorialStep.WatchDelivery, TutorialTrigger.ResourceLanded),
            (TutorialStep.Merge, TutorialTrigger.Merged),
            (TutorialStep.Convert, TutorialTrigger.Converted),
            (TutorialStep.Goal, TutorialTrigger.Next),
            (TutorialStep.Contract, TutorialTrigger.ContractClosed),
            (TutorialStep.OpenWood, TutorialTrigger.RoadBuilt),
            (TutorialStep.CraftBoard, TutorialTrigger.Merged),
            (TutorialStep.Wall, TutorialTrigger.Next),
            (TutorialStep.Bypass, TutorialTrigger.RoadBuilt),
            (TutorialStep.Bridge, TutorialTrigger.RoadBuilt),
            (TutorialStep.SellButton, TutorialTrigger.Sold),
            (TutorialStep.Heat, TutorialTrigger.Next),
            (TutorialStep.Sweep, TutorialTrigger.Next)
        };

        GameConfig config;
        MergeRules rules;
        LevelConfig level;
        HexMap map;
        StorageGrid storage;
        RoadNetwork roads;

        [SetUp]
        public void SetUp()
        {
            config = AssetDatabase.LoadAssetAtPath<GameConfig>(BalanceBotTests.ConfigPath);
            rules = AssetDatabase.LoadAssetAtPath<MergeRules>(BalanceBotTests.RulesPath);
            level = AssetDatabase.LoadAssetAtPath<LevelConfig>(LevelPath);
            Assert.IsNotNull(config, $"нет ассета {BalanceBotTests.ConfigPath}");
            Assert.IsNotNull(rules, $"нет ассета {BalanceBotTests.RulesPath}");
            Assert.IsNotNull(level, $"нет ассета {LevelPath}");

            map = MapGenerator.Generate(config.MapGenerationSettingsFor(level.Seed, level));
            storage = new StorageGrid(config.StorageSize);
            roads = new RoadNetwork(map);
        }

        /// <summary>
        /// Первый уровень обязан идти по рукотворной карте: на сгенерированной у сценария нет
        /// ни одной гарантии — ни что сосед один с камнем, ни что проход сквозь гряду один.
        /// </summary>
        [Test]
        public void FirstLevel_UsesTheHandMadeMap()
        {
            Assert.IsTrue(level.HandMadeMap, "у первого уровня снят флаг рукотворной карты");
        }

        /// <summary>Раскладка коридора, по которому обучение ведёт игрока за руку.</summary>
        [Test]
        public void Map_IsTheCorridorTheTutorialLeadsThrough()
        {
            Assert.IsTrue(map.TryGetTile(TutorialMap.StoneTile, out var stone));
            Assert.AreEqual(1, stone.Deposits.Count, "у соседа с камнем обязано быть одно месторождение");
            Assert.AreEqual(ResourceType.Stone, stone.Deposits[0].Type);

            Assert.IsTrue(map.TryGetTile(TutorialMap.WoodTile, out var wood));
            Assert.AreEqual(1, wood.Deposits.Count, "у соседа с лесом обязано быть одно месторождение");
            Assert.AreEqual(ResourceType.Wood, wood.Deposits[0].Type);

            foreach (var coord in TutorialMap.Ridge)
            {
                Assert.IsTrue(map.TryGetTile(coord, out var ridge));
                Assert.IsFalse(ridge.IsPassable, $"гряда {coord} проходима: урок про стену показать нечем");
            }

            Assert.IsTrue(map.TryGetTile(TutorialMap.BypassTile, out var bypass));
            Assert.IsTrue(bypass.IsPassable, "прохода сквозь гряду нет");

            Assert.IsTrue(map.TryGetTile(TutorialMap.RiverTile, out var river));
            Assert.IsTrue(river.HasRiver && river.IsPassable, "переправа не переправа");
        }

        /// <summary>
        /// Обход у гряды ровно один. Будь их два, подсветка указывала бы на одну плитку, а игрок
        /// с равным правом открывал бы другую — и шаг закрывался бы мимо подсказки.
        /// </summary>
        [Test]
        public void Ridge_HasExactlyOneWayThrough()
        {
            var ways = 0;
            foreach (var tile in map.Tiles.Values)
                if (tile.Coord.R == 2 && tile.IsPassable)
                    ways++;

            Assert.AreEqual(1, ways, "во втором ряду проходима не одна плитка");

            var crossings = 0;
            foreach (var tile in map.Tiles.Values)
                if (tile.Coord.R == 3 && tile.IsPassable)
                    crossings++;

            Assert.AreEqual(1, crossings, "через реку ведёт не одна переправа");
        }

        /// <summary>Всё проходимое поле достижимо: запертых кусков рукотворная карта не оставляет.</summary>
        [Test]
        public void WholePassableField_IsReachable()
        {
            var passable = 0;
            foreach (var tile in map.Tiles.Values)
                if (tile.IsPassable)
                    passable++;

            Assert.AreEqual(passable, map.ReachableFromMetropolis().Count);
        }

        [Test]
        public void Scenario_WalksEveryStep_InOrder()
        {
            var tutorial = NewTutorial();
            var finished = 0;
            tutorial.Finished += () => finished++;

            foreach (var (step, trigger) in Scenario)
            {
                Assert.AreEqual(step, tutorial.Step, "шаги пошли не по порядку");
                Assert.IsTrue(tutorial.Waits(trigger), $"шаг {step} ждёт не того события");
                Close(tutorial, step, trigger);
            }

            Assert.AreEqual(TutorialStep.Done, tutorial.Step);
            Assert.IsFalse(tutorial.IsRunning);
            Assert.AreEqual(1, finished, "обучение обязано отчитаться о конце ровно один раз");
        }

        /// <summary>
        /// Шаг с действием кнопкой «Дальше» не закрывается, а шаг-читалка не закрывается ничем,
        /// кроме неё. Иначе игрок либо проскакивает урок, либо застревает в нём.
        /// </summary>
        [Test]
        public void NextButton_LivesOnlyOnStepsWithNothingToDo()
        {
            var tutorial = NewTutorial();

            tutorial.Next();
            Assert.AreEqual(TutorialStep.OpenStone, tutorial.Step, "«Дальше» закрыла шаг с действием");

            Walk(tutorial, TutorialStep.Goal);
            Assert.IsTrue(tutorial.Waits(TutorialTrigger.Next));

            tutorial.Notify(TutorialTrigger.Merged);
            Assert.AreEqual(TutorialStep.Goal, tutorial.Step, "читалку закрыло чужое событие");

            tutorial.Next();
            Assert.AreEqual(TutorialStep.Contract, tutorial.Step);
        }

        [Test]
        public void ForeignEvent_DoesNotAdvanceTheStep()
        {
            var tutorial = NewTutorial();

            tutorial.Notify(TutorialTrigger.Merged);
            tutorial.Notify(TutorialTrigger.Sold);

            Assert.AreEqual(TutorialStep.OpenStone, tutorial.Step, "шаг закрылся чужим событием");
        }

        /// <summary>Каждый шаг с целью на поле открывает ровно одну плитку — свою.</summary>
        [Test]
        public void EveryFieldStep_OpensExactlyItsOwnTile()
        {
            var tutorial = NewTutorial();

            AssertOnlyTileOpen(tutorial, TutorialMap.StoneTile);

            Walk(tutorial, TutorialStep.OpenWood);
            AssertOnlyTileOpen(tutorial, TutorialMap.WoodTile);

            Walk(tutorial, TutorialStep.Bypass);
            AssertOnlyTileOpen(tutorial, TutorialMap.BypassTile);

            Walk(tutorial, TutorialStep.Bridge);
            AssertOnlyTileOpen(tutorial, TutorialMap.RiverTile);
        }

        /// <summary>Шаг про стену не открывает ничего: гряду не тапают, её обходят следующим шагом.</summary>
        [Test]
        public void Wall_OpensNothing_AndPointsAtTheRidge()
        {
            var tutorial = NewTutorial();
            Walk(tutorial, TutorialStep.Wall);

            Assert.AreEqual(TutorialMap.Ridge.Length, tutorial.TargetTiles.Count);

            foreach (var tile in map.Tiles.Values)
                Assert.IsFalse(tutorial.AllowsTile(tile.Coord), $"плитка {tile.Coord} открыта на шаге-читалке");
        }

        /// <summary>
        /// Шаг про очки не пускает к доскам: они лежат под контракт, и обменянные здесь оставили
        /// бы его без цели, а мост потом без оплаты.
        /// </summary>
        [Test]
        public void Convert_KeepsTheBoardsTheContractWillAskFor()
        {
            var tutorial = NewTutorial();
            Walk(tutorial, TutorialStep.Convert);

            storage.TryStore(ResourceType.Gravel);
            storage.TryStore(ResourceType.Board);
            tutorial.Refresh();

            Assert.IsTrue(tutorial.AllowsCell(storage.IndexOf(ResourceType.Gravel)));
            Assert.IsFalse(tutorial.AllowsCell(storage.IndexOf(ResourceType.Board)), "доска открыта раньше контракта");
        }

        /// <summary>
        /// На шаге про кнопку открыты только слияния: обмен проедает тот самый запас сверх
        /// резерва, без которого кнопка не приходит вовсе.
        /// </summary>
        [Test]
        public void SellStep_OpensMergesButNotExchange()
        {
            var tutorial = NewTutorial();
            Walk(tutorial, TutorialStep.SellButton);

            for (var i = 0; i < rules.SmallCount; i++)
                storage.TryStore(ResourceType.Stone);

            storage.TryStore(ResourceType.Gravel);
            tutorial.Refresh();

            Assert.IsTrue(tutorial.AllowsCell(storage.IndexOf(ResourceType.Stone)), "слияние закрыто");
            Assert.IsFalse(tutorial.AllowsCell(storage.IndexOf(ResourceType.Gravel)), "обмен проест запас под кнопку");
        }

        /// <summary>
        /// Шаг, который просит ресурс, обязан дать его сделать. Дорога стоит щебня, мост — двух
        /// щебня и двух досок: со всем закрытым складом взять их было бы негде, и шаг повис бы
        /// навсегда. Обмен при этом закрыт — им запас и проедается.
        /// </summary>
        [Test]
        public void StepsThatCostResources_OpenMerges()
        {
            foreach (var step in new[] { TutorialStep.OpenWood, TutorialStep.Bypass, TutorialStep.Bridge })
            {
                var tutorial = NewTutorial();
                Walk(tutorial, step);

                for (var i = 0; i < rules.SmallCount; i++)
                    storage.TryStore(ResourceType.Wood);

                storage.TryStore(ResourceType.Gravel);
                tutorial.Refresh();

                Assert.IsTrue(tutorial.AllowsCell(storage.IndexOf(ResourceType.Wood)),
                    $"шаг {step} просит ресурс, но слияния закрыты");
                Assert.IsFalse(tutorial.AllowsCell(storage.IndexOf(ResourceType.Gravel)),
                    $"шаг {step} пускает к обмену: он проест то, что шаг и просит");

                storage.TryRemove(ResourceType.Wood, rules.SmallCount);
                storage.TryRemove(ResourceType.Gravel, 1);
            }
        }

        /// <summary>
        /// Контракт просит доски, значит доски на его шаге и открыты. Шаг про очки их, наоборот,
        /// закрывает — и эти два списка легко перепутать: «крафт кроме досок» и «только доски».
        /// </summary>
        [Test]
        public void Contract_OpensExactlyTheBoardsItAsksFor()
        {
            var tutorial = NewTutorial();
            Walk(tutorial, TutorialStep.Contract);

            storage.TryStore(ResourceType.Board);
            storage.TryStore(ResourceType.Gravel);
            tutorial.Refresh();

            var board = storage.IndexOf(ResourceType.Board);
            Assert.IsTrue(tutorial.AllowsCell(board), "доски закрыты на шаге, который их и просит");
            CollectionAssert.Contains(tutorial.TargetCells, board, "доска не подсвечена");
            Assert.IsFalse(tutorial.AllowsCell(storage.IndexOf(ResourceType.Gravel)),
                "щебень открыт на шаге про контракт: контракт просит не его");
        }

        /// <summary>Кнопка продажи не приходит до своего шага и не держится после обучения.</summary>
        [Test]
        public void Sale_IsHeldUntilItsOwnStep()
        {
            var tutorial = NewTutorial();
            Assert.IsTrue(tutorial.HoldsSale);

            Walk(tutorial, TutorialStep.SellButton);
            Assert.IsFalse(tutorial.HoldsSale, "на своём шаге кнопка обязана прийти");

            Walk(tutorial, TutorialStep.Done);
            Assert.IsFalse(tutorial.HoldsSale);
        }

        /// <summary>Пройденное обучение не держит ни поля, ни склада.</summary>
        [Test]
        public void FinishedTutorial_HoldsNothing()
        {
            var tutorial = NewTutorial();
            tutorial.Skip();

            foreach (var tile in map.Tiles.Values)
                Assert.IsTrue(tutorial.AllowsTile(tile.Coord));
        }

        [Test]
        public void Skip_EndsTheTutorial_AtOnce()
        {
            var tutorial = NewTutorial();
            var finished = 0;
            tutorial.Finished += () => finished++;

            tutorial.Skip();

            Assert.AreEqual(TutorialStep.Done, tutorial.Step);
            Assert.AreEqual(1, finished);

            tutorial.Skip();
            Assert.AreEqual(1, finished, "пройденное обучение второй раз не заканчивается");
        }

        void AssertOnlyTileOpen(TutorialSystem tutorial, HexCoord open)
        {
            CollectionAssert.AreEqual(new[] { open }, tutorial.TargetTiles, "подсветка не на той плитке");

            foreach (var tile in map.Tiles.Values)
                Assert.AreEqual(tile.Coord == open, tutorial.AllowsTile(tile.Coord),
                    $"плитка {tile.Coord} открыта не по шагу {tutorial.Step}");
        }

        TutorialSystem NewTutorial() => new(map, storage, roads, rules);

        void Walk(TutorialSystem tutorial, TutorialStep target)
        {
            foreach (var (step, trigger) in Scenario)
            {
                if (tutorial.Step == target)
                    return;

                Close(tutorial, step, trigger);
            }
        }

        /// <summary>Закрыть шаг тем, чем его закрывает игрок: действием или кнопкой «Дальше».</summary>
        void Close(TutorialSystem tutorial, TutorialStep step, TutorialTrigger trigger)
        {
            if (trigger == TutorialTrigger.Next)
            {
                tutorial.Next();
                return;
            }

            switch (step)
            {
                case TutorialStep.CraftBoard:
                    storage.TryStore(ResourceType.Board);
                    break;
                case TutorialStep.Bridge:
                    roads.Build(TutorialMap.RiverTile);
                    break;
            }

            tutorial.Notify(trigger);
        }
    }
}
