using System;
using System.Collections.Generic;
using Game.Core;
using Game.Core.Balance;
using Game.Economy;
using Game.Grid;
using Game.Merge;
using NUnit.Framework;
using UnityEditor;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// Бот играет на текущих ассетах баланса, а не на дефолтах класса: замер снимается с той
    /// партии, которую видит игрок, — с `fieldRows` 14, а не 18 из конструктора.
    /// </summary>
    public sealed class BalanceBotTests
    {
        public const string ConfigPath = "Assets/_Project/ScriptableObjects/GameConfig.asset";
        public const string RulesPath = "Assets/_Project/ScriptableObjects/MergeRules.asset";
        public const int Seeds = 200;

        GameConfig config;
        MergeRules rules;

        [SetUp]
        public void SetUp()
        {
            config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
            rules = AssetDatabase.LoadAssetAtPath<MergeRules>(RulesPath);
            Assert.IsNotNull(config, $"нет ассета {ConfigPath}");
            Assert.IsNotNull(rules, $"нет ассета {RulesPath}");
        }

        /// <summary>Строка без последнего столбца: миллисекунды реального времени детерминизму не подчиняются.</summary>
        static string Table(BalanceRun run)
        {
            var row = run.ToCsv();
            return row.Substring(0, row.LastIndexOf(';'));
        }

        [Test]
        public void SameSeedTwice_GivesTheSameTable()
        {
            var first = new BalanceBot(config, rules, 7).Play();
            var second = new BalanceBot(config, rules, 7).Play();

            Assert.AreEqual(Table(first), Table(second));
            Assert.IsTrue(first.Ended, "партия не дошла до конца за предел времени");
        }

        [Test]
        public void SeedZero_IsRefused_BecauseItMeansRandom() =>
            Assert.Throws<ArgumentException>(() => new BalanceBot(config, rules, 0));

        [Test]
        public void OnePlayPerBot()
        {
            var bot = new BalanceBot(config, rules, 3);
            bot.Play();
            Assert.Throws<InvalidOperationException>(() => bot.Play());
        }

        /// <summary>
        /// Бот проверяет всё до действия: ни одного отказа правил, ни одной открытой стены или
        /// недостижимой плитки, ни одной дороги на стене. Правила и сами бы отказали — тест
        /// стережёт, что бот на них не полагается.
        /// </summary>
        [Test]
        public void NeverAsksForTheIllegal_OnTwentySeeds()
        {
            for (var seed = 1; seed <= 20; seed++)
            {
                var bot = new BalanceBot(config, rules, seed);
                bot.Play();

                Assert.AreEqual(0, bot.Refusals, $"seed {seed}: правила отказали боту");

                var reachable = new HashSet<HexCoord>();
                foreach (var tile in bot.State.Map.ReachableFromMetropolis())
                    reachable.Add(tile.Coord);

                foreach (var tile in bot.State.Map.Tiles.Values)
                {
                    if (tile.State is not (TileState.Revealed or TileState.Depleted) || tile.IsMetropolis)
                        continue;

                    Assert.IsTrue(tile.IsPassable, $"seed {seed}: открыта стена {tile.Coord}");
                    Assert.IsTrue(reachable.Contains(tile.Coord), $"seed {seed}: открыта недостижимая {tile.Coord}");
                }

                foreach (var road in bot.State.Roads.Roads)
                {
                    Assert.IsTrue(bot.State.Map.TryGetTile(road, out var tile), $"seed {seed}: дорога вне поля {road}");
                    Assert.IsTrue(tile.IsPassable, $"seed {seed}: дорога на стене {road}");
                    Assert.IsTrue(bot.State.Roads.IsConnected(road), $"seed {seed}: оторванная дорога {road}");
                }
            }
        }

        /// <summary>
        /// На 200 сидах партия кончается сама, а счёт не выходит за то, что поле способно дать:
        /// обмен не приносит больше, чем крафта можно выжать из запаса достижимых месторождений
        /// пятёрками и тройками плюс стартовый щебень — по цене крафта с наибольшим множителем,
        /// какой возможен на этом поле, — а итог не выше аналитического потолка с тем же
        /// множителем и поправкой на то, чего потолок не считает: стартовый щебень, награды
        /// контрактов и два бонуса за пройденное поле.
        /// </summary>
        [Test]
        public void EndsOnItsOwn_AndStaysBelowTheCeiling_OnEverySeed()
        {
            for (var seed = 1; seed <= Seeds; seed++)
            {
                var run = new BalanceBot(config, rules, seed).Play();
                Assert.IsTrue(run.Ended, $"seed {seed}: партия упёрлась в предел времени");

                var achievableCrafts = config.StartingGravel;
                foreach (var units in UnitsByType(seed).Values)
                    achievableCrafts += Crafts(units);

                var maxMultiplier = 1f + config.MultiplierStep * run.Score.FieldTiles + config.StreakMax;
                var craftPoints = (int)Math.Ceiling(rules.CraftedPoints * maxMultiplier);

                var exchanged = run.Score.Earned - run.ContractPoints;
                Assert.LessOrEqual(
                    exchanged, achievableCrafts * craftPoints,
                    $"seed {seed}: обмен принёс больше, чем есть крафта на поле");

                var allowance = config.StartingGravel * craftPoints
                    + run.ContractPoints
                    + config.FullFieldBonus + config.FullDepositBonus;
                var ceiling = (int)Math.Ceiling(run.Ceiling * maxMultiplier);
                Assert.LessOrEqual(run.Score.Total, ceiling + allowance, $"seed {seed}: счёт выше потолка");
            }
        }

        /// <summary>Запас достижимых месторождений по типам — с чистой карты, до партии.</summary>
        Dictionary<ResourceType, int> UnitsByType(int seed)
        {
            var map = MapGenerator.Generate(config.MapGenerationSettingsFor(seed));
            var units = new Dictionary<ResourceType, int>();

            foreach (var tile in map.ReachableFromMetropolis())
                foreach (var deposit in tile.Deposits)
                    units[deposit.Type] = units.GetValueOrDefault(deposit.Type) + deposit.Reserve;

            return units;
        }

        /// <summary>Сколько крафта выжимается из запаса: сначала пятёрки, остаток — тройками.</summary>
        int Crafts(int units)
        {
            rules.TryResolve(ResourceType.Wood, rules.LargeCount, out var large);
            rules.TryResolve(ResourceType.Wood, rules.SmallCount, out var small);
            return units / rules.LargeCount * large.Produced + units % rules.LargeCount / rules.SmallCount * small.Produced;
        }
    }
}
