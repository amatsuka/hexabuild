using System;
using Game.Grid;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Roads
{
    /// <summary>
    /// Вью всей дорожной сети: один меш насыпи на партию, а не по вью на плитку.
    ///
    /// Меш по плитке дорога перерасти не могла — участок обрывался на границе гекса, и шов между
    /// соседями оставался открытым. Пересборка на постройку дешёвая: дорог за партию десятки,
    /// а вершин у насыпи считаные сотни.
    ///
    /// Меш строится в мировых координатах, поэтому объект обязан стоять в начале координат без
    /// поворота и масштаба.
    ///
    /// Мешей на самом деле три: насыпь, кладка поверх неё вместе с каменными арками мостов
    /// и деревянные настилы. Делит их не геометрия, а цвет — он в этом проекте живёт
    /// на рендерере, и одним мешем ни камень от полотна, ни дерево от камня не отличить.
    /// </summary>
    public sealed class RoadView : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] Material roadMaterial;
        [SerializeField] Color roadColor = new(0.80f, 0.74f, 0.62f);
        [Tooltip("Цвет обочных камней, колейных плашек и каменных арок")]
        [SerializeField] Color masonryColor = new(0.56f, 0.53f, 0.48f);
        [Tooltip("Цвет деревянного настила мостов: досок, балок, свай и перил")]
        [SerializeField] Color timberColor = new(0.58f, 0.40f, 0.26f);
        [Tooltip("Ширина полотна. С откосами она не должна вылезти за крышку плитки: потолок ~0.26")]
        [SerializeField, Range(0.08f, 0.26f)] float width = 0.24f;

        Mesh bedMesh;
        Mesh masonryMesh;
        Mesh timberMesh;
        MeshFilter bed;
        MeshFilter masonry;
        MeshFilter timber;

        /// <summary>Пересобрать сеть целиком: связность меняется всей цепочкой, а не по плитке.</summary>
        public void Show(RoadNetwork network, Func<HexCoord, RoadGround> groundAt)
        {
            if (bed == null)
                Create();

            RoadMeshBuilder.Build(bedMesh, masonryMesh, timberMesh, network, groundAt, width);
            bed.sharedMesh = bedMesh;
            masonry.sharedMesh = masonryMesh;
            timber.sharedMesh = timberMesh;
        }

        void Create()
        {
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;

            bedMesh = Layer("Roadbed");
            masonryMesh = Layer("Masonry");
            timberMesh = Layer("Timber");

            bed = Attach(gameObject, bedMesh, roadColor);
            masonry = Attach(Child("Masonry"), masonryMesh, masonryColor);
            timber = Attach(Child("Timber"), timberMesh, timberColor);
        }

        static Mesh Layer(string layerName)
        {
            var mesh = new Mesh { name = layerName };

            // Меш перестраивается каждую постройку: без этого Unity держит копию в памяти CPU
            // и загружает её в GPU целиком на каждой правке.
            mesh.MarkDynamic();
            return mesh;
        }

        GameObject Child(string childName)
        {
            var child = new GameObject(childName);
            child.transform.SetParent(transform, false);
            return child;
        }

        MeshFilter Attach(GameObject host, Mesh mesh, Color color)
        {
            var filter = host.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;

            var layerRenderer = host.AddComponent<MeshRenderer>();
            layerRenderer.sharedMaterial = roadMaterial;

            // Насыпь и кладка — тела, а не наклейки: они стоят в свету наравне с призмами плиток.
            layerRenderer.shadowCastingMode = ShadowCastingMode.On;
            layerRenderer.receiveShadows = true;

            var propertyBlock = new MaterialPropertyBlock();
            layerRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(BaseColorId, color);
            layerRenderer.SetPropertyBlock(propertyBlock);
            return filter;
        }

        void OnDestroy()
        {
            if (bedMesh != null)
                Destroy(bedMesh);

            if (masonryMesh != null)
                Destroy(masonryMesh);

            if (timberMesh != null)
                Destroy(timberMesh);
        }
    }
}
