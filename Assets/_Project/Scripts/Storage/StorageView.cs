using System.Collections;
using System.Collections.Generic;
using Game.Economy;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Storage
{
    /// <summary>Панель склада справа: сетка клеток и красная вспышка при потере ресурса.</summary>
    [RequireComponent(typeof(Image))]
    public sealed class StorageView : MonoBehaviour
    {
        [SerializeField] ResourcePalette palette;
        [SerializeField] Color panelColor = new(0.12f, 0.12f, 0.14f, 0.9f);
        [SerializeField] Color emptyCellColor = new(0.22f, 0.22f, 0.25f);
        [SerializeField] Color lossFlashColor = new(0.75f, 0.15f, 0.15f, 0.95f);
        [SerializeField] float flashSeconds = 0.4f;
        [SerializeField] int columns = 5;
        [SerializeField] float cellSize = 40f;
        [SerializeField] float spacing = 6f;
        [SerializeField] float padding = 10f;

        [Header("Анимация слияния")]
        [SerializeField] float flySeconds = 0.28f;
        [SerializeField] float popSeconds = 0.18f;
        [SerializeField] float popScale = 1.3f;
        [SerializeField] float flyEndScale = 0.45f;

        readonly HashSet<int> pendingCells = new();

        Image panel;
        Image[] cells;
        StorageGrid grid;
        float flashTimer;

        public void Bind(StorageGrid storage)
        {
            grid = storage;
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

            cells[index].rectTransform.localScale = Vector3.zero;
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
            var target = cells[resultCells[0]].rectTransform.position;
            var flying = new List<RectTransform>(consumedCells.Count);
            var origins = new List<Vector3>(consumedCells.Count);
            foreach (var index in consumedCells)
            {
                var origin = cells[index].rectTransform.position;
                origins.Add(origin);
                flying.Add(CreateFlyingCopy(origin, movedType));
            }

            foreach (var index in resultCells)
                cells[index].rectTransform.localScale = Vector3.zero;

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
                    cells[index].rectTransform.localScale = Vector3.one * scale;

                yield return null;
            }

            foreach (var index in popped)
                cells[index].rectTransform.localScale = Vector3.one;
        }

        /// <summary>
        /// Летящая копия висит на канвасе, а не на панели: у панели `GridLayoutGroup`, он бы
        /// растащил её обратно по клеткам сетки.
        /// </summary>
        RectTransform CreateFlyingCopy(Vector3 position, ResourceType type)
        {
            var copy = new GameObject("MergeFly", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)copy.transform;
            rect.SetParent(transform.parent, false);
            rect.SetAsLastSibling();
            rect.sizeDelta = Vector2.one * cellSize;
            rect.position = position;
            copy.GetComponent<Image>().color = palette.Get(type);
            return rect;
        }

        /// <summary>Клетка склада под экранной точкой: панель сама разбирает клики по себе.</summary>
        public bool TryGetCellIndex(Vector2 screenPosition, out int index)
        {
            for (index = 0; index < cells.Length; index++)
                if (RectTransformUtility.RectangleContainsScreenPoint(cells[index].rectTransform, screenPosition))
                    return true;

            index = -1;
            return false;
        }

        /// <summary>Мировая точка клетки: туда прыгает доехавший до Метрополии ресурс.</summary>
        public Vector3 CellWorldPoint(int index, Camera worldCamera)
        {
            var screenPoint = RectTransformUtility.WorldToScreenPoint(null, cells[index].rectTransform.position);
            var world = worldCamera.ScreenToWorldPoint(
                new Vector3(screenPoint.x, screenPoint.y, -worldCamera.transform.position.z));
            return new Vector3(world.x, world.y, 0f);
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
            if (grid == null)
                return;

            grid.Changed -= Refresh;
            grid.ResourceLost -= OnResourceLost;
        }

        void Update()
        {
            if (flashTimer <= 0f)
                return;

            flashTimer = Mathf.Max(0f, flashTimer - Time.deltaTime);
            panel.color = Color.Lerp(panelColor, lossFlashColor, flashTimer / flashSeconds);
        }

        void BuildPanel()
        {
            var rows = Mathf.CeilToInt((float)grid.Capacity / columns);

            panel = GetComponent<Image>();
            panel.color = panelColor;

            var rect = (RectTransform)transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 16f);
            rect.sizeDelta = new Vector2(
                columns * cellSize + (columns - 1) * spacing + padding * 2f,
                rows * cellSize + (rows - 1) * spacing + padding * 2f);

            var layout = gameObject.AddComponent<GridLayoutGroup>();
            layout.cellSize = Vector2.one * cellSize;
            layout.spacing = Vector2.one * spacing;
            layout.padding = new RectOffset((int)padding, (int)padding, (int)padding, (int)padding);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = columns;

            cells = new Image[grid.Capacity];
            for (var i = 0; i < cells.Length; i++)
            {
                var cell = new GameObject($"Cell {i}", typeof(RectTransform), typeof(Image));
                cell.transform.SetParent(transform, false);
                cells[i] = cell.GetComponent<Image>();
            }
        }

        void Refresh()
        {
            for (var i = 0; i < cells.Length; i++)
            {
                var content = grid[i];
                var shown = content.HasValue && !pendingCells.Contains(i);
                cells[i].color = shown ? palette.Get(content.Value) : emptyCellColor;
            }
        }

        void OnResourceLost(ResourceType type) => flashTimer = flashSeconds;
    }
}
