using UnityEngine;

namespace Game.UI
{
    /// <summary>Ортографическая камера: пан перетаскиванием, зум колесом, границы по габаритам поля.</summary>
    [RequireComponent(typeof(Camera))]
    public sealed class CameraRig : MonoBehaviour
    {
        // Портрет узкий: на максимальном зуме поле влезает по высоте, ширину проходим паном.
        [SerializeField] float minZoom = 2.5f;
        [SerializeField] float maxZoom = 8.5f;
        [SerializeField] float zoomStepPerScroll = 0.6f;

        Camera cameraComponent;
        Rect fieldBounds = new(-1000f, -1000f, 2000f, 2000f);

        Camera Cam => cameraComponent != null ? cameraComponent : cameraComponent = GetComponent<Camera>();

        void Awake() => Cam.orthographic = true;

        /// <summary>Прямоугольник поля в мировых координатах: за него камера не выходит.</summary>
        public void SetFieldBounds(Rect bounds)
        {
            fieldBounds = bounds;
            ClampPosition();
        }

        /// <summary>Поставить камеру к низу поля, где стоит Метрополия.</summary>
        public void FocusOnBottom()
        {
            transform.position = new Vector3(fieldBounds.center.x, fieldBounds.yMin + Cam.orthographicSize, transform.position.z);
            ClampPosition();
        }

        public void Pan(Vector2 screenDelta)
        {
            var worldPerPixel = Cam.orthographicSize * 2f / Screen.height;
            transform.position -= (Vector3)(screenDelta * worldPerPixel);
            ClampPosition();
        }

        /// <summary>Шаг зума: ±1 от колеса мыши, дробное значение от щипка пальцами.</summary>
        public void Zoom(float steps)
        {
            Cam.orthographicSize = Mathf.Clamp(Cam.orthographicSize - steps * zoomStepPerScroll, minZoom, maxZoom);
            ClampPosition();
        }

        void ClampPosition()
        {
            var halfHeight = Cam.orthographicSize;
            var halfWidth = halfHeight * Cam.aspect;

            var position = transform.position;
            transform.position = new Vector3(
                ClampAxis(position.x, fieldBounds.xMin, fieldBounds.xMax, halfWidth),
                ClampAxis(position.y, fieldBounds.yMin, fieldBounds.yMax, halfHeight),
                position.z);
        }

        /// <summary>Если поле уже обзора, камера стоит по центру этой оси.</summary>
        static float ClampAxis(float value, float min, float max, float halfSize)
        {
            if (max - min <= halfSize * 2f)
                return (min + max) * 0.5f;

            return Mathf.Clamp(value, min + halfSize, max - halfSize);
        }
    }
}
