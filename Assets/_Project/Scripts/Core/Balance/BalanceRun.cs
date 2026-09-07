using System.Globalization;

namespace Game.Core.Balance
{
    /// <summary>
    /// Итог одной партии бота: строка таблицы замера. Всё, что в ней есть, снято с тех же систем,
    /// что показывает финальный экран, — счёт тот же `FinalScore`, а не своя арифметика.
    /// </summary>
    public readonly struct BalanceRun
    {
        public BalanceRun(
            int seed,
            FinalScore score,
            int ceiling,
            int contractsCompleted,
            int contractsFailed,
            float seconds,
            bool ended,
            long milliseconds)
        {
            Seed = seed;
            Score = score;
            Ceiling = ceiling;
            ContractsCompleted = contractsCompleted;
            ContractsFailed = contractsFailed;
            Seconds = seconds;
            Ended = ended;
            Milliseconds = milliseconds;
        }

        public int Seed { get; }

        public FinalScore Score { get; }

        /// <summary>Аналитический потолок того же сида, см. <see cref="BalanceCeiling"/>.</summary>
        public int Ceiling { get; }

        public int ContractsCompleted { get; }

        public int ContractsFailed { get; }

        /// <summary>Сколько игровых секунд шла партия.</summary>
        public float Seconds { get; }

        /// <summary>Партию закончил `GameEndSystem`, а не предел времени.</summary>
        public bool Ended { get; }

        /// <summary>
        /// Партия кончилась, а месторождения на достижимом поле остались: боту не на что было
        /// их подключить или открыть. Пройденное до конца поле тупиком не считается, даже если
        /// пустые плитки остались закрытыми.
        /// </summary>
        public bool Deadlock => Ended && !Score.DepositsCleared;

        /// <summary>Сколько миллисекунд реального времени ушло на партию вместе с генерацией карты.</summary>
        public long Milliseconds { get; }

        public float OpenedShare => Score.FieldTiles > 0 ? Score.OpenedTiles / (float)Score.FieldTiles : 0f;

        public float ExhaustedShare =>
            Score.FieldDeposits > 0 ? Score.ExhaustedDeposits / (float)Score.FieldDeposits : 0f;

        /// <summary>Доля достижимого: счёт бота против аналитического потолка того же сида.</summary>
        public float CeilingShare => Ceiling > 0 ? Score.Total / (float)Ceiling : 0f;

        public const string CsvHeader =
            "seed;total;earned;ceiling;opened;fieldTiles;exhausted;fieldDeposits;lost;" +
            "contractsDone;contractsFailed;seconds;perfect;deadlock;ended;ms";

        /// <summary>Строка CSV с разделителем `;`: числа в инвариантной культуре.</summary>
        public string ToCsv() => string.Join(";",
            Seed.ToString(CultureInfo.InvariantCulture),
            Score.Total.ToString(CultureInfo.InvariantCulture),
            Score.Earned.ToString(CultureInfo.InvariantCulture),
            Ceiling.ToString(CultureInfo.InvariantCulture),
            Score.OpenedTiles.ToString(CultureInfo.InvariantCulture),
            Score.FieldTiles.ToString(CultureInfo.InvariantCulture),
            Score.ExhaustedDeposits.ToString(CultureInfo.InvariantCulture),
            Score.FieldDeposits.ToString(CultureInfo.InvariantCulture),
            Score.Lost.ToString(CultureInfo.InvariantCulture),
            ContractsCompleted.ToString(CultureInfo.InvariantCulture),
            ContractsFailed.ToString(CultureInfo.InvariantCulture),
            Seconds.ToString("0.00", CultureInfo.InvariantCulture),
            Score.IsPerfect ? "1" : "0",
            Deadlock ? "1" : "0",
            Ended ? "1" : "0",
            Milliseconds.ToString(CultureInfo.InvariantCulture));
    }
}
