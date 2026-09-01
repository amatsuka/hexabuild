using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// Сборка карточек HUD. Карточка — один рендерер `UiPanelGraphic` на шейдере `Game/UiPanel`;
    /// содержимое кладут детьми прямо в неё, они идут по иерархии следом и рисуются поверх.
    /// </summary>
    public static class UiPanel
    {
        /// <summary>Карточка HUD без содержимого.</summary>
        public static UiPanelGraphic Create(string name, Transform parent, UiTheme theme) =>
            Create(name, parent, theme, theme.Card);

        /// <summary>Карточка своим стилем: клетка склада, жёлоб полосы, её заливка.</summary>
        public static UiPanelGraphic Create(string name, Transform parent, UiTheme theme, in UiPanelStyle style)
        {
            // `CanvasRenderer` перечислен явно: конструктор `GameObject(имя, типы)` не разбирает
            // `RequireComponent`, и без него графика молча не рисуется.
            var created = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(UiPanelGraphic));
            created.transform.SetParent(parent, false);

            var panel = created.GetComponent<UiPanelGraphic>();
            panel.raycastTarget = false;
            panel.Apply(theme.PanelShader, style);
            return panel;
        }

        /// <summary>Растянуть прямоугольник по всему родителю: сетка клеток, слой поверх карточки.</summary>
        public static RectTransform Stretch(this RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        public static RectTransform NewRect(string name, Transform parent)
        {
            var created = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)created.transform;
            rect.SetParent(parent, false);
            return rect;
        }
    }
}
