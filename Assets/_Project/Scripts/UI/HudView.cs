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
        const float IslandWidth = 158f;
        const float IslandSpacing = 12f;
        const float IslandPadding = 14f;
        const float StripPadding = 16f;
        const float IconSize = 56f;
        const float CoinSize = 64f;
        const float ScrollSize = 60f;
        // Карточка контракта уже полосы ресурсов и прижата к тому же правому краю: в референсе
        // она не спорит с ней шириной и не отъедает четверть экрана.
        const float CardWidth = 430f;
        const float CardHeight = 288f;
        const float CardPadding = 20f;
        /// <summary>Левая колонка карточки под свиток; текст идёт правее неё.</summary>
        const float CardColumn = 92f;
        const float GainWidth = 250f;
        const float MessageWidth = 470f;
        const float ToastHeight = 92f;
        const float BarHeight = 26f;
        const float ProgressWidth = 110f;
        const float BarInset = 3f;

        [SerializeField] UiTheme theme = new();

        [Header("Иконки HUD")]
        [Tooltip("Модель монеты для карточки очков")]
        [SerializeField] Mesh coinMesh;
        [SerializeField] Material coinMaterial;
        [Tooltip("Разворот монеты в снимке: лицом к камере, монета лежит нормалью по X")]
        [SerializeField] Vector3 coinAngles = new(-10f, 90f, 0f);
        [Tooltip("Материал свитка контракта. Сам меш строит `ScrollMesh` кодом: модели в паке нет")]
        [SerializeField] Material scrollMaterial;
        [SerializeField] Vector3 scrollAngles = new(-14f, 24f, 0f);
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
        UiPanelGraphic contractBarFill;
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
            UiText.Label("Text", card, theme, 28f, theme.Bad, TextAlignmentOptions.Left)
                .Stretch(22f, 22f, 12f, 12f)
                .text = text;
        }

        /// <summary>Прибавка очков: «+30» и иконка того, за что заплатили.</summary>
        public void ShowGain(int points, ResourceType source)
        {
            var card = PushToast(GainWidth);

            var value = UiText.Bold("Value", card, theme, 44f, theme.Good, TextAlignmentOptions.Left);
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
            contractBarFill.enabled = filled > 0f;
            contractBarFill.rectTransform.anchorMax = new Vector2(filled, 1f);
        }

        /// <summary>Контракт закрыт: награда всплывает плашкой с иконкой того же крафта.</summary>
        void OnContractCompleted(int reward) => ShowGain(reward, contracts.Type);

        void BuildPointsCard()
        {
            var card = UiPanel.Create("Points", transform, theme).rectTransform;
            Place(card, new Vector2(0f, 1f), new Vector2(Margin, -Margin), new Vector2(PointsWidth, TopHeight));

            // Монета вместо подписи «ОЧКИ»: в референсе у счёта иконка, а не слово, и мелкая
            // подпись на прозрачном стекле всё равно была самым слабым местом карточки.
            var coin = CreateIcon("Coin", card, CoinSize);
            coin.rectTransform.anchorMin = coin.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            coin.rectTransform.pivot = new Vector2(0f, 0.5f);
            coin.rectTransform.anchoredPosition = new Vector2(22f, 0f);
            storageView.ShowIcon(coin, coinMesh, coinMaterial, coinAngles);

            pointsValue = UiText.Bold("Value", card, theme, 62f, theme.Gold, TextAlignmentOptions.Right);
            pointsValue.Stretch(22f + CoinSize + 10f, 26f, 0f, 0f);
        }

        void BuildResourceStrip()
        {
            var width = StripTypes.Length * IslandWidth + (StripTypes.Length - 1) * IslandSpacing
                        + StripPadding * 2f;
            var strip = UiPanel.Create("Resources", transform, theme).rectTransform;
            Place(strip, new Vector2(1f, 1f), new Vector2(-Margin, -Margin), new Vector2(width, TopHeight));

            for (var i = 0; i < StripTypes.Length; i++)
            {
                // Каждый ресурс на своём островке: без подложки число одного упирается в иконку
                // следующего, и полоса читается сплошной кашей вместо трёх счётчиков.
                var island = UiPanel.Create($"Island {StripTypes[i]}", strip, theme, theme.Island).rectTransform;
                Place(island, new Vector2(0f, 0.5f),
                    new Vector2(StripPadding + i * (IslandWidth + IslandSpacing), 0f),
                    new Vector2(IslandWidth, TopHeight - StripPadding * 2f));

                var icon = CreateIcon("Icon", island, IconSize);
                icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(0f, 0.5f);
                icon.rectTransform.pivot = new Vector2(0f, 0.5f);
                icon.rectTransform.anchoredPosition = new Vector2(IslandPadding, 0f);
                storageView.ShowIcon(icon, StripTypes[i]);

                stripCounts[i] = UiText.Bold("Count", island, theme, 42f, theme.Text, TextAlignmentOptions.Right);
                stripCounts[i].Stretch(IslandPadding + IconSize + 6f, IslandPadding, 0f, 0f);
            }
        }

        void BuildContractCard()
        {
            contractCard = UiPanel.Create("Contract", transform, theme).rectTransform;
            Place(contractCard, new Vector2(1f, 1f),
                new Vector2(-Margin, -(Margin + TopHeight + 16f)), new Vector2(CardWidth, CardHeight));

            var inner = CardWidth - CardPadding * 2f;
            var textWidth = CardWidth - CardColumn - CardPadding;

            // Свиток — левая колонка во всю высоту заголовка и первой пары, как в референсе.
            var scroll = CreateIcon("ScrollIcon", contractCard, ScrollSize);
            Place((RectTransform)scroll.transform, new Vector2(0f, 1f),
                new Vector2(CardPadding, -20f), new Vector2(ScrollSize, ScrollSize));
            storageView.ShowIcon(scroll, ScrollMesh.Shared, scrollMaterial, scrollAngles);

            Column(UiText.Bold("Title", contractCard, theme, 36f, theme.Text, TextAlignmentOptions.Left),
                CardColumn, 22f, textWidth, 44f).text = "Контракт";

            // Таймер уходит в шапку справа: в референсе его нет вовсе, а строку под полосой он
            // занимал зря — там место счётчика.
            contractTimer = Column(
                UiText.Label("Timer", contractCard, theme, 26f, theme.Muted, TextAlignmentOptions.Right),
                CardColumn, 30f, textWidth, 32f);

            // Пары «подпись — значение» идут в столбик: подпись сверху, иконка со значением под
            // ней. Так они стоят в референсе, и так значение читается крупнее подписи.
            Column(UiText.Label("GoalLabel", contractCard, theme, 26f, theme.Muted, TextAlignmentOptions.Left),
                CardColumn, 72f, textWidth, 32f).text = "Добыть";
            contractIcon = CreateIcon("GoalIcon", contractCard, 46f);
            Place((RectTransform)contractIcon.transform, new Vector2(0f, 1f),
                new Vector2(CardColumn, -104f), new Vector2(46f, 46f));
            contractGoal = Column(
                UiText.Bold("GoalValue", contractCard, theme, 40f, theme.Text, TextAlignmentOptions.Left),
                CardColumn + 58f, 106f, textWidth - 58f, 44f);

            Column(UiText.Label("RewardLabel", contractCard, theme, 26f, theme.Muted, TextAlignmentOptions.Left),
                CardColumn, 154f, textWidth, 32f).text = "Награда";
            var rewardIcon = CreateIcon("RewardIcon", contractCard, 46f);
            Place((RectTransform)rewardIcon.transform, new Vector2(0f, 1f),
                new Vector2(CardColumn, -186f), new Vector2(46f, 46f));
            storageView.ShowIcon(rewardIcon, coinMesh, coinMaterial, coinAngles);
            contractReward = Column(
                UiText.Bold("RewardValue", contractCard, theme, 40f, theme.Gold, TextAlignmentOptions.Left),
                CardColumn + 58f, 188f, textWidth - 58f, 44f);

            // Счётчик стоит справа от полосы на одной с ней строке, а не под ней.
            contractProgress = Column(
                UiText.Bold("Progress", contractCard, theme, 28f, theme.Text, TextAlignmentOptions.Right),
                CardWidth - CardPadding - ProgressWidth, 238f, ProgressWidth, 32f);
            BuildContractBar(inner - ProgressWidth - 12f);
        }

        /// <summary>
        /// Прогресс контракта: тёмный жёлоб и золотая заливка внутри него. Заливка тянется
        /// якорем, а не `fillAmount`, — так её скруглённые торцы остаются скруглёнными.
        /// </summary>
        void BuildContractBar(float width)
        {
            var track = UiPanel.Create("Bar", contractCard, theme, theme.BarTrack).rectTransform;
            Place(track, new Vector2(0f, 1f), new Vector2(CardPadding, -240f), new Vector2(width, BarHeight));

            contractBarFill = UiPanel.Create("Fill", track, theme, theme.BarFill);
            var fill = contractBarFill.rectTransform;
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(0f, 1f);
            fill.offsetMin = new Vector2(BarInset, BarInset);
            fill.offsetMax = new Vector2(-BarInset, -BarInset);
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

            var card = UiPanel.Create("Toast", toastColumn, theme).rectTransform;
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

        static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }
}
