using System.Collections;
using Game.Core;
using Game.Economy;
using Game.Storage;
using TMPro;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// HUD поля карточками: счёт слева сверху, крафтовые ресурсы справа сверху и карточка
    /// контракта под ними. Всё рисуется кодом на скруглённых спрайтах и SDF-шрифте — панели
    /// в проекте не ассеты, а числа палитры.
    ///
    /// Сообщений в углу экрана HUD не держит: прибавка, отказ и награда всплывают попапами
    /// над тем объектом, с которым игрок работает, — этим занят <see cref="PopupView"/>.
    /// </summary>
    public sealed class HudView : MonoBehaviour
    {
        /// <summary>Что показывает полоса ресурсов: крафт, который меняют на очки.</summary>
        static readonly ResourceType[] StripTypes = { ResourceType.Gravel, ResourceType.Board, ResourceType.Ingot };

        const float Margin = 24f;
        const float TopHeight = 116f;
        const float PointsWidth = 320f;
        /// <summary>
        /// Докуда ужимается счёт: место под него в карточке одно, а число растёт. 62 держит
        /// четыре знака, 34 — восемь, и до восьми знаков партия не доходит ни на одном уровне.
        /// </summary>
        const float PointsMinSize = 34f;
        const float IslandWidth = 158f;
        const float IslandSpacing = 12f;
        const float IslandPadding = 14f;
        const float StripPadding = 16f;
        const float IconSize = 56f;
        const float CoinSize = 64f;
        const float ScrollSize = 56f;
        // Карточка контракта прижата к правому краю и узкая: её ширину задаёт шапка — свиток
        // и слово «Контракт», — а не самая длинная строка внутри. Решение человека: карточка
        // не должна отъедать половину верха экрана, поле под ней важнее.
        const float CardWidth = 300f;
        const float CardHeight = 302f;
        const float CardPadding = 18f;
        /// <summary>Левая колонка карточки под свиток; текст шапки идёт правее неё.</summary>
        const float CardColumn = 84f;
        /// <summary>
        /// Отступ карточки от верхней полосы. Он заметно больше зазора между карточками сверху:
        /// карточка контракта висит сама по себе, и прижатая к полосе она читалась её продолжением.
        /// </summary>
        const float CardTopGap = 44f;
        const float BarHeight = 26f;
        const float ProgressWidth = 76f;
        const float BarInset = 3f;

        /// <summary>
        /// Бар до потолка партии — своя узкая карточка под карточкой очков, шириной с неё.
        /// Внутрь карточки очков он не влез бы: крупное число и монета заняли её целиком, а
        /// звёздам и подписи «рекорд карты» там места нет вовсе.
        /// </summary>
        const float CeilingCardHeight = 78f;
        const float CeilingCardGap = 12f;
        const float CeilingPadding = 16f;
        const float CeilingBarHeight = 24f;
        const float CeilingCaptionHeight = 30f;
        /// <summary>Ширина колонки под процент справа от подписи «рекорд карты».</summary>
        const float CeilingPercentWidth = 100f;
        const float StarSize = 26f;

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

        [Header("Анимация карточки контракта")]
        [Tooltip("Сколько карточка выезжает при выдаче контракта")]
        [SerializeField] float cardShowSeconds = 0.28f;
        [Tooltip("Сколько она уходит, когда контракт закрыт или провален")]
        [SerializeField] float cardHideSeconds = 0.22f;
        [Tooltip("С какого масштаба карточка появляется и до какого схлопывается")]
        [SerializeField, Range(0.5f, 1f)] float cardMinScale = 0.82f;
        [Tooltip("Перелёт карточки за единицу масштаба: без него появление читается вялым")]
        [SerializeField, Range(1f, 1.3f)] float cardOvershoot = 1.06f;
        [Tooltip("Сколько сданный ресурс летит из клетки склада в карточку контракта")]
        [SerializeField] float deliveryFlySeconds = 0.42f;
        [Tooltip("Насколько высоко ресурс выгибает дугу по пути, пиксели канваса")]
        [SerializeField] float deliveryArc = 140f;
        [Tooltip("Во сколько раз иконка цели подскакивает, приняв ресурс")]
        [SerializeField, Range(1f, 1.6f)] float goalPunch = 1.35f;
        [Tooltip("Во сколько раз бар подскакивает на пройденной вехе")]
        [SerializeField, Range(1f, 1.4f)] float milestonePunch = 1.14f;

        readonly TextMeshProUGUI[] stripCounts = new TextMeshProUGUI[StripTypes.Length];

        /// <summary>Ширина канваса, на которой верх раскладывался в последний раз.</summary>
        float appliedCanvasWidth = -1f;

        /// <summary>Полоса ресурсов: её кладут в строку вместе с очками и контрактом.</summary>
        RectTransform resourceStrip;

        /// <summary>Ширина полосы ресурсов: она считается из числа островков, а не из константы.</summary>
        static float StripWidth =>
            StripTypes.Length * IslandWidth + (StripTypes.Length - 1) * IslandSpacing + StripPadding * 2f;

        /// <summary>Ширина строки из трёх карточек, поставленных встык через <see cref="Margin"/>.</summary>
        static float RowWidth => PointsWidth + Margin + StripWidth + Margin + CardWidth;

        /// <summary>Верх по центру: якорь строки. Пивот там же, поэтому позиция — это верх карточки.</summary>
        static readonly Vector2 TopCenter = new(0.5f, 1f);

        TextMeshProUGUI pointsValue;
        RectTransform pointsCard;
        RectTransform ceilingCard;
        RectTransform ceilingTrack;
        UiPanelGraphic ceilingFill;
        TextMeshProUGUI ceilingPercent;
        TextMeshProUGUI ceilingRecord;
        StarGraphic[] ceilingStars;
        float[] starShares;

        /// <summary>Потолок партии в очках: доли звёзд и вех считаются от него. Ноль — бара нет.</summary>
        int ceiling;

        /// <summary>Залит ли бар золотом рекорда: перекрашивать его каждый кадр незачем.</summary>
        bool goldFill;

        RectTransform contractCard;
        ResourceIcon contractIcon;
        TextMeshProUGUI contractGoal;
        TextMeshProUGUI contractReward;
        TextMeshProUGUI contractProgress;
        TextMeshProUGUI contractTimer;
        UiPanelGraphic contractBarFill;
        PopupView popups;

        CanvasGroup contractFade;
        Coroutine cardAnimation;
        bool cardShown;

        /// <summary>Сколько ресурсов сейчас летит в карточку: пока летят, она не уходит.</summary>
        int flyingToContract;

        int shownSeconds = -1;
        ContractSystem contracts;
        ScoreMultiplier multiplier;
        Wallet wallet;
        StorageGrid storage;
        StorageView storageView;

        /// <summary>
        /// Склад передаётся сюда не ради данных, а ради иконок: снимки моделей печёт он, и
        /// второй такой же пекарь на партию — это второй набор `RenderTexture` ни за чем.
        ///
        /// Потолок партии приходит готовым числом: в кампании он лежит в `LevelConfig`, вне её
        /// снят ботом на старте — считать его HUD не вправе. Доли звёзд есть только у уровня
        /// кампании: вне её звёзд не выдают, и обещать их отметками на баре было бы враньём.
        /// </summary>
        public void Bind(
            GameState game, ContractSystem contractSystem, StorageView storage, int gameCeiling, float[] stars)
        {
            contracts = contractSystem;
            multiplier = game.Multiplier;
            wallet = game.Wallet;
            this.storage = game.Storage;
            storageView = storage;
            ceiling = gameCeiling;
            starShares = stars;

            BuildPointsCard();
            BuildCeilingCard();
            BuildResourceStrip();
            BuildContractCard();
            LayoutTop();

            // Слой попапов заводится последним ребёнком: попап встаёт над объектом и должен
            // идти поверх карточек HUD, а порядок рисования в канвасе — это порядок иерархии.
            popups = PopupView.Create((RectTransform)transform, theme, storage, multiplier);
            popups.BindCoin(coinMesh, coinMaterial, coinAngles);

            wallet.Changed += Refresh;
            this.storage.Changed += Refresh;
            contracts.Issued += RefreshContract;
            contracts.Progressed += RefreshContract;
            contracts.Failed += RefreshContract;
            contracts.Completed += OnContractCompleted;
            // Награда контракта живёт с множителем: открытая плитка меняет её на карточке.
            multiplier.Changed += RefreshContract;
            Refresh();
        }

        /// <summary>
        /// Высота верхней полосы HUD в пикселях экрана: от кромки кадра до низа карточек.
        /// По ней камера узнаёт, какую часть кадра поле занимает, но показать не может.
        /// </summary>
        public float TopHeightPixels
        {
            get
            {
                // Нижняя карточка левой колонки, а не карточка очков: бар потолка висит под ней
                // и тоже закрывает поле. Карточку контракта здесь по-прежнему не считают — она
                // приходит и уходит, и камера ходила бы за ней.
                var lowest = ceilingCard != null ? ceilingCard : pointsCard;
                var canvas = lowest != null ? lowest.GetComponentInParent<Canvas>() : null;
                if (canvas == null)
                    return 0f;

                // Расстояние от верха канваса до низа карточки. Канвас экранный, его пиксели —
                // это пиксели экрана; отсчёт от него, а не от `Screen.height`, не врёт и до
                // того, как канвас узнал размер окна. Вырез под чёлку учтён: полосу в него уже
                // не пустил `SafeAreaFitter`.
                var corners = new Vector3[4];
                lowest.GetWorldCorners(corners);
                var cardBottom = corners[0].y;

                ((RectTransform)canvas.transform).GetWorldCorners(corners);
                return Mathf.Max(corners[1].y - cardBottom, 0f);
            }
        }

        /// <summary>
        /// Попапы над объектами. Их заводит и держит HUD: слой живёт в его иерархии, а иконки
        /// печёт тот же пекарь снимков, — но кто и над чем их показывает, знает партия.
        /// </summary>
        public PopupView Popups => popups;

        /// <summary>Карточка бара с потолком партии: над ней встаёт подсказка про цель партии.</summary>
        public RectTransform CeilingCard => ceilingCard;

        /// <summary>Карточка контракта: над ней встаёт подсказка про заказ Метрополии.</summary>
        public RectTransform ContractCard => contractCard;

        /// <summary>
        /// Сданный по контракту ресурс летит из своей клетки склада в карточку: без этого
        /// прогресс менялся сам по себе, и связь между кликом на складе и полосой контракта
        /// игрок читал только по числу. Пока ресурс в пути, карточка не уходит с экрана, даже
        /// если этот же ресурс контракт и закрыл.
        /// </summary>
        public void PlayContractDelivery(Vector3 origin, ResourceType type)
        {
            if (!isActiveAndEnabled || !contractCard.gameObject.activeSelf)
                return;

            flyingToContract++;
            StartCoroutine(FlyToContract(origin, type));
        }

        IEnumerator FlyToContract(Vector3 origin, ResourceType type)
        {
            var flying = CreateIcon("Delivery", transform, IconSize);
            flying.rectTransform.SetAsLastSibling();
            flying.rectTransform.position = origin;
            storageView.ShowIcon(flying, type);

            var target = (RectTransform)contractIcon.transform;

            for (var elapsed = 0f; elapsed < deliveryFlySeconds; elapsed += Time.deltaTime)
            {
                var progress = Mathf.Clamp01(elapsed / deliveryFlySeconds);
                var eased = Mathf.SmoothStep(0f, 1f, progress);

                // Цель берётся каждый кадр: карточка в это время может ещё выезжать.
                var point = Vector3.Lerp(origin, target.position, eased);

                // Дуга — половина синуса: ресурс уходит вверх и падает в карточку, а не ползёт
                // по прямой через полэкрана.
                point.y += Mathf.Sin(eased * Mathf.PI) * deliveryArc;
                flying.rectTransform.position = point;
                flying.rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, 0.7f, eased);
                yield return null;
            }

            Destroy(flying.gameObject);
            flyingToContract--;

            if (contractCard.gameObject.activeSelf)
                yield return Punch(target, goalPunch);
        }

        /// <summary>Иконка цели принимает ресурс: короткий подскок масштабом и обратно.</summary>
        IEnumerator Punch(RectTransform rect, float scale)
        {
            const float PunchSeconds = 0.16f;

            for (var elapsed = 0f; elapsed < PunchSeconds; elapsed += Time.deltaTime)
            {
                var progress = elapsed / PunchSeconds;
                rect.localScale = Vector3.one * (progress < 0.5f
                    ? Mathf.Lerp(1f, scale, progress * 2f)
                    : Mathf.Lerp(scale, 1f, (progress - 0.5f) * 2f));
                yield return null;
            }

            rect.localScale = Vector3.one;
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
            multiplier.Changed -= RefreshContract;
        }

        void Update()
        {
            // Окно браузера тянут, телефон поворачивают — верх раскладывается заново. Проверка
            // на собранность обязательна: вью лежит в сцене и живёт с её загрузки, а собирается
            // только в `Bind` — до первой партии трогать здесь нечего.
            if (pointsCard != null)
                LayoutTop();

            // Обратный отсчёт перерисовывается только на смене целой секунды: строка каждый кадр
            // ничего не добавляет глазу, зато мусорит строками в куче.
            if (contracts != null && contracts.IsActive && Mathf.CeilToInt(contracts.SecondsLeft) != shownSeconds)
                RefreshContract();
        }

        /// <summary>
        /// Раскладывает верх HUD. Считает по ширине родителя, а не по `Screen.width`: родитель —
        /// это уже безопасная зона в пикселях канваса, то есть тот же счёт, в котором заданы
        /// отступы карточек.
        /// </summary>
        void LayoutTop()
        {
            var width = ((RectTransform)transform).rect.width;
            if (width <= 0f || Mathf.Approximately(width, appliedCanvasWidth))
                return;

            appliedCanvasWidth = width;
            if (width >= RowWidth)
                LayoutRow();
            else
                LayoutCorners();
        }

        /// <summary>
        /// Широкий кадр: очки, ресурсы и контракт стоят одной строкой по центру, верхними
        /// кромками на одном уровне. Решение человека 09.09.2026: по углам канваса, который
        /// втрое шире эталона, они разбегались, и глаз ходил от угла к углу.
        /// </summary>
        void LayoutRow()
        {
            var left = -RowWidth * 0.5f;

            var pointsX = left + PointsWidth * 0.5f;
            Move(pointsCard, TopCenter, new Vector2(pointsX, -Margin));
            Move(ceilingCard, TopCenter, new Vector2(pointsX, -(Margin + TopHeight + CeilingCardGap)));
            Move(resourceStrip, TopCenter, new Vector2(left + PointsWidth + Margin + StripWidth * 0.5f, -Margin));
            Move(contractCard, TopCenter, new Vector2(-left - CardWidth * 0.5f, -Margin));
        }

        /// <summary>
        /// Узкий кадр — портрет телефона: строка в него не влезает (эталон 1080, строке нужно
        /// около 1200), и карточки остаются по углам, как задумывались. Карточка контракта
        /// висит под полосой ресурсов, а не рядом с ней.
        /// </summary>
        void LayoutCorners()
        {
            var left = new Vector2(0f, 1f);
            var right = new Vector2(1f, 1f);

            Move(pointsCard, left, new Vector2(Margin, -Margin));
            Move(ceilingCard, left, new Vector2(Margin, -(Margin + TopHeight + CeilingCardGap)));
            Move(resourceStrip, right, new Vector2(-Margin, -Margin));
            Move(contractCard, right, new Vector2(-Margin, -(Margin + TopHeight + CardTopGap)));
        }

        /// <summary>
        /// Переставляет карточку, не трогая её размер: размеры заданы при сборке, и строка
        /// с углами отличаются только тем, где карточка стоит. Ноль пропускается — карточки
        /// потолка нет, когда потолка нет.
        /// </summary>
        static void Move(RectTransform rect, Vector2 anchor, Vector2 position)
        {
            if (rect == null)
                return;

            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = position;
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
                HideContractCard();
                shownSeconds = -1;
                return;
            }

            ShowContractCard();
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

        /// <summary>
        /// Контракт закрыт: награда всплывает попапом над самой карточкой. Она и есть объект,
        /// с которым игрок работал, — ресурсы он сдавал в неё.
        /// </summary>
        void OnContractCompleted(int reward) =>
            popups.ShowGain(reward, contracts.Type, PopupView.Anchor.On(contractCard));

        /// <summary>
        /// Контракт выдан: карточка выезжает масштабом и прозрачностью. Между контрактами
        /// Метрополия молчит (3.10), и карточки в это время нет вовсе — появляться ей теперь
        /// есть откуда, поэтому появление и показывается, а не включается кадром.
        /// </summary>
        void ShowContractCard()
        {
            if (cardShown)
                return;

            cardShown = true;
            contractCard.gameObject.SetActive(true);

            if (!Animate(ShowCard()))
                SetCard(1f, 1f);
        }

        /// <summary>
        /// Контракт закрыт или провален: карточка уходит. Уходит она не раньше, чем долетит
        /// последний сданный ресурс, — иначе он падал бы в пустое место.
        /// </summary>
        void HideContractCard()
        {
            if (!cardShown)
                return;

            cardShown = false;

            if (!Animate(HideCard()))
            {
                SetCard(0f, 1f);
                contractCard.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Анимация карточки всегда одна: новая обрывает недоигранную прежнюю. На выключенном
        /// HUD корутина не заводится вовсе — тогда вызвавший ставит конечное состояние сам,
        /// иначе карточка застыла бы прозрачной.
        /// </summary>
        bool Animate(IEnumerator animation)
        {
            if (cardAnimation != null)
                StopCoroutine(cardAnimation);

            cardAnimation = null;
            if (!isActiveAndEnabled)
                return false;

            cardAnimation = StartCoroutine(animation);
            return true;
        }

        /// <summary>Прозрачность и масштаб карточки разом: конец любой её анимации.</summary>
        void SetCard(float alpha, float scale)
        {
            contractFade.alpha = alpha;
            contractCard.localScale = Vector3.one * scale;
        }

        IEnumerator ShowCard()
        {
            for (var elapsed = 0f; elapsed < cardShowSeconds; elapsed += Time.deltaTime)
            {
                var progress = Mathf.Clamp01(elapsed / cardShowSeconds);
                contractFade.alpha = progress;

                // Перелёт за единицу и возврат: карточка «доезжает» до места, а не тормозит в нём.
                contractCard.localScale = Vector3.one * (progress < 0.7f
                    ? Mathf.Lerp(cardMinScale, cardOvershoot, progress / 0.7f)
                    : Mathf.Lerp(cardOvershoot, 1f, (progress - 0.7f) / 0.3f));
                yield return null;
            }

            SetCard(1f, 1f);
            cardAnimation = null;
        }

        IEnumerator HideCard()
        {
            while (flyingToContract > 0)
                yield return null;

            for (var elapsed = 0f; elapsed < cardHideSeconds; elapsed += Time.deltaTime)
            {
                var progress = Mathf.Clamp01(elapsed / cardHideSeconds);
                contractFade.alpha = 1f - progress;
                contractCard.localScale = Vector3.one * Mathf.Lerp(1f, cardMinScale, progress);
                yield return null;
            }

            SetCard(0f, 1f);
            contractCard.gameObject.SetActive(false);
            cardAnimation = null;
        }

        void BuildPointsCard()
        {
            var card = pointsCard = UiPanel.Create("Points", transform, theme).rectTransform;
            Place(card, new Vector2(0f, 1f), new Vector2(Margin, -Margin), new Vector2(PointsWidth, TopHeight));

            // Монета вместо подписи «ОЧКИ»: в референсе у счёта иконка, а не слово, и мелкая
            // подпись на прозрачном стекле всё равно была самым слабым местом карточки.
            var coin = CreateIcon("Coin", card, CoinSize);
            coin.rectTransform.anchorMin = coin.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            coin.rectTransform.pivot = new Vector2(0f, 0.5f);
            coin.rectTransform.anchoredPosition = new Vector2(22f, 0f);
            storageView.ShowIcon(coin, coinMesh, coinMaterial, coinAngles);

            pointsValue = UiText.Bold("Value", card, theme, 62f, theme.Gold, TextAlignmentOptions.Right);
            pointsValue.Stretch(22f + CoinSize + 10f, 26f, 0f, 0f).Fit(PointsMinSize);
        }

        /// <summary>
        /// Бар до потолка партии: жёлоб с заливкой, процент справа сверху и подпись «рекорд
        /// карты» слева от него, а на самом жёлобе — звёзды уровня на своих долях. Вехи своих
        /// отметок не имеют намеренно: их доли (25 / 50 / 75%) наложились бы на звёздные
        /// (50 / 75 / 100%), и одна полоса читалась бы двумя разметками сразу. Веха проявляется
        /// моментом — вспышкой бара и попапом, — а постоянная разметка на нём одна, звёздная.
        ///
        /// Потолка нет — нет и карточки: показывать долю не от чего.
        /// </summary>
        void BuildCeilingCard()
        {
            if (ceiling <= 0)
                return;

            var card = ceilingCard = UiPanel.Create("Ceiling", transform, theme).rectTransform;
            Place(card, new Vector2(0f, 1f), new Vector2(Margin, -(Margin + TopHeight + CeilingCardGap)),
                new Vector2(PointsWidth, CeilingCardHeight));

            var inner = PointsWidth - CeilingPadding * 2f;

            // Подпись появляется только за 100%: до него она пустует, и место под ней занимает
            // сам бар, а не строка «пока не рекорд».
            ceilingRecord = UiText.Bold(
                "Record", card, theme, 22f, theme.Gold, TextAlignmentOptions.Left);
            Place(ceilingRecord.rectTransform, new Vector2(0f, 1f), new Vector2(CeilingPadding, -8f),
                new Vector2(inner - CeilingPercentWidth, CeilingCaptionHeight));
            ceilingRecord.text = "рекорд карты";
            ceilingRecord.enabled = false;

            ceilingPercent = UiText.Bold("Percent", card, theme, 26f, theme.Text, TextAlignmentOptions.Right);
            Place(ceilingPercent.rectTransform, new Vector2(0f, 1f),
                new Vector2(PointsWidth - CeilingPadding - CeilingPercentWidth, -8f),
                new Vector2(CeilingPercentWidth, CeilingCaptionHeight));

            var track = ceilingTrack = UiPanel.Create("Bar", card, theme, theme.BarTrack).rectTransform;
            Place(track, new Vector2(0f, 1f), new Vector2(CeilingPadding, -44f),
                new Vector2(inner, CeilingBarHeight));

            ceilingFill = UiPanel.Create("Fill", track, theme, theme.CeilingFill);
            var fill = ceilingFill.rectTransform;
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(0f, 1f);
            fill.offsetMin = new Vector2(BarInset, BarInset);
            fill.offsetMax = new Vector2(-BarInset, -BarInset);

            BuildStars(track, inner);
        }

        /// <summary>
        /// Звёзды уровня стоят на жёлобе, каждая на своей доле потолка: игрок видит не только
        /// «сколько набрал», но и «дотягиваю ли до второй». Заводятся после заливки — порядок
        /// рисования в канвасе это порядок иерархии, и заведённые раньше ушли бы под неё.
        /// </summary>
        void BuildStars(RectTransform track, float width)
        {
            if (starShares == null || starShares.Length == 0)
                return;

            ceilingStars = new StarGraphic[starShares.Length];
            for (var i = 0; i < starShares.Length; i++)
            {
                var star = ceilingStars[i] = StarGraphic.Create($"Star {i + 1}", track, StarSize, theme.Muted);

                // Звезда стоит центром на своей доле: у трёх звёзд она же и правый торец жёлоба,
                // и звезда садится на него, наполовину свесившись, — так и должно быть.
                var rect = star.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = new Vector2(width * starShares[i], 0f);
            }
        }

        /// <summary>
        /// Счёт изменился: бар показывает долю от потолка. За 100% полоса упирается в край и
        /// перекрашивается в золото — дальше расти ей некуда, а разница между «дотянул» и
        /// «обыграл бота» обязана быть видна. Процент при этом за 100% уходит: он и есть то,
        /// насколько игрок ушёл вперёд.
        /// </summary>
        public void ShowProgress(int total)
        {
            if (ceilingCard == null)
                return;

            var share = total / (float)ceiling;
            ceilingPercent.text = HudFormat.Percent(share);

            var record = share >= 1f;
            ceilingRecord.enabled = record;
            ceilingPercent.color = record ? theme.Gold : theme.Text;

            var filled = Mathf.Clamp01(share);
            ceilingFill.enabled = filled > 0f;
            ceilingFill.rectTransform.anchorMax = new Vector2(filled, 1f);
            SetFillGold(record);

            if (ceilingStars == null)
                return;

            for (var i = 0; i < ceilingStars.Length; i++)
                ceilingStars[i].color = share >= starShares[i] ? theme.Gold : theme.Muted;
        }

        /// <summary>
        /// Пройдена веха: плашка над баром и его короткий подскок. Отдельной анимации у вехи
        /// нет — решение человека: третий вид празднования игроку не нужен, а плашка прибавки
        /// уже читается как начисление.
        /// </summary>
        public void PlayMilestone(float share, int gravel)
        {
            if (ceilingCard == null)
                return;

            popups.ShowMilestone(share, gravel, PopupView.Anchor.On(ceilingCard));

            if (isActiveAndEnabled)
                StartCoroutine(FlashCeilingBar());
        }

        /// <summary>Вспышка бара: заливка на миг уходит в золото и подскакивает вместе с жёлобом.</summary>
        IEnumerator FlashCeilingBar()
        {
            ApplyFill(true);
            yield return Punch(ceilingTrack, milestonePunch);

            // Возвращается то, что бару положено по счёту: вспышка могла застать его уже золотым.
            ApplyFill(goldFill);
        }

        /// <summary>
        /// Цвет заливки по счёту. Стиль применяется только на смене: `Apply` пересобирает меш
        /// графики, а счёт меняется каждым обменом.
        /// </summary>
        void SetFillGold(bool gold)
        {
            if (goldFill == gold)
                return;

            goldFill = gold;
            ApplyFill(gold);
        }

        void ApplyFill(bool gold) =>
            ceilingFill.Apply(theme.PanelShader, gold ? theme.BarFill : theme.CeilingFill);

        void BuildResourceStrip()
        {
            var width = StripWidth;
            var strip = resourceStrip = UiPanel.Create("Resources", transform, theme).rectTransform;
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
                new Vector2(-Margin, -(Margin + TopHeight + CardTopGap)), new Vector2(CardWidth, CardHeight));

            // Прозрачность всей карточки разом: у неё своя графика, три иконки и пять строк,
            // и гасить их по отдельности значило бы держать список того, что гасить.
            contractFade = contractCard.gameObject.AddComponent<CanvasGroup>();
            contractCard.gameObject.SetActive(false);

            var inner = CardWidth - CardPadding * 2f;
            var headWidth = CardWidth - CardColumn - CardPadding;

            // Шапка: свиток слева, слово «Контракт» справа от него. Ширина карточки посчитана
            // ровно по этой строке — с запасом на пробел, но без места на что-то ещё.
            var scroll = CreateIcon("ScrollIcon", contractCard, ScrollSize);
            Place((RectTransform)scroll.transform, new Vector2(0f, 1f),
                new Vector2(CardPadding, -18f), new Vector2(ScrollSize, ScrollSize));
            storageView.ShowIcon(scroll, ScrollMesh.Shared, scrollMaterial, scrollAngles);

            Column(UiText.Bold("Title", contractCard, theme, 34f, theme.Text, TextAlignmentOptions.Left),
                CardColumn, 18f, headWidth, 42f).text = "Контракт";

            // Таймер сошёл со строки заголовка под неё: в шапку узкой карточки он больше не
            // влезает, а место под заголовком всё равно пустует — слева там свиток.
            contractTimer = Column(
                UiText.Label("Timer", contractCard, theme, 24f, theme.Muted, TextAlignmentOptions.Right),
                CardColumn, 58f, headWidth, 28f);

            // Пары «подпись — значение» идут в столбик под шапкой во всю ширину карточки:
            // левая колонка ниже свитка свободна, и отступать от неё узкой карточке нечем.
            Column(UiText.Label("GoalLabel", contractCard, theme, 24f, theme.Muted, TextAlignmentOptions.Left),
                CardPadding, 96f, inner, 30f).text = "Добыть";
            contractIcon = CreateIcon("GoalIcon", contractCard, 44f);
            Place((RectTransform)contractIcon.transform, new Vector2(0f, 1f),
                new Vector2(CardPadding, -124f), new Vector2(44f, 44f));
            contractGoal = Column(
                UiText.Bold("GoalValue", contractCard, theme, 36f, theme.Text, TextAlignmentOptions.Left),
                CardPadding + 54f, 126f, inner - 54f, 40f);

            Column(UiText.Label("RewardLabel", contractCard, theme, 24f, theme.Muted, TextAlignmentOptions.Left),
                CardPadding, 174f, inner, 30f).text = "Награда";
            var rewardIcon = CreateIcon("RewardIcon", contractCard, 44f);
            Place((RectTransform)rewardIcon.transform, new Vector2(0f, 1f),
                new Vector2(CardPadding, -202f), new Vector2(44f, 44f));
            storageView.ShowIcon(rewardIcon, coinMesh, coinMaterial, coinAngles);
            contractReward = Column(
                UiText.Bold("RewardValue", contractCard, theme, 36f, theme.Gold, TextAlignmentOptions.Left),
                CardPadding + 54f, 204f, inner - 54f, 40f);

            // Счётчик стоит справа от полосы на одной с ней строке, а не под ней.
            contractProgress = Column(
                UiText.Bold("Progress", contractCard, theme, 26f, theme.Text, TextAlignmentOptions.Right),
                CardWidth - CardPadding - ProgressWidth, 252f, ProgressWidth, 30f);
            BuildContractBar(inner - ProgressWidth - 10f);
        }

        /// <summary>
        /// Прогресс контракта: тёмный жёлоб и золотая заливка внутри него. Заливка тянется
        /// якорем, а не `fillAmount`, — так её скруглённые торцы остаются скруглёнными.
        /// </summary>
        void BuildContractBar(float width)
        {
            var track = UiPanel.Create("Bar", contractCard, theme, theme.BarTrack).rectTransform;
            Place(track, new Vector2(0f, 1f), new Vector2(CardPadding, -254f), new Vector2(width, BarHeight));

            contractBarFill = UiPanel.Create("Fill", track, theme, theme.BarFill);
            var fill = contractBarFill.rectTransform;
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(0f, 1f);
            fill.offsetMin = new Vector2(BarInset, BarInset);
            fill.offsetMax = new Vector2(-BarInset, -BarInset);
        }

        static ResourceIcon CreateIcon(string name, Transform parent, float size) =>
            ResourceIcon.Create(name, parent, size);

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
