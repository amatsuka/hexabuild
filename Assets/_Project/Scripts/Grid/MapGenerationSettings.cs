namespace Game.Grid
{
    /// <summary>Числа генерации карты, снятые с `GameConfig`: правила остаются чистым C#.</summary>
    public readonly struct MapGenerationSettings
    {
        public MapGenerationSettings(
            int rows,
            int seed,
            float emptyWeight,
            float singleDepositWeight,
            float twoDepositsWeight,
            float threeDepositsWeight,
            int minReserve,
            int maxReserve,
            float reserveRowGrowth = 0f,
            float biomeNoiseScale = 0.18f)
        {
            Rows = rows;
            Seed = seed;
            EmptyWeight = emptyWeight;
            SingleDepositWeight = singleDepositWeight;
            TwoDepositsWeight = twoDepositsWeight;
            ThreeDepositsWeight = threeDepositsWeight;
            MinReserve = minReserve;
            MaxReserve = maxReserve;
            ReserveRowGrowth = reserveRowGrowth;
            BiomeNoiseScale = biomeNoiseScale;
        }

        public int Rows { get; }

        /// <summary>0 — случайная партия, иначе воспроизводимая.</summary>
        public int Seed { get; }

        public float EmptyWeight { get; }

        public float SingleDepositWeight { get; }

        public float TwoDepositsWeight { get; }

        public float ThreeDepositsWeight { get; }

        public int MinReserve { get; }

        public int MaxReserve { get; }

        /// <summary>
        /// На сколько растёт диапазон запаса за каждый ряд от Метрополии: границы умножаются на
        /// `1 + ReserveRowGrowth × ряд`. Дальние плитки живут дольше, а не быстрее — интервал
        /// добычи один на всех.
        /// </summary>
        public float ReserveRowGrowth { get; }

        /// <summary>Частота шума ландшафта: меньше — крупнее пятна биомов.</summary>
        public float BiomeNoiseScale { get; }
    }
}
