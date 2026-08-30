using System;
using UnityEngine;

namespace Game.Economy
{
    /// <summary>Иконка ресурса, едущая по дороге на склад: бревно, валун, кристалл.</summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class ResourceMover : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] ResourcePalette palette;
        [SerializeField] float scale = 0.34f;
        [Tooltip("Высота над землёй: ресурс едет над полотном дороги, а не в нём")]
        [SerializeField] float height = 0.22f;
        [SerializeField] float hopSeconds = 0.35f;
        [SerializeField] float hopHeight = 0.7f;

        Delivery delivery;
        Action landed;
        Vector3 hopFrom;
        Vector3 hopTo;
        float hopElapsed = -1f;

        public void Bind(Delivery bound)
        {
            delivery = bound;
            transform.localScale = Vector3.one * scale;

            // Тот же полигон, что лежит в клетке склада: иначе едущий ресурс и приехавший
            // выглядели бы разными предметами.
            GetComponent<MeshFilter>().sharedMesh = ResourceShape.Icon(bound.Type);

            var meshRenderer = GetComponent<MeshRenderer>();
            var propertyBlock = new MaterialPropertyBlock();
            meshRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(BaseColorId, palette.Get(bound.Type));
            meshRenderer.SetPropertyBlock(propertyBlock);

            MoveToDelivery();
        }

        /// <summary>
        /// Ресурс доехал до Метрополии и перепрыгивает в клетку склада. `onLanded` вызывается
        /// ровно один раз — в конце прыжка или при уничтожении кружка, чтобы клетка не осталась
        /// скрытой навсегда.
        /// </summary>
        public void HopTo(Vector3 target, Action onLanded = null)
        {
            delivery = null;
            landed = onLanded;
            hopFrom = transform.position;
            hopTo = new Vector3(target.x, height, target.z);
            hopElapsed = 0f;
        }

        void OnDestroy() => Land();

        void Land()
        {
            var callback = landed;
            landed = null;
            callback?.Invoke();
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
            {
                Land();
                Destroy(gameObject);
            }
        }

        void MoveToDelivery()
        {
            // `Delivery.Position` живёт в плоских координатах поля: x вправо, y вглубь.
            var position = delivery.Position;
            transform.position = new Vector3(position.x, height, position.y);
        }
    }
}
