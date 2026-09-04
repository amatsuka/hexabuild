using System.Collections;
using System.Collections.Generic;
using Game.Economy;
using Game.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Storage
{
    /// <summary>
    /// Полоса склада внизу: сетка клеток и красная вспышка при потере ресурса. Панель и клетки
    /// собраны той же карточкой, что и HUD, — тень, светлая кромка, тёмная заливка.
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

        [Header("Анимация слияния")]
        [SerializeField] float flySeconds = 0.28f;
        [SerializeField] float popSeconds = 0.18f;
        [SerializeField] float popScale = 1.3f;
        [SerializeField] float flyEndScale = 0.45f;

        readonly HashSet<int> pendingCells = new();

        UiPanelGraphic panel;
        RectTransform[] cells;
        UiPanelGraphic[] cellPanels;
        ResourceIcon[] icons;
        StorageGrid grid;
        ResourceIconBaker snapshots;
        float flashTimer;

        public void Bind(StorageGrid storage)
        {
            grid = storage;
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

        /// <summary>Ресурс приземлился: клетка проявляется и выскакивает масштабом.</summary>
        public void ReleaseCell(int index)
        {
            if (!pendingCells.Remove(index))
                return;

            Refresh();

            if (!isActiveAndEnabled || !grid[index].HasValue)
                return;

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

            StartCoroutine(AnimateMerge(consumedCells, resultCells, movedType));
        }

        IEnumerator AnimateMerge(IReadOnlyList<int> consumedCells, IReadOnlyList<int> resultCells, ResourceType movedType)
        {
            var target = cells[resultCells[0]].position;
            var flying = new List<RectTransform>(consumedCells.Count);
            var origins = new List<Vector3>(consumedCells.Count);
            foreach (var index in consumedCells)
            {
                var origin = cells[index].position;
                origins.Add(origin);
                flying.Add(CreateFlyingCopy(origin, movedType));
            }

            foreach (var index in resultCells)
                cells[index].localScale = Vector3.zero;

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

            yield return PopCells(resultCells);
        }

        /// <summary>Клетка выскакивает из нуля в чуть больший масштаб и оседает в единицу.</summary>
        IEnumerator PopCells(IReadOnlyList<int> popped)
        {
            for (var elapsed = 0f; elapsed < popSeconds; elapsed += Time.deltaTime)
            {
                var progress = elapsed / popSeconds;
                var scale = progress < 0.5f
                    ? Mathf.Lerp(0f, popScale, progress * 2f)
                    : Mathf.Lerp(popScale, 1f, (progress - 0.5f) * 2f);

                foreach (var index in popped)
                    cells[index].localScale = Vector3.one * scale;

                yield return null;
            }

            foreach (var index in popped)
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

        void Update()
        {
            if (flashTimer > 0f)
            {
                flashTimer = Mathf.Max(0f, flashTimer - Time.deltaTime);
                // Красится вершинным цветом, то есть вся карточка разом: шейдер домножает на него
                // и градиент, и кромку, и свечение. Белый — панель в своём цвете.
                panel.color = Color.Lerp(Color.white, lossFlashTint, flashTimer / flashSeconds);
            }
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

            cells = new RectTransform[grid.Capacity];
            cellPanels = new UiPanelGraphic[grid.Capacity];
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
