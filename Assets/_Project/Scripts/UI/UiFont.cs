using TMPro;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// Шрифты интерфейса. Встроенный `LegacyRuntime.ttf` не годится: он опирается на системный
    /// Arial, а в веб-сборке системных шрифтов нет — кириллица там просто не рисуется, и в
    /// редакторе этого не видно.
    ///
    /// Интерфейс рисуется SDF-шрифтами целиком — с M19 и финальный экран тоже: крупное число
    /// с обводкой обычному `Text` не задать вовсе, а
    /// SDF ещё и не мылится на зуме канваса. Жирный вес — отдельный ассет: подмена начертания
    /// растяжением контура из Regular даёт кашу на кириллице.
    /// </summary>
    public static class UiFont
    {
        static TMP_FontAsset regular;
        static TMP_FontAsset bold;

        /// <summary>Обычный вес: подписи и второстепенные строки.</summary>
        public static TMP_FontAsset Regular =>
            regular != null ? regular : regular = Resources.Load<TMP_FontAsset>("Inter-Regular SDF");

        /// <summary>Жирный вес: числа и заголовки карточек.</summary>
        public static TMP_FontAsset Bold =>
            bold != null ? bold : bold = Resources.Load<TMP_FontAsset>("Inter-SemiBold SDF");
    }
}
