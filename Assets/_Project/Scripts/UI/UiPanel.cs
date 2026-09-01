using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Карточка HUD: тень, светлая кромка и тёмная заливка тремя слоями поверх друг друга.
    /// Одним слоем это не собрать — `Image` красит спрайт одним цветом, а кромка и заливка
    /// в референсе разного цвета, и тень должна лежать ещё и со сдвигом.
    ///
    /// Содержимое карточки кладут детьми прямо в корень: они идут в порядке иерархии после
    /// заливки, то есть рисуются поверх неё.
    /// </summary>
    public static class UiPanel
    {
        /// <summary>Карточка без содержимого. Возвращает корень: его и позиционируют.</summary>
        public static RectTransform Create(string name, Transform parent, UiTheme theme, bool withShadow = true)
        {
            var root = NewRect(name, parent);

            if (withShadow)
                AddLayer(root, "Shadow", theme.CornerRadius, theme.PanelShadow, 0f, -theme.ShadowDrop);

            AddLayer(root, "Edge", theme.CornerRadius, theme.PanelEdge, 0f, 0f);
            AddLayer(root, "Fill", theme.CornerRadius - theme.EdgeWidth, theme.PanelFill, theme.EdgeWidth, 0f);
            return root;
        }

        /// <summary>Клетка склада: та же карточка, но мельче, без тени и с своей палитрой.</summary>
        public static Image CreateSlot(string name, Transform parent, UiTheme theme)
        {
            var root = NewRect(name, parent);
            AddLayer(root, "Edge", theme.SlotRadius, theme.SlotEdge, 0f, 0f);
            return AddLayer(root, "Fill", theme.SlotRadius - 2, theme.SlotEmpty, 2f, 0f);
        }

        /// <summary>Заливка карточки: её перекрашивают, когда панель мигает на потере ресурса.</summary>
        public static Image FillOf(RectTransform panel) => panel.Find("Fill").GetComponent<Image>();

        /// <summary>Растянуть прямоугольник по всему родителю: слой панели, сетка клеток.</summary>
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

        /// <summary>Слой карточки во всю её площадь, вжатый на `inset` и сдвинутый по вертикали.</summary>
        static Image AddLayer(RectTransform parent, string name, int radius, Color color, float inset, float offsetY)
        {
            // `CanvasRenderer` перечислен явно: конструктор `GameObject(имя, типы)` не разбирает
            // `RequireComponent`, и без него графика молча не рисуется.
            var layer = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rect = (RectTransform)layer.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset + offsetY);
            rect.offsetMax = new Vector2(-inset, -inset + offsetY);

            var image = layer.GetComponent<Image>();
            image.sprite = UiSprites.Rounded(radius);
            image.type = Image.Type.Sliced;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }
    }
}
