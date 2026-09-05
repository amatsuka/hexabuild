using UnityEngine;

namespace Game.Grid
{
    /// <summary>
    /// Ширина русла по течению: у истока лента уже, к устью — шире. Раньше ширина была одна на
    /// всю реку, и это делало исток и устье одинаковыми — ещё один источник прямизны M22 берёт.
    /// </summary>
    public static class RiverWidth
    {
        /// <summary>Ширина воды у истока, в мировых единицах (гекс шириной 1).</summary>
        public const float SourceWidth = 0.13f;

        /// <summary>Ширина воды у устья.</summary>
        public const float MouthWidth = 0.26f;

        /// <summary>Поток, на котором ширина достигает `MouthWidth` и дальше не растёт.</summary>
        public const float FlowFull = 10f;

        /// <summary>Запас берега сверх ширины воды: он и делает канаву руслом, а не дырой.</summary>
        public const float BankMargin = 0.07f;

        /// <summary>
        /// Ширина воды на воротах с заданным потоком. `gateFlow` — не целое число шагов, а поток
        /// с полушагом: см. <see cref="GateFlow"/>.
        /// </summary>
        public static float Water(float gateFlow) =>
            Mathf.Lerp(SourceWidth, MouthWidth, Mathf.Clamp01(gateFlow / FlowFull));

        /// <summary>Ширина берега на тех же воротах — вода плюс запас.</summary>
        public static float Bank(float gateFlow) => Water(gateFlow) + BankMargin;

        /// <summary>
        /// Поток на воротах грани, а не на самой плитке: вдоль русла соседние плитки отличаются
        /// потоком ровно на единицу, и полушаг в каждую сторону делает число на шве одинаковым
        /// с обеих сторон. Расходится оно может только на последних воротах притока перед
        /// слиянием — там ступенька уместна, приток уже главного русла.
        /// </summary>
        public static float GateFlow(TileData tile, int direction) =>
            (tile.RiverDownMask & (1 << direction)) != 0 ? tile.RiverFlow + 0.5f : tile.RiverFlow - 0.5f;
    }
}
