using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Карточка HUD одним рендерером. Меш — прямоугольник шире самой карточки на запас под
    /// свечение; всё остальное — градиент, кромка, блик, свечение — считает шейдер `Game/UiPanel`
    /// по расстоянию до кромки. Своя графика нужна затем, что `Image` умеет красить спрайт лишь
    /// одним цветом, а размеры карточки в пикселях шейдеру взять больше неоткуда.
    /// </summary>
    public sealed class UiPanelGraphic : MaskableGraphic
    {
        static readonly Dictionary<UiPanelStyle, Material> Materials = new();

        UiPanelStyle style;

        /// <summary>Задать вид карточки. Материал общий на стиль, а не на карточку.</summary>
        public void Apply(Shader shader, in UiPanelStyle panelStyle)
        {
            style = panelStyle;
            material = MaterialFor(shader, panelStyle);
            SetVerticesDirty();
            SetMaterialDirty();
        }

        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();

            var rect = GetPixelAdjustedRect();
            var half = new Vector2(rect.width, rect.height) * 0.5f;
            var centre = rect.center;
            var outer = half + Vector2.one * style.Padding;

            // uv0 несёт и пиксельные координаты вершины от центра, и полуразмер самой карточки:
            // меш шире неё, поэтому из UV 0..1 размер уже не восстановить.
            AddCorner(helper, centre, new Vector2(-outer.x, -outer.y), half);
            AddCorner(helper, centre, new Vector2(-outer.x, outer.y), half);
            AddCorner(helper, centre, new Vector2(outer.x, outer.y), half);
            AddCorner(helper, centre, new Vector2(outer.x, -outer.y), half);

            helper.AddTriangle(0, 1, 2);
            helper.AddTriangle(2, 3, 0);
        }

        void AddCorner(VertexHelper helper, Vector2 centre, Vector2 offset, Vector2 half) =>
            helper.AddVert(centre + offset, color, new Vector4(offset.x, offset.y, half.x, half.y));

        static Material MaterialFor(Shader shader, in UiPanelStyle style)
        {
            if (Materials.TryGetValue(style, out var cached) && cached != null)
                return cached;

            var created = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            created.SetColor(FillTopId, style.FillTop);
            created.SetColor(FillBottomId, style.FillBottom);
            created.SetColor(EdgeId, style.Edge);
            created.SetColor(GlowId, style.Glow);
            created.SetFloat(RadiusId, style.Radius);
            created.SetFloat(EdgeWidthId, style.EdgeWidth);
            created.SetFloat(GlowSizeId, style.GlowSize);
            created.SetFloat(GlowOffsetId, style.GlowOffset);
            created.SetFloat(HighlightId, style.Highlight);
            created.SetFloat(HighlightSpreadId, style.HighlightSpread);
            created.SetFloat(SheenId, style.Sheen);
            created.SetFloat(DarkenId, style.Darken);
            created.SetFloat(BackLightId, style.BackLight);
            created.SetFloat(SpecId, style.Spec);
            created.SetVector(LightDirId, style.LightDirection);

            Materials[style] = created;
            return created;
        }

        static readonly int FillTopId = Shader.PropertyToID("_FillTop");
        static readonly int FillBottomId = Shader.PropertyToID("_FillBottom");
        static readonly int EdgeId = Shader.PropertyToID("_EdgeColor");
        static readonly int GlowId = Shader.PropertyToID("_GlowColor");
        static readonly int RadiusId = Shader.PropertyToID("_Radius");
        static readonly int EdgeWidthId = Shader.PropertyToID("_EdgeWidth");
        static readonly int GlowSizeId = Shader.PropertyToID("_GlowSize");
        static readonly int GlowOffsetId = Shader.PropertyToID("_GlowOffset");
        static readonly int HighlightId = Shader.PropertyToID("_Highlight");
        static readonly int HighlightSpreadId = Shader.PropertyToID("_HighlightSpread");
        static readonly int SheenId = Shader.PropertyToID("_Sheen");
        static readonly int DarkenId = Shader.PropertyToID("_Darken");
        static readonly int BackLightId = Shader.PropertyToID("_BackLight");
        static readonly int SpecId = Shader.PropertyToID("_Spec");
        static readonly int LightDirId = Shader.PropertyToID("_LightDir");
    }
}
