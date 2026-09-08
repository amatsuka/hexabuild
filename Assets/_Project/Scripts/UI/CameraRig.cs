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
        // Нижняя граница держит гекс крупным: при 6 юнитах он занимает около 160 px из 1080,
        // при 2 — заполняет треть экрана, и это ближний предел разглядывания плитки.
        [SerializeField] float minZoom = 2f;
        [Tooltip("Нижняя граница верхнего предела. Если поле в неё не влезает, предел поднимается сам — до зума, на котором видно всё поле")]
        [SerializeField] float maxZoom = 6f;
        [Tooltip("Во сколько раз можно отъехать дальше зума, на котором поле видно целиком")]
        [SerializeField, Range(1f, 2f)] float zoomOutSlack = 1.3f;
        [SerializeField] float zoomStepPerScroll = 0.6f;

        [Header("Запас хода")]
        [Tooltip("Насколько камера заходит за край поля, в долях свободной от интерфейса части кадра. 0.5 — край поля доезжает до её середины, 1 — до дальней кромки")]
        [SerializeField, Range(0f, 1f)] float panSlack = 0.5f;

        // Наклон камеры к земле. 90 — прежний вид строго сверху, поэтому вся стадия
        // откатывается одним числом.
        [Header("Наклон")]
        [SerializeField, Range(20f, 90f)] float pitch = 55f;
        [Tooltip("Отвод камеры от земли. При ортографии влияет только на клиппинг и дальность теней")]
        [SerializeField] float distance = 20f;

        // Тряска кадра. До M30 камеру не трогали вовсе — игрок ей панит, и дёрганый кадр читался
        // бы как сбой пана. Решением человека 08.09.2026 запрет снят ради одного события:
        // переполнение сожгло накал. Больше её никто не дёргает.
        [Header("Тряска")]
        [Tooltip("Сколько длится удар по кадру")]
        [SerializeField] float shakeSeconds = 0.32f;
        [Tooltip("Размах тряски в долях полукадра: на дальнем зуме мировая единица мельче, а удар обязан читаться так же")]
        [SerializeField, Range(0f, 0.2f)] float shakeShare = 0.045f;

        /// <summary>Запас вокруг поля на самом дальнем зуме: край не должен лежать впритык к кромке.</summary>
        const float FitMargin = 1.04f;

        /// <summary>Больше этой доли кадра одна полоса интерфейса не съедает — иначе кадр вырождается.</summary>
        const float MaxInset = 0.4f;

        /// <summary>Меньше этой доли кадра под поле не остаётся: на ней считается зум «видно всё».</summary>
        const float MinFreeShare = 0.3f;

        Camera cameraComponent;
        Rect fieldBounds = new(-1000f, -1000f, 2000f, 2000f);

        // Полосы интерфейса поверх кадра, в долях его высоты: сверху HUD, снизу панель склада.
        // Кадр под ними показывает то же поле, поэтому клампа считает не весь кадр, а только
        // свободную часть — иначе крайние ряды поля навсегда остаются под панелями.
        float insetTop;
        float insetBottom;

        /// <summary>Зум, на котором поле видно целиком. Считается из габаритов в `SetFieldBounds`.</summary>
        float fitZoom;

        /// <summary>Соотношение сторон, при котором считался `fitZoom`.</summary>
        float fitAspect;

        bool initialized;

        /// <summary>Сколько осталось трясти. Ноль — камера стоит там, где её оставил игрок.</summary>
        float shakeTimer;

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

        /// <summary>Полоса HUD сверху кадра, в мировых единицах глубины.</summary>
        float TopBand => insetTop * 2f * HalfDepth;

        /// <summary>Полоса склада снизу кадра, в мировых единицах глубины.</summary>
        float BottomBand => insetBottom * 2f * HalfDepth;

        /// <summary>Глубина той части кадра, где поле не закрыто интерфейсом.</summary>
        float FreeDepth => Mathf.Max(2f * HalfDepth - TopBand - BottomBand, 0.1f);

        void Awake()
        {
            Initialize();
            ApplyTransform();
        }

        /// <summary>
        /// Удар по кадру. Ходит только вбок: вертикальная тряска на наклонённой камере читается
        /// как скачок поля к наблюдателю, а не как удар, — по той же причине клетки склада трясёт
        /// вбок <see cref="PressPulse.ShakeSideways"/>.
        /// </summary>
        public void Shake() => shakeTimer = shakeSeconds;

        void Update()
        {
            if (shakeTimer <= 0f)
                return;

            // Нескалированное время: hitstop замедляет партию, а удар обязан отыграть свои
            // 0.32 с целиком — иначе он размазывается ровно там, где должен быть резким.
            shakeTimer = Mathf.Max(0f, shakeTimer - Time.unscaledDeltaTime);

            // Последний кадр приходит с нулевым таймером и возвращает камеру на место сам:
            // отдельного «конец тряски» не нужно.
            ApplyTransform();
        }

        /// <summary>Прямоугольник поля по земле: x — вправо, y — вглубь. За него камера не выходит.</summary>
        public void SetFieldBounds(Rect bounds)
        {
            Initialize();
            fieldBounds = bounds;
            fitAspect = Cam.aspect;
            fitZoom = FitZoom(bounds);
            // Через зум, а не сразу клампой: новые габариты меняют и предел зума, и текущий
            // размер кадра может оказаться за ним.
            Zoom(0f);
        }

        /// <summary>
        /// Полосы интерфейса поверх кадра — HUD сверху, панель склада снизу, в долях высоты
        /// экрана. Камера доводит поле до их кромки, а не до кромки кадра, и на дальнем зуме
        /// вмещает поле в свободный просвет между ними. Пересказывать при смене размера окна.
        /// </summary>
        public void SetViewportInsets(float topShare, float bottomShare)
        {
            Initialize();
            insetTop = Mathf.Clamp(topShare, 0f, MaxInset);
            insetBottom = Mathf.Clamp(bottomShare, 0f, MaxInset);
            fitZoom = FitZoom(fieldBounds);
            Zoom(0f);
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

                return Mathf.Max(maxZoom, fitZoom * zoomOutSlack);
            }
        }

        /// <summary>
        /// Зум, при котором поле помещается и по ширине, и по глубине. По глубине кадр короче
        /// в `Foreshortening` раз: земля наклонена, и в ту же высоту экрана её влезает меньше.
        /// Считается по свободному просвету, а не по всему кадру: под полосами интерфейса поле
        /// хоть и нарисовано, но не видно.
        /// </summary>
        float FitZoom(Rect bounds)
        {
            var freeShare = Mathf.Max(1f - insetTop - insetBottom, MinFreeShare);
            var byWidth = bounds.width / (2f * Mathf.Max(Cam.aspect, 0.01f));
            var byDepth = bounds.height * Foreshortening * 0.5f / freeShare;
            return Mathf.Max(byWidth, byDepth) * FitMargin;
        }

        /// <summary>Поставить камеру к ближнему краю поля, где стоит Метрополия.</summary>
        public void FocusOnBottom()
        {
            Initialize();
            focus = new Vector2(fieldBounds.center.x, fieldBounds.yMin + HalfDepth - BottomBand);
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
            var halfWidth = Cam.orthographicSize * Cam.aspect;
            var depthSlack = FreeDepth * panSlack;
            var widthSlack = halfWidth * 2f * panSlack;

            focus = new Vector2(
                ClampAxis(focus.x, fieldBounds.xMin, fieldBounds.xMax, halfWidth, widthSlack, widthSlack),
                ClampAxis(focus.y, fieldBounds.yMin, fieldBounds.yMax, HalfDepth,
                    BottomBand + depthSlack, TopBand + depthSlack));

            ApplyTransform();
        }

        /// <summary>Камера садится на луч, выходящий из точки фокуса против направления взгляда.</summary>
        void ApplyTransform()
        {
            var rotation = Quaternion.Euler(pitch, 0f, 0f);
            transform.rotation = rotation;
            transform.position = new Vector3(focus.x, 0f, focus.y)
                - rotation * Vector3.forward * distance
                + rotation * Vector3.right * ShakeOffset();
        }

        /// <summary>
        /// Смещение тряски вбок, в мировых единицах. Размах считается от текущего зума, а не
        /// задан числом: одна и та же мировая амплитуда на ближнем зуме сносит поле на полэкрана,
        /// а на дальнем не видна вовсе.
        /// </summary>
        float ShakeOffset() =>
            shakeTimer <= 0f
                ? 0f
                : Anim.Shake(1f - shakeTimer / shakeSeconds) * Cam.orthographicSize * shakeShare;

        /// <summary>
        /// Предел фокуса по оси. `half` — полкадра, `lowMargin` и `highMargin` — насколько
        /// камере разрешено уйти за край поля с каждой стороны: там полоса интерфейса, за
        /// которой поле не видно, плюс запас хода. При запасе в половину просвета промежуток
        /// вырождается в сам отрезок поля: фокус ходит от края до края независимо от зума,
        /// то есть карту можно утащить до середины экрана. Меньший запас на дальнем зуме
        /// промежуток схлопывает — тогда поле встаёт по центру просвета.
        /// </summary>
        static float ClampAxis(float value, float min, float max, float half, float lowMargin, float highMargin)
        {
            var low = min + half - lowMargin;
            var high = max - half + highMargin;

            return low > high ? (low + high) * 0.5f : Mathf.Clamp(value, low, high);
        }
    }
}
