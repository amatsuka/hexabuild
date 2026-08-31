using UnityEngine;

namespace Game.UI
{
    /// <summary>Ортографическая камера: пан перетаскиванием, зум колесом, границы по габаритам поля.</summary>
    [RequireComponent(typeof(Camera))]
    public sealed class CameraRig : MonoBehaviour
    {
        /// <summary>Насколько вбок от центра уходит потерянный ресурс, в долях полукадра.</summary>
        const float OffScreenSide = 0.85f;

        /// <summary>Насколько ниже кромки кадра он оказывается, в долях высоты кадра.</summary>
        const float OffScreenDrop = 0.35f;

        // Портрет узкий: на максимальном зуме поле влезает по высоте, ширину проходим паном.
        // Верхняя граница держит гекс крупным: при 6 юнитах он занимает около 160 px из 1080.
        [SerializeField] float minZoom = 3f;
        [Tooltip("Нижняя граница верхнего предела. Если поле в неё не влезает, предел поднимается сам — до зума, на котором видно всё поле")]
        [SerializeField] float maxZoom = 6f;
        [SerializeField] float zoomStepPerScroll = 0.6f;

        // Наклон камеры к земле. 90 — прежний вид строго сверху, поэтому вся стадия
        // откатывается одним числом.
        [Header("Наклон")]
        [SerializeField, Range(20f, 90f)] float pitch = 55f;
        [Tooltip("Отвод камеры от земли. При ортографии влияет только на клиппинг и дальность теней")]
        [SerializeField] float distance = 20f;

        /// <summary>Запас вокруг поля на самом дальнем зуме: край не должен лежать впритык к кромке.</summary>
        const float FitMargin = 1.04f;

        Camera cameraComponent;
        Rect fieldBounds = new(-1000f, -1000f, 2000f, 2000f);

        /// <summary>Зум, на котором поле видно целиком. Считается из габаритов в `SetFieldBounds`.</summary>
        float fitZoom;

        /// <summary>Соотношение сторон, при котором считался `fitZoom`.</summary>
        float fitAspect;

        bool initialized;

        /// <summary>Точка земли, на которую смотрит камера: x вправо, y вглубь поля. Позиция выводится из неё.</summary>
        Vector2 focus;

        Camera Cam => cameraComponent != null ? cameraComponent : cameraComponent = GetComponent<Camera>();

        /// <summary>
        /// Насколько земля укорочена по вертикали экрана. На виде сверху (90°) единица: поле
        /// показано как есть. Чем сильнее наклон, тем меньше мира влезает в ту же высоту кадра
        /// и тем длиннее шаг пана на тот же пиксель.
        /// </summary>
        float Foreshortening => Mathf.Max(Mathf.Sin(pitch * Mathf.Deg2Rad), 0.1f);

        /// <summary>Половина видимой глубины поля в мировых единицах, уже с поправкой на наклон.</summary>
        float HalfDepth => Cam.orthographicSize / Foreshortening;

        void Awake()
        {
            Initialize();
            ApplyTransform();
        }

        /// <summary>Прямоугольник поля по земле: x — вправо, y — вглубь. За него камера не выходит.</summary>
        public void SetFieldBounds(Rect bounds)
        {
            Initialize();
            fieldBounds = bounds;
            fitAspect = Cam.aspect;
            fitZoom = FitZoom(bounds);
            ClampPosition();
        }

        /// <summary>
        /// Верхний предел зума. Не число из инспектора: поле должно влезать в кадр целиком,
        /// а это зависит от соотношения сторон экрана и от наклона камеры. Заданное число
        /// работает нижней границей — предел не опускается ниже него на маленьком поле.
        /// </summary>
        float MaxZoom
        {
            get
            {
                // Пересчёт при смене соотношения сторон: окно браузера тянут, телефон
                // поворачивают, и на узком экране прежнего предела на всё поле уже не хватит.
                if (!Mathf.Approximately(fitAspect, Cam.aspect))
                {
                    fitAspect = Cam.aspect;
                    fitZoom = FitZoom(fieldBounds);
                }

                return Mathf.Max(maxZoom, fitZoom);
            }
        }

        /// <summary>
        /// Зум, при котором поле помещается и по ширине, и по глубине. По глубине кадр короче
        /// в `Foreshortening` раз: земля наклонена, и в ту же высоту экрана её влезает меньше.
        /// </summary>
        float FitZoom(Rect bounds)
        {
            var byWidth = bounds.width / (2f * Mathf.Max(Cam.aspect, 0.01f));
            var byDepth = bounds.height * Foreshortening * 0.5f;
            return Mathf.Max(byWidth, byDepth) * FitMargin;
        }

        /// <summary>Поставить камеру к ближнему краю поля, где стоит Метрополия.</summary>
        public void FocusOnBottom()
        {
            Initialize();
            focus = new Vector2(fieldBounds.center.x, fieldBounds.yMin + HalfDepth);
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

        /// <summary>
        /// Точка на земле за нижней кромкой кадра. Туда улетает ресурс, которому не хватило
        /// места на складе: он должен покинуть экран, а не растаять внутри него. `side` от −1
        /// до 1 разводит такие ресурсы влево и вправо.
        ///
        /// Камера ортографическая, значит луч из точки за кромкой параллелен всем остальным
        /// и упирается в землю ближе к наблюдателю — ровно там, куда экран уже не смотрит.
        /// Точка зависит от зума, поэтому спрашивается каждый раз, а не считается один раз.
        /// </summary>
        public Vector3 OffScreenPoint(float side)
        {
            Initialize();

            var ray = Cam.ViewportPointToRay(new Vector3(0.5f + side * OffScreenSide, -OffScreenDrop, 0f));
            return new Plane(Vector3.up, Vector3.zero).Raycast(ray, out var distance)
                ? ray.GetPoint(distance)
                : transform.position + ray.direction * 20f;
        }

        /// <summary>Шаг зума: ±1 от колеса мыши, дробное значение от щипка пальцами.</summary>
        public void Zoom(float steps)
        {
            Initialize();
            Cam.orthographicSize = Mathf.Clamp(Cam.orthographicSize - steps * zoomStepPerScroll, minZoom, MaxZoom);
            ClampPosition();
        }

        /// <summary>
        /// Позиция камеры выводится из фокуса, поэтому прочитать её как фокус можно ровно один
        /// раз. `GameSession.Awake` дёргает риг из своего `Awake`, а порядок между ними Unity не
        /// обещает: без этого флага смещение по наклону накладывалось бы дважды и камера уезжала
        /// бы мимо поля.
        /// </summary>
        void Initialize()
        {
            if (initialized)
                return;

            initialized = true;
            Cam.orthographic = true;
            focus = new Vector2(transform.position.x, transform.position.z);
        }

        void ClampPosition()
        {
            var halfDepth = HalfDepth;
            var halfWidth = Cam.orthographicSize * Cam.aspect;

            focus = new Vector2(
                ClampAxis(focus.x, fieldBounds.xMin, fieldBounds.xMax, halfWidth),
                ClampAxis(focus.y, fieldBounds.yMin, fieldBounds.yMax, halfDepth));

            ApplyTransform();
        }

        /// <summary>Камера садится на луч, выходящий из точки фокуса против направления взгляда.</summary>
        void ApplyTransform()
        {
            var rotation = Quaternion.Euler(pitch, 0f, 0f);
            transform.rotation = rotation;
            transform.position = new Vector3(focus.x, 0f, focus.y) - rotation * Vector3.forward * distance;
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
