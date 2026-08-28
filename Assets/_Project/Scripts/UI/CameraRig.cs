using UnityEngine;

namespace Game.UI
{
    /// <summary>Ортографическая камера: пан перетаскиванием, зум колесом, границы по размеру поля.</summary>
    [RequireComponent(typeof(Camera))]
    public sealed class CameraRig : MonoBehaviour
    {
        [SerializeField] float minZoom = 2f;
        [SerializeField] float maxZoom = 8f;
        [SerializeField] float zoomStepPerScroll = 0.5f;

        Camera cameraComponent;
        Vector2 fieldHalfExtents = Vector2.positiveInfinity;

        Camera Cam => cameraComponent != null ? cameraComponent : cameraComponent = GetComponent<Camera>();

        void Awake() => Cam.orthographic = true;

        /// <summary>Половина размеров поля в мировых координатах: за них камера не выходит.</summary>
        public void SetFieldHalfExtents(Vector2 halfExtents)
        {
            fieldHalfExtents = halfExtents;
            ClampPosition();
        }

        public void Pan(Vector2 screenDelta)
        {
            var worldPerPixel = Cam.orthographicSize * 2f / Screen.height;
            transform.position -= (Vector3)(screenDelta * worldPerPixel);
            ClampPosition();
        }

        public void Zoom(float scroll)
        {
            Cam.orthographicSize =
                Mathf.Clamp(Cam.orthographicSize - Mathf.Sign(scroll) * zoomStepPerScroll, minZoom, maxZoom);
            ClampPosition();
        }

        void ClampPosition()
        {
            var halfHeight = Cam.orthographicSize;
            var halfWidth = halfHeight * Cam.aspect;

            var limitX = Mathf.Max(0f, fieldHalfExtents.x - halfWidth);
            var limitY = Mathf.Max(0f, fieldHalfExtents.y - halfHeight);

            var position = transform.position;
            transform.position = new Vector3(
                Mathf.Clamp(position.x, -limitX, limitX),
                Mathf.Clamp(position.y, -limitY, limitY),
                position.z);
        }
    }
}
