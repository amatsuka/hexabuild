using System;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// Палитра и метрика интерфейса: цвета панелей, текста и акцентов, радиус скругления,
    /// толщина обводки. Живёт на компонентах как `[SerializeField]`, не в `GameConfig`, — так же,
    /// как `ResourcePalette` и `BiomePalette`.
    ///
    /// Цвета подобраны против финального кадра, то есть после тонмаппинга и bloom: панель тёмная
    /// и холодная, кромка светлая и холодная, текст почти белый. Меняешь постобработку —
    /// пересматриваешь и это.
    /// </summary>
    [Serializable]
    public sealed class UiTheme
    {
        [Header("Панель")]
        [SerializeField] Color panelFill = new(0.08f, 0.20f, 0.32f, 0.88f);
        [SerializeField] Color panelEdge = new(0.50f, 0.79f, 0.94f, 0.95f);
        [SerializeField] Color panelShadow = new(0.02f, 0.05f, 0.10f, 0.45f);
        [Tooltip("Насколько тень уходит вниз из-под панели, пиксели канваса")]
        [SerializeField] float shadowDrop = 7f;
        [SerializeField] int cornerRadius = 26;
        [SerializeField] int edgeWidth = 3;

        [Header("Клетка склада")]
        [SerializeField] Color slotEmpty = new(0.05f, 0.13f, 0.22f, 0.85f);
        [SerializeField] Color slotFilled = new(0.14f, 0.31f, 0.45f, 0.95f);
        [SerializeField] Color slotEdge = new(0.30f, 0.52f, 0.67f, 0.38f);
        [SerializeField] int slotRadius = 18;

        [Header("Текст")]
        [SerializeField] Color text = new(0.91f, 0.97f, 1f);
        [Tooltip("Подписи и второстепенные строки")]
        [SerializeField] Color muted = new(0.68f, 0.83f, 0.91f);
        [SerializeField] Color gold = new(1f, 0.82f, 0.31f);
        [SerializeField] Color good = new(0.44f, 0.89f, 0.50f);
        [SerializeField] Color bad = new(1f, 0.46f, 0.42f);
        [Tooltip("Обводка крупных чисел: она и делает их читаемыми поверх пёстрой карты")]
        [SerializeField] Color textOutline = new(0.02f, 0.07f, 0.13f, 1f);
        [SerializeField, Range(0f, 0.5f)] float outlineWidth = 0.2f;

        public Color PanelFill => panelFill;
        public Color PanelEdge => panelEdge;
        public Color PanelShadow => panelShadow;
        public float ShadowDrop => shadowDrop;
        public int CornerRadius => cornerRadius;
        public int EdgeWidth => edgeWidth;
        public Color SlotEmpty => slotEmpty;
        public Color SlotFilled => slotFilled;
        public Color SlotEdge => slotEdge;
        public int SlotRadius => slotRadius;
        public Color Text => text;
        public Color Muted => muted;
        public Color Gold => gold;
        public Color Good => good;
        public Color Bad => bad;
        public Color TextOutline => textOutline;
        public float OutlineWidth => outlineWidth;
    }
}
