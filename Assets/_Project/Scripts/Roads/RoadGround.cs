namespace Game.Roads
{
    /// <summary>
    /// Чем дорога переходит реку на этой плитке. Мост появляется там, где русло режет плитку
    /// с дорогой: до шага 4 стадии M20 река переходилась насыпью, и это было видно.
    /// </summary>
    public enum BridgeKind
    {
        /// <summary>Реки нет — дорога идёт по земле насыпью.</summary>
        None,

        /// <summary>Деревянный настил на сваях: вариант по умолчанию.</summary>
        Timber,

        /// <summary>Каменная арка: достаётся скальной плитке, там для неё есть камень.</summary>
        Stone
    }

    /// <summary>
    /// Что дорога застаёт на плитке: мировая высота крышки и мост, если плитку режет река.
    ///
    /// Высота берётся с уже поставленной плитки, а не считается заново: весь рельеф умножается
    /// на масштаб высоты, и насыпь обязана лечь на то, что видно.
    /// </summary>
    public readonly struct RoadGround
    {
        public RoadGround(float top, BridgeKind bridge = BridgeKind.None)
        {
            Top = top;
            Bridge = bridge;
        }

        /// <summary>Мировая высота крышки плитки.</summary>
        public float Top { get; }

        public BridgeKind Bridge { get; }
    }
}
