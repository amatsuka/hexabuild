using Game.Grid;
using UnityEngine;

namespace Game.Roads
{
    /// <summary>Белая дорога поверх гекса: узел в центре и отрезки к соседним дорогам.</summary>
    public sealed class RoadView : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] Material roadMaterial;
        [SerializeField] Color roadColor = Color.white;
        [SerializeField, Range(0f, 1f)] float disconnectedAlpha = 0.35f;
        [SerializeField] float nodeScale = 0.26f;
        [SerializeField] float segmentWidth = 0.13f;

        readonly MeshRenderer[] segments = new MeshRenderer[6];

        MeshRenderer node;
        MaterialPropertyBlock propertyBlock;

        /// <summary>Отрисовать дорогу: <paramref name="linkMask"/> — биты направлений с соседней дорогой.</summary>
        public void Show(bool connected, int linkMask)
        {
            var color = roadColor;
            if (!connected)
                color.a = disconnectedAlpha;

            node ??= CreatePart("Node", Vector3.zero, Quaternion.identity, new Vector3(nodeScale, nodeScale, 1f));
            SetColor(node, color);

            for (var direction = 0; direction < segments.Length; direction++)
            {
                var linked = (linkMask & (1 << direction)) != 0;
                if (linked)
                    segments[direction] ??= CreateSegment(direction);

                if (segments[direction] == null)
                    continue;

                segments[direction].gameObject.SetActive(linked);
                if (linked)
                    SetColor(segments[direction], color);
            }
        }

        MeshRenderer CreateSegment(int direction)
        {
            var offset = HexCoord.Directions[direction].ToWorld();
            var angle = Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg - 90f;
            // Отрезок тянется от центра плитки до середины общей грани.
            var length = offset.magnitude * 0.5f / (2f * HexCoord.Size);

            return CreatePart(
                $"Link {direction}",
                new Vector3(offset.x * 0.25f, offset.y * 0.25f, 0f),
                Quaternion.Euler(0f, 0f, angle),
                new Vector3(segmentWidth, length, 1f));
        }

        MeshRenderer CreatePart(string partName, Vector3 localPosition, Quaternion localRotation, Vector3 localScale)
        {
            var part = new GameObject(partName, typeof(MeshFilter), typeof(MeshRenderer));
            part.transform.SetParent(transform, false);
            part.transform.SetLocalPositionAndRotation(localPosition, localRotation);
            part.transform.localScale = localScale;
            part.GetComponent<MeshFilter>().sharedMesh = HexMeshBuilder.Shared;

            var partRenderer = part.GetComponent<MeshRenderer>();
            partRenderer.sharedMaterial = roadMaterial;
            partRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            partRenderer.receiveShadows = false;
            return partRenderer;
        }

        void SetColor(MeshRenderer target, Color color)
        {
            propertyBlock ??= new MaterialPropertyBlock();

            target.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(BaseColorId, color);
            target.SetPropertyBlock(propertyBlock);
        }
    }
}
