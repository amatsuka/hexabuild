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
        [Tooltip("Тёмный ореол под иконкой. Панель прозрачная, и без него иконка растворяется в карте")]
        [SerializeField] Color haloColor = new(0f, 0f, 0f, 0.55f);
        [Tooltip("Насколько ореол больше самой иконки")]
        [SerializeField, Range(1f, 1.4f)] float haloScale = 1.14f;
        [Tooltip("Снос ореола вниз, пиксели: ореол работает и обводкой, и тенью разом")]
        [SerializeField] float haloDrop = 3f;

        /// <summary>Чем рисовать полигон, если снимка модели нет. У монеты и свитка его нет.</summary>
        ResourceType? shape;

        Texture snapshot;
        bool visible;

        /// <summary>Снимок модели, если он есть; иначе канвас берёт белую текстуру по умолчанию.</summary>
        public override Texture mainTexture => snapshot != null ? snapshot : base.mainTexture;

        public void Show(ResourceType resource, Color iconColor, Texture modelSnapshot = null)
        {
            shape = resource;
            color = iconColor;
            snapshot = modelSnapshot;
            visible = true;
            SetVerticesDirty();
            SetMaterialDirty();
        }

        /// <summary>Снимок без ресурса за ним: монета и свиток в `ResourceType` не значатся.</summary>
        public void Show(Texture modelSnapshot, Color iconColor)
        {
            shape = null;
            color = iconColor;
            snapshot = modelSnapshot;
            visible = modelSnapshot != null;
            SetVerticesDirty();
            SetMaterialDirty();
        }

        public void Hide()
        {
            if (!visible)
                return;

            shape = null;
            snapshot = null;
            visible = false;
            SetVerticesDirty();
            SetMaterialDirty();
        }

        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            if (!visible || (snapshot == null && !shape.HasValue))
                return;

            // Полигон нарисован в квадрате 1×1, поэтому вписываем его в меньшую сторону клетки:
            // на неквадратной клетке иконка иначе растянулась бы.
            var rect = GetPixelAdjustedRect();
            var size = Mathf.Min(rect.width, rect.height);
            var centre = rect.center;

            // Ореол — та же фигура, крупнее и тёмная, нарисованная первой, то есть под иконкой.
            // Одним мешем и одной текстурой: отдельный объект под тень удвоил бы их число на
            // складе, где иконок два десятка.
            if (haloColor.a > 0f)
                Emit(helper, centre + new Vector2(0f, -haloDrop), size * haloScale, haloColor);

            Emit(helper, centre, size, color);
        }

        void Emit(VertexHelper helper, Vector2 centre, float size, Color tint)
        {
            var start = helper.currentVertCount;

            if (snapshot != null)
            {
                // Снимок квадратный, поэтому вписываем его в меньшую сторону клетки — так же,
                // как вписывается полигон.
                var half = size * 0.5f;
                helper.AddVert(new Vector3(centre.x - half, centre.y - half, 0f), tint, new Vector2(0f, 0f));
                helper.AddVert(new Vector3(centre.x - half, centre.y + half, 0f), tint, new Vector2(0f, 1f));
                helper.AddVert(new Vector3(centre.x + half, centre.y + half, 0f), tint, new Vector2(1f, 1f));
                helper.AddVert(new Vector3(centre.x + half, centre.y - half, 0f), tint, new Vector2(1f, 0f));
                helper.AddTriangle(start, start + 1, start + 2);
                helper.AddTriangle(start + 2, start + 3, start);
                return;
            }

            var outline = ResourceShape.Outline(shape.Value);
            foreach (var point in outline.Vertices)
                helper.AddVert(new Vector3(centre.x + point.x * size, centre.y + point.y * size, 0f), tint, Vector2.zero);

            var triangles = outline.Triangles;
            for (var i = 0; i < triangles.Count; i += 3)
                helper.AddTriangle(start + triangles[i], start + triangles[i + 1], start + triangles[i + 2]);
        }
    }
}
