using Game.Economy;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Storage
{
    /// <summary>
    /// Иконка ресурса в канвасе. У добываемых ресурсов это снимок их модели, у крафтовых —
    /// тот же полигон, что и меш на поле, через `OnPopulateMesh`: `Image` без спрайта умеет
    /// только прямоугольник.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ResourceIcon : MaskableGraphic
    {
        ResourceType? content;
        Texture snapshot;

        /// <summary>Снимок модели, если он есть; иначе канвас берёт белую текстуру по умолчанию.</summary>
        public override Texture mainTexture => snapshot != null ? snapshot : base.mainTexture;

        public void Show(ResourceType resource, Color iconColor, Texture modelSnapshot = null)
        {
            content = resource;
            color = iconColor;
            snapshot = modelSnapshot;
            SetVerticesDirty();
            SetMaterialDirty();
        }

        public void Hide()
        {
            if (!content.HasValue)
                return;

            content = null;
            snapshot = null;
            SetVerticesDirty();
            SetMaterialDirty();
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

            if (snapshot != null)
            {
                // Снимок квадратный, поэтому вписываем его в меньшую сторону клетки — так же,
                // как вписывается полигон.
                var half = size * 0.5f;
                helper.AddVert(new Vector3(center.x - half, center.y - half, 0f), color, new Vector2(0f, 0f));
                helper.AddVert(new Vector3(center.x - half, center.y + half, 0f), color, new Vector2(0f, 1f));
                helper.AddVert(new Vector3(center.x + half, center.y + half, 0f), color, new Vector2(1f, 1f));
                helper.AddVert(new Vector3(center.x + half, center.y - half, 0f), color, new Vector2(1f, 0f));
                helper.AddTriangle(0, 1, 2);
                helper.AddTriangle(2, 3, 0);
                return;
            }

            var outline = ResourceShape.Outline(content.Value);
            foreach (var point in outline.Vertices)
                helper.AddVert(new Vector3(center.x + point.x * size, center.y + point.y * size, 0f), color, Vector2.zero);

            var triangles = outline.Triangles;
            for (var i = 0; i < triangles.Count; i += 3)
                helper.AddTriangle(triangles[i], triangles[i + 1], triangles[i + 2]);
        }
    }
}
