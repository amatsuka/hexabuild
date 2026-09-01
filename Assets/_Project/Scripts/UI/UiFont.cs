using TMPro;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// Шрифты интерфейса. Встроенный `LegacyRuntime.ttf` не годится: он опирается на системный
    /// Arial, а в веб-сборке системных шрифтов нет — кириллица там просто не рисуется, и в
    /// редакторе этого не видно.
    ///
    /// HUD рисуется SDF-шрифтами: крупное число с обводкой обычному `Text` не задать вовсе, а
    /// SDF ещё и не мылится на зуме канваса. Жирный вес — отдельный ассет: подмена начертания
    /// растяжением контура из Regular даёт кашу на кириллице.
    /// </summary>
    public static class UiFont
    {
        static Font legacy;
        static TMP_FontAsset regular;
        static TMP_FontAsset bold;

        /// <summary>Обычный вес: подписи и второстепенные строки.</summary>
        public static TMP_FontAsset Regular =>
            regular != null ? regular : regular = Resources.Load<TMP_FontAsset>("Inter-Regular SDF");

        /// <summary>Жирный вес: числа и заголовки карточек.</summary>
        public static TMP_FontAsset Bold =>
            bold != null ? bold : bold = Resources.Load<TMP_FontAsset>("Inter-SemiBold SDF");

        /// <summary>Обычный `Font` для того, что ещё не переехало на TMP.</summary>
        public static Font Shared => legacy != null ? legacy : legacy = Resources.Load<Font>("Inter-Regular");
    }
}
