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
        [SerializeField] float hopSeconds = 0.35f;
        [SerializeField] float hopHeight = 0.7f;

        Delivery delivery;
        Vector3 hopFrom;
        Vector3 hopTo;
        float hopElapsed = -1f;

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

        /// <summary>Ресурс доехал до Метрополии и перепрыгивает в клетку склада.</summary>
        public void HopTo(Vector3 target)
        {
            delivery = null;
            hopFrom = transform.position;
            hopTo = new Vector3(target.x, target.y, depth);
            hopElapsed = 0f;
        }

        void Update()
        {
            if (hopElapsed >= 0f)
            {
                Hop();
                return;
            }

            if (delivery != null)
                MoveToDelivery();
        }

        void Hop()
        {
            hopElapsed += Time.deltaTime;
            var progress = Mathf.Clamp01(hopElapsed / hopSeconds);

            var position = Vector3.Lerp(hopFrom, hopTo, progress);
            position.y += Mathf.Sin(progress * Mathf.PI) * hopHeight;
            transform.position = position;

            if (progress >= 1f)
                Destroy(gameObject);
        }

        void MoveToDelivery()
        {
            var position = delivery.Position;
            transform.position = new Vector3(position.x, position.y, depth);
        }
    }
}
