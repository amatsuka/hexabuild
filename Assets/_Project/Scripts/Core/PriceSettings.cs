namespace Game.Core
{
    /// <summary>
    /// Цены партии, снятые с `GameConfig`: правила остаются чистым C#. Отдельная структура,
    /// потому что пяти голых `int` в конструкторе `GameState` уже не разобрать на месте вызова.
    /// </summary>
    public readonly struct PriceSettings
    {
        public PriceSettings(int tileOpen, int openStep, int openGroup, int road, int bridge)
        {
            TileOpen = tileOpen;
            OpenStep = openStep;
            // Ноль в инспекторе означал бы деление на ноль в цене открытия: считаем его единицей.
            OpenGroup = openGroup < 1 ? 1 : openGroup;
            Road = road;
            Bridge = bridge;
        }

        /// <summary>Цена первой открытой плитки. Дальше растёт по `OpenStep`.</summary>
        public int TileOpen { get; }

        /// <summary>Надбавка к цене открытия за каждую группу уже открытых плиток.</summary>
        public int OpenStep { get; }

        /// <summary>Сколько плиток нужно открыть, чтобы цена подросла на шаг.</summary>
        public int OpenGroup { get; }

        public int Road { get; }

        /// <summary>Надбавка к дороге за мост: через реку и по воде.</summary>
        public int Bridge { get; }
    }
}
