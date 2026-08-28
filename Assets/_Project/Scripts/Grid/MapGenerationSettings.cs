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

        /// <summary>Частота шума ландшафта: меньше — крупнее пятна биомов.</summary>
        public float BiomeNoiseScale { get; }
    }
}
