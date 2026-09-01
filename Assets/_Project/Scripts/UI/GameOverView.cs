using System;
using Game.Core;
using Game.Economy;
using Game.Storage;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Финальный экран: чем кончилась партия, что она принесла и как это смотрится на фоне
    /// рекорда. Собран той же системой, что HUD, — карточка `UiPanel`, палитра и метрика из
    /// `UiTheme`, текст SDF-шрифтом, иконки снимками моделей.
    ///
    /// В отличие от HUD он ловит ввод: с M19 у партии есть кнопки «Повторить карту» и «Новая
    /// карта». Поле после конца партии по-прежнему не принимает ничего — принимает только этот
    /// экран, и разбирает клик он сам, попаданием в прямоугольник кнопки.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public sealed class GameOverView : MonoBehaviour
    {
        /// <summary>Что показывает строка добытого: базовые ресурсы, которые выдало поле.</summary>
        static readonly ResourceType[] MinedTypes = { ResourceType.Wood, ResourceType.Stone, ResourceType.Ore };

        const float CardWidth = 880f;
        const float CardPadding = 28f;
        const float BannerHeight = 210f;
        const float CardHeight = 780f;
        const float ButtonWidth = 420f;
        const float ButtonHeight = 150f;
        const float ButtonGap = 40f;
        const float ButtonBottom = 60f;
        const float CrownSize = 240f;
        const float CoinSize = 100f;
        const float ScrollSize = 76f;
        const float MinedIconSize = 96f;
        const float PlateWidth = 640f;
        const float PlateHeight = 88f;
        const float DividerHeight = 2f;

        [SerializeField] UiTheme theme = new();
        [Tooltip("Затемнение поля под экраном. Карта сквозь него видна, но спорить с карточками " +
                 "перестаёт")]
        [SerializeField] Color backdropColor = new(0.03f, 0.05f, 0.09f, 0.82f);

        [Header("Иконки экрана")]
        [Tooltip("Модель монеты: та же, что у карточки очков в HUD")]
        [SerializeField] Mesh coinMesh;
        [SerializeField] Material coinMaterial;
        [SerializeField] Vector3 coinAngles = new(-10f, 90f, 0f);
        [Tooltip("Материал свитка: сам меш строит `ScrollMesh` кодом")]
        [SerializeField] Material scrollMaterial;
        [SerializeField] Vector3 scrollAngles = new(-14f, 24f, 0f);
        [Tooltip("Материал короны: меш строит `CrownMesh` кодом, модели короны в паке нет")]
        [SerializeField] Material crownMaterial;
        [SerializeField] Vector3 crownAngles = new(-8f, 18f, 0f);

        StorageView icons;
        ProductionSystem production;
        ContractSystem contracts;

        UiButton repeat;
        UiButton renew;
        bool built;

        /// <summary>Игрок просит новую партию. `true` — та же карта, `false` — свежая.</summary>
        public event Action<bool> RestartRequested;

        /// <summary>
        /// Экран берёт снимки моделей у склада — второй пекарь на партию завёл бы второй набор
        /// `RenderTexture` ни за чем, — а числа добытого и закрытых контрактов у тех систем,
        /// которые их и считают.
        /// </summary>
        public void Bind(StorageView storage, ProductionSystem productionSystem, ContractSystem contractSystem)
        {
            icons = storage;
            production = productionSystem;
            contracts = contractSystem;
        }

        /// <summary>Показать итог партии. Второй раз не вызывается: партия кончается один раз.</summary>
        public void Show(FinalScore final)
        {
            if (built)
                return;

            built = true;
            var isRecord = HighScore.Submit(final.Total);

            GetComponent<Image>().color = backdropColor;

            // Корона и конфетти — за пройденное поле, а не за любой конец партии: праздновать
            // тупик, в который игрок сам себя загнал, значит врать ему в лицо.
            if (final.IsPerfect)
            {
                BuildConfetti();
                BuildCrown();
            }

            BuildBanner(final.IsPerfect);
            BuildCard(final, isRecord);
            BuildButtons();

            gameObject.SetActive(true);
        }

        /// <summary>
        /// Клик по экрану: его разбирают только кнопки. Мимо кнопки клик гаснет здесь и на поле
        /// не уходит — партия кончена, и поле уже ничего не принимает.
        /// </summary>
        public void HandleClick(Vector2 screenPosition)
        {
            if (!built)
                return;

            if (!repeat.TryClick(screenPosition))
                renew.TryClick(screenPosition);
        }

        void BuildConfetti()
        {
            var created = new GameObject("Confetti", typeof(RectTransform), typeof(CanvasRenderer), typeof(Confetti));
            ((RectTransform)created.transform).SetParent(transform, false);
            ((RectTransform)created.transform).Stretch();
            created.GetComponent<Confetti>().raycastTarget = false;
        }

        void BuildCrown()
        {
            var crown = CreateIcon("Crown", transform, CrownSize);
            Place(crown.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -40f),
                new Vector2(CrownSize, CrownSize));
            icons.ShowIcon(crown, CrownMesh.Shared, crownMaterial, crownAngles);
        }

        /// <summary>Лента заголовка: чем кончилась партия, одной строкой и подписью под ней.</summary>
        void BuildBanner(bool isPerfect)
        {
            var banner = UiPanel.Create("Banner", transform, theme).rectTransform;
            Place(banner, new Vector2(0.5f, 1f), new Vector2(0f, isPerfect ? -250f : -110f),
                new Vector2(CardWidth, BannerHeight));

            var title = UiText.Bold("Title", banner, theme, 68f, theme.Gold, TextAlignmentOptions.Center);
            Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -30f),
                new Vector2(CardWidth - CardPadding * 2f, 90f));
            title.text = isPerfect ? "Отличная работа!" : "Партия окончена";

            var subtitle = UiText.Label("Subtitle", banner, theme, 36f, theme.Muted, TextAlignmentOptions.Center);
            Place(subtitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -126f),
                new Vector2(CardWidth - CardPadding * 2f, 50f));
            subtitle.text = isPerfect ? "Карта пройдена!" : "Заработать больше нечем";
        }

        /// <summary>
        /// Карточка итога: счёт крупно, добытое иконками, закрытые контракты и рекорд. Слагаемых
        /// счёта тут нет — решение человека 01.09.2026, состав экрана взят с референса.
        /// </summary>
        void BuildCard(FinalScore final, bool isRecord)
        {
            var card = UiPanel.Create("Result", transform, theme).rectTransform;
            Place(card, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(CardWidth, CardHeight));

            var inner = CardWidth - CardPadding * 2f;

            Caption(card, "ScoreLabel", "Получено очков", 30f);

            // Монета и счёт стоят парой в своей коробке по центру карточки: число тут любой
            // длины, и центрировать его отдельно от монеты значило бы возить монету по строке.
            var scoreRow = UiPanel.NewRect("Score", card);
            Place(scoreRow, new Vector2(0.5f, 1f), new Vector2(0f, -76f), new Vector2(480f, 128f));

            var coin = CreateIcon("Coin", scoreRow, CoinSize);
            Place(coin.rectTransform, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(CoinSize, CoinSize));
            icons.ShowIcon(coin, coinMesh, coinMaterial, coinAngles);

            UiText.Bold("Value", scoreRow, theme, 100f, theme.Gold, TextAlignmentOptions.Left)
                .Stretch(CoinSize + 22f, 0f, 0f, 0f)
                .text = HudFormat.Points(final.Total);

            Divider(card, inner, 224f);
            Caption(card, "MinedLabel", "Добыто ресурсов", 250f);
            BuildMined(card, inner);

            Divider(card, inner, 500f);
            Caption(card, "ContractsLabel", "Выполнено контрактов", 526f);
            BuildContracts(card);

            BuildRecord(card, isRecord);
        }

        /// <summary>Добытое: иконка ресурса и число под ней, три столбца в ряд.</summary>
        void BuildMined(RectTransform card, float inner)
        {
            var column = inner / MinedTypes.Length;

            for (var i = 0; i < MinedTypes.Length; i++)
            {
                var left = CardPadding + column * i;

                var icon = CreateIcon($"Mined {MinedTypes[i]}", card, MinedIconSize);
                Place(icon.rectTransform, new Vector2(0f, 1f),
                    new Vector2(left + (column - MinedIconSize) * 0.5f, -300f),
                    new Vector2(MinedIconSize, MinedIconSize));
                icons.ShowIcon(icon, MinedTypes[i]);

                var count = UiText.Bold($"Count {MinedTypes[i]}", card, theme, 48f, theme.Text,
                    TextAlignmentOptions.Center);
                Place(count.rectTransform, new Vector2(0f, 1f), new Vector2(left, -410f), new Vector2(column, 60f));
                count.text = production.MinedOf(MinedTypes[i]).ToString();
            }
        }

        /// <summary>Закрытые контракты: свиток и число рядом, той же парой, что монета и счёт.</summary>
        void BuildContracts(RectTransform card)
        {
            var row = UiPanel.NewRect("Contracts", card);
            Place(row, new Vector2(0.5f, 1f), new Vector2(0f, -570f), new Vector2(300f, 90f));

            var scroll = CreateIcon("ScrollIcon", row, ScrollSize);
            Place(scroll.rectTransform, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(ScrollSize, ScrollSize));
            icons.ShowIcon(scroll, ScrollMesh.Shared, scrollMaterial, scrollAngles);

            UiText.Bold("Value", row, theme, 60f, theme.Text, TextAlignmentOptions.Left)
                .Stretch(ScrollSize + 20f, 0f, 0f, 0f)
                .text = contracts.CompletedCount.ToString();
        }

        /// <summary>Плашка рекорда: побитый — золотом, иначе прежний счёт спокойной строкой.</summary>
        void BuildRecord(RectTransform card, bool isRecord)
        {
            var plate = UiPanel.Create("Record", card, theme, theme.Accent).rectTransform;
            Place(plate, new Vector2(0.5f, 1f), new Vector2(0f, -668f), new Vector2(PlateWidth, PlateHeight));

            var line = UiText.Bold("Text", plate, theme, 40f, isRecord ? theme.Gold : theme.Muted,
                TextAlignmentOptions.Center);
            line.Stretch(20f, 20f, 8f, 8f);
            line.text = isRecord ? "Новый рекорд!" : "Рекорд: " + HudFormat.Points(HighScore.Best);
        }

        /// <summary>
        /// Кнопки внизу экрана. «Новая карта» — главная и тёплая: партия кончилась, и по
        /// умолчанию игрок идёт дальше, а не переигрывает ту же карту.
        /// </summary>
        void BuildButtons()
        {
            var offset = (ButtonWidth + ButtonGap) * 0.5f;

            repeat = UiButton.Create("Repeat", transform, theme, theme.ButtonSecondary,
                "Повторить\nкарту", 42f, () => RestartRequested?.Invoke(true));
            Place(repeat.Rect, new Vector2(0.5f, 0f), new Vector2(-offset, ButtonBottom),
                new Vector2(ButtonWidth, ButtonHeight));

            renew = UiButton.Create("Renew", transform, theme, theme.ButtonPrimary,
                "Новая карта", 46f, () => RestartRequested?.Invoke(false));
            Place(renew.Rect, new Vector2(0.5f, 0f), new Vector2(offset, ButtonBottom),
                new Vector2(ButtonWidth, ButtonHeight));
        }

        /// <summary>Подпись блока: мелкая строка по центру карточки.</summary>
        void Caption(RectTransform card, string name, string text, float top)
        {
            var label = UiText.Label(name, card, theme, 30f, theme.Muted, TextAlignmentOptions.Center);
            Place(label.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -top),
                new Vector2(CardWidth - CardPadding * 2f, 40f));
            label.text = text;
        }

        /// <summary>Линия между блоками карточки: та же карточка толщиной в пару пикселей.</summary>
        void Divider(RectTransform card, float width, float top)
        {
            var line = UiPanel.Create("Divider", card, theme, theme.Divider).rectTransform;
            Place(line, new Vector2(0.5f, 1f), new Vector2(0f, -top), new Vector2(width, DividerHeight));
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

        static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }
}
