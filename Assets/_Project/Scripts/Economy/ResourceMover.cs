using Game.Grid;
using UnityEngine;

namespace Game.Economy
{
    /// <summary>Кружок цвета ресурса, едущий по дороге на склад.</summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class ResourceMover : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] ResourcePalette palette;
        [SerializeField] float scale = 0.24f;
        [SerializeField] float depth = -0.08f;

        Delivery delivery;

        public void Bind(Delivery bound)
        {
            delivery = bound;
            transform.localScale = Vector3.one * scale;
            GetComponent<MeshFilter>().sharedMesh = HexMeshBuilder.Shared;

            var meshRenderer = GetComponent<MeshRenderer>();
            var propertyBlock = new MaterialPropertyBlock();
            meshRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(BaseColorId, palette.Get(bound.Type));
            meshRenderer.SetPropertyBlock(propertyBlock);

            MoveToDelivery();
        }

        void Update()
        {
            if (delivery != null)
                MoveToDelivery();
        }

        void MoveToDelivery()
        {
            var position = delivery.Position;
            transform.position = new Vector3(position.x, position.y, depth);
        }
    }
}
