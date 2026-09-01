using System;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// Палитра и метрика интерфейса. Живёт на компонентах как `[SerializeField]`, не в
    /// `GameConfig`, — так же, как `ResourcePalette` и `BiomePalette`.
    ///
    /// Цвета подобраны против финального кадра, то есть после тонмаппинга и bloom. Меняешь
    /// постобработку — пересматриваешь и это.
    /// </summary>
    [Serializable]
    public sealed class UiTheme
    {
        [Tooltip("Шейдер Game/UiPanel. Ссылка нужна здесь, иначе шейдер не попадёт в сборку: " +
                 "по Shader.Find он находится только в редакторе")]
        [SerializeField] Shader panelShader;

        [Header("Карточка")]
        [SerializeField] Color fillTop = new(0.30f, 0.56f, 0.74f, 0.26f);
        [SerializeField] Color fillBottom = new(0.05f, 0.17f, 0.30f, 0.34f);
        [SerializeField] Color edge = new(0.76f, 0.92f, 1f, 1f);
        [SerializeField] Color glow = new(0.01f, 0.03f, 0.07f, 0.55f);
        [SerializeField] float radius = 26f;
        [SerializeField] float edgeWidth = 3.5f;
        [SerializeField] float glowSize = 34f;
        [Tooltip("Насколько свечение снесено вниз: это же и тень карточки")]
        [SerializeField] float glowOffset = 8f;
        [Tooltip("Насколько кромка светлеет к верху карточки")]
        [SerializeField, Range(0f, 2f)] float highlight = 0.85f;
        [Tooltip("Насколько блик сползает от верхней кромки к бокам")]
        [SerializeField, Range(0.05f, 1f)] float highlightSpread = 0.65f;
        [Tooltip("Глубина фаски от кромки внутрь, пиксели: по ней читается толщина стекла")]
        [SerializeField] float sheen = 20f;
        [Tooltip("Нижняя фаска долей от верхней: свет, прошедший сквозь толщу. Без неё профиль " +
                 "яркости монотонный, и панель читается наклонной плоскостью, а не объёмом")]
        [SerializeField, Range(0f, 1f)] float backLight = 0.50f;
        [Tooltip("Отражение источника в поверхности: широкое пятно со стороны света")]
        [SerializeField, Range(0f, 1f)] float spec = 0.30f;
        [Tooltip("Откуда светит. Один на весь интерфейс, иначе панели освещены вразнобой")]
        [SerializeField] Vector2 lightDirection = new(-0.45f, 0.89f);
        [Tooltip("Насколько стекло гасит фон под собой. Это и держит контраст текста и иконок " +
                 "одинаковым над тёмной картой и над яркой водой")]
        [SerializeField, Range(0f, 1f)] float darken = 0.62f;

        [Header("Клетка склада")]
        [SerializeField] Color slotEmptyTop = new(0.20f, 0.40f, 0.56f, 0.22f);
        [SerializeField] Color slotEmptyBottom = new(0.03f, 0.12f, 0.22f, 0.30f);
        [SerializeField] Color slotFilledTop = new(0.44f, 0.72f, 0.92f, 0.34f);
        [SerializeField] Color slotFilledBottom = new(0.10f, 0.32f, 0.52f, 0.42f);
        [SerializeField] Color slotEdge = new(0.72f, 0.90f, 1f, 0.55f);
        [SerializeField] float slotRadius = 18f;

        [Header("Полоса прогресса")]
        [SerializeField] Color barTrack = new(0.02f, 0.10f, 0.20f, 0.78f);
        [SerializeField] Color barFillTop = new(1f, 0.90f, 0.48f, 1f);
        [SerializeField] Color barFillBottom = new(0.98f, 0.68f, 0.16f, 1f);

        [Header("Текст")]
        [SerializeField] Color text = new(0.97f, 0.99f, 1f);
        [Tooltip("Подписи и второстепенные строки")]
        [SerializeField] Color muted = new(0.80f, 0.91f, 0.97f);
        [SerializeField] Color gold = new(1f, 0.84f, 0.32f);
        [SerializeField] Color good = new(0.52f, 0.96f, 0.56f);
        [SerializeField] Color bad = new(1f, 0.52f, 0.46f);
        [Tooltip("Обводка крупных чисел: она отделяет светлый текст от карты, которая " +
                 "просвечивает сквозь стекло, и держит его читаемым над светлой водой")]
        [SerializeField] Color textOutline = new(0.02f, 0.06f, 0.12f, 0.95f);
        [SerializeField, Range(0f, 0.5f)] float outlineWidth = 0.16f;
        [Tooltip("Утолщение штриха. В поставке редактора есть только Regular и SemiBold, " +
                 "жирнее веса нет — вес добирается раздутием контура SDF, а не подменой начертания")]
        [SerializeField, Range(0f, 0.4f)] float faceWeight = 0.16f;
        [Tooltip("Тень под текстом: она добавляет буквам объём там, где одной обводки мало")]
        [SerializeField] Color textShadow = new(0f, 0f, 0f, 0.65f);
        [SerializeField, Range(0f, 1f)] float textShadowOffset = 0.18f;
        [SerializeField, Range(0f, 1f)] float textShadowSoftness = 0.12f;

        public Shader PanelShader => panelShader;

        /// <summary>Карточка HUD: градиент, светлая кромка с бликом сверху, свечение вниз.</summary>
        public UiPanelStyle Card => new(
            fillTop, fillBottom, edge, glow, radius, edgeWidth, glowSize, glowOffset, highlight, highlightSpread,
            sheen, darken, backLight, spec, lightDirection);

        /// <summary>Пустая клетка склада: та же карточка мельче и без свечения.</summary>
        public UiPanelStyle SlotEmpty => new(
            slotEmptyTop, slotEmptyBottom, slotEdge, Color.clear, slotRadius, 2f, 0f, 0f, highlight * 0.6f,
            highlightSpread, sheen * 0.5f, darken * 0.55f, backLight, spec * 0.6f, lightDirection);

        /// <summary>Занятая клетка: светлее пустой, иконка лежит на ней.</summary>
        public UiPanelStyle SlotFilled => new(
            slotFilledTop, slotFilledBottom, slotEdge, Color.clear, slotRadius, 2f, 0f, 0f, highlight * 0.6f,
            highlightSpread, sheen * 0.5f, darken * 0.75f, backLight, spec * 0.6f, lightDirection);

        /// <summary>
        /// Островок под ресурс в полосе HUD. Тот же слот, что на складе: это одна и та же
        /// сущность — рамка вокруг иконки с числом, — и разводить ей два стиля незачем.
        /// </summary>
        public UiPanelStyle Island => SlotEmpty;

        /// <summary>Жёлоб полосы прогресса: тёмный, без кромки и свечения.</summary>
        public UiPanelStyle BarTrack => new(
            barTrack, barTrack, Color.clear, Color.clear, 15f, 0f, 0f, 0f, 0f, highlightSpread, 0f, 0.85f, 0f, 0f, lightDirection);

        /// <summary>Заливка полосы прогресса: золото градиентом.</summary>
        public UiPanelStyle BarFill => new(
            barFillTop, barFillBottom, Color.clear, Color.clear, 12f, 0f, 0f, 0f, 0f, highlightSpread, 0f, 0f, 0f, 0f, lightDirection);

        public Color Text => text;

        public Color Muted => muted;

        public Color Gold => gold;

        public Color Good => good;

        public Color Bad => bad;

        public Color TextOutline => textOutline;

        public float OutlineWidth => outlineWidth;

        public float FaceWeight => faceWeight;

        public Color TextShadow => textShadow;

        public float TextShadowOffset => textShadowOffset;

        public float TextShadowSoftness => textShadowSoftness;
    }
}
