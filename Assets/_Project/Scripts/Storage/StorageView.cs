using System;
using System.Collections;
using System.Collections.Generic;
using Game.Economy;
using Game.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Storage
{
    /// <summary>
    /// Полоса склада внизу: сетка клеток и красная вспышка при потере ресурса. Панель и клетки
    /// собраны той же карточкой, что и HUD, — тень, светлая кромка, тёмная заливка.
    ///
    /// Он же показывает накал (M29): рамка греется цветом по ступени, над складом стоит текущий
    /// множитель партии, под складом — полоса паузы, после которой накал потечёт. Множитель был
    /// в игре и раньше, но виден был только в гаснущей плашке прибавки, и склад читался
    /// бухгалтерией, а не ставкой.
    /// </summary>
    public sealed class StorageView : MonoBehaviour
    {
        [SerializeField] ResourcePalette palette;
        [Tooltip("Модели добываемых ресурсов. Пусто — клетки рисуют полигоны, как было")]
        [SerializeField] ResourceModels models = new();
        [Tooltip("Сторона снимка модели в пикселях")]
        [SerializeField] int snapshotResolution = 256;
        [Tooltip("Разворот модели в снимке")]
        [SerializeField] Vector3 snapshotAngles = new(25f, 35f, 0f);
        [Tooltip("Запас вокруг модели в снимке, доля габарита")]
        [SerializeField, Range(1f, 2f)] float snapshotMargin = 1.2f;
        [SerializeField] UiTheme theme = new();
        [Tooltip("Во что красится вся панель на потере ресурса. Белый — цвет без изменений")]
        [SerializeField] Color lossFlashTint = new(1f, 0.30f, 0.26f, 1f);
        [SerializeField] float flashSeconds = 0.4f;
        [SerializeField] int columns = 8;
        [SerializeField] float cellSize = 88f;
        [SerializeField] float spacing = 8f;
        [SerializeField] float padding = 12f;

        [Header("Отклик")]
        [Tooltip("Размах тряски клетки на отказе, пиксели канваса")]
        [SerializeField] float refusalShake = 14f;

        [Header("Накал")]
        [Tooltip("Во что красится панель на полном накале. Белый — цвет без изменений")]
        [SerializeField] Color heatTint = new(1f, 0.74f, 0.42f, 1f);
        [Tooltip("Во что вспыхивает склад на премии за чистоту")]
        [SerializeField] Color sweepFlashTint = new(1f, 0.88f, 0.45f, 1f);
        [SerializeField] float sweepSeconds = 0.5f;
        [Tooltip("Насколько склад раздувается на премии")]
        [SerializeField] float sweepScale = 1.04f;
        [Tooltip("Кегль числа множителя на нулевом и на полном накале")]
        [SerializeField] float factorSizeCold = 34f;
        [SerializeField] float factorSizeHot = 50f;
        [Tooltip("Просвет между числом множителя и верхом склада")]
        [SerializeField] float factorGap = 10f;
        [Tooltip("Кнопка «Продать всё» над складом справа: ширина, высота и кегль подписи")]
        [SerializeField] float sellWidth = 232f;
        [SerializeField] float sellHeight = 62f;
        [SerializeField] float sellFontSize = 30f;

        [Tooltip("Высота полосы утечки под складом и её просвет от панели")]
        [SerializeField] float fuseHeight = 6f;
        [SerializeField] float fuseGap = 5f;

        [Header("Тревога и сгорание")]
        [Tooltip("При скольких свободных клетках склад начинает тревожиться. Тревога идёт до переполнения, а не после него")]
        [SerializeField] int alarmFreeCells = 3;

        [Tooltip("Цвет тревожной пульсации на свободных клетках")]
        [SerializeField] Color alarmTint = new(1f, 0.40f, 0.32f, 1f);

        [Tooltip("Пульсаций в секунду, когда свободна последняя клетка. На трёх свободных втрое реже")]
        [SerializeField] float alarmSpeed = 1.1f;

        [Tooltip("Сколько длится вспышка сгоревшего накала")]
        [SerializeField] float burnSeconds = 0.34f;

        [Tooltip("Цвет вспышки: карточка выбеливается, а не краснеет — красное уже занято потерей ресурса")]
        [SerializeField] Color burnTint = new(1f, 0.90f, 0.78f, 0.85f);

        [Tooltip("На сколько пикселей падает число множителя, пока гаснет")]
        [SerializeField] float burnDrop = 28f;

        [Header("Каждое действие платит")]
        [Tooltip("Выше какой доли накала кромка склада становится горячей")]
        [SerializeField, Range(0f, 1f)] float hotEdgeShare = 0.7f;

        [Tooltip("Сколько живёт призрак прибавки накала и на сколько пикселей он всплывает")]
        [SerializeField] float ghostSeconds = 0.55f;
        [SerializeField] float ghostRise = 62f;
        [Tooltip("Снос призрака вбок: над клеткой уже стоит плашка прибавки очков")]
        [SerializeField] float ghostDrift = 30f;
        [SerializeField] float ghostSize = 26f;

        [Tooltip("Сколько живёт кольцо ударной волны и во сколько раз оно разрастается")]
        [SerializeField] float ringSeconds = 0.3f;
        [SerializeField] float ringScale = 2.4f;

        [Header("Истечение паузы")]
        [Tooltip("Сколько живёт искра на голове полосы утечки и во сколько раз она разрастается")]
        [SerializeField] float sparkSeconds = 0.32f;
        [SerializeField] float sparkScale = 3.4f;
        [Tooltip("Размах и длительность дрожи числа множителя, когда накал потёк")]
        [SerializeField] float factorShake = 7f;
        [SerializeField] float factorShakeSeconds = 0.3f;

        [Header("Волна премии")]
        [Tooltip("Задержка волны на клетку расстояния от той, которой склад дочистили")]
        [SerializeField] float sweepStepSeconds = 0.045f;
        [Tooltip("Сколько горит одна клетка в волне")]
        [SerializeField] float sweepCellSeconds = 0.3f;


        [Header("Подсветка обучения")]
        [Tooltip("Насколько ободок цели притухает между вспышками")]
        [SerializeField, Range(0f, 1f)] float highlightFloor = 0.25f;
        [SerializeField] float highlightSpeed = 3.2f;

        [Header("Анимация слияния")]
        [SerializeField] float flySeconds = 0.28f;
        [SerializeField] float popSeconds = 0.18f;
        [Tooltip("Амплитуда удара по клетке, из которой крафт ушёл в очки")]
        [SerializeField] float popScale = 1.3f;
        [SerializeField] float flyEndScale = 0.45f;

        /// <summary>
        /// Сколько призраков и колец держим наготове. Больше на экране и не бывает: призрак
        /// живёт полсекунды, а быстрее четырёх действий в секунду по складу не кликают.
        /// </summary>
        const int GhostCount = 4;

        const int RingCount = 3;

        readonly HashSet<int> pendingCells = new();

        /// <summary>Клетки, на которые указывает обучение. Пусто — подсветки нет.</summary>
        readonly List<int> highlightedCells = new();

        UiPanelGraphic panel;
        RectTransform[] cells;
        UiPanelGraphic[] cellPanels;
        UiPanelGraphic[] cellRings;
        ResourceIcon[] icons;
        StorageGrid grid;
        ScoreMultiplier multiplier;
        ResourceIconBaker snapshots;
        float flashTimer;
        float sweepTimer;
        float burnTimer;
        float alarmPhase;
        bool alarmOn;
        bool hotEdge;

        /// <summary>Время от начала волны премии. Отрицательное — волны нет.</summary>
        float sweepWave = -1f;

        /// <summary>Клетка, которой склад дочистили: из неё волна и расходится.</summary>
        int sweepOrigin;

        /// <summary>Пауза до утечки ещё не вышла на прошлом кадре: по этому и ловится её конец.</summary>
        bool holding;

        float factorShakeTimer;

        /// <summary>Клетка под пальцем: её цвет ведёт нажатие, и тревога в него не лезет.</summary>
        int pressedCell = -1;

        UiPanelGraphic burnPanel;

        /// <summary>
        /// Призраки прибавки и кольца ударной волны заведены штуками и переиспользуются,
        /// а не создаются на каждое действие. Причина не в экономии объектов, а в TMP:
        /// `UiText` правит `fontMaterial`, то есть на каждую созданную строку заводится свой
        /// инстанс материала, — а действий на складе несколько в секунду.
        /// </summary>
        TextMeshProUGUI[] ghosts;

        Coroutine[] ghostRuns;
        int ghostCursor;

        UiPanelGraphic[] rings;
        Coroutine[] ringRuns;
        int ringCursor;

        RectTransform spark;
        UiPanelGraphic sparkPanel;
        Coroutine sparkRun;

        TextMeshProUGUI factor;
        UiButton sellButton;

        /// <summary>Кнопка сейчас в режиме подсказки конца партии: цвет и подпись уже стоят.</summary>
        bool sellHint;

        RectTransform fuse;
        RectTransform fuseFill;
        UiPanelGraphic fuseFillPanel;

        /// <summary>Кнопку «Продать всё» нажали. Что она продаёт, знает партия, а не склад.</summary>
        public event Action SellRequested;

        /// <summary>
        /// Кнопка на экране, и в каком она виде. Пока партия идёт — холодная «Продать всё»;
        /// когда на поле не осталось хода — тёплая «Забрать всё»: это и есть подсказка, что
        /// докликивать хвост партии не надо, и она приходит и на пройденном поле, и в тупике.
        /// </summary>
        public void ShowSellButton(bool visible, bool endOfGame)
        {
            if (sellButton == null)
                return;

            sellButton.Visible = visible;
            if (!visible || endOfGame == sellHint)
                return;

            sellHint = endOfGame;
            sellButton.Restyle(
                theme,
                endOfGame ? theme.ButtonPrimary : theme.ButtonSecondary,
                endOfGame ? "Забрать всё" : "Продать всё");
        }

        /// <summary>Прямоугольник кнопки: над ним встаёт подсказка обучения про резерв.</summary>
        public RectTransform SellRect => sellButton?.Rect;

        /// <summary>
        /// Палитра склада: по ней обучение красит свою кнопку «Пропустить». Она стоит над
        /// складом и обязана выглядеть его частью, а не гостьей с чужого экрана.
        /// </summary>
        public UiTheme Theme => theme;

        /// <summary>Панель склада целиком: над ней встают подсказки обучения про склад.</summary>
        public RectTransform PanelRect => panel != null ? panel.rectTransform : (RectTransform)transform;

        /// <summary>
        /// Клетки, на которые указывает обучение: над ними горит ободок. Масштабом клетки
        /// подсветку не сделать — его уже занимают слияние, удар по клетке и пружина нажатия.
        /// </summary>
        public void Highlight(IReadOnlyList<int> indices)
        {
            if (cellRings == null)
                return;

            foreach (var index in highlightedCells)
                cellRings[index].gameObject.SetActive(false);

            highlightedCells.Clear();
            highlightedCells.AddRange(indices);

            foreach (var index in highlightedCells)
                Ring(index).gameObject.SetActive(true);
        }

        /// <summary>
        /// Ободок клетки заводится по требованию и остаётся её ребёнком: так он едет вместе
        /// с клеткой на всех её анимациях и не считает своё место сам.
        /// </summary>
        UiPanelGraphic Ring(int index)
        {
            if (cellRings[index] != null)
                return cellRings[index];

            var ring = UiPanel.Create($"Highlight {index}", cells[index], theme, theme.Ring(cellSize));
            ring.rectTransform.Stretch();
            return cellRings[index] = ring;
        }

        public bool TrySellPress(Vector2 screenPosition) =>
            sellButton != null && sellButton.TryPress(screenPosition);

        public void ReleaseSellPress() => sellButton?.Release();

        public bool TrySellClick(Vector2 screenPosition) =>
            sellButton != null && sellButton.TryClick(screenPosition);

        public void Bind(StorageGrid storage, ScoreMultiplier score)
        {
            grid = storage;
            multiplier = score;
            snapshots = new ResourceIconBaker(models, snapshotResolution, snapshotAngles, snapshotMargin);
            BuildPanel();

            grid.Changed += Refresh;
            grid.ResourceLost += OnResourceLost;
            Refresh();
        }

        /// <summary>
        /// Клетка занята доставкой, но показывать её рано: ресурс ещё летит с Метрополии.
        /// </summary>
        public void HoldCell(int index)
        {
            pendingCells.Add(index);
            Refresh();
        }

        /// <summary>Палец лёг на клетку: она проседает, как кнопка.</summary>
        public void PressCell(int index)
        {
            pressedCell = index;
            PressPulse.HoldCard(cellPanels[index]);
        }

        /// <summary>Палец снят с клетки.</summary>
        public void ReleasePress(int index)
        {
            if (pressedCell == index)
                pressedCell = -1;

            PressPulse.Release(cellPanels[index]);
        }

        /// <summary>Отказ по клетке: она коротко дрожит поперёк.</summary>
        public void ShakeCell(int index) => PressPulse.ShakeSideways(cellPanels[index], refusalShake);

        /// <summary>Ресурс приземлился: клетка проявляется и выскакивает масштабом.</summary>
        public void ReleaseCell(int index)
        {
            if (!pendingCells.Remove(index))
                return;

            Refresh();

            if (!isActiveAndEnabled || !grid[index].HasValue)
                return;

            // Пружина нажатия пишет тот же масштаб: не оборви её — и она перебьёт выскакивание.
            PressPulse.Cancel(cellPanels[index]);
            cells[index].localScale = Vector3.zero;
            StartCoroutine(PopCells(new[] { index }));
        }

        /// <summary>
        /// Слияние: копии списанных ресурсов слетаются в клетку результата, затем сам результат
        /// выскакивает масштабом. Чистый визуал — склад к этому моменту уже пересчитан.
        /// </summary>
        public void PlayMerge(IReadOnlyList<int> consumedCells, IReadOnlyList<int> resultCells, ResourceType movedType)
        {
            if (resultCells.Count == 0 || !isActiveAndEnabled)
                return;

            PlayHeatGain(resultCells[0]);
            StartCoroutine(AnimateMerge(consumedCells, resultCells, movedType));
        }

        IEnumerator AnimateMerge(IReadOnlyList<int> consumedCells, IReadOnlyList<int> resultCells, ResourceType movedType)
        {
            var target = cells[resultCells[0]].position;
            var flying = new List<RectTransform>(consumedCells.Count);
            var origins = new List<Vector3>(consumedCells.Count);
            foreach (var index in consumedCells)
            {
                // Списанная клетка сейчас опустеет, и держать на ней пружину нажатия не за что.
                PressPulse.Cancel(cellPanels[index]);
                var origin = cells[index].position;
                origins.Add(origin);
                flying.Add(CreateFlyingCopy(origin, movedType));
            }

            foreach (var index in resultCells)
            {
                PressPulse.Cancel(cellPanels[index]);
                cells[index].localScale = Vector3.zero;
            }

            for (var elapsed = 0f; elapsed < flySeconds; elapsed += Time.deltaTime)
            {
                var progress = Mathf.SmoothStep(0f, 1f, elapsed / flySeconds);
                for (var i = 0; i < flying.Count; i++)
                {
                    flying[i].position = Vector3.Lerp(origins[i], target, progress);
                    flying[i].localScale = Vector3.one * Mathf.Lerp(1f, flyEndScale, progress);
                }

                yield return null;
            }

            foreach (var copy in flying)
                Destroy(copy.gameObject);

            // Кольцо идёт не с клика, а отсюда: копии только что сошлись в одну точку, и удар
            // случается здесь. На клике его перебила бы сама сходка.
            PlayRing(resultCells[0]);

            yield return PopCells(resultCells);
        }

        /// <summary>
        /// Клетка выскакивает из нуля пружиной: <see cref="Anim.OutBack"/> уходит за единицу и
        /// оседает обратно. Линейный домик «вверх и вниз» читался механическим — это был подъём
        /// с изломом на пике, а не рывок. Размах перелёта теперь задаёт сама кривая, а
        /// <see cref="popScale"/> остался амплитудой удара по опустевшей клетке.
        /// </summary>
        IEnumerator PopCells(IReadOnlyList<int> popped)
        {
            for (var elapsed = 0f; elapsed < popSeconds; elapsed += Time.deltaTime)
            {
                var scale = Anim.OutBack(elapsed / popSeconds);

                foreach (var index in popped)
                    cells[index].localScale = Vector3.one * scale;

                yield return null;
            }

            foreach (var index in popped)
                cells[index].localScale = Vector3.one;
        }

        /// <summary>
        /// Обмен крафта на очки: опустевшая клетка коротко толкается подскоком. Выскакивать ей
        /// неоткуда — она пустеет, а не наполняется, — поэтому это удар от единицы, а не рост
        /// из нуля.
        /// </summary>
        public void PunchCell(int index)
        {
            if (!isActiveAndEnabled)
                return;

            PressPulse.Cancel(cellPanels[index]);
            PlayHeatGain(index);
            StartCoroutine(Punch(index));
        }

        IEnumerator Punch(int index)
        {
            for (var elapsed = 0f; elapsed < popSeconds; elapsed += Time.deltaTime)
            {
                cells[index].localScale =
                    Vector3.one * (1f + (popScale - 1f) * Anim.Hop(elapsed / popSeconds));
                yield return null;
            }

            cells[index].localScale = Vector3.one;
        }

        /// <summary>
        /// Летящая копия висит на канвасе, а не на панели: у панели `GridLayoutGroup`, он бы
        /// растащил её обратно по клеткам сетки.
        /// </summary>
        RectTransform CreateFlyingCopy(Vector3 position, ResourceType type)
        {
            var copy = new GameObject("MergeFly", typeof(RectTransform), typeof(CanvasRenderer), typeof(ResourceIcon));
            var rect = (RectTransform)copy.transform;
            rect.SetParent(transform.parent, false);
            rect.SetAsLastSibling();
            rect.sizeDelta = Vector2.one * cellSize;
            rect.position = position;
            ShowIcon(copy.GetComponent<ResourceIcon>(), type);
            return rect;
        }

        /// <summary>Клетка склада под экранной точкой: панель сама разбирает клики по себе.</summary>
        public bool TryGetCellIndex(Vector2 screenPosition, out int index)
        {
            for (index = 0; index < cells.Length; index++)
                if (RectTransformUtility.RectangleContainsScreenPoint(cells[index], screenPosition))
                    return true;

            index = -1;
            return false;
        }

        /// <summary>
        /// Точка клетки на канвасе: отсюда сданный ресурс улетает в карточку контракта.
        /// Мировая точка сцены для этого не годится — карточка живёт в канвасе, а не на поле.
        /// </summary>
        public Vector3 CellPoint(int index) => cells[index].position;

        /// <summary>Прямоугольник клетки: над ним всплывает попап того, что с ней сделали.</summary>
        public RectTransform CellRect(int index) => cells[index];

        /// <summary>Мировая точка клетки: туда прыгает доехавший до Метрополии ресурс.</summary>
        public Vector3 CellWorldPoint(int index, Camera worldCamera)
        {
            // Камера наклонена, поэтому цель прыжка берётся пересечением луча с землёй,
            // а не отсчётом по глубине камеры.
            var screenPoint = RectTransformUtility.WorldToScreenPoint(null, cells[index].position);
            var ray = worldCamera.ScreenPointToRay(screenPoint);
            return new Plane(Vector3.up, Vector3.zero).Raycast(ray, out var distance)
                ? ray.GetPoint(distance)
                : Vector3.zero;
        }

        /// <summary>Высота панели в пикселях экрана — по ней камера отодвигается от низа поля.</summary>
        public float PanelHeightPixels
        {
            get
            {
                var corners = new Vector3[4];
                ((RectTransform)transform).GetWorldCorners(corners);
                return corners[1].y - corners[0].y;
            }
        }

        /// <summary>Точка попала в панель склада, а не в поле за ней.</summary>
        public bool ContainsScreenPoint(Vector2 screenPosition) =>
            RectTransformUtility.RectangleContainsScreenPoint((RectTransform)transform, screenPosition);

        void OnDestroy()
        {
            snapshots?.Dispose();

            if (grid == null)
                return;

            grid.Changed -= Refresh;
            grid.ResourceLost -= OnResourceLost;
        }

        /// <summary>
        /// Премия за чистый склад: сам склад коротко раздувается, а золото идёт волной по
        /// клеткам от той, которой его дочистили. Одновременная вспышка всей панели говорила
        /// «что-то случилось», волна говорит «вот это ты и разгрёб» — и показывает пустоту,
        /// за которую заплатили, клетка за клеткой.
        /// </summary>
        public void PlaySweep(int cell)
        {
            sweepTimer = sweepSeconds;
            sweepWave = 0f;
            sweepOrigin = cell;
        }

        /// <summary>
        /// Накал сгорел на переполнении: карточка выбеливается вспышкой, а число множителя
        /// падает вниз и гаснет — оно и есть то, что игрок только что потерял. Красное здесь
        /// не годится: красным уже мигает потеря самого ресурса, и два разных убытка одним
        /// цветом слились бы в один.
        /// </summary>
        public void PlayBurn()
        {
            if (burnPanel == null)
                return;

            burnTimer = burnSeconds;
            burnPanel.gameObject.SetActive(true);
        }

        /// <summary>
        /// Действие на складе заплатило накалом: от клетки вылетает призрачное «+0.05» и тает.
        /// До M30 каждое действие платило молча — накал рос числом, которого игрок не связывал
        /// со своим кликом. На потолке призрака нет вовсе: там действие не платит ничего, и
        /// нарисовать прибавку значило бы соврать.
        /// </summary>
        void PlayHeatGain(int cell)
        {
            var gain = multiplier?.LastBump ?? 0f;
            if (!isActiveAndEnabled || gain <= 0f || ghosts == null)
                return;

            var slot = ghostCursor;
            ghostCursor = (ghostCursor + 1) % ghosts.Length;

            if (ghostRuns[slot] != null)
                StopCoroutine(ghostRuns[slot]);

            ghosts[slot].text = HudFormat.Bonus(gain);
            ghostRuns[slot] = StartCoroutine(FloatGhost(slot, LocalPointOf(cells[cell])));
        }

        IEnumerator FloatGhost(int slot, Vector3 from)
        {
            var ghost = ghosts[slot];
            ghost.gameObject.SetActive(true);

            for (var elapsed = 0f; elapsed < ghostSeconds; elapsed += Time.deltaTime)
            {
                var progress = elapsed / ghostSeconds;

                // Вверх с замедлением, вбок ровно: так призрак читается всплывающим, а не
                // брошенным. Вбок — потому что прямо над клеткой стоит плашка прибавки очков.
                ghost.rectTransform.localPosition = from + new Vector3(
                    ghostDrift * progress, ghostRise * Mathf.Sqrt(progress), 0f);

                var color = theme.Gold;
                color.a = 1f - progress * progress;
                ghost.color = color;
                yield return null;
            }

            ghost.gameObject.SetActive(false);
        }

        /// <summary>
        /// Кольцо ударной волны от точки слияния: кромка без заливки, разрастается масштабом
        /// и гаснет. Масштабом, а не размером: радиус скругления и толщина кромки живут в
        /// пикселях меша, и растущий прямоугольник перестал бы быть кругом на втором кадре.
        /// </summary>
        void PlayRing(int cell)
        {
            if (!isActiveAndEnabled || rings == null)
                return;

            var slot = ringCursor;
            ringCursor = (ringCursor + 1) % rings.Length;

            if (ringRuns[slot] != null)
                StopCoroutine(ringRuns[slot]);

            rings[slot].rectTransform.localPosition = LocalPointOf(cells[cell]);
            ringRuns[slot] = StartCoroutine(ExpandRing(slot));
        }

        IEnumerator ExpandRing(int slot)
        {
            var ring = rings[slot];
            ring.gameObject.SetActive(true);

            for (var elapsed = 0f; elapsed < ringSeconds; elapsed += Time.deltaTime)
            {
                var progress = elapsed / ringSeconds;
                ring.rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, ringScale, Mathf.Sqrt(progress));
                ring.color = new Color(1f, 1f, 1f, 1f - progress);
                yield return null;
            }

            ring.gameObject.SetActive(false);
        }

        /// <summary>
        /// Пауза вышла, накал потёк: искра на голове полосы и дрожь числа множителя. Окно в
        /// 2.5 с игрок выучивает только тогда, когда слышен его конец, — до M30 полоса молча
        /// доезжала до нуля, и утечка начиналась ниоткуда.
        /// </summary>
        void PlayLeak()
        {
            factorShakeTimer = factorShakeSeconds;

            if (!isActiveAndEnabled || spark == null)
                return;

            if (sparkRun != null)
                StopCoroutine(sparkRun);

            // Голова полосы там, где её застало истечение: это край заливки, а он к этому
            // моменту доехал до левого конца жёлоба.
            spark.anchoredPosition = new Vector2((multiplier.HoldShare - 0.5f) * fuse.sizeDelta.x, 0f);
            sparkRun = StartCoroutine(FlashSpark());
        }

        IEnumerator FlashSpark()
        {
            spark.gameObject.SetActive(true);

            for (var elapsed = 0f; elapsed < sparkSeconds; elapsed += Time.deltaTime)
            {
                var progress = elapsed / sparkSeconds;
                spark.localScale = Vector3.one * Mathf.Lerp(1f, sparkScale, progress);
                sparkPanel.color = new Color(heatTint.r, heatTint.g, heatTint.b, 1f - progress);
                yield return null;
            }

            spark.gameObject.SetActive(false);
        }

        /// <summary>
        /// Точка клетки в координатах слоя, на котором живут призраки и кольца. Мировая точка
        /// им не годится: смещения заданы в пикселях канваса, а канвас масштабируется под экран.
        /// </summary>
        Vector3 LocalPointOf(RectTransform cell) => transform.parent.InverseTransformPoint(cell.position);

        void Update()
        {
            // Склад стоит в сцене с её загрузки, а собирается только в `Bind`: до первой партии
            // красить и обновлять нечего. Раньше это сходило с рук — вспышка потери начиналась
            // с нуля и до панели не доходила, — а нагрев по накалу идёт каждый кадр.
            if (panel == null)
                return;

            TickPanelTint();
            TickHeat();
            TickSweepWave();
            TickAlarm();
            TickBurn();
            TickHighlight();
            PlaceFactor();
        }

        /// <summary>Ободки подсвеченных клеток дышат прозрачностью: в канвасе альфа работает.</summary>
        void TickHighlight()
        {
            if (highlightedCells.Count == 0)
                return;

            var pulse = Mathf.Lerp(
                highlightFloor, 1f, (Mathf.Sin(Time.time * highlightSpeed) + 1f) * 0.5f);

            foreach (var index in highlightedCells)
                cellRings[index].color = new Color(1f, 1f, 1f, pulse);
        }

        /// <summary>
        /// Волна премии: клетка загорается тем позже, чем дальше она от той, которой склад
        /// дочистили. Расстояние считается по сетке, а не по номеру клетки: соседний ряд — это
        /// соседняя клетка глазами, а по номеру он в восьми шагах.
        ///
        /// Идёт волна **до** тревоги: тревога — про угрозу, и перебивать её праздником нельзя.
        /// Практически они не встречаются — премию платят на разгребённом складе, — но порядок
        /// здесь смысловой, а не случайный.
        /// </summary>
        void TickSweepWave()
        {
            if (sweepWave < 0f)
                return;

            sweepWave += Time.deltaTime;
            var running = false;

            for (var i = 0; i < cellPanels.Length; i++)
            {
                if (i == pressedCell || PressPulse.IsBusy(cellPanels[i]))
                    continue;

                var progress = (sweepWave - CellDistance(i, sweepOrigin) * sweepStepSeconds) / sweepCellSeconds;
                if (progress >= 1f)
                {
                    cellPanels[i].color = Color.white;
                    continue;
                }

                running = true;
                if (progress > 0f)
                    cellPanels[i].color = Color.Lerp(Color.white, sweepFlashTint, Anim.Hop(progress));
            }

            if (!running)
                sweepWave = -1f;
        }

        /// <summary>Сколько шагов по сетке между клетками: столько раз волна и задержится.</summary>
        int CellDistance(int from, int to) =>
            Mathf.Abs(from % columns - to % columns) + Mathf.Abs(from / columns - to / columns);

        /// <summary>
        /// Место числа множителя: просвет над складом, падение на сгорании и дрожь на истечении
        /// паузы пишут одну и ту же координату, и складывать их приходится в одном месте — иначе
        /// последний в кадре стирает работу остальных.
        /// </summary>
        void PlaceFactor()
        {
            var shake = 0f;
            if (factorShakeTimer > 0f)
            {
                factorShakeTimer = Mathf.Max(0f, factorShakeTimer - Time.deltaTime);
                shake = Anim.Shake(1f - factorShakeTimer / factorShakeSeconds) * factorShake;
            }

            var drop = burnTimer > 0f ? burnDrop * (1f - burnTimer / burnSeconds) : 0f;
            factor.rectTransform.anchoredPosition = new Vector2(shake, factorGap - drop);
        }

        /// <summary>
        /// Тревога склада. Пульсируют **свободные** клетки, потому что кончаются именно они, и
        /// идёт она **до** переполнения, а не после: после терять уже нечего, а весь смысл в
        /// том, чтобы игрок успел разгрести склад и не сжечь накал. До M30 угроза была не видна
        /// вовсе — первым сигналом был улетевший ресурс, то есть уже случившийся убыток.
        ///
        /// Цвет пишется только тем клеткам, которые его меняют: `Graphic.color` на равном
        /// значении не трогает меш, а тревога по определению зажигается на двух-трёх клетках
        /// из двадцати четырёх.
        /// </summary>
        void TickAlarm()
        {
            var free = grid.Capacity - grid.Count;
            var alarmed = free > 0 && free <= alarmFreeCells;

            if (!alarmed)
            {
                if (!alarmOn)
                    return;

                alarmOn = false;
                alarmPhase = 0f;
                foreach (var cell in cellPanels)
                    if (!PressPulse.IsBusy(cell))
                        cell.color = Color.white;

                return;
            }

            alarmOn = true;

            // Чем меньше осталось, тем чаще пульс: на последней клетке он втрое быстрее, чем
            // на трёх свободных. Ровный пульс сообщал бы «склад полнеет», а нужно «сейчас будет».
            alarmPhase += Time.deltaTime * alarmSpeed * (alarmFreeCells + 1 - free);
            var pulse = 0.5f - 0.5f * Mathf.Cos(alarmPhase * Mathf.PI * 2f);
            var tint = Color.Lerp(Color.white, alarmTint, pulse);

            for (var i = 0; i < cellPanels.Length; i++)
            {
                if (i == pressedCell || PressPulse.IsBusy(cellPanels[i]))
                    continue;

                cellPanels[i].color = grid[i].HasValue ? Color.white : tint;
            }
        }

        /// <summary>
        /// Вспышка сгорания — своим слоем поверх клеток, а не вершинным цветом карточки, как
        /// нагрев и потеря. Причина техническая и стоит того, чтобы её тут записать: вершинный
        /// цвет uGUI лежит в `Color32`, выше единицы он не поднимается, и «ярче обычного» им не
        /// сказать вовсе — белый тинт это ровно карточка в своём цвете. Поднять карточку выше
        /// её собственной яркости можно только чем-то, что лежит сверху.
        ///
        /// Слой гасится целиком, когда отыграл: лишний прозрачный прямоугольник во весь склад
        /// каждый кадр — это overdraw ни за чем.
        /// </summary>
        void TickBurn()
        {
            if (burnTimer <= 0f)
                return;

            // Нескалированное время: сгорание совпадает с hitstop по времени, а вспышка обязана
            // отыграть свои доли секунды целиком.
            burnTimer = Mathf.Max(0f, burnTimer - Time.unscaledDeltaTime);

            if (burnTimer <= 0f)
            {
                burnPanel.gameObject.SetActive(false);
                return;
            }

            var progress = 1f - burnTimer / burnSeconds;
            burnPanel.color = new Color(burnTint.r, burnTint.g, burnTint.b, burnTint.a * Anim.Hop(progress));

            // Число уже пересчитано `TickHeat` на этом же кадре — здесь оно только гаснет,
            // а падает его в `PlaceFactor`. Порядок в `Update` на это и рассчитан.
            var faded = factor.color;
            faded.a = 1f - progress;
            factor.color = faded;
        }

        /// <summary>
        /// Цвет панели — один канал на три источника: нагрев по накалу, золотая вспышка премии
        /// и красная вспышка потери. Порядок обратный их громкости: потеря перебивает премию,
        /// премия — нагрев. Красится вершинным цветом, то есть вся карточка разом: шейдер
        /// домножает на него и градиент, и кромку, и свечение. Белый — панель в своём цвете.
        /// </summary>
        void TickPanelTint()
        {
            var heat = multiplier?.HeatShare ?? 0f;
            var tint = Color.Lerp(Color.white, heatTint, heat);

            // Раздутие склада на премии. Цвет его больше не сопровождает: золото ушло волной
            // по клеткам, а панель целиком вспыхивала мимо того, за что премию дали.
            if (sweepTimer > 0f)
            {
                sweepTimer = Mathf.Max(0f, sweepTimer - Time.deltaTime);
                transform.localScale = sweepTimer > 0f
                    ? Vector3.one * Mathf.Lerp(1f, sweepScale, Anim.Hop(1f - sweepTimer / sweepSeconds))
                    : Vector3.one;
            }

            if (flashTimer > 0f)
            {
                flashTimer = Mathf.Max(0f, flashTimer - Time.deltaTime);
                tint = Color.Lerp(tint, lossFlashTint, flashTimer / flashSeconds);
            }

            panel.color = tint;

            // Выше порога у карточки меняется не тинт, а сама кромка: вершинный цвет лежит в
            // `Color32` и ярче собственного цвета карточку не сделает. Материал переставляется
            // на переходе, а не каждый кадр, — их всего два и меняются они на ступени.
            var hot = heat >= hotEdgeShare;
            if (hot == hotEdge)
                return;

            hotEdge = hot;
            panel.Apply(theme.PanelShader, hot ? theme.CardHot : theme.Card);
        }

        /// <summary>
        /// Число множителя и полоса утечки. Оба идут по времени, а не по событию: пауза до
        /// утечки тикает сама, и подписка на `Changed` о ней ничего не знает.
        /// </summary>
        void TickHeat()
        {
            if (multiplier == null)
                return;

            var heat = multiplier.HeatShare;

            // Пауза кончилась ровно сейчас: это и есть окно 2.5 с, которое игрок выучивает
            // только тогда, когда его конец слышно. Полоса до этого молча доезжала до нуля.
            var held = multiplier.HoldShare > 0f;
            if (holding && !held && multiplier.Heat > 0f)
                PlayLeak();

            holding = held;

            factor.text = HudFormat.Multiplier(multiplier.Total);
            factor.fontSize = Mathf.Lerp(factorSizeCold, factorSizeHot, heat);
            factor.color = Color.Lerp(theme.Muted, theme.Gold, heat);

            // Полоса живёт, только пока есть чему утекать: пустая она была бы третьей линией
            // внизу экрана, которая ничего не значит.
            var burning = multiplier.Heat > 0f;
            if (fuse.gameObject.activeSelf != burning)
                fuse.gameObject.SetActive(burning);

            if (!burning)
                return;

            fuseFill.anchorMax = new Vector2(multiplier.HoldShare, 1f);
            fuseFillPanel.color = Color.Lerp(Color.white, heatTint, heat);
        }

        void BuildPanel()
        {
            var rows = Mathf.CeilToInt((float)grid.Capacity / columns);

            var rect = (RectTransform)transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 24f);
            rect.sizeDelta = new Vector2(
                columns * cellSize + (columns - 1) * spacing + padding * 2f,
                rows * cellSize + (rows - 1) * spacing + padding * 2f);

            // Слои карточки лежат детьми, а сетка — своим ребёнком поверх них: `GridLayoutGroup`
            // растащил бы тень и кромку по клеткам, окажись они прямыми детьми панели.
            panel = UiPanel.Create("Panel", transform, theme);
            panel.rectTransform.Stretch();

            var cellsRoot = UiPanel.NewRect("Cells", transform).Stretch();
            var layout = cellsRoot.gameObject.AddComponent<GridLayoutGroup>();
            layout.cellSize = Vector2.one * cellSize;
            layout.spacing = Vector2.one * spacing;
            layout.padding = new RectOffset((int)padding, (int)padding, (int)padding, (int)padding);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = columns;

            BuildHeat(rect);
            BuildSellButton();

            cells = new RectTransform[grid.Capacity];
            cellPanels = new UiPanelGraphic[grid.Capacity];
            cellRings = new UiPanelGraphic[grid.Capacity];
            icons = new ResourceIcon[grid.Capacity];
            for (var i = 0; i < cells.Length; i++)
            {
                cellPanels[i] = UiPanel.Create($"Cell {i}", cellsRoot, theme, theme.SlotEmpty);
                var cell = cellPanels[i].gameObject;
                cells[i] = cellPanels[i].rectTransform;

                // Иконка — ребёнок подложки: она едет вместе с ней на пульсации и на выскакивании
                // масштабом, а `GridLayoutGroup` раскладывает только прямых детей панели.
                // `CanvasRenderer` перечислен явно: конструктор `GameObject(имя, типы)` не разбирает
                // `RequireComponent`, и без него графика молча не рисуется.
                var icon = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(ResourceIcon));
                var iconRect = (RectTransform)icon.transform;
                iconRect.SetParent(cell.transform, false);
                iconRect.anchorMin = Vector2.zero;
                iconRect.anchorMax = Vector2.one;
                iconRect.sizeDelta = Vector2.zero;

                icons[i] = icon.GetComponent<ResourceIcon>();
                icons[i].raycastTarget = false;
            }

            // Последним ребёнком, то есть поверх клеток: вспышка гасит карточку целиком, а не
            // подкрашивает её подложку снизу, как это делает нагрев.
            burnPanel = UiPanel.Create("Burn", transform, theme, theme.Flash);
            burnPanel.rectTransform.Stretch();
            burnPanel.raycastTarget = false;
            burnPanel.gameObject.SetActive(false);

            BuildGhosts();
        }

        /// <summary>
        /// Призраки прибавки и кольца ударной волны. Живут не на самой панели, а рядом с ней:
        /// у панели `GridLayoutGroup`, он растащил бы их по клеткам сетки, — и вылетать им
        /// нужно за её край. Заводятся штуками и переиспользуются по кругу: `UiText` правит
        /// `fontMaterial`, то есть каждая созданная строка — это ещё один инстанс материала.
        /// </summary>
        void BuildGhosts()
        {
            ghosts = new TextMeshProUGUI[GhostCount];
            ghostRuns = new Coroutine[GhostCount];
            for (var i = 0; i < GhostCount; i++)
            {
                ghosts[i] = UiText.Bold(
                    $"Heat gain {i}", transform.parent, theme, ghostSize, theme.Gold, TextAlignmentOptions.Center);
                ghosts[i].rectTransform.sizeDelta = new Vector2(cellSize * 2f, ghostSize * 1.6f);
                ghosts[i].gameObject.SetActive(false);
            }

            rings = new UiPanelGraphic[RingCount];
            ringRuns = new Coroutine[RingCount];
            for (var i = 0; i < RingCount; i++)
            {
                rings[i] = UiPanel.Create($"Ring {i}", transform.parent, theme, theme.Ring(cellSize));
                rings[i].rectTransform.sizeDelta = Vector2.one * cellSize;
                rings[i].gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Кнопка «Продать всё» — над складом, прижата к правому краю панели. Палец на темпе
        /// партии и так живёт над складом, а число множителя стоит по центру и остаётся видно.
        /// Спрятанная кнопка не ловит ни нажатий, ни кликов, поэтому её место ничего не отнимает
        /// у поля, пока она не пришла.
        /// </summary>
        void BuildSellButton()
        {
            sellButton = UiButton.Create(
                "Sell", transform, theme, theme.ButtonSecondary, "Продать всё", sellFontSize,
                () => SellRequested?.Invoke());

            var button = sellButton.Rect;
            button.anchorMin = button.anchorMax = new Vector2(1f, 1f);
            button.pivot = new Vector2(1f, 0f);
            button.sizeDelta = new Vector2(sellWidth, sellHeight);
            button.anchoredPosition = new Vector2(0f, factorGap);
            sellButton.Visible = false;
        }

        /// <summary>
        /// Число множителя над складом и полоса утечки под ним. Оба — дети склада, а не HUD:
        /// они говорят именно про склад, и уезжать они обязаны вместе с ним.
        /// </summary>
        void BuildHeat(RectTransform rect)
        {
            factor = UiText.Bold("Factor", transform, theme, factorSizeCold, theme.Muted, TextAlignmentOptions.Center);
            var label = factor.rectTransform;
            label.anchorMin = label.anchorMax = new Vector2(0.5f, 1f);
            label.pivot = new Vector2(0.5f, 0f);
            label.sizeDelta = new Vector2(rect.sizeDelta.x, factorSizeHot * 1.4f);
            label.anchoredPosition = new Vector2(0f, factorGap);

            fuse = UiPanel.Create("Fuse", transform, theme, theme.BarTrack).rectTransform;
            fuse.anchorMin = fuse.anchorMax = new Vector2(0.5f, 0f);
            fuse.pivot = new Vector2(0.5f, 1f);
            fuse.sizeDelta = new Vector2(rect.sizeDelta.x - padding * 2f, fuseHeight);
            fuse.anchoredPosition = new Vector2(0f, -fuseGap);

            fuseFillPanel = UiPanel.Create("Fill", fuse, theme, theme.BarFill);
            fuseFill = fuseFillPanel.rectTransform;
            fuseFill.anchorMin = Vector2.zero;
            fuseFill.anchorMax = new Vector2(1f, 1f);
            fuseFill.offsetMin = fuseFill.offsetMax = Vector2.zero;

            // Искра сидит на самой полосе: она отмечает голову заливки, и уезжать ей нужно
            // вместе с полосой, а не считаться от склада.
            sparkPanel = UiPanel.Create("Spark", fuse, theme, theme.Flash);
            spark = sparkPanel.rectTransform;
            spark.anchorMin = spark.anchorMax = spark.pivot = new Vector2(0.5f, 0.5f);
            spark.sizeDelta = Vector2.one * fuseHeight * 2f;
            spark.gameObject.SetActive(false);

            fuse.gameObject.SetActive(false);
        }

        void Refresh()
        {
            for (var i = 0; i < cells.Length; i++)
            {
                var content = grid[i];
                var shown = content.HasValue && !pendingCells.Contains(i);
                cellPanels[i].Apply(theme.PanelShader, shown ? theme.SlotFilled : theme.SlotEmpty);

                if (shown)
                    ShowIcon(icons[i], content.Value);
                else
                    icons[i].Hide();
            }
        }

        /// <summary>
        /// Иконка ресурса: снимок модели, если он есть. У модели цвет уже лежит в палитре
        /// текстуры, и красить снимок цветом ресурса нельзя — он перемножится. Крафтовый ресурс
        /// модели не имеет и красится, как раньше. Публичный: HUD берёт иконки отсюда же,
        /// второй пекарь снимков на партию — это второй набор `RenderTexture` ни за чем.
        /// </summary>
        public void ShowIcon(ResourceIcon icon, ResourceType type)
        {
            var snapshot = snapshots?.Get(type);
            icon.Show(type, snapshot != null ? Color.white : palette.Get(type), snapshot);
        }

        /// <summary>
        /// Иконка по произвольной модели: так HUD берёт монету и свиток. Печёт тот же пекарь —
        /// второй набор `RenderTexture` на партию заводить незачем.
        /// </summary>
        public void ShowIcon(ResourceIcon icon, Mesh model, Material material, Vector3 angles) =>
            icon.Show(snapshots?.Get(model, material, Quaternion.Euler(angles)), Color.white);

        void OnResourceLost(ResourceType type) => flashTimer = flashSeconds;
    }
}
