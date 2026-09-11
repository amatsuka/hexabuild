using Game.Core;
using Game.Economy;
using Game.Grid;
using Game.Merge;
using Game.Roads;
using Game.Storage;
using Game.Tutorial;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// Обучение идёт по фиксированной карте первого уровня и закрывается событиями партии.
    /// Тесты стерегут две вещи: порядок шагов и саму раскладку — сменится сид уровня, и
    /// подсветка начнёт указывать в пустое место, а игра об этом не скажет.
    /// </summary>
    public sealed class TutorialSystemTests
    {
        const string LevelPath = "Assets/_Project/ScriptableObjects/Campaign/Level 01.asset";

        /// <summary>
        /// Сценарий целиком: тринадцать шагов и событие, которым закрывается каждый. Таблица
        /// повторена здесь намеренно — она и есть проверяемый контракт, а не деталь реализации.
        /// </summary>
        static readonly (TutorialStep Step, TutorialTrigger Trigger)[] Scenario =
        {
            (TutorialStep.OpenStone, TutorialTrigger.TileRevealed),
            (TutorialStep.BuildRoad, TutorialTrigger.RoadBuilt),
            (TutorialStep.WatchDelivery, TutorialTrigger.ResourceLanded),
            (TutorialStep.Merge, TutorialTrigger.Merged),
            (TutorialStep.Convert, TutorialTrigger.Converted),
            (TutorialStep.Goal, TutorialTrigger.Merged),
            (TutorialStep.Contract, TutorialTrigger.ContractClosed),
            (TutorialStep.CraftBoard, TutorialTrigger.Merged),
            (TutorialStep.Wall, TutorialTrigger.TileRevealed),
            (TutorialStep.Bridge, TutorialTrigger.RoadBuilt),
            (TutorialStep.SellButton, TutorialTrigger.Sold),
            (TutorialStep.Heat, TutorialTrigger.HeatLeaked),
            (TutorialStep.Sweep, TutorialTrigger.None)
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
        /// Раскладка, по которой обучение ведёт игрока: камень и дерево у Метрополии, гряда гор
        /// стеной в третьем ряду и река рядом с ней. Сменится сид уровня — тест падает первым.
        /// </summary>
        [Test]
        public void FirstLevelMap_HoldsTheLayoutTheTutorialPointsAt()
        {
            Assert.IsTrue(map.TryGetTile(TutorialSystem.StoneTile, out var stone), "нет плитки с камнем");
            Assert.IsTrue(HasDeposit(stone, ResourceType.Stone), "у первого соседа Метрополии нет камня");
            Assert.AreEqual(1, stone.Deposits.Count, "у первого соседа больше одного месторождения: три камня подряд не выйдут");

            Assert.IsTrue(map.TryGetTile(TutorialSystem.WoodTile, out var wood), "нет плитки с лесом");
            Assert.IsTrue(HasDeposit(wood, ResourceType.Wood), "у второго соседа Метрополии нет дерева");

            Assert.IsTrue(map.TryGetTile(TutorialSystem.RiverTile, out var river), "нет речной плитки");
            Assert.IsTrue(river.HasRiver, "плитка переправы без реки: мост учить не на чем");
            Assert.IsTrue(river.IsPassable, "плитка переправы непроходима: обойти гряду будет некуда");

            foreach (var coord in new[] { new HexCoord(-1, 3), new HexCoord(0, 3) })
            {
                Assert.IsTrue(map.TryGetTile(coord, out var wall), $"нет плитки гряды {coord}");
                Assert.IsFalse(wall.IsPassable, $"плитка гряды {coord} проходима: урок «стена» показать нечем");
            }
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

                if (trigger == TutorialTrigger.None)
                {
                    Assert.IsFalse(tutorial.Waits(trigger), "последний шаг ничего не ждёт");
                    tutorial.Tick(60f);
                    continue;
                }

                Assert.IsTrue(tutorial.Waits(trigger), $"шаг {step} ждёт не того события");
                Satisfy(step);
                tutorial.Notify(trigger);
            }

            Assert.AreEqual(TutorialStep.Done, tutorial.Step);
            Assert.IsFalse(tutorial.IsRunning);
            Assert.AreEqual(1, finished, "обучение обязано отчитаться о конце ровно один раз");
        }

        [Test]
        public void ForeignEvent_DoesNotAdvanceTheStep()
        {
            var tutorial = NewTutorial();

            tutorial.Notify(TutorialTrigger.Merged);
            tutorial.Notify(TutorialTrigger.Sold);

            Assert.AreEqual(TutorialStep.OpenStone, tutorial.Step, "шаг закрылся чужим событием");
        }

        /// <summary>Гора не открывается: шаг про стену закрывает только выход к реке.</summary>
        [Test]
        public void Wall_WaitsForARevealedRiverTile()
        {
            var tutorial = NewTutorial();
            Walk(tutorial, TutorialStep.Wall);

            map.TryGetTile(TutorialSystem.WoodTile, out var dry);
            dry.Reveal();
            tutorial.Notify(TutorialTrigger.TileRevealed);
            Assert.AreEqual(TutorialStep.Wall, tutorial.Step, "сухая плитка закрыла шаг про обход гряды");

            map.TryGetTile(TutorialSystem.RiverTile, out var river);
            river.Reveal();
            tutorial.Notify(TutorialTrigger.TileRevealed);
            Assert.AreEqual(TutorialStep.Bridge, tutorial.Step);
        }

        /// <summary>
        /// Урок про кнопку стоит после моста, и это не вкусовщина: до моста сценарий сам
        /// проедает и стартовые доски (контракт), и щебень (две дороги), а кнопка приходит
        /// только с запасом сверх резерва 4/4. На пустом складе ей нечего было бы продавать.
        /// </summary>
        [Test]
        public void SellStep_ComesAfterTheBridge()
        {
            Assert.Greater((int)TutorialStep.SellButton, (int)TutorialStep.Bridge);
            Assert.Greater((int)TutorialStep.Heat, (int)TutorialStep.SellButton,
                "утечку накала показывает волна продажи: накал она и выносит к потолку");
        }

        /// <summary>Мост — это дорога на речной плитке, а не любая дорога.</summary>
        [Test]
        public void Bridge_WaitsForARoadOnTheRiver()
        {
            var tutorial = NewTutorial();
            Walk(tutorial, TutorialStep.Bridge);

            roads.Build(TutorialSystem.StoneTile);
            tutorial.Notify(TutorialTrigger.RoadBuilt);
            Assert.AreEqual(TutorialStep.Bridge, tutorial.Step, "сухая дорога сошла за мост");

            roads.Build(TutorialSystem.RiverTile);
            tutorial.Notify(TutorialTrigger.RoadBuilt);
            Assert.AreEqual(TutorialStep.SellButton, tutorial.Step);
        }

        /// <summary>Шаг про доски закрывает не всякое слияние, а то, из которого вышла доска.</summary>
        [Test]
        public void CraftBoard_WaitsForABoardInStorage()
        {
            var tutorial = NewTutorial();
            Walk(tutorial, TutorialStep.CraftBoard);

            storage.TryStore(ResourceType.Gravel);
            tutorial.Notify(TutorialTrigger.Merged);
            Assert.AreEqual(TutorialStep.CraftBoard, tutorial.Step, "щебень сошёл за доску");

            storage.TryStore(ResourceType.Board);
            tutorial.Notify(TutorialTrigger.Merged);
            Assert.AreEqual(TutorialStep.Wall, tutorial.Step);
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

            // Второй раз кнопка ничего не делает: флаг уже поставлен.
            tutorial.Skip();
            Assert.AreEqual(1, finished);
        }

        /// <summary>
        /// Премию за чистый склад подсказкой не подвести: последний шаг ничего не ждёт, а гаснет
        /// сам — и только он.
        /// </summary>
        [Test]
        public void LastStep_FadesByItself()
        {
            var tutorial = NewTutorial();
            Walk(tutorial, TutorialStep.Sweep);

            tutorial.Tick(1f);
            Assert.AreEqual(TutorialStep.Sweep, tutorial.Step, "последний шаг погас, не дав себя прочитать");

            tutorial.Tick(60f);
            Assert.AreEqual(TutorialStep.Done, tutorial.Step);
        }

        [Test]
        public void EarlierSteps_DoNotFadeByThemselves()
        {
            var tutorial = NewTutorial();

            tutorial.Tick(600f);

            Assert.AreEqual(TutorialStep.OpenStone, tutorial.Step, "подсказка обязана ждать игрока, а не таймер");
        }

        /// <summary>
        /// Кнопка «Продать всё» приходит жребием 10–20 с и без обучения выпала бы посреди первых
        /// уроков. До своего шага обучение её придерживает.
        /// </summary>
        [Test]
        public void Sale_IsHeldUntilItsOwnStep()
        {
            var tutorial = NewTutorial();
            Assert.IsTrue(tutorial.HoldsSale);

            Walk(tutorial, TutorialStep.SellButton);
            Assert.IsFalse(tutorial.HoldsSale, "на своём шаге кнопка обязана прийти");

            Walk(tutorial, TutorialStep.Done);
            Assert.IsFalse(tutorial.HoldsSale, "после обучения кнопка живёт по своим правилам");
        }

        [Test]
        public void FirstSteps_AimAtTheStoneNeighbour()
        {
            var tutorial = NewTutorial();

            Assert.AreEqual(TutorialAim.Tiles, tutorial.Aim);
            CollectionAssert.AreEqual(new[] { TutorialSystem.StoneTile }, tutorial.TargetTiles);
        }

        /// <summary>Шаг слияния подсвечивает клетки того типа, которого набралось на тройку.</summary>
        [Test]
        public void MergeStep_AimsAtTheCellsThatCanMerge()
        {
            var tutorial = NewTutorial();
            Walk(tutorial, TutorialStep.Merge);

            for (var i = 0; i < rules.SmallCount; i++)
                storage.TryStore(ResourceType.Stone);

            tutorial.Refresh();

            Assert.AreEqual(TutorialAim.Cells, tutorial.Aim);
            Assert.AreEqual(rules.SmallCount, tutorial.TargetCells.Count);
        }

        /// <summary>
        /// Крестик на подсказке пропускает шаг, а не обучение: следующий встаёт на его место,
        /// и последний доводит обучение до конца — как если бы игрок прошёл его сам.
        /// </summary>
        [Test]
        public void SkipStep_MovesToTheNextStep_AndTheLastOneEndsTheTutorial()
        {
            var tutorial = NewTutorial();
            var finished = 0;
            tutorial.Finished += () => finished++;

            tutorial.SkipStep();
            Assert.AreEqual(TutorialStep.BuildRoad, tutorial.Step);
            Assert.AreEqual(0, finished, "пропуск шага не снимает обучение");

            for (var i = 0; i < Scenario.Length; i++)
                tutorial.SkipStep();

            Assert.AreEqual(TutorialStep.Done, tutorial.Step);
            Assert.AreEqual(1, finished, "последний пропущенный шаг доводит обучение до конца");

            tutorial.SkipStep();
            Assert.AreEqual(1, finished, "пройденное обучение второй раз не заканчивается");
        }

        /// <summary>
        /// Первые шаги открывают ровно свою плитку: игрок не может уйти с маршрута обучения.
        /// </summary>
        [Test]
        public void EarlySteps_OpenOnlyTheirOwnTile()
        {
            var tutorial = NewTutorial();

            Assert.IsTrue(tutorial.AllowsTile(TutorialSystem.StoneTile));
            Assert.IsFalse(tutorial.AllowsTile(TutorialSystem.WoodTile), "лес открыт раньше своего шага");
            Assert.IsFalse(tutorial.AllowsTile(TutorialSystem.RiverTile));

            Walk(tutorial, TutorialStep.CraftBoard);
            Assert.IsTrue(tutorial.AllowsTile(TutorialSystem.WoodTile));
            Assert.IsFalse(tutorial.AllowsTile(TutorialSystem.StoneTile), "камень остался открытым на чужом шаге");
        }

        /// <summary>
        /// Шаг про обход гряды открывает поле целиком: дорогу к реке игрок ищет сам, а гора
        /// откажет ему сама — в этом и урок.
        /// </summary>
        [Test]
        public void Wall_OpensTheWholeField()
        {
            var tutorial = NewTutorial();
            Walk(tutorial, TutorialStep.Wall);

            foreach (var tile in map.Tiles.Values)
                Assert.IsTrue(tutorial.AllowsTile(tile.Coord), $"плитка {tile.Coord} закрыта на шаге обхода");
        }

        /// <summary>
        /// Шаг про очки не пускает к доскам: они лежат под контракт седьмого шага, и обменянные
        /// здесь оставили бы его без цели, а мост — без оплаты.
        /// </summary>
        [Test]
        public void Convert_KeepsTheBoardsTheContractWillAskFor()
        {
            var tutorial = NewTutorial();
            Walk(tutorial, TutorialStep.Convert);

            storage.TryStore(ResourceType.Gravel);
            storage.TryStore(ResourceType.Board);
            tutorial.Refresh();

            var gravel = storage.IndexOf(ResourceType.Gravel);
            var board = storage.IndexOf(ResourceType.Board);

            Assert.IsTrue(tutorial.AllowsCell(gravel), "щебень обменять нельзя, а шаг ровно про это");
            Assert.IsFalse(tutorial.AllowsCell(board), "доска открыта раньше контракта");
        }

        /// <summary>
        /// С шага про кнопку склад открыт целиком. Иначе шаг встал бы намертво: кнопка приходит
        /// только с запасом сверх резерва, а набрать его можно одними слияниями.
        /// </summary>
        [Test]
        public void SellStep_OpensTheWholeStorage()
        {
            var tutorial = NewTutorial();
            Walk(tutorial, TutorialStep.SellButton);

            storage.TryStore(ResourceType.Stone);
            storage.TryStore(ResourceType.Gravel);
            tutorial.Refresh();

            Assert.IsTrue(tutorial.AllowsCell(storage.IndexOf(ResourceType.Stone)));
            Assert.IsTrue(tutorial.AllowsCell(storage.IndexOf(ResourceType.Gravel)));
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

        TutorialSystem NewTutorial() => new(map, storage, roads, rules);

        /// <summary>Провести обучение до нужного шага, выполняя по дороге всё, чего шаги ждут.</summary>
        void Walk(TutorialSystem tutorial, TutorialStep target)
        {
            foreach (var (step, trigger) in Scenario)
            {
                if (tutorial.Step == target)
                    return;

                if (trigger == TutorialTrigger.None)
                {
                    tutorial.Tick(60f);
                    continue;
                }

                Satisfy(step);
                tutorial.Notify(trigger);
            }
        }

        /// <summary>Состояние партии, без которого шаг не закрывается даже своим событием.</summary>
        void Satisfy(TutorialStep step)
        {
            switch (step)
            {
                case TutorialStep.CraftBoard:
                    storage.TryStore(ResourceType.Board);
                    break;
                case TutorialStep.Wall:
                    map.TryGetTile(TutorialSystem.RiverTile, out var river);
                    river.Reveal();
                    break;
                case TutorialStep.Bridge:
                    roads.Build(TutorialSystem.RiverTile);
                    break;
            }
        }

        static bool HasDeposit(TileData tile, ResourceType type)
        {
            foreach (var deposit in tile.Deposits)
                if (deposit.Type == type)
                    return true;

            return false;
        }
    }
}
