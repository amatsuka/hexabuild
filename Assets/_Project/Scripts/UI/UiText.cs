using TMPro;
using UnityEngine;

namespace Game.UI
{
    /// <summary>Строка HUD на TMP: шрифт, размер, цвет и, где нужно, обводка контура.</summary>
    public static class UiText
    {
        /// <summary>Подпись обычным весом.</summary>
        public static TextMeshProUGUI Label(
            string name, Transform parent, float size, Color color, TextAlignmentOptions alignment) =>
            Create(name, parent, UiFont.Regular, size, color, alignment);

        /// <summary>Число или заголовок жирным весом.</summary>
        public static TextMeshProUGUI Bold(
            string name, Transform parent, float size, Color color, TextAlignmentOptions alignment) =>
            Create(name, parent, UiFont.Bold, size, color, alignment);

        /// <summary>
        /// Обводка контура. Ставится через материал-инстанс: `fontMaterial` его и заводит, а
        /// править общий материал шрифта нельзя — обводка уехала бы на весь текст в игре.
        /// </summary>
        public static TextMeshProUGUI Outlined(this TextMeshProUGUI text, UiTheme theme)
        {
            var material = text.fontMaterial;
            material.EnableKeyword(ShaderUtilities.Keyword_Outline);
            material.SetColor(ShaderUtilities.ID_OutlineColor, theme.TextOutline);
            material.SetFloat(ShaderUtilities.ID_OutlineWidth, theme.OutlineWidth);
            return text;
        }

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

        static TextMeshProUGUI Create(
            string name, Transform parent, TMP_FontAsset font, float size, Color color, TextAlignmentOptions alignment)
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
            return text;
        }
    }
}
