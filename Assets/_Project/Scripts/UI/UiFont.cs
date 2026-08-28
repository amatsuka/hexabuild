using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// Шрифт интерфейса. Встроенный `LegacyRuntime.ttf` не годится: он опирается на системный
    /// Arial, а в веб-сборке системных шрифтов нет — кириллица там просто не рисуется, и в
    /// редакторе этого не видно.
    /// </summary>
    public static class UiFont
    {
        static Font shared;

        public static Font Shared => shared != null ? shared : shared = Resources.Load<Font>("Inter-Regular");
    }
}
