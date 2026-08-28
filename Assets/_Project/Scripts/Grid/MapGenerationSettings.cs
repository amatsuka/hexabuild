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
            int maxReserve)
        {
            Rows = rows;
            Seed = seed;
            EmptyWeight = emptyWeight;
            SingleDepositWeight = singleDepositWeight;
            TwoDepositsWeight = twoDepositsWeight;
            ThreeDepositsWeight = threeDepositsWeight;
            MinReserve = minReserve;
            MaxReserve = maxReserve;
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
    }
}
