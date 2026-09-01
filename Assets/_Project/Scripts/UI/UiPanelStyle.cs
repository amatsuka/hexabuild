using System;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// Как выглядит карточка: цвета градиента, кромки и свечения плюс их размеры в пикселях.
    /// Значение, а не ссылка, — по нему кэшируются материалы: стилей в HUD пять-шесть, а карточек
    /// с ними три десятка, и материал на каждую был бы расточительством.
    /// </summary>
    [Serializable]
    public readonly struct UiPanelStyle : IEquatable<UiPanelStyle>
    {
        public UiPanelStyle(
            Color fillTop, Color fillBottom, Color edge, Color glow,
            float radius, float edgeWidth, float glowSize, float glowOffset,
            float highlight, float highlightSpread, float sheen, float darken,
            float backLight, float spec, Vector2 lightDirection)
        {
            FillTop = fillTop;
            FillBottom = fillBottom;
            Edge = edge;
            Glow = glow;
            Radius = radius;
            EdgeWidth = edgeWidth;
            GlowSize = glowSize;
            GlowOffset = glowOffset;
            Highlight = highlight;
            HighlightSpread = highlightSpread;
            Sheen = sheen;
            Darken = darken;
            BackLight = backLight;
            Spec = spec;
            LightDirection = lightDirection;
        }

        public Color FillTop { get; }

        public Color FillBottom { get; }

        public Color Edge { get; }

        public Color Glow { get; }

        public float Radius { get; }

        public float EdgeWidth { get; }

        public float GlowSize { get; }

        public float GlowOffset { get; }

        /// <summary>Насколько кромка светлеет к верху карточки.</summary>
        public float Highlight { get; }

        /// <summary>Насколько блик сползает от верхней кромки к бокам.</summary>
        public float HighlightSpread { get; }

        /// <summary>Глубина фаски от кромки внутрь, пиксели: по ней и читается толщина стекла.</summary>
        public float Sheen { get; }

        /// <summary>
        /// Нижняя фаска долей от верхней. Это не прямой свет, а прошедший сквозь толщу, и без
        /// него профиль яркости остаётся монотонным — панель читается наклонной плоскостью.
        /// </summary>
        public float BackLight { get; }

        /// <summary>Отражение источника в поверхности: широкое пятно со стороны света.</summary>
        public float Spec { get; }

        /// <summary>Откуда светит. Один на весь интерфейс: иначе панели освещены вразнобой.</summary>
        public Vector2 LightDirection { get; }

        /// <summary>
        /// Насколько стекло гасит фон под собой. Ноль — панель просто подмешивается к тому, что
        /// позади, и контраст текста зависит от карты; единица — под панелью почти чёрное.
        /// </summary>
        public float Darken { get; }

        /// <summary>
        /// Запас меша за краем карточки. Свечение живёт снаружи фигуры, и меш ровно по карточке
        /// обрезал бы его по кромке.
        /// </summary>
        public float Padding => GlowSize + Mathf.Max(GlowOffset, 0f) + 1f;

        public bool Equals(UiPanelStyle other) =>
            FillTop == other.FillTop && FillBottom == other.FillBottom && Edge == other.Edge &&
            Glow == other.Glow && Radius.Equals(other.Radius) && EdgeWidth.Equals(other.EdgeWidth) &&
            GlowSize.Equals(other.GlowSize) && GlowOffset.Equals(other.GlowOffset) &&
            Highlight.Equals(other.Highlight) && HighlightSpread.Equals(other.HighlightSpread) &&
            Sheen.Equals(other.Sheen) && Darken.Equals(other.Darken) && BackLight.Equals(other.BackLight) &&
            Spec.Equals(other.Spec) && LightDirection == other.LightDirection;

        public override bool Equals(object other) => other is UiPanelStyle style && Equals(style);

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(FillTop);
            hash.Add(FillBottom);
            hash.Add(Edge);
            hash.Add(Glow);
            hash.Add(Radius);
            hash.Add(EdgeWidth);
            hash.Add(GlowSize);
            hash.Add(GlowOffset);
            hash.Add(Highlight);
            hash.Add(HighlightSpread);
            hash.Add(Sheen);
            hash.Add(Darken);
            hash.Add(BackLight);
            hash.Add(Spec);
            hash.Add(LightDirection);
            return hash.ToHashCode();
        }
    }
}
