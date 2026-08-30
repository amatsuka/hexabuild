using Game.Economy;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Storage
{
    /// <summary>
    /// Иконка ресурса в канвасе. Рисует тот же полигон, что и меш на поле, — через
    /// `OnPopulateMesh`, без спрайта: ассетов в проекте нет, а `Image` без спрайта умеет
    /// только прямоугольник.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ResourceIcon : MaskableGraphic
    {
        ResourceType? content;

        public void Show(ResourceType resource, Color iconColor)
        {
            content = resource;
            color = iconColor;
            SetVerticesDirty();
        }

        public void Hide()
        {
            if (!content.HasValue)
                return;

            content = null;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            if (!content.HasValue)
                return;

            // Полигон нарисован в квадрате 1×1, поэтому вписываем его в меньшую сторону клетки:
            // на неквадратной клетке иконка иначе растянулась бы.
            var rect = GetPixelAdjustedRect();
            var size = Mathf.Min(rect.width, rect.height);
            var center = rect.center;

            var outline = ResourceShape.Outline(content.Value);
            foreach (var point in outline.Vertices)
                helper.AddVert(new Vector3(center.x + point.x * size, center.y + point.y * size, 0f), color, Vector2.zero);

            var triangles = outline.Triangles;
            for (var i = 0; i < triangles.Count; i += 3)
                helper.AddTriangle(triangles[i], triangles[i + 1], triangles[i + 2]);
        }
    }
}
