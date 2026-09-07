using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Game.Core;
using Game.Core.Balance;
using Game.Merge;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// Прогон бота на 200 сидах текущего `GameConfig`: строки — в `Temp/balance-probe.csv`,
    /// сводка — в лог теста и в `Temp/balance-probe-summary.md`. Три числа, ради которых стадия:
    /// доля от аналитического потолка, длительность партии в минутах и доля тупиков.
    /// </summary>
    public sealed class BalanceProbeTests
    {
        /// <summary>
        /// Бюджет на 200 сидов. Ориентир плана — 10 секунд, чтобы M26 могла считать потолок
        /// случайного сида на лету; предел здесь шире, он ловит зависание, а не медленную машину.
        /// Фактическое время — в сводке.
        /// </summary>
        const float BudgetSeconds = 30f;

        static string TempPath(string file) =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", file));

        [Test]
        public void TwoHundredSeeds_FitTheBudget_AndLeaveATable()
        {
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(BalanceBotTests.ConfigPath);
            var rules = AssetDatabase.LoadAssetAtPath<MergeRules>(BalanceBotTests.RulesPath);
            Assert.IsNotNull(config);
            Assert.IsNotNull(rules);

            var runs = new List<BalanceRun>(BalanceBotTests.Seeds);
            var watch = Stopwatch.StartNew();
            for (var seed = 1; seed <= BalanceBotTests.Seeds; seed++)
                runs.Add(new BalanceBot(config, rules, seed).Play());
            watch.Stop();

            var csv = new StringBuilder();
            csv.AppendLine(BalanceRun.CsvHeader);
            foreach (var run in runs)
                csv.AppendLine(run.ToCsv());

            Directory.CreateDirectory(Path.GetDirectoryName(TempPath("x")));
            File.WriteAllText(TempPath("balance-probe.csv"), csv.ToString());

            var summary = Summarize(runs, watch.Elapsed.TotalSeconds);
            File.WriteAllText(TempPath("balance-probe-summary.md"), summary);
            Debug.Log(summary);

            Assert.IsTrue(runs.All(run => run.Ended), "не все партии дошли до конца");
            Assert.Less(watch.Elapsed.TotalSeconds, BudgetSeconds, "200 сидов не уложились в бюджет времени");
        }

        static string Summarize(List<BalanceRun> runs, double elapsedSeconds)
        {
            var text = new StringBuilder();
            text.AppendLine($"Balance probe: {runs.Count} seeds, {elapsedSeconds:0.00} s total, " +
                            $"{elapsedSeconds * 1000 / runs.Count:0.0} ms per seed");
            text.AppendLine();
            text.AppendLine("| Что | Среднее | Медиана | Мин | Макс |");
            text.AppendLine("|---|---|---|---|---|");
            Row(text, "Счёт бота (Total)", runs, run => run.Score.Total);
            Row(text, "Заработано (Earned)", runs, run => run.Score.Earned);
            Row(text, "Ресурсный потолок M17 (без множителя)", runs, run => run.Ceiling);
            Row(text, "Доля от ресурсного потолка", runs, run => run.CeilingShare, "0.000");
            Row(text, "Открыто плиток", runs, run => run.Score.OpenedTiles);
            Row(text, "Достижимых плиток", runs, run => run.Score.FieldTiles);
            Row(text, "Доля открытых", runs, run => run.OpenedShare, "0.000");
            Row(text, "Выработано месторождений", runs, run => run.Score.ExhaustedDeposits);
            Row(text, "Месторождений на поле", runs, run => run.Score.FieldDeposits);
            Row(text, "Доля выработанных", runs, run => run.ExhaustedShare, "0.000");
            Row(text, "Потеряно на переполнении", runs, run => run.Score.Lost);
            Row(text, "Контрактов закрыто", runs, run => run.ContractsCompleted);
            Row(text, "Контрактов провалено", runs, run => run.ContractsFailed);
            Row(text, "Очки контрактов", runs, run => run.ContractPoints);
            Row(text, "Доля заработка от контрактов", runs, run => run.ContractShare, "0.000");
            Row(text, "Минут партии", runs, run => run.Seconds / 60f, "0.0");
            Row(text, "Мс на сид", runs, run => run.Milliseconds);
            text.AppendLine();
            text.AppendLine($"Поле пройдено (perfect): {runs.Count(run => run.Score.IsPerfect)} из {runs.Count}");
            text.AppendLine($"Все плитки открыты: {runs.Count(run => run.Score.FieldCleared)} из {runs.Count}");
            text.AppendLine($"Все месторождения выработаны: {runs.Count(run => run.Score.DepositsCleared)} из {runs.Count}");
            text.AppendLine($"Тупиков: {runs.Count(run => run.Deadlock)} из {runs.Count} " +
                            $"({runs.Count(run => run.Deadlock) / (float)runs.Count:P1})");
            text.AppendLine($"Дошли до конца сами: {runs.Count(run => run.Ended)} из {runs.Count}");
            text.AppendLine($"Средний Total против бота M23 (3334.5): {runs.Average(run => run.Score.Total) / 3334.5f:P1}");

            var perfect = runs.Where(run => run.Score.IsPerfect).ToList();
            if (perfect.Count > 0)
                text.AppendLine($"Пройденное поле: средний Total {perfect.Average(run => run.Score.Total):0.0}, " +
                                $"Earned {perfect.Average(run => run.Score.Earned):0.0}, " +
                                $"минут {perfect.Average(run => run.Seconds / 60f):0.0}");

            var deadlocks = runs.Where(run => run.Deadlock).ToList();
            if (deadlocks.Count > 0)
                text.AppendLine($"Тупики: средний Total {deadlocks.Average(run => run.Score.Total):0.0}, " +
                                $"открыто {deadlocks.Average(run => run.Score.OpenedTiles):0.0}");

            text.AppendLine($"Доля заработка от контрактов по сумме: " +
                            $"{runs.Sum(run => (long)run.ContractPoints) / (float)runs.Sum(run => (long)run.Score.Earned):P1}");
            return text.ToString();
        }

        static void Row(StringBuilder text, string label, List<BalanceRun> runs, Func<BalanceRun, float> value,
            string format = "0.0")
        {
            var values = runs.Select(value).OrderBy(v => v).ToArray();
            var median = values.Length % 2 == 1
                ? values[values.Length / 2]
                : (values[values.Length / 2 - 1] + values[values.Length / 2]) * 0.5f;

            text.AppendLine(string.Join(" | ",
                "| " + label,
                values.Average().ToString(format, CultureInfo.InvariantCulture),
                median.ToString(format, CultureInfo.InvariantCulture),
                values[0].ToString(format, CultureInfo.InvariantCulture),
                values[^1].ToString(format, CultureInfo.InvariantCulture) + " |"));
        }
    }
}
