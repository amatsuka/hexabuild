using System;
using Game.Audio;
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
        readonly TextMeshProUGUI caption;
        readonly Action clicked;
        readonly string clickSound;

        UiButton(UiPanelGraphic card, TextMeshProUGUI caption, Action clicked, string clickSound)
        {
            this.card = card;
            this.caption = caption;
            this.clicked = clicked;
            this.clickSound = clickSound;
        }

        public RectTransform Rect => card.rectTransform;

        /// <summary>
        /// Звук клика по умолчанию берётся из стиля: главная кнопка отзывается плотнее
        /// обычной. Клавиатуре сида и «Назад» он задаётся явно — цвет у них общий, а звучат
        /// они разным.
        /// </summary>
        public static UiButton Create(
            string name, Transform parent, UiTheme theme, in UiPanelStyle style,
            string caption, float fontSize, Action clicked, string clickSound = null)
        {
            var card = UiPanel.Create(name, parent, theme, style);

            var label = UiText.Bold(
                "Caption", card.rectTransform, theme, fontSize, theme.Text, TextAlignmentOptions.Center)
                .Stretch(16f, 16f, 8f, 8f);
            label.text = caption;

            clickSound ??= style.Equals(theme.ButtonPrimary) ? "sfx_ui_click_primary" : "sfx_ui_click";
            return new UiButton(card, label, clicked, clickSound);
        }

        /// <summary>Кнопка на экране или её нет вовсе: спрятанная не ловит ни нажатий, ни кликов.</summary>
        public bool Visible
        {
            get => card.gameObject.activeSelf;
            set => card.gameObject.SetActive(value);
        }

        /// <summary>Сменить цвет и подпись на ходу: одна и та же кнопка говорит разное.</summary>
        public void Restyle(UiTheme theme, in UiPanelStyle style, string text)
        {
            card.Apply(theme.PanelShader, style);
            caption.text = text;
        }

        /// <summary>
        /// Палец лёг на кнопку: она проседает и темнеет, пока его не снимут. Возвращает true —
        /// нажатие принадлежит ей, и дальше его разбирать не надо, ровно как у клика.
        /// </summary>
        public bool TryPress(Vector2 screenPosition)
        {
            if (!Visible || !Contains(screenPosition))
                return false;

            PressPulse.HoldCard(card);
            GameAudio.Play("sfx_ui_press", 0.3f);
            return true;
        }

        /// <summary>Отпустить. Ненажатая кнопка молчит, поэтому звать можно на все разом.</summary>
        public void Release() => PressPulse.Release(card);

        /// <summary>Клик попал в кнопку: она нажата, и дальше его разбирать не надо.</summary>
        public bool TryClick(Vector2 screenPosition)
        {
            if (!Visible || !Contains(screenPosition))
                return false;

            GameAudio.Play(clickSound, 0.6f);
            clicked();
            return true;
        }

        bool Contains(Vector2 screenPosition) =>
            RectTransformUtility.RectangleContainsScreenPoint(card.rectTransform, screenPosition);
    }
}
