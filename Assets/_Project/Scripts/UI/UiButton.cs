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
        readonly UiPanelGraphic card;
        readonly Action clicked;

        UiButton(UiPanelGraphic card, Action clicked)
        {
            this.card = card;
            this.clicked = clicked;
        }

        public RectTransform Rect => card.rectTransform;

        public static UiButton Create(
            string name, Transform parent, UiTheme theme, in UiPanelStyle style,
            string caption, float fontSize, Action clicked)
        {
            var card = UiPanel.Create(name, parent, theme, style);

            UiText.Bold("Caption", card.rectTransform, theme, fontSize, theme.Text, TextAlignmentOptions.Center)
                .Stretch(16f, 16f, 8f, 8f)
                .text = caption;

            return new UiButton(card, clicked);
        }

        /// <summary>
        /// Палец лёг на кнопку: она проседает и темнеет, пока его не снимут. Возвращает true —
        /// нажатие принадлежит ей, и дальше его разбирать не надо, ровно как у клика.
        /// </summary>
        public bool TryPress(Vector2 screenPosition)
        {
            if (!Contains(screenPosition))
                return false;

            PressPulse.HoldCard(card);
            return true;
        }

        /// <summary>Отпустить. Ненажатая кнопка молчит, поэтому звать можно на все разом.</summary>
        public void Release() => PressPulse.Release(card);

        /// <summary>Клик попал в кнопку: она нажата, и дальше его разбирать не надо.</summary>
        public bool TryClick(Vector2 screenPosition)
        {
            if (!Contains(screenPosition))
                return false;

            clicked();
            return true;
        }

        bool Contains(Vector2 screenPosition) =>
            RectTransformUtility.RectangleContainsScreenPoint(card.rectTransform, screenPosition);
    }
}
