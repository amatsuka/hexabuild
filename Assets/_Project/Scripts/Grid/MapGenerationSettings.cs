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
            float biomeNoiseScale = 0.18f,
            float reserveScale = 1f,
            float waterCeiling = MapGenerator.WaterCeiling,
            float rocksCeiling = MapGenerator.RocksCeiling,
            bool handMade = false)
        {
            HandMade = handMade;
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
            ReserveScale = reserveScale <= 0f ? 1f : reserveScale;
            // Порог воды выше порога скал вывернул бы кривую наизнанку: вода оказалась бы над
            // горами. Негодную пару считаем канонической, а не чиним по одному числу.
            var proper = waterCeiling >= 0f && rocksCeiling <= 1f && waterCeiling < rocksCeiling;
            WaterCeiling = proper ? waterCeiling : MapGenerator.WaterCeiling;
            RocksCeiling = proper ? rocksCeiling : MapGenerator.RocksCeiling;
        }

        public int Rows { get; }

        /// <summary>
        /// Карта собрана руками, а не шумом: у первого уровня она рукотворная, и весь набор
        /// чисел генерации ниже к ней не применяется — см. <see cref="TutorialMap"/>.
        /// </summary>
        public bool HandMade { get; }

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

        /// <summary>
        /// Во сколько раз уровень кампании растягивает базовый диапазон запаса поверх роста
        /// по рядам. Единица — как в `GameConfig`, двойка — вдвое более щедрая карта.
        /// </summary>
        public float ReserveScale { get; }

        /// <summary>
        /// Доля сырого шума, ниже которой плитка уходит под воду. Ноль — воды на карте нет.
        /// Порог считается по шуму до растяжки: и биом, и высота дальше живут на канонических
        /// отметках `MapGenerator`, а карта растягивается под уровень (см. `MapGenerator.Stretch`).
        /// </summary>
        public float WaterCeiling { get; }

        /// <summary>
        /// Доля сырого шума, выше которой начинаются непроходимые горы. Единица — гор нет.
        /// </summary>
        public float RocksCeiling { get; }
    }
}
