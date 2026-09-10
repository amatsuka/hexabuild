using Game.Core;
using Game.Core.Balance;
using Game.Grid;
using Game.Merge;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// Кампания сверяется с ботом: потолок каждого уровня записан в ассете, и если баланс уехал —
    /// правкой `GameConfig`, рычага уровня или самих правил, — тест падает первым, а не игрок
    /// получает три звезды за половину карты. Пункт «Снять потолок ботом» в контекстном меню
    /// `LevelConfig` этот же прогон и делает.
    /// </summary>
    public sealed class CampaignTests
    {
        const string CampaignPath = "Assets/_Project/ScriptableObjects/Campaign/Campaign.asset";

        /// <summary>Партия бота на уровне кампании не должна растягиваться вдвое против соседей.</summary>
        const float MaxLevelSeconds = 600f;

        CampaignConfig campaign;
        GameConfig config;
        MergeRules rules;

        [SetUp]
        public void SetUp()
        {
            campaign = AssetDatabase.LoadAssetAtPath<CampaignConfig>(CampaignPath);
            config = AssetDatabase.LoadAssetAtPath<GameConfig>(BalanceBotTests.ConfigPath);
            rules = AssetDatabase.LoadAssetAtPath<MergeRules>(BalanceBotTests.RulesPath);
            Assert.IsNotNull(campaign, $"нет ассета {CampaignPath}");
            Assert.IsNotNull(config, $"нет ассета {BalanceBotTests.ConfigPath}");
            Assert.IsNotNull(rules, $"нет ассета {BalanceBotTests.RulesPath}");
        }

        [Test]
        public void CampaignHasLevels_AndEveryOneIsFilled()
        {
            Assert.Greater(campaign.Count, 0, "в кампании нет уровней");

            for (var i = 0; i < campaign.Count; i++)
            {
                var level = campaign[i];
                Assert.IsNotNull(level, $"уровень {i + 1} не назначен");
                Assert.AreNotEqual(0, level.Seed, $"уровень {i + 1}: сид 0 значит «случайный», кампания так не играется");
                Assert.Greater(level.BotCeiling, 0, $"уровень {i + 1}: потолок не снят ботом");
                Assert.Less(level.WaterCeiling, level.RocksCeiling,
                    $"уровень {i + 1}: вода выше гор — кривая высоты вывернута");
            }
        }

        [Test]
        public void StarThresholds_Grow_AndTopIsTheBot()
        {
            for (var i = 0; i < campaign.Count; i++)
            {
                var level = campaign[i];
                Assert.Less(level.OneStarShare, level.TwoStarShare, $"уровень {i + 1}: пороги звёзд не растут");
                Assert.Less(level.TwoStarShare, level.ThreeStarShare, $"уровень {i + 1}: пороги звёзд не растут");

                Assert.AreEqual(3, level.Stars(level.BotCeiling), $"уровень {i + 1}: счёт бота обязан давать три звезды");
                Assert.AreEqual(0, level.Stars(0), $"уровень {i + 1}: нулевой счёт звёзд не даёт");
            }
        }

        /// <summary>
        /// Главный тест стадии: потолок в ассете — это `Total` бота на том же сиде и тех же
        /// рычагах. Расхождение значит, что баланс менялся после того, как потолок сняли.
        /// </summary>
        [Test]
        public void EveryLevelCeiling_MatchesTheBot()
        {
            for (var i = 0; i < campaign.Count; i++)
            {
                var level = campaign[i];
                var run = new BalanceBot(config, rules, level.Seed, level).Play();

                Assert.AreEqual(level.BotCeiling, run.Score.Total,
                    $"уровень {i + 1}: потолок в ассете разошёлся с ботом — сними его заново");
                Assert.IsTrue(run.Ended, $"уровень {i + 1}: партия бота не дошла до конца");
                Assert.IsFalse(run.Deadlock, $"уровень {i + 1}: тупиковый сид в кампанию не берут");
                Assert.Less(run.Seconds, MaxLevelSeconds, $"уровень {i + 1}: партия длиннее десяти минут");
            }
        }

        /// <summary>Первый уровень заменяет вырезанное обучение: воды на нём нет, контракт длинный.</summary>
        [Test]
        public void FirstLevel_HasNoWater_AndTheLongestContract()
        {
            var first = campaign[0];
            var map = MapGenerator.Generate(config.MapGenerationSettingsFor(first.Seed, first));

            foreach (var tile in map.Tiles.Values)
                Assert.AreNotEqual(BiomeType.Water, tile.Biome, $"вода на первом уровне: {tile.Coord}");

            for (var i = 1; i < campaign.Count; i++)
                Assert.GreaterOrEqual(config.ContractSecondsFor(first), config.ContractSecondsFor(campaign[i]),
                    $"уровень {i + 1} даёт на контракт больше времени, чем первый");
        }

        /// <summary>Давление растёт: последний уровень крупнее, злее и щедрее первого.</summary>
        [Test]
        public void LastLevel_PressesHarderThanTheFirst()
        {
            var first = campaign[0];
            var last = campaign[campaign.Count - 1];

            Assert.Greater(last.FieldRows, first.FieldRows, "поле не выросло");
            Assert.Less(config.ContractSecondsFor(last), config.ContractSecondsFor(first), "таймер не ужался");
            Assert.Greater(config.ContractGoalFor(last), config.ContractGoalFor(first), "цель контракта не выросла");
            Assert.Less(config.ExtractionIntervalFor(last), config.ExtractionIntervalFor(first), "добыча не ускорилась");
            Assert.Greater(last.ReserveScale, first.ReserveScale, "запас месторождений не вырос");
        }

        /// <summary>
        /// Уровень переопределяет дефолты, а без уровня партия идёт ровно на них: `null` — это
        /// свободная игра, и она не должна меняться от появления кампании.
        /// </summary>
        [Test]
        public void NoLevel_MeansDefaults()
        {
            var settings = config.MapGenerationSettingsFor(11);

            Assert.AreEqual(config.FieldRows, settings.Rows);
            Assert.AreEqual(1f, settings.ReserveScale);
            Assert.AreEqual(MapGenerator.WaterCeiling, settings.WaterCeiling);
            Assert.AreEqual(MapGenerator.RocksCeiling, settings.RocksCeiling);
            Assert.AreEqual(config.ContractGoal, config.ContractGoalFor(null));
            Assert.AreEqual(config.ContractSeconds, config.ContractSecondsFor(null));
            Assert.AreEqual(config.ExtractionInterval, config.ExtractionIntervalFor(null));
        }

        /// <summary>
        /// Растяжка шума переводит пороги уровня в канонические отметки, а канонические пороги
        /// оставляет как есть: карта вне кампании обязана остаться прежней до числа.
        /// </summary>
        [Test]
        public void Stretch_IsIdentity_OnCanonicalCeilings()
        {
            for (var noise = 0f; noise <= 1f; noise += 0.05f)
                Assert.AreEqual(noise,
                    MapGenerator.Stretch(noise, MapGenerator.WaterCeiling, MapGenerator.RocksCeiling), 1e-4f,
                    $"растяжка сдвинула шум {noise:0.00}");
        }

        [Test]
        public void Stretch_MovesLevelCeilings_ToCanonicalOnes()
        {
            // Порог воды 0.4 значит «под водой 40% шума»: эта отметка обязана лечь ровно на урез.
            Assert.AreEqual(MapGenerator.WaterCeiling, MapGenerator.Stretch(0.4f, 0.4f, 0.8f), 1e-4f);
            Assert.AreEqual(MapGenerator.RocksCeiling, MapGenerator.Stretch(0.8f, 0.4f, 0.8f), 1e-4f);

            // Ноль как порог воды значит «воды нет»: даже самый низкий шум остаётся сушей.
            Assert.GreaterOrEqual(MapGenerator.Stretch(0f, 0f, 0.8f), MapGenerator.WaterCeiling);
        }

        /// <summary>Щедрость уровня умножает запас, а не заменяет рост по рядам.</summary>
        [Test]
        public void ReserveScale_MultipliesReserves()
        {
            var lean = Reserves(MapSettings(1f));
            var rich = Reserves(MapSettings(2f));

            Assert.Greater(rich, lean * 1.5f, $"щедрость не сработала: {lean} против {rich}");
        }

        MapGenerationSettings MapSettings(float reserveScale) => new(
            8, 12345, 30f, 45f, 20f, 5f, 8, 20, 0.06f, 0.18f, reserveScale);

        static int Reserves(MapGenerationSettings settings)
        {
            var map = MapGenerator.Generate(settings);
            var total = 0;

            foreach (var tile in map.Tiles.Values)
                foreach (var deposit in tile.Deposits)
                    total += deposit.Reserve;

            return total;
        }

        /// <summary>
        /// Прогресс живёт в `PlayerPrefs` и переживает перезапуск. Тест пишет в те же ключи,
        /// что и игра, поэтому за собой прибирает.
        /// </summary>
        [Test]
        public void Progress_KeepsStarsAndBest_AndUnlocksTheNextLevel()
        {
            var levels = campaign.Count;
            CampaignProgress.Clear(levels);

            Assert.AreEqual(0, CampaignProgress.Unlocked, "по умолчанию открыт только первый уровень");
            Assert.IsTrue(CampaignProgress.IsUnlocked(0));
            Assert.IsFalse(CampaignProgress.IsUnlocked(1));

            CampaignProgress.Submit(0, 500, 2, levels);
            Assert.AreEqual(2, CampaignProgress.StarsAt(0));
            Assert.AreEqual(500, CampaignProgress.BestAt(0));
            Assert.IsTrue(CampaignProgress.IsUnlocked(1), "пройденный уровень открывает следующий");

            // Партия хуже прежней не отнимает ни звёзд, ни рекорда.
            CampaignProgress.Submit(0, 100, 1, levels);
            Assert.AreEqual(2, CampaignProgress.StarsAt(0));
            Assert.AreEqual(500, CampaignProgress.BestAt(0));

            // Обучение живёт в тех же ключах и ставится один раз на всю кампанию.
            Assert.IsFalse(CampaignProgress.TutorialDone, "обучение не должно считаться пройденным до партии");
            CampaignProgress.CompleteTutorial();
            Assert.IsTrue(CampaignProgress.TutorialDone);

            CampaignProgress.Clear(levels);
            Assert.IsFalse(CampaignProgress.TutorialDone, "сброс кампании возвращает и обучение");
        }

        [Test]
        public void Stars_AreSharesOfTheBotCeiling()
        {
            var level = campaign[0];
            var ceiling = level.BotCeiling;

            Assert.AreEqual(0, level.Stars(Mathf.RoundToInt(ceiling * 0.49f)));
            Assert.AreEqual(1, level.Stars(Mathf.RoundToInt(ceiling * 0.51f)));
            Assert.AreEqual(2, level.Stars(Mathf.RoundToInt(ceiling * 0.76f)));
            Assert.AreEqual(3, level.Stars(Mathf.RoundToInt(ceiling * 1.01f)));
        }
    }
}
