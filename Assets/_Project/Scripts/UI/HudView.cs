using System.Collections;
using System.Collections.Generic;
using Game.Core;
using Game.Economy;
using Game.Storage;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// HUD поля карточками: счёт слева сверху, крафтовые ресурсы справа сверху, карточка
    /// контракта под ними и столбец всплывашек слева. Всё рисуется кодом на скруглённых
    /// спрайтах и SDF-шрифте — панели в проекте не ассеты, а числа палитры.
    /// </summary>
    public sealed class HudView : MonoBehaviour
    {
        /// <summary>Что показывает полоса ресурсов: крафт, который меняют на очки.</summary>
        static readonly ResourceType[] StripTypes = { ResourceType.Gravel, ResourceType.Board, ResourceType.Ingot };

        const float Margin = 24f;
        const float TopHeight = 116f;
        const float PointsWidth = 320f;
        const float CellWidth = 138f;
        const float CellSpacing = 10f;
        const float StripPadding = 20f;
        const float IconSize = 60f;
        const float CardWidth = 474f;
        const float CardHeight = 292f;
        const float CardPadding = 24f;
        const float GainWidth = 250f;
        const float MessageWidth = 470f;
        const float ToastHeight = 92f;
        const float BarHeight = 30f;
        const float BarInset = 3f;

        [SerializeField] UiTheme theme = new();
        [Tooltip("Сколько живёт плашка отказа и плашка прибавки")]
        [SerializeField] float toastSeconds = 2f;
        [Tooltip("Сколько плашка гаснет в конце жизни")]
        [SerializeField] float fadeSeconds = 0.35f;
        [Tooltip("Больше плашек столбец не держит: самая старая уходит сразу")]
        [SerializeField] int maxToasts = 3;

        readonly List<GameObject> toasts = new();
        readonly TextMeshProUGUI[] stripCounts = new TextMeshProUGUI[StripTypes.Length];

        TextMeshProUGUI pointsValue;
        RectTransform contractCard;
        ResourceIcon contractIcon;
        TextMeshProUGUI contractGoal;
        TextMeshProUGUI contractReward;
        TextMeshProUGUI contractProgress;
        TextMeshProUGUI contractTimer;
        RectTransform contractBarFill;
        Image contractBarFillImage;
        RectTransform toastColumn;

        int shownSeconds = -1;
        ContractSystem contracts;
        Wallet wallet;
        StorageGrid storage;
        StorageView storageView;

        /// <summary>
        /// Склад передаётся сюда не ради данных, а ради иконок: снимки моделей печёт он, и
        /// второй такой же пекарь на партию — это второй набор `RenderTexture` ни за чем.
        /// </summary>
        public void Bind(GameState game, ContractSystem contractSystem, StorageView storage)
        {
            contracts = contractSystem;
            wallet = game.Wallet;
            this.storage = game.Storage;
            storageView = storage;

            BuildPointsCard();
            BuildResourceStrip();
            BuildContractCard();
            toastColumn = BuildToastColumn();

            wallet.Changed += Refresh;
            this.storage.Changed += Refresh;
            contracts.Issued += RefreshContract;
            contracts.Progressed += RefreshContract;
            contracts.Failed += RefreshContract;
            contracts.Completed += OnContractCompleted;
            Refresh();
        }

        /// <summary>Отказ: красная плашка в общем столбце, живёт пару секунд и гаснет.</summary>
        public void ShowMessage(string text)
        {
            var card = PushToast(MessageWidth);
            UiText.Label("Text", card, 28f, theme.Bad, TextAlignmentOptions.Left)
                .Stretch(22f, 22f, 12f, 12f)
                .text = text;
        }

        /// <summary>Прибавка очков: «+30» и иконка того, за что заплатили.</summary>
        public void ShowGain(int points, ResourceType source)
        {
            var card = PushToast(GainWidth);

            var value = UiText.Bold("Value", card, 44f, theme.Good, TextAlignmentOptions.Left).Outlined(theme);
            value.rectTransform.anchorMin = value.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            value.rectTransform.pivot = new Vector2(0f, 0.5f);
            value.rectTransform.anchoredPosition = new Vector2(22f, 0f);
            value.rectTransform.sizeDelta = new Vector2(GainWidth - 44f - IconSize, 56f);
            value.text = "✓ " + HudFormat.Gain(points);

            var icon = CreateIcon("Icon", card, IconSize);
            icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            icon.rectTransform.pivot = new Vector2(1f, 0.5f);
            icon.rectTransform.anchoredPosition = new Vector2(-22f, 0f);
            storageView.ShowIcon(icon, source);
        }

        void OnDestroy()
        {
            if (wallet == null)
                return;

            wallet.Changed -= Refresh;
            storage.Changed -= Refresh;
            contracts.Issued -= RefreshContract;
            contracts.Progressed -= RefreshContract;
            contracts.Failed -= RefreshContract;
            contracts.Completed -= OnContractCompleted;
        }

        void Update()
        {
            // Обратный отсчёт перерисовывается только на смене целой секунды: строка каждый кадр
            // ничего не добавляет глазу, зато мусорит строками в куче.
            if (contracts != null && contracts.IsActive && Mathf.CeilToInt(contracts.SecondsLeft) != shownSeconds)
                RefreshContract();
        }

        void Refresh()
        {
            pointsValue.text = HudFormat.Points(wallet.Points);

            for (var i = 0; i < StripTypes.Length; i++)
                stripCounts[i].text = storage.CountOf(StripTypes[i]).ToString();

            RefreshContract();
        }

        /// <summary>Карточка контракта: что просят, сколько сдано и сколько осталось времени.</summary>
        void RefreshContract()
        {
            if (!contracts.IsActive)
            {
                contractCard.gameObject.SetActive(false);
                shownSeconds = -1;
                return;
            }

            contractCard.gameObject.SetActive(true);
            shownSeconds = Mathf.CeilToInt(contracts.SecondsLeft);

            storageView.ShowIcon(contractIcon, contracts.Type);
            contractGoal.text = contracts.Goal.ToString();
            contractReward.text = HudFormat.Gain(contracts.Reward);
            contractProgress.text = $"{contracts.Delivered} / {contracts.Goal}";
            contractTimer.text = HudFormat.Seconds(shownSeconds);

            var filled = contracts.Goal > 0 ? Mathf.Clamp01((float)contracts.Delivered / contracts.Goal) : 0f;
            contractBarFillImage.enabled = filled > 0f;
            contractBarFill.anchorMax = new Vector2(filled, 1f);
        }

        /// <summary>Контракт закрыт: награда всплывает плашкой с иконкой того же крафта.</summary>
        void OnContractCompleted(int reward) => ShowGain(reward, contracts.Type);

        void BuildPointsCard()
        {
            var card = UiPanel.Create("Points", transform, theme);
            Place(card, new Vector2(0f, 1f), new Vector2(Margin, -Margin), new Vector2(PointsWidth, TopHeight));

            var label = UiText.Label("Label", card, 28f, theme.Muted, TextAlignmentOptions.Left);
            label.Stretch(26f, PointsWidth * 0.5f, 0f, 0f);
            label.characterSpacing = 10f;
            label.text = "ОЧКИ";

            pointsValue = UiText.Bold("Value", card, 62f, theme.Gold, TextAlignmentOptions.Right).Outlined(theme);
            pointsValue.Stretch(PointsWidth * 0.35f, 26f, 0f, 0f);
        }

        void BuildResourceStrip()
        {
            var width = StripTypes.Length * CellWidth + (StripTypes.Length - 1) * CellSpacing + StripPadding * 2f;
            var strip = UiPanel.Create("Resources", transform, theme);
            Place(strip, new Vector2(1f, 1f), new Vector2(-Margin, -Margin), new Vector2(width, TopHeight));

            for (var i = 0; i < StripTypes.Length; i++)
            {
                var cell = UiPanel.NewRect($"Cell {StripTypes[i]}", strip);
                Place(cell, new Vector2(0f, 0.5f),
                    new Vector2(StripPadding + i * (CellWidth + CellSpacing), 0f),
                    new Vector2(CellWidth, TopHeight - StripPadding * 2f));
                cell.pivot = new Vector2(0f, 0.5f);

                var icon = CreateIcon("Icon", cell, IconSize);
                icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(0f, 0.5f);
                icon.rectTransform.pivot = new Vector2(0f, 0.5f);
                icon.rectTransform.anchoredPosition = Vector2.zero;
                storageView.ShowIcon(icon, StripTypes[i]);

                stripCounts[i] = UiText.Bold("Count", cell, 42f, theme.Text, TextAlignmentOptions.Right).Outlined(theme);
                stripCounts[i].Stretch(IconSize + 8f, 0f, 0f, 0f);
            }
        }

        void BuildContractCard()
        {
            contractCard = UiPanel.Create("Contract", transform, theme);
            Place(contractCard, new Vector2(1f, 1f),
                new Vector2(-Margin, -(Margin + TopHeight + 16f)), new Vector2(CardWidth, CardHeight));

            var inner = CardWidth - CardPadding * 2f;

            Row(UiText.Bold("Title", contractCard, 38f, theme.Text, TextAlignmentOptions.Left), 20f, inner, 46f).text =
                "Контракт";

            Row(UiText.Label("GoalLabel", contractCard, 30f, theme.Muted, TextAlignmentOptions.Left), 76f, 160f, 40f)
                .text = "Сдать";
            contractIcon = CreateIcon("GoalIcon", contractCard, 52f);
            Place((RectTransform)contractIcon.transform, new Vector2(0f, 1f),
                new Vector2(CardPadding + 164f, -70f), new Vector2(52f, 52f));
            contractGoal = Column(
                UiText.Bold("GoalValue", contractCard, 42f, theme.Text, TextAlignmentOptions.Left).Outlined(theme),
                258f, 74f, inner - 258f + CardPadding, 44f);

            Row(UiText.Label("RewardLabel", contractCard, 30f, theme.Muted, TextAlignmentOptions.Left), 128f, 160f, 40f)
                .text = "Награда";
            contractReward = Column(
                UiText.Bold("RewardValue", contractCard, 42f, theme.Gold, TextAlignmentOptions.Left).Outlined(theme),
                170f, 126f, inner - 170f + CardPadding, 44f);

            BuildContractBar(inner);

            contractTimer = Row(UiText.Label("Timer", contractCard, 30f, theme.Muted, TextAlignmentOptions.Left),
                230f, inner, 36f);
            contractProgress = Row(UiText.Label("Progress", contractCard, 30f, theme.Text, TextAlignmentOptions.Right),
                230f, inner, 36f);
        }

        /// <summary>
        /// Прогресс контракта: тёмный жёлоб и золотая заливка внутри него. Заливка тянется
        /// якорем, а не `fillAmount`, — так её скруглённые торцы остаются скруглёнными.
        /// </summary>
        void BuildContractBar(float inner)
        {
            var track = UiPanel.NewRect("Bar", contractCard);
            Place(track, new Vector2(0f, 1f), new Vector2(CardPadding, -190f), new Vector2(inner, BarHeight));
            track.pivot = new Vector2(0f, 1f);
            Fill(track, theme.SlotEmpty, theme.SlotRadius, 0f);

            contractBarFill = UiPanel.NewRect("Fill", track);
            contractBarFill.anchorMin = Vector2.zero;
            contractBarFill.anchorMax = new Vector2(0f, 1f);
            contractBarFill.offsetMin = new Vector2(BarInset, BarInset);
            contractBarFill.offsetMax = new Vector2(-BarInset, -BarInset);
            contractBarFillImage = Fill(contractBarFill, theme.Gold, theme.SlotRadius - (int)BarInset, 0f);
        }

        RectTransform BuildToastColumn()
        {
            var column = UiPanel.NewRect("Toasts", transform);
            Place(column, new Vector2(0f, 1f),
                new Vector2(Margin, -(Margin + TopHeight + 16f)), new Vector2(MessageWidth, 0f));

            var layout = column.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = layout.childControlHeight = false;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            return column;
        }

        /// <summary>Новая плашка сверху столбца. Старые уходят, когда столбец перерос предел.</summary>
        RectTransform PushToast(float width)
        {
            while (toasts.Count >= maxToasts)
            {
                var oldest = toasts[0];
                toasts.RemoveAt(0);
                if (oldest != null)
                    Destroy(oldest);
            }

            var card = UiPanel.Create("Toast", toastColumn, theme);
            card.sizeDelta = new Vector2(width, ToastHeight);
            card.gameObject.AddComponent<CanvasGroup>();

            toasts.Add(card.gameObject);
            if (isActiveAndEnabled)
                StartCoroutine(FadeToast(card.gameObject));

            return card;
        }

        IEnumerator FadeToast(GameObject card)
        {
            yield return new WaitForSeconds(toastSeconds);

            var group = card.GetComponent<CanvasGroup>();
            for (var elapsed = 0f; elapsed < fadeSeconds; elapsed += Time.deltaTime)
            {
                group.alpha = 1f - elapsed / fadeSeconds;
                yield return null;
            }

            toasts.Remove(card);
            Destroy(card);
        }

        ResourceIcon CreateIcon(string name, Transform parent, float size)
        {
            var created = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(ResourceIcon));
            var rect = (RectTransform)created.transform;
            rect.SetParent(parent, false);
            rect.sizeDelta = new Vector2(size, size);

            var icon = created.GetComponent<ResourceIcon>();
            icon.raycastTarget = false;
            return icon;
        }

        /// <summary>Строка карточки: от левого верхнего угла вниз на `top` пикселей.</summary>
        static TextMeshProUGUI Row(TextMeshProUGUI text, float top, float width, float height) =>
            Column(text, CardPadding, top, width, height);

        /// <summary>Строка карточки со своим отступом слева: второй столбец в той же строке.</summary>
        static TextMeshProUGUI Column(TextMeshProUGUI text, float left, float top, float width, float height)
        {
            Place(text.rectTransform, new Vector2(0f, 1f), new Vector2(left, -top), new Vector2(width, height));
            return text;
        }

        static Image Fill(RectTransform rect, Color color, int radius, float inset)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = UiSprites.Rounded(Mathf.Max(0, radius));
            image.type = Image.Type.Sliced;
            image.color = color;
            image.raycastTarget = false;
            if (inset > 0f)
            {
                rect.offsetMin += new Vector2(inset, inset);
                rect.offsetMax -= new Vector2(inset, inset);
            }

            return image;
        }

        static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }
}
