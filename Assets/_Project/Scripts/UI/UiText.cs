using TMPro;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// Строка HUD на TMP. Обводку и тень получает **любая** строка, а не только крупные числа:
    /// панель прозрачная, сквозь неё видно карту, и подпись без контура на ней расплывается.
    /// Обычному `Text` ни того, ни другого не задать — это и есть причина, по которой HUD на SDF.
    /// </summary>
    public static class UiText
    {
        /// <summary>
        /// У обычного веса обводка тоньше: подписи мельче, и контур в полную ширину заливает
        /// тонкие штрихи кириллицы до нечитаемого пятна.
        /// </summary>
        const float LabelOutlineShare = 0.55f;

        /// <summary>Подпись тяжелеет меньше значения: иначе весовой контраст между ними пропадает.</summary>
        const float LabelWeightShare = 0.5f;

        /// <summary>Подпись обычным весом.</summary>
        public static TextMeshProUGUI Label(
            string name, Transform parent, UiTheme theme, float size, Color color, TextAlignmentOptions alignment) =>
            Create(name, parent, UiFont.Regular, theme, size, color, alignment,
                theme.OutlineWidth * LabelOutlineShare, theme.FaceWeight * LabelWeightShare);

        /// <summary>Число или заголовок жирным весом.</summary>
        public static TextMeshProUGUI Bold(
            string name, Transform parent, UiTheme theme, float size, Color color, TextAlignmentOptions alignment) =>
            Create(name, parent, UiFont.Bold, theme, size, color, alignment, theme.OutlineWidth,
                theme.FaceWeight);

        /// <summary>Растянуть строку по всей карточке с отступом: так текст не считают в пикселях.</summary>
        public static TextMeshProUGUI Stretch(this TextMeshProUGUI text, float left, float right, float top, float bottom)
        {
            var rect = text.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
            return text;
        }

        /// <summary>
        /// Строка в одну линию: перенос выключен, а кегль ужимается, пока число не влезет
        /// в свою ширину. Счёт за партию вырастает на порядок, и разделитель тысяч из
        /// <see cref="HudFormat.Points"/> давал точку переноса: с пятизначного счёта «25 195»
        /// ломалось на две строки и вываливалось из карточки вверх и вниз.
        /// </summary>
        public static TextMeshProUGUI Fit(this TextMeshProUGUI text, float minSize)
        {
            // Кегль читается до включения автоподбора: с ним `fontSize` уже подобранный,
            // и потолок съехал бы на первое же длинное число.
            var size = text.fontSize;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.fontSizeMax = size;
            text.fontSizeMin = minSize;
            text.enableAutoSizing = true;
            return text;
        }

        static TextMeshProUGUI Create(
            string name, Transform parent, TMP_FontAsset font, UiTheme theme,
            float size, Color color, TextAlignmentOptions alignment, float outlineWidth, float faceWeight)
        {
            var created = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            created.transform.SetParent(parent, false);

            var text = created.GetComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.richText = false;

            // Обводка и тень ставятся на материал-инстанс: `fontMaterial` его и заводит, а править
            // общий материал шрифта нельзя — контур уехал бы на весь текст в игре.
            var material = text.fontMaterial;

            // Жирнее SemiBold в поставке редактора начертаний нет, поэтому вес добирается
            // раздутием контура SDF. Подменять начертание нечем, а растягивать Regular — каша.
            material.SetFloat(ShaderUtilities.ID_FaceDilate, faceWeight);

            material.EnableKeyword(ShaderUtilities.Keyword_Outline);
            material.SetColor(ShaderUtilities.ID_OutlineColor, theme.TextOutline);
            material.SetFloat(ShaderUtilities.ID_OutlineWidth, outlineWidth);

            material.EnableKeyword(ShaderUtilities.Keyword_Underlay);
            material.SetColor(ShaderUtilities.ID_UnderlayColor, theme.TextShadow);
            material.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0f);
            material.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -theme.TextShadowOffset);
            material.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.1f);
            material.SetFloat(ShaderUtilities.ID_UnderlaySoftness, theme.TextShadowSoftness);
            return text;
        }
    }
}
