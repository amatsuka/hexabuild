using Game.Grid;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Roads
{
    /// <summary>
    /// Дорога поверх гекса в два слоя: тёмная обочина пошире, светлое полотно поверх неё — тем же
    /// приёмом, что обводка гекса. Ширина и оттенок берутся из хеша координаты, иначе сеть
    /// выглядит штампованной.
    /// </summary>
    public sealed class RoadView : MonoBehaviour
    {
        /// <summary>Полотно лежит поверх обочины: земля — XZ, «поверх» это выше по Y.</summary>
        const float SurfaceHeight = 0.006f;

        /// <summary>Настил моста ниже обочины и шире её: он торчит из-под дороги оторочкой.</summary>
        const float BridgeHeight = -0.004f;

        /// <summary>
        /// Ступеней ширины. Непрерывная ширина размножила бы кэш мешей до одного на плитку;
        /// три ступени глаз читает как разнобой, а мешей остаётся десяток.
        /// </summary>
        const int WidthSteps = 3;

        const int WidthSalt = 0;
        const int ShadeSalt = 1;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] Material roadMaterial;
        [SerializeField] Color surfaceColor = new(0.84f, 0.81f, 0.73f);
        [SerializeField] Color shoulderColor = new(0.19f, 0.17f, 0.15f);
        [SerializeField, Range(0f, 1f)] float disconnectedAlpha = 0.35f;
        [SerializeField] float surfaceWidth = 0.17f;
        [Tooltip("Насколько обочина шире полотна: по половине с каждой стороны")]
        [SerializeField] float shoulderExtra = 0.08f;
        [SerializeField] Color bridgeColor = new(0.58f, 0.42f, 0.26f);
        [Tooltip("Насколько настил моста шире обочины")]
        [SerializeField] float bridgeExtra = 0.12f;
        [Tooltip("Разброс ширины от координаты плитки, доля от базовой")]
        [SerializeField, Range(0f, 0.4f)] float widthJitter = 0.16f;
        [Tooltip("Разброс яркости от координаты плитки")]
        [SerializeField, Range(0f, 0.4f)] float shadeJitter = 0.12f;

        MeshFilter shoulder;
        MeshFilter surface;
        MeshFilter bridge;
        MaterialPropertyBlock propertyBlock;

        /// <summary>
        /// Отрисовать дорогу: <paramref name="linkMask"/> — биты направлений маршрута,
        /// <paramref name="bridgeMask"/> — те из них, где дорога идёт мостом (через реку или по воде).
        /// </summary>
        public void Show(HexCoord coord, bool connected, int linkMask, int bridgeMask)
        {
            bridge ??= CreateLayer("Bridge", BridgeHeight);
            shoulder ??= CreateLayer("Shoulder", 0f);
            surface ??= CreateLayer("Surface", SurfaceHeight);

            var width = surfaceWidth * WidthFactor(coord);
            var shade = Mathf.Lerp(1f - shadeJitter, 1f + shadeJitter, coord.Hash01(ShadeSalt));

            Draw(bridge, RoadMeshBuilder.Bridge(bridgeMask, width + shoulderExtra + bridgeExtra),
                Tinted(bridgeColor, shade, connected));
            Draw(shoulder, RoadMeshBuilder.Get(linkMask, width + shoulderExtra), Tinted(shoulderColor, shade, connected));
            Draw(surface, RoadMeshBuilder.Get(linkMask, width), Tinted(surfaceColor, shade, connected));
        }

        /// <summary>Ширина квантуется: кэш мешей живёт по паре «маска + ширина».</summary>
        float WidthFactor(HexCoord coord)
        {
            var step = (int)(coord.Hash01(WidthSalt) * WidthSteps);
            return Mathf.Lerp(1f - widthJitter, 1f + widthJitter, step / (float)(WidthSteps - 1));
        }

        Color Tinted(Color color, float shade, bool connected) =>
            new(color.r * shade, color.g * shade, color.b * shade, connected ? color.a : disconnectedAlpha);

        void Draw(MeshFilter layer, Mesh mesh, Color color)
        {
            layer.sharedMesh = mesh;

            propertyBlock ??= new MaterialPropertyBlock();
            var layerRenderer = layer.GetComponent<MeshRenderer>();
            layerRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(BaseColorId, color);
            layerRenderer.SetPropertyBlock(propertyBlock);
        }

        MeshFilter CreateLayer(string layerName, float height)
        {
            var layer = new GameObject(layerName, typeof(MeshFilter), typeof(MeshRenderer));
            layer.transform.SetParent(transform, false);
            layer.transform.localPosition = new Vector3(0f, height, 0f);

            var layerRenderer = layer.GetComponent<MeshRenderer>();
            layerRenderer.sharedMaterial = roadMaterial;
            layerRenderer.shadowCastingMode = ShadowCastingMode.Off;
            layerRenderer.receiveShadows = false;
            return layer.GetComponent<MeshFilter>();
        }
    }
}
