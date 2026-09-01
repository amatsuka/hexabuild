using System;
using UnityEngine;

namespace Game.Grid
{
    /// <summary>
    /// Плитка под лучом на рельефе. Луч по земле `y = 0` попадает не в ту плитку, по которой
    /// целился игрок: приподнятая крышка даёт параллакс «высота / tg(pitch)», и чем выше рельеф,
    /// тем сильнее промах. Коллайдеров на плитках нет и не заводится — вместо них луч спускается
    /// по рельефу сам: камера наклонена, значит вдоль луча высота падает, и первая плитка, чья
    /// крышка догнала луч, и есть ближайшая к камере, то есть видимая.
    /// </summary>
    public static class TilePicker
    {
        /// <summary>
        /// На сколько высот разбит спуск. Шаг по высоте — это и есть точность: на поле высотой
        /// 0.44 юнита двадцать четыре пробы дают 0.02, то есть двадцатую долю плитки.
        /// </summary>
        public const int Samples = 24;

        /// <param name="coordAtHeight">Плитка, в которую луч приходит на заданной высоте.</param>
        /// <param name="heightOf">Высота крышки плитки. Плитки вне поля высоты не имеют.</param>
        /// <param name="highest">Самая высокая крышка поля: отсюда луч и начинает спуск.</param>
        public static HexCoord Resolve(
            Func<float, HexCoord> coordAtHeight, Func<HexCoord, float?> heightOf, float highest)
        {
            for (var sample = 0; sample < Samples; sample++)
            {
                var height = Mathf.Lerp(highest, 0f, sample / (Samples - 1f));
                var coord = coordAtHeight(height);
                var surface = heightOf(coord);

                if (surface.HasValue && surface.Value >= height)
                    return coord;
            }

            // Спуск не встретил ни одной плитки поля: клик пришёлся за край, и там луч по земле —
            // единственный осмысленный ответ.
            return coordAtHeight(0f);
        }
    }
}
