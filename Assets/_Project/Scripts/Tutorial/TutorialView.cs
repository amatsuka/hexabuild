using System;
using System.Collections.Generic;
using Game.Grid;
using Game.Storage;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Tutorial
{
    /// <summary>
    /// Подсказка обучения: строка под HUD, надпись «Пропустить» и пульсирующая подсветка цели.
    /// Тапы ловит только «Пропустить» — за самой подсказкой кликается поле.
    /// </summary>
    public sealed class TutorialView : MonoBehaviour
    {
        const string SkipLabel = "Пропустить";

        [SerializeField] Color backingColor = new(0.06f, 0.06f, 0.08f, 0.72f);
        [SerializeField] Color textColor = new(0.96f, 0.92f, 0.76f);
        [SerializeField] Color stuckColor = new(0.95f, 0.55f, 0.45f);
        [SerializeField] Color skipColor = new(0.62f, 0.62f, 0.68f);
        [SerializeField] Color highlightColor = new(1f, 0.84f, 0.30f);
        [SerializeField] int fontSize = 38;
        [SerializeField] float topOffset = 200f;
        [SerializeField] float sideMargin = 24f;
        [SerializeField] float pulseSpeed = 3.2f;
        [SerializeField, Range(0f, 1f)] float minHighlightAlpha = 0.25f;

        Text hint;
        Text skip;
        TutorialSystem tutorial;
        IReadOnlyDictionary<HexCoord, TileView> tiles;
        StorageView storage;
        HexCoord? highlighted;

        public void Bind(
            TutorialSystem system,
            IReadOnlyDictionary<HexCoord, TileView> tileViews,
            StorageView storageView)
        {
            tutorial = system;
            tiles = tileViews;
            storage = storageView;

            Build();
            tutorial.Changed += Redraw;
            Redraw();
        }

        /// <summary>Надпись «Пропустить» — единственное место подсказки, которое ловит тап.</summary>
        public bool ContainsSkipPoint(Vector2 screenPosition) =>
            tutorial.IsRunning
            && RectTransformUtility.RectangleContainsScreenPoint(skip.rectTransform, screenPosition);

        void OnDestroy()
        {
            if (tutorial != null)
                tutorial.Changed -= Redraw;
        }

        /// <summary>Подсветка плитки дышит альфой: кольцо то разгорается, то почти гаснет.</summary>
        void Update()
        {
            if (!highlighted.HasValue || !tiles.TryGetValue(highlighted.Value, out var view))
                return;

            var color = highlightColor;
            color.a = Mathf.Lerp(minHighlightAlpha, 1f, (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f);
            view.SetHighlight(color);
        }

        void Redraw()
        {
            if (!tutorial.IsRunning)
            {
                ClearTileHighlight();
                storage.Highlight(Array.Empty<int>());
                gameObject.SetActive(false);
                return;
            }

            hint.text = tutorial.IsStuck ? StuckText : TextOf(tutorial.Step);
            hint.color = tutorial.IsStuck ? stuckColor : textColor;

            if (highlighted != tutorial.TargetTile)
            {
                ClearTileHighlight();
                highlighted = tutorial.TargetTile;
            }

            storage.Highlight(tutorial.TargetCells);
        }

        void ClearTileHighlight()
        {
            if (highlighted.HasValue && tiles.TryGetValue(highlighted.Value, out var view))
                view.SetHighlight(Color.clear);

            highlighted = null;
        }

        static string StuckText =>
            "Щебень кончился, а дорог нет: добывать нечем и заработать не на чем.\nПерезапусти партию";

        static string TextOf(TutorialStep step)
        {
            switch (step)
            {
                case TutorialStep.OpenTile:
                    return "Тапни по подсвеченной плитке.\n20 очков — и увидишь, что под ней";
                case TutorialStep.BuildRoad:
                    return "Без дороги до Метрополии плитка молчит.\nТапни ещё раз: 1 щебень — дорога";
                case TutorialStep.WatchDelivery:
                    return "Плитка добывает ресурс раз в 3 секунды\nи шлёт его по дороге на склад";
                case TutorialStep.Merge:
                    return "Копи три одинаковых и тапни по любому:\nтри дают один крафт, пять дают два";
                case TutorialStep.Convert:
                    return "Крафт — единственный источник очков, тап даёт 15.\nЩебень тоже, но он платит за дороги";
                default:
                    return "Очки → плитка → дорога → ресурсы → очки.\nОткрой следующую — дальше сам";
            }
        }

        void Build()
        {
            var backingObject = new GameObject("Hint", typeof(RectTransform), typeof(Image));
            backingObject.transform.SetParent(transform, false);
            backingObject.GetComponent<Image>().color = backingColor;

            var backing = (RectTransform)backingObject.transform;
            backing.anchorMin = new Vector2(0f, 1f);
            backing.anchorMax = new Vector2(1f, 1f);
            backing.pivot = new Vector2(0.5f, 1f);
            backing.sizeDelta = new Vector2(-sideMargin * 2f, fontSize * 2.9f);
            backing.anchoredPosition = new Vector2(0f, -topOffset);

            hint = CreateText("Text", backing, textColor, fontSize, TextAnchor.MiddleCenter);
            hint.rectTransform.anchorMin = Vector2.zero;
            hint.rectTransform.anchorMax = Vector2.one;
            hint.rectTransform.offsetMin = new Vector2(16f, 6f);
            hint.rectTransform.offsetMax = new Vector2(-16f, -6f);

            skip = CreateText("Skip", (RectTransform)transform, skipColor, fontSize - 4, TextAnchor.LowerRight);
            skip.text = SkipLabel;
            skip.rectTransform.anchorMin = skip.rectTransform.anchorMax = new Vector2(1f, 0f);
            skip.rectTransform.pivot = new Vector2(1f, 0f);
            skip.rectTransform.sizeDelta = new Vector2(260f, 76f);
            skip.rectTransform.anchoredPosition = new Vector2(-sideMargin, sideMargin);
        }

        static Text CreateText(string textName, RectTransform parent, Color color, int size, TextAnchor anchor)
        {
            var line = new GameObject(textName, typeof(RectTransform), typeof(Text));
            line.transform.SetParent(parent, false);

            var text = line.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.color = color;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }
    }
}
