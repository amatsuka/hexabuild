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

        /// <summary>Клетка склада под экранной точкой: панель сама разбирает клики по себе.</summary>
        public bool TryGetCellIndex(Vector2 screenPosition, out int index)
        {
            for (index = 0; index < cells.Length; index++)
                if (RectTransformUtility.RectangleContainsScreenPoint((RectTransform)cells[index].transform, screenPosition))
                    return true;

            index = -1;
            return false;
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
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 0.5f);
            rect.anchoredPosition = new Vector2(-20f, 0f);
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
                cells[i].color = content.HasValue ? palette.Get(content.Value) : emptyCellColor;
            }
        }

        void OnResourceLost(ResourceType type) => flashTimer = flashSeconds;
    }
}
