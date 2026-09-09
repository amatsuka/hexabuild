namespace Game.Core
{
    /// <summary>
    /// Цены партии, снятые с `GameConfig`: правила остаются чистым C#. Отдельная структура,
    /// потому что голые числа в конструкторе `GameState` уже не разобрать на месте вызова.
    /// </summary>
    public readonly struct PriceSettings
    {
        public PriceSettings(int tileOpen, float openGrowth, int road, int bridgeGravel, int bridgeBoards)
        {
            TileOpen = tileOpen;
            // Рост ниже единицы делал бы каждую следующую плитку дешевле, ноль — бесплатной:
            // считаем единицей, то есть постоянной ценой.
            OpenGrowth = openGrowth < 1f ? 1f : openGrowth;
            Road = new RoadCost(road, 0);
            Bridge = new RoadCost(bridgeGravel, bridgeBoards);
        }

        /// <summary>Цена первой открытой плитки. Дальше растёт по <see cref="OpenGrowth"/>.</summary>
        public int TileOpen { get; }

        /// <summary>
        /// Во сколько раз дорожает открытие с каждой открытой игроком плиткой:
        /// n-я стоит `TileOpen × OpenGrowth^n` вниз до целого.
        /// </summary>
        public float OpenGrowth { get; }

        /// <summary>Обычная дорога: только щебень.</summary>
        public RoadCost Road { get; }

        /// <summary>
        /// Дорога на плитке с рекой. Полная цена, а не надбавка: с M33 у крафта появилось
        /// назначение помимо обмена на очки, и мост — единственное, что просит доски.
        /// </summary>
        public RoadCost Bridge { get; }

        public RoadCost For(bool hasRiver) => hasRiver ? Bridge : Road;
    }
}
