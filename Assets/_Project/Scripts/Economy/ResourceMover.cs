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
        [Tooltip("Модели добываемых ресурсов. Пусто — едет полигон, как было")]
        [SerializeField] ResourceModels models = new();
        [SerializeField] float scale = 0.34f;
        [Tooltip("Высота над землёй: ресурс едет над полотном дороги, а не в нём")]
        [SerializeField] float height = 0.22f;
        [SerializeField] float hopSeconds = 0.35f;
        [SerializeField] float hopHeight = 0.7f;

        [Header("Промах мимо склада")]
        [Tooltip("Скорость улёта за кадр, юнитов в секунду. Время считается из неё и дистанции: за кромку далеко, и оно зависит от зума")]
        [SerializeField] float missSpeed = 9f;
        [Tooltip("Границы длительности улёта, чтобы он не тянулся и не мелькал. На рабочих зумах упор в них не приходится: скорость решает сама")]
        [SerializeField] Vector2 missSecondsRange = new(0.45f, 1.4f);
        [Tooltip("Подброс в начале, прежде чем ресурс сорвётся вниз")]
        [SerializeField] float missRise = 0.5f;
        [Tooltip("Насколько глубоко ресурс проваливается под поле, пока летит")]
        [SerializeField] float missDrop = 2.2f;
        [Tooltip("Кувырок в полёте, градусов в секунду")]
        [SerializeField] float missSpin = 480f;

        Delivery delivery;
        Action landed;
        Vector3 hopFrom;
        Vector3 hopTo;
        float hopElapsed = -1f;

        /// <summary>
        /// Высота пивота, при которой середина груза оказывается на `height`. У полигона центр
        /// и пивот совпадают, у моделей — нет: бревно центрировано, кирпич стоит на основании.
        /// </summary>
        float carryHeight;

        /// <summary>Длительность улёта: считается из дистанции в `MissTo`, а не задана числом.</summary>
        float missSeconds;

        /// <summary>Ресурс не влез на склад и улетает мимо, а не прыгает в клетку.</summary>
        bool missed;

        /// <summary>Ось кувырка. Косая и постоянная: падение должно читаться, а не рябить.</summary>
        static readonly Vector3 TumbleAxis = new Vector3(1f, 0.4f, 0.6f).normalized;

        public void Bind(Delivery bound)
        {
            delivery = bound;

            // Тот же предмет, что лежит в клетке склада: иначе едущий ресурс и приехавший
            // выглядели бы разными. У добываемых это модель, у крафтовых — полигон, как было.
            var model = models.Get(bound.Type);
            var meshRenderer = GetComponent<MeshRenderer>();
            var propertyBlock = new MaterialPropertyBlock();
            meshRenderer.GetPropertyBlock(propertyBlock);

            if (model != null)
            {
                var fit = ResourceModels.ScaleFor(model, scale);
                transform.localScale = Vector3.one * fit;
                GetComponent<MeshFilter>().sharedMesh = model;
                meshRenderer.sharedMaterial = models.Material;
                // Цвет модели лежит в атласе: красить её цветом ресурса нельзя, он перемножится.
                propertyBlock.SetColor(BaseColorId, Color.white);
                carryHeight = height - model.bounds.center.y * fit;
            }
            else
            {
                transform.localScale = Vector3.one * scale;
                GetComponent<MeshFilter>().sharedMesh = ResourceShape.Icon(bound.Type);
                propertyBlock.SetColor(BaseColorId, palette.Get(bound.Type));
                carryHeight = height;
            }

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
            hopTo = new Vector3(target.x, carryHeight, target.z);
            hopElapsed = 0f;
        }

        /// <summary>
        /// Складу не хватило места: ресурс уходит мимо панели за кромку экрана. Потеря уже
        /// засчитана — это чистый визуал, но без него ресурс исчезал в воздухе, и переполнение
        /// было видно только вспышкой панели. `target` — точка за кадром, её даёт `CameraRig`.
        ///
        /// Время считается из дистанции, а не задано числом: точка за кромкой тем дальше, чем
        /// шире зум, и постоянная длительность превращала бы улёт то в полёт пули, то в тягучее
        /// сползание.
        /// </summary>
        public void MissTo(Vector3 target)
        {
            delivery = null;
            landed = null;
            missed = true;
            hopFrom = transform.position;
            hopTo = new Vector3(target.x, carryHeight, target.z);
            missSeconds = Mathf.Clamp(
                Vector3.Distance(hopFrom, hopTo) / Mathf.Max(missSpeed, 0.01f),
                missSecondsRange.x,
                missSecondsRange.y);
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
                if (missed)
                    Miss();
                else
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

        void Miss()
        {
            hopElapsed += Time.deltaTime;
            var progress = Mathf.Clamp01(hopElapsed / missSeconds);

            // Подброс синусом, падение квадратом: вверх ресурс уходит плавно, вниз — с разгоном.
            var position = Vector3.Lerp(hopFrom, hopTo, progress);
            position.y += missRise * Mathf.Sin(progress * Mathf.PI) - missDrop * progress * progress;
            transform.position = position;
            transform.Rotate(TumbleAxis, missSpin * Time.deltaTime, Space.World);

            // Размер не трогаем: ресурс должен уйти за кромку целым. Усадка читалась бы как
            // «растаял на месте», а нужно «улетел».
            if (progress >= 1f)
                Destroy(gameObject);
        }

        void MoveToDelivery()
        {
            // `Delivery.Position` живёт в плоских координатах поля: x вправо, y вглубь.
            var position = delivery.Position;
            transform.position = new Vector3(position.x, carryHeight, position.y);
        }
    }
}
