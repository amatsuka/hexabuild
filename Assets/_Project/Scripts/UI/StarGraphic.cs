using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Звезда уровня одним рендерером: пятиконечник веером треугольников от центра. Своя графика
    /// нужна по той же причине, что и `UiPanelGraphic`, — спрайта под звезду в проекте нет,
    /// а заводить бинарный ассет ради выпуклого многоугольника незачем: он считается формулой.
    /// Пустая звезда — та же форма приглушённым цветом, чтобы места под звёзды были видны все три.
    /// </summary>
    public sealed class StarGraphic : MaskableGraphic
    {
        /// <summary>Отношение внутреннего радиуса к внешнему: классическая пятиконечная звезда.</summary>
        const float InnerRatio = 0.42f;

        const int Points = 5;

        /// <summary>Звезда на карточке: заработанная — золотом, пустая — фоном под ней.</summary>
        public static StarGraphic Create(string name, Transform parent, float size, Color color)
        {
            var created = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(StarGraphic));
            var rect = (RectTransform)created.transform;
            rect.SetParent(parent, false);
            rect.sizeDelta = new Vector2(size, size);

            var star = created.GetComponent<StarGraphic>();
            star.raycastTarget = false;
            star.color = color;
            return star;
        }

        /// <summary>
        /// Ряд из трёх звёзд по центру родителя: заработанные золотом, остальные приглушённо.
        /// Ряд — обычный `RectTransform`, его ставит на место тот, кто его заказал.
        /// </summary>
        public static RectTransform Row(
            string name, Transform parent, int stars, int total, float size, float gap, Color earned, Color empty)
        {
            var row = UiPanel.NewRect(name, parent);
            var step = size + gap;
            var left = -(total - 1) * step * 0.5f;

            for (var i = 0; i < total; i++)
            {
                var star = Create($"Star {i + 1}", row, size, i < stars ? earned : empty);
                var rect = star.rectTransform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = new Vector2(left + step * i, 0f);
            }

            return row;
        }

        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();

            var rect = GetPixelAdjustedRect();
            var centre = rect.center;
            var outer = Mathf.Min(rect.width, rect.height) * 0.5f;
            var inner = outer * InnerRatio;

            helper.AddVert(centre, color, Vector2.zero);

            // Вершины идут через одну: остриё, впадина, остриё. Первое остриё смотрит вверх,
            // поэтому отсчёт начинается с четверти оборота.
            for (var i = 0; i < Points * 2; i++)
            {
                var angle = Mathf.PI * 0.5f + i * Mathf.PI / Points;
                var radius = i % 2 == 0 ? outer : inner;
                var offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                helper.AddVert(centre + offset, color, Vector2.zero);
            }

            for (var i = 0; i < Points * 2; i++)
                helper.AddTriangle(0, 1 + i, 1 + (i + 1) % (Points * 2));
        }
    }
}
