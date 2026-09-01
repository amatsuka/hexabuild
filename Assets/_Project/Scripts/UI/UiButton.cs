using System;
using TMPro;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// Кнопка финального экрана: та же карточка `UiPanel`, что и весь интерфейс, плюс подпись
    /// по центру. Нажатие ловится попаданием в прямоугольник, а не `EventSystem`ом: в сцене его
    /// нет, ввод в проекте читает один `GameInput`, и заводить вторую систему ввода ради двух
    /// кнопок незачем. Клетки склада проверяются ровно так же.
    /// </summary>
    public sealed class UiButton
    {
        readonly RectTransform rect;
        readonly Action clicked;

        UiButton(RectTransform rect, Action clicked)
        {
            this.rect = rect;
            this.clicked = clicked;
        }

        public RectTransform Rect => rect;

        public static UiButton Create(
            string name, Transform parent, UiTheme theme, in UiPanelStyle style,
            string caption, float fontSize, Action clicked)
        {
            var card = UiPanel.Create(name, parent, theme, style).rectTransform;

            UiText.Bold("Caption", card, theme, fontSize, theme.Text, TextAlignmentOptions.Center)
                .Stretch(16f, 16f, 8f, 8f)
                .text = caption;

            return new UiButton(card, clicked);
        }

        /// <summary>Клик попал в кнопку: она нажата, и дальше его разбирать не надо.</summary>
        public bool TryClick(Vector2 screenPosition)
        {
            if (!RectTransformUtility.RectangleContainsScreenPoint(rect, screenPosition))
                return false;

            clicked();
            return true;
        }
    }
}
