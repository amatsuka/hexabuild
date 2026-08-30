using UnityEngine;

namespace Game.UI
{
    /// <summary>Ортографическая камера: пан перетаскиванием, зум колесом, границы по габаритам поля.</summary>
    [RequireComponent(typeof(Camera))]
    public sealed class CameraRig : MonoBehaviour
    {
        // Портрет узкий: на максимальном зуме поле влезает по высоте, ширину проходим паном.
        // Верхняя граница держит гекс крупным: при 6 юнитах он занимает около 160 px из 1080.
        [SerializeField] float minZoom = 3f;
        [SerializeField] float maxZoom = 6f;
        [SerializeField] float zoomStepPerScroll = 0.6f;

        // Разведка 3D. Мир по-прежнему лежит в плоскости XY, наклоняется только камера: это
        // проверка вида и света, а не переезд в XZ. Ноль градусов возвращает прежний вид сверху,
        // поэтому вся стадия откатывается одним числом.
        [Header("Разведка 3D")]
        [SerializeField, Range(0f, 70f)] float pitch;
        [Tooltip("Отвод камеры от плоскости поля. При ортографии влияет только на клиппинг")]
        [SerializeField] float distance = 20f;

        Camera cameraComponent;
        Rect fieldBounds = new(-1000f, -1000f, 2000f, 2000f);
        bool initialized;

        /// <summary>Точка плоскости поля, на которую смотрит камера. Позиция выводится из неё.</summary>
        Vector2 focus;

        Camera Cam => cameraComponent != null ? cameraComponent : cameraComponent = GetComponent<Camera>();

        /// <summary>
        /// Наклон укорачивает поле по вертикали экрана: тот же пиксель тянет больше мира, и
        /// столько же мира влезает в кадр. Один множитель на пан, на границы и на фокус.
        /// </summary>
        float Foreshortening => Mathf.Max(Mathf.Cos(pitch * Mathf.Deg2Rad), 0.1f);

        /// <summary>Половина видимой высоты поля в мировых единицах, уже с поправкой на наклон.</summary>
        float HalfHeight => Cam.orthographicSize / Foreshortening;

        /// <summary>
        /// Позиция камеры выводится из фокуса, поэтому прочитать её как фокус можно ровно один
        /// раз. `GameSession.Awake` дёргает риг из своего `Awake`, а порядок между ними Unity не
        /// обещает: без этого флага смещение по наклону накладывалось бы дважды и камера уезжала
        /// выше поля.
        /// </summary>
        void Initialize()
        {
            if (initialized)
                return;

            initialized = true;
            Cam.orthographic = true;
            focus = transform.position;
        }

        void Awake()
        {
            Initialize();
            ApplyTransform();
        }

        /// <summary>Прямоугольник поля в мировых координатах: за него камера не выходит.</summary>
        public void SetFieldBounds(Rect bounds)
        {
            Initialize();
            fieldBounds = bounds;
            ClampPosition();
        }

        /// <summary>Поставить камеру к низу поля, где стоит Метрополия.</summary>
        public void FocusOnBottom()
        {
            Initialize();
            focus = new Vector2(fieldBounds.center.x, fieldBounds.yMin + HalfHeight);
            ClampPosition();
        }

        public void Pan(Vector2 screenDelta)
        {
            Initialize();

            var worldPerPixel = Cam.orthographicSize * 2f / Screen.height;
            focus -= new Vector2(
                screenDelta.x * worldPerPixel,
                screenDelta.y * worldPerPixel / Foreshortening);
            ClampPosition();
        }

        /// <summary>Шаг зума: ±1 от колеса мыши, дробное значение от щипка пальцами.</summary>
        public void Zoom(float steps)
        {
            Initialize();
            Cam.orthographicSize = Mathf.Clamp(Cam.orthographicSize - steps * zoomStepPerScroll, minZoom, maxZoom);
            ClampPosition();
        }

        void ClampPosition()
        {
            var halfHeight = HalfHeight;
            var halfWidth = Cam.orthographicSize * Cam.aspect;

            focus = new Vector2(
                ClampAxis(focus.x, fieldBounds.xMin, fieldBounds.xMax, halfWidth),
                ClampAxis(focus.y, fieldBounds.yMin, fieldBounds.yMax, halfHeight));

            ApplyTransform();
        }

        /// <summary>Камера садится на луч, выходящий из точки фокуса против направления взгляда.</summary>
        void ApplyTransform()
        {
            var rotation = Quaternion.Euler(pitch, 0f, 0f);
            transform.rotation = rotation;
            transform.position = new Vector3(focus.x, focus.y, 0f) - rotation * Vector3.forward * distance;
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
