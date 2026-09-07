namespace Game.Core
{
    /// <summary>
    /// Цены партии, снятые с `GameConfig`: правила остаются чистым C#. Отдельная структура,
    /// потому что четыре голых числа в конструкторе `GameState` уже не разобрать на месте вызова.
    /// </summary>
    public readonly struct PriceSettings
    {
        public PriceSettings(int tileOpen, float openGrowth, int road, int bridge)
        {
            TileOpen = tileOpen;
            // Рост ниже единицы делал бы каждую следующую плитку дешевле, ноль — бесплатной:
            // считаем единицей, то есть постоянной ценой.
            OpenGrowth = openGrowth < 1f ? 1f : openGrowth;
            Road = road;
            Bridge = bridge;
        }

        /// <summary>Цена первой открытой плитки. Дальше растёт по <see cref="OpenGrowth"/>.</summary>
        public int TileOpen { get; }

        /// <summary>
        /// Во сколько раз дорожает открытие с каждой открытой игроком плиткой:
        /// n-я стоит `TileOpen × OpenGrowth^n` вниз до целого.
        /// </summary>
        public float OpenGrowth { get; }

        public int Road { get; }

        /// <summary>Надбавка к дороге за мост: через реку и по воде.</summary>
        public int Bridge { get; }
    }
}
