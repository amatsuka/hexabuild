using System.Collections;
using System.Collections.Generic;
using Game.Audio;
using Game.Economy;
using Game.Grid;
using Game.Merge;
using Game.Roads;
using Game.Storage;
using Game.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Core
{
    /// <summary>Точка входа: создаёт партию и системы, порождает визуалы и связывает подписки.</summary>
    public sealed class GameSession : MonoBehaviour
    {
        [SerializeField] GameConfig config;
        [SerializeField] MergeRules mergeRules;
        [SerializeField] TileView tilePrefab;
        [SerializeField] RoadView roadPrefab;
        [SerializeField] ResourceMover moverPrefab;
        [SerializeField] Transform tilesRoot;
        [Tooltip("Материал `Game/Water` на подложку. Пусто — воды нет и виден фон камеры")]
        [SerializeField] Material waterMaterial;
        [Tooltip("Насколько подложка выступает за поле. Камера зажата границами поля, но зумом отходит от них")]
        [SerializeField] float waterMargin = 40f;
        [SerializeField] Transform moversRoot;
        [SerializeField] GameInput input;
        [SerializeField] CameraRig cameraRig;
        [SerializeField] StorageView storageView;
        [Tooltip("Пульс виньетки на высоком накале. Пусто — эффекта нет, партия идёт как раньше")]
        [SerializeField] HeatVignette heatVignette;
        [Tooltip("Провал цвета в момент, когда переполнение сожгло накал. Пусто — кадр не реагирует")]
        [SerializeField] BurnFlash burnFlash;
        [Tooltip("Проба M30: остановка времени на крупном слиянии. Пусто — партия идёт как раньше")]
        [SerializeField] Hitstop hitstop;
        [Tooltip("Шаг волны продажи пачкой: столько между двумя проданными единицами")]
        [SerializeField] float sellStepSeconds = 0.05f;

        [SerializeField] HudView hudView;
        [SerializeField] GameOverView gameOverView;
        [SerializeField] PauseView pauseView;

        readonly Dictionary<HexCoord, TileView> views = new();
        readonly Dictionary<Delivery, ResourceMover> movers = new();
        readonly List<HexCoord> path = new();

        /// <summary>Самая высокая крышка поля: отсюда начинает спуск луч клика.</summary>
        float fieldCeiling;

        /// <summary>Размер экрана, под который камере посчитаны полосы интерфейса.</summary>
        Vector2Int viewport;

        /// <summary>
        /// Над чем всплывёт отказ. Правило сообщает о нём событием, а место взаимодействия
        /// знает только тот, кто это правило вызвал, — здесь оно и запоминается перед вызовом.
        /// </summary>
        PopupView.Anchor refusalAnchor;

        /// <summary>
        /// Что дрожит на отказе: клетка склада или плитка поля. Ровно одно из двух — отказ
        /// приходит на то же взаимодействие, что задало <see cref="refusalAnchor"/>.
        /// </summary>
        TileView refusalTile;

        int refusalCell = -1;

        /// <summary>
        /// Плитка, за которую сейчас висит подтверждение открытия. Второй тап именно по ней —
        /// и есть согласие, поэтому её координата живёт рядом с попапом, пока он не снят.
        /// </summary>
        HexCoord? askedTile;

        /// <summary>Что прижато пальцем прямо сейчас. Указатель один, поэтому и цель одна.</summary>
        TileView pressedTile;

        int pressedCell = -1;

        /// <summary>
        /// Сид этой партии — уже разрешённый, а не ноль из конфига. По нему живут и карта, и
        /// контракты, и его же забирает кнопка «Повторить карту» на финальном экране.
        /// </summary>
        int seed;

        /// <summary>Уровень кампании этой партии. Пусто — свободная игра.</summary>
        LevelConfig level;

        /// <summary>Потолок партии в очках: счёт бота на этой карте. От него считаются звёзды и вехи.</summary>
        int ceiling;

        /// <summary>Вехи этой партии: доли потолка, за которые платят щебнем.</summary>
        Milestones milestones;

        /// <summary>Одно вью на всю дорожную сеть: меш у неё общий.</summary>
        RoadView roadView;

        GameState state;
        ProductionSystem production;
        DeliverySystem deliveries;
        MergeSystem merges;
        BatchSale sale;

        /// <summary>Идущая волна продажи. Пока она идёт, кнопки на экране нет.</summary>
        Coroutine selling;
        ContractSystem contracts;
        GameEndSystem end;

        void Awake()
        {
            // Уровень кампании выбран в меню и лежит статикой: он переопределяет рычаги
            // `GameConfig` — поле, таймер и цель контракта, интервал добычи, щедрость карты
            // и доли воды и гор. Вне кампании его нет, и партия идёт на дефолтах.
            level = CampaignSession.Level;
            seed = SessionSeed.Take(config.Seed);

            // Глобалы поля переживают перезагрузку сцены, а рестарт партии — это она: без
            // сброса новое поле началось бы разогретым с прошлой партии.
            FieldPulse.Reset();

            var map = MapGenerator.Generate(config.MapGenerationSettingsFor(seed, level));
            var wallet = new Wallet(config.StartingPoints);
            var storage = new StorageGrid(config.StorageSize);
            var multiplier = config.NewMultiplier();
            state = new GameState(map, wallet, storage, config.Prices, multiplier);

            production = new ProductionSystem(map, state.Roads, config.ExtractionIntervalFor(level));
            deliveries = new DeliverySystem(config.DeliverySecondsPerTile);
            merges = new MergeSystem(
                storage, wallet, mergeRules, multiplier, config.SweepCells, config.SweepBonus);
            contracts = new ContractSystem(
                wallet,
                mergeRules.CraftedTypes(),
                config.ContractGoalFor(level),
                config.ContractSecondsFor(level),
                config.ContractReward,
                config.ContractPauseMin,
                config.ContractPauseMax,
                seed,
                multiplier);
            end = new GameEndSystem(
                state, mergeRules, deliveries, config.LossPenalty, config.FullFieldBonus, config.FullDepositBonus);
            // Свой поток жребия: приход кнопки не должен ходить в такт с паузами контрактов.
            sale = new BatchSale(
                storage, merges, mergeRules, end, config.SellPauseMin, config.SellPauseMax,
                config.SellReserveGravel, config.SellReserveBoards, seed + 7919);

            ceiling = MeasureCeiling();
            milestones = config.NewMilestones(ceiling);

            SpawnTiles(map);
            SpawnWater(map);
            storageView.Bind(storage, multiplier);

            if (heatVignette != null)
                heatVignette.Bind(multiplier);

            // Сгорание накала — момент, а не состояние, и рассылает его партия, как и остальные
            // моменты склада: вью про множитель знают ровно столько, сколько им сказали.
            // Отписки нет намеренно — множитель живёт ровно партию и уходит вместе с ней.
            multiplier.Burned += OnBurned;

            hudView.Bind(state, contracts, storageView, ceiling, StarShares());
            gameOverView.Bind(storageView, production, contracts);
            pauseView.Bind(storageView);

            // Звук партии слушает системы сам: ни один обработчик выше о нём не знает, и в
            // сцене под него нет ни объекта, ни ссылки в инспекторе.
            gameObject.AddComponent<AudioDirector>()
                .Bind(state, production, deliveries, merges, contracts, milestones, end);
        }

        /// <summary>
        /// Потолок партии в очках. В кампании он снят ботом заранее и лежит в ассете уровня —
        /// его же сверяет `CampaignTests`. Вне кампании уровня нет, и бот прогоняется прямо
        /// здесь: без потолка «Случайная карта» и «Ввести сид» остались бы без единственной
        /// мерки, с которой игрок может сравнить свой счёт. Прогон стоит десятки миллисекунд
        /// на 14 рядах и до сотни на 18 — это задержка перед первым кадром, решение человека
        /// 07.09.2026.
        /// </summary>
        int MeasureCeiling() =>
            level != null && level.BotCeiling > 0
                ? level.BotCeiling
                : new Balance.BalanceBot(config, mergeRules, seed, level).Play().Score.Total;

        /// <summary>
        /// Доли звёзд для отметок на баре. Вне кампании звёзд не выдают вовсе — и обещать их
        /// отметками бар не вправе: там он показывает только долю потолка.
        /// </summary>
        float[] StarShares() => level != null
            ? new[] { level.OneStarShare, level.TwoStarShare, level.ThreeStarShare }
            : null;

        /// <summary>
        /// Счёт партии изменился: бар показывает его долю от потолка, а вехи проверяют, не
        /// перешагнул ли он свою отметку. Счёт здесь — тот же `BuildScore().Total`, который
        /// покажет финальный экран: считай бар одно, а итог другое, и звёзды на баре разошлись
        /// бы со звёздами на финальном экране.
        /// </summary>
        void RefreshProgress()
        {
            var total = end.BuildScore().Total;
            hudView.ShowProgress(total);
            milestones.Report(total);
        }

        /// <summary>
        /// Веха пройдена: щебень на склад и празднование на баре. Награда идёт щебнем, а не
        /// очками: очки за веху двигали бы сам счёт, по которому веха и считается. На полный
        /// склад щебень не кладётся — переполнение уничтожает ресурс и штрафует счёт, и награда
        /// обернулась бы наказанием; сколько щебня реально легло, столько и показывает плашка.
        /// </summary>
        void OnMilestoneReached(float share)
        {
            var given = 0;
            while (given < config.MilestoneGravel && state.Storage.Count < state.Storage.Capacity)
            {
                state.Storage.TryStore(ResourceType.Gravel);
                given++;
            }

            hudView.PlayMilestone(share, given);
        }

        void OnEnable()
        {
            input.Clicked += OnClicked;
            input.Pressed += OnPressed;
            input.PressEnded += OnPressEnded;
            input.Dragged += OnDragged;
            input.Zoomed += OnZoomed;
            state.TileChanged += OnTileChanged;
            state.ActionRefused += ShowRefusal;
            state.Roads.Changed += OnRoadsChanged;
            // Счёт растёт обменом, а проседает потерей на складе: бар слушает оба источника.
            state.Wallet.Changed += RefreshProgress;
            state.Storage.Changed += RefreshProgress;
            milestones.Reached += OnMilestoneReached;
            production.Produced += OnProduced;
            production.TileDepleted += OnTileChanged;
            deliveries.Started += OnDeliveryStarted;
            deliveries.Arrived += OnDeliveryArrived;
            merges.Refused += ShowRefusal;
            merges.Merged += OnMerged;
            merges.Converted += OnConverted;
            storageView.SellRequested += SellBatch;
            merges.Swept += OnSwept;
            end.Ended += OnGameEnded;
            gameOverView.RestartRequested += Restart;
            gameOverView.NextLevelRequested += NextLevel;
            gameOverView.MenuRequested += ExitToMenu;
            pauseView.RestartRequested += RestartSameMap;
            pauseView.ExitRequested += ExitToMenu;
        }

        void OnDisable()
        {
            input.Clicked -= OnClicked;
            input.Pressed -= OnPressed;
            input.PressEnded -= OnPressEnded;
            input.Dragged -= OnDragged;
            input.Zoomed -= OnZoomed;
            state.TileChanged -= OnTileChanged;
            state.ActionRefused -= ShowRefusal;
            state.Roads.Changed -= OnRoadsChanged;
            state.Wallet.Changed -= RefreshProgress;
            state.Storage.Changed -= RefreshProgress;
            milestones.Reached -= OnMilestoneReached;
            production.Produced -= OnProduced;
            production.TileDepleted -= OnTileChanged;
            deliveries.Started -= OnDeliveryStarted;
            deliveries.Arrived -= OnDeliveryArrived;
            merges.Refused -= ShowRefusal;
            merges.Merged -= OnMerged;
            merges.Converted -= OnConverted;
            storageView.SellRequested -= SellBatch;
            merges.Swept -= OnSwept;
            end.Ended -= OnGameEnded;
            gameOverView.RestartRequested -= Restart;
            gameOverView.NextLevelRequested -= NextLevel;
            gameOverView.MenuRequested -= ExitToMenu;
            pauseView.RestartRequested -= RestartSameMap;
            pauseView.ExitRequested -= ExitToMenu;
        }

        void Start()
        {
            // Камера настраивается здесь, а не в `Awake`: доли панелей меряются по их
            // прямоугольникам, а канвас доводит их до экранных размеров на своём включении —
            // то есть в неизвестном порядке относительно чужого `Awake`. К `Start` всё готово.
            ApplyViewportInsets();
            cameraRig.SetFieldBounds(FieldBounds(state.Map));
            cameraRig.FocusOnBottom();

            state.Begin();
            for (var i = 0; i < config.StartingGravel; i++)
                state.Storage.TryStore(ResourceType.Gravel);

            // Доски на первый мост: без них переправа зависит от того, лёг ли лес на берегу,
            // а это не выбор игрока, а жребий генератора.
            for (var i = 0; i < config.StartingBoards; i++)
                state.Storage.TryStore(ResourceType.Board);

            // Бар рисуется нулём до первого обмена: карточка стоит на месте с самого начала,
            // иначе верх экрана перекладывался бы на глазах у первой же прибавки.
            RefreshProgress();

            // Первый контракт партии идёт без паузы (3.8). Дальше система выдаёт их сама,
            // отмолчав между ними случайную паузу.
            contracts.Issue();

            // Шестерёнка появляется только когда партия реально началась — не на экране меню,
            // где `pauseView.Bind` уже отработал, но ей ещё нечего показывать.
            pauseView.gameObject.SetActive(true);
        }

        void Update()
        {
            // Окно браузера тянут, телефон поворачивают: полосы интерфейса меняют свою долю
            // экрана, а с ней и то, насколько далеко камере позволено уходить за край поля.
            if (viewport.x != Screen.width || viewport.y != Screen.height)
                ApplyViewportInsets();

            // Накал склада греет и поле: ободок плиток уползает в тёплое. Стоит это одного
            // глобала в кадр, и ставится он до всех выходов — на паузе и после конца партии
            // поле обязано остаться таким же тёплым, каким его застали.
            FieldPulse.Heat(state.Multiplier.HeatShare);

            // Кнопка продажи опрашивается и на паузе, и после конца партии: там она обязана
            // пропасть с экрана, а не остаться висеть под карточкой.
            var playing = !end.HasEnded && !pauseView.IsOpen;
            storageView.ShowSellButton(
                playing && selling == null && sale.CanSell, sale.Everything);

            if (end.HasEnded || pauseView.IsOpen)
                return;

            production.Tick(Time.deltaTime);
            deliveries.Tick(Time.deltaTime);
            contracts.Tick(Time.deltaTime);
            state.Multiplier.Tick(Time.deltaTime);
            sale.Tick(Time.deltaTime);
            end.Tick();
        }

        void SpawnTiles(HexMap map)
        {
            foreach (var tile in map.Tiles.Values)
            {
                var view = Instantiate(tilePrefab, tilesRoot);
                // Интервал добычи плитка знает не ради счёта, а ради картинки: по нему стек
                // накаляется к своей выдаче. В кампании он свой на каждом уровне.
                view.Bind(tile, config.ExtractionIntervalFor(level));
                views.Add(tile.Coord, view);
                fieldCeiling = Mathf.Max(fieldCeiling, view.SurfaceHeight);
            }
        }

        /// <summary>
        /// Подложка воды под всем полем: на ней лежит карта, и за её краем фона камеры уже нет.
        /// Плоскость одна на партию и стоит на урезе — глубину шейдер берёт из буфера глубины,
        /// то есть из того, насколько дно под ней ниже. Тени она не отбрасывает: горизонтальная
        /// плоскость на урезе накрыла бы тенью всё поле разом.
        /// </summary>
        void SpawnWater(HexMap map)
        {
            if (waterMaterial == null)
                return;

            var bounds = FieldBounds(map);
            var size = Mathf.Max(bounds.width, bounds.height) + waterMargin * 2f;

            var water = new GameObject("Water", typeof(MeshFilter), typeof(MeshRenderer));
            water.transform.SetParent(tilesRoot, false);
            water.transform.localPosition = new Vector3(bounds.center.x, TileView.WaterSurface, bounds.center.y);
            water.GetComponent<MeshFilter>().sharedMesh = WaterMesh.Build(size);

            var waterRenderer = water.GetComponent<MeshRenderer>();
            waterRenderer.sharedMaterial = waterMaterial;
            waterRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            waterRenderer.receiveShadows = false;
        }

        /// <summary>
        /// Сколько кадра занимают HUD сверху и панель склада снизу. Камера по этим полосам
        /// понимает, где кончается видимая часть поля, и доводит его край до их кромки.
        /// </summary>
        void ApplyViewportInsets()
        {
            viewport = new Vector2Int(Screen.width, Screen.height);
            cameraRig.SetViewportInsets(
                hudView.TopHeightPixels / Screen.height,
                storageView.PanelHeightPixels / Screen.height);
        }

        /// <summary>
        /// Прямоугольник поля с учётом вершин крайних гексов. Чисто геометрический: поправку
        /// на панели интерфейса держит сама камера, ей же нужен и не сдвинутый край поля.
        /// </summary>
        Rect FieldBounds(HexMap map)
        {
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);

            foreach (var tile in map.Tiles.Values)
            {
                var center = tile.Coord.ToPlane();
                min = Vector2.Min(min, center - new Vector2(HexCoord.Width * 0.5f, HexCoord.Size));
                max = Vector2.Max(max, center + new Vector2(HexCoord.Width * 0.5f, HexCoord.Size));
            }

            return new Rect(min, max - min);
        }

        /// <summary>
        /// Палец лёг на экран. Разбирается тем же порядком, что и клик — пауза, попап, склад,
        /// поле, — иначе прижалось бы одно, а сработало другое. Прижатая цель одна: указатель
        /// в игре один, и <see cref="OnPressEnded"/> придёт ровно один раз на это нажатие.
        /// </summary>
        void OnPressed(Vector2 screenPosition)
        {
            if (end.HasEnded)
            {
                gameOverView.HandlePress(screenPosition);
                return;
            }

            if (pauseView.HandlePress(screenPosition))
                return;

            if (storageView.TrySellPress(screenPosition))
                return;

            if (storageView.TryGetCellIndex(screenPosition, out var cell))
            {
                // Пустая клетка не отзывается: по ней и клик ничего не делает.
                if (!state.Storage[cell].HasValue)
                    return;

                pressedCell = cell;
                storageView.PressCell(cell);
                return;
            }

            if (storageView.ContainsScreenPoint(screenPosition))
                return;

            var pressedCoord = TileUnderPointer(screenPosition);
            if (views.TryGetValue(pressedCoord, out var view))
            {
                pressedTile = view;
                view.Press();
            }
        }

        /// <summary>
        /// Палец снят, сорвался в протяжку или в щипок: прижатое возвращается. Приходит раньше
        /// клика, поэтому анимации слияния и открытия застают пружину уже отпущенной.
        /// Отпускаем всё разом, не разбирая: ненажатое молчит.
        /// </summary>
        void OnPressEnded()
        {
            gameOverView.ReleasePress();
            pauseView.ReleasePress();
            storageView.ReleaseSellPress();

            if (pressedCell >= 0)
            {
                storageView.ReleasePress(pressedCell);
                pressedCell = -1;
            }

            if (pressedTile == null)
                return;

            pressedTile.Release();
            pressedTile = null;
        }

        /// <summary>
        /// Клик разбирается по слоям: финальный экран, попап подтверждения, склад, поле под ним.
        /// После конца партии поле не принимает ничего, а кнопки экрана принимают — это и есть
        /// правило 3.11.
        /// </summary>
        void OnClicked(Vector2 screenPosition)
        {
            if (end.HasEnded)
            {
                gameOverView.HandleClick(screenPosition);
                return;
            }

            // Шестерёнка и, пока открыта, карточка паузы разбирают клик сами — полю и попапам
            // он не достаётся, партия стоит.
            if (pauseView.HandleClick(screenPosition))
                return;

            // Пока висит ценник открытия, клик принадлежит ему. Согласие — второй тап по той
            // же плитке: гекс под пальцем большой, а прицел в кнопку на темпе партии стоит
            // дороже самого открытия. Всё прочее снимает ценник, ничего не выполняя, — и
            // соседняя плитка тоже: открыть её тем же тапом, которым игрок передумал, значило
            // бы платить за промах.
            if (hudView.Popups.IsAsking)
            {
                if (askedTile.HasValue
                    && !storageView.ContainsScreenPoint(screenPosition)
                    && TileUnderPointer(screenPosition) == askedTile.Value)
                    hudView.Popups.AcceptAsk();
                else
                    hudView.Popups.CancelAsk();

                return;
            }

            // Кнопка продажи сама зовёт `SellBatch` через `SellRequested`: здесь важно только
            // то, что клик её и дальше не идёт.
            if (storageView.TrySellClick(screenPosition))
                return;

            if (storageView.TryGetCellIndex(screenPosition, out var cell))
            {
                var content = state.Storage[cell];
                if (!content.HasValue)
                    return;

                refusalAnchor = PopupView.Anchor.On(storageView.CellRect(cell));
                refusalCell = cell;
                refusalTile = null;

                // Базовый ресурс мержится, крафтовый превращается в очки.
                if (mergeRules.CanMerge(content.Value))
                    merges.TryMerge(cell);
                else
                    merges.TryConvert(cell);

                return;
            }

            if (storageView.ContainsScreenPoint(screenPosition))
                return;

            OnFieldClicked(TileUnderPointer(screenPosition));
        }

        /// <summary>
        /// Клик по полю. Открытие плитки стоит очков и потому идёт в два тапа: первый вешает
        /// ценник над самой плиткой, второй по ней же открывает. Дорога и отказы идут сразу —
        /// щебень столько не весит, а отказ и есть ответ. Цену попап берёт до клика: она растёт
        /// по ходу партии (3.1), и показать её ровно там, где игрок целится, — единственное
        /// место, где она ему нужна.
        /// </summary>
        void OnFieldClicked(HexCoord coord)
        {
            if (!state.Map.TryGetTile(coord, out var tile))
                return;

            refusalAnchor = TileAnchor(coord);
            refusalCell = -1;
            refusalTile = views.TryGetValue(coord, out var clicked) ? clicked : null;

            if (tile.State == TileState.Available && tile.IsPassable)
            {
                askedTile = coord;
                hudView.Popups.Ask(state.NextTileCost, refusalAnchor, () => state.TryRevealTile(coord));
                return;
            }

            state.HandleTileClick(coord);
        }

        /// <summary>Крышка плитки: над ней всплывают её попапы, и пан камеры их за собой везёт.</summary>
        PopupView.Anchor TileAnchor(HexCoord coord)
        {
            if (views.TryGetValue(coord, out var view))
                return PopupView.Anchor.OnField(view.transform.position);

            var plane = coord.ToPlane();
            return PopupView.Anchor.OnField(new Vector3(plane.x, 0f, plane.y));
        }

        /// <summary>
        /// Отказ всплывает над тем, по чему кликнули: над плиткой или над клеткой склада. Оно же
        /// и дрожит — один текст читается как «ничего не произошло», а промах по недоступной
        /// плитке в этой игре самый частый из всех.
        /// </summary>
        void ShowRefusal(string text)
        {
            hudView.Popups.ShowMessage(text, refusalAnchor);

            if (refusalCell >= 0)
                storageView.ShakeCell(refusalCell);
            else if (refusalTile != null)
                refusalTile.Refuse();
        }

        /// <summary>
        /// Плитка, по которой игрок целился. Луч по земле уходит на соседа тем дальше, чем выше
        /// плитка, поэтому он спускается по рельефу от самой высокой крышки поля вниз.
        /// </summary>
        HexCoord TileUnderPointer(Vector2 screenPosition) => TilePicker.Resolve(
            height => input.CoordAt(screenPosition, height),
            coord => views.TryGetValue(coord, out var view) ? view.SurfaceHeight : null,
            fieldCeiling);

        /// <summary>Камера замирает вместе с полем: после конца партии её тоже не двигают.</summary>
        void OnDragged(Vector2 delta)
        {
            if (!end.HasEnded && !pauseView.IsOpen)
                cameraRig.Pan(delta);
        }

        void OnZoomed(float amount)
        {
            if (!end.HasEnded && !pauseView.IsOpen)
                cameraRig.Zoom(amount);
        }

        /// <summary>
        /// Рестарт с финального экрана: сцена загружается заново. Сохранения партии в проекте
        /// нет, поэтому «начать заново» и значит «собрать всё с нуля»; между загрузками живёт
        /// только заказанный сид, и он решает, будет карта той же или новой.
        /// </summary>
        void Restart(bool sameMap)
        {
            if (sameMap)
                SessionSeed.Repeat(seed);
            else
                SessionSeed.Renew();

            // Сид уже выбран — сцена обязана поднять партию напрямую, а не главное меню,
            // которое иначе показывает по умолчанию после перезагрузки.
            SessionSeed.SkipMenu = true;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        /// <summary>«Рестарт карты» из паузы: та же карта, тот же путь, что «Повторить карту».</summary>
        void RestartSameMap() => Restart(true);

        /// <summary>
        /// «Следующий уровень»: кампания сдвигается на шаг и сама заказывает сид нового уровня,
        /// сцена перезагружается прямо в партию — меню между уровнями не показывается.
        /// </summary>
        void NextLevel()
        {
            CampaignSession.Advance();
            SessionSeed.SkipMenu = true;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        /// <summary>
        /// «Выход в главное меню» из паузы: сцена просто перезагружается, без заказа сида.
        /// `Game` в сохранённой сцене выключен по умолчанию, `MainMenu` включён — reload
        /// естественно возвращает на экран меню.
        /// </summary>
        void ExitToMenu()
        {
            // Уровень кампании остаётся выбранным до самого меню, иначе перезагрузка подняла бы
            // партию по нему заново. Прогресс от этого не страдает: он лежит в `PlayerPrefs`.
            CampaignSession.Clear();
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        void OnTileChanged(TileData tile)
        {
            if (views.TryGetValue(tile.Coord, out var view))
                view.Apply(tile);
        }

        /// <summary>
        /// Дороги перестраиваются целиком одним мешем: их немного, а насыпь идёт через границы
        /// гексов, и по отдельной плитке её собрать нельзя.
        /// </summary>
        void OnRoadsChanged()
        {
            if (roadView == null)
            {
                // Меш насыпи живёт в мировых координатах, поэтому вью встаёт в начало координат.
                roadView = Instantiate(roadPrefab, tilesRoot);
                roadView.name = "Roads";
            }

            roadView.Show(state.Roads, GroundAt);
        }

        /// <summary>
        /// Что дорога застаёт на плитке. Высота — та, на которую плитку поставил `TileView`,
        /// а не расчётная: весь рельеф ещё умножается на масштаб высоты, и насыпь обязана лечь
        /// на то, что видно. Река на плитке означает мост: каменную арку скальной плитке,
        /// деревянный настил всем остальным.
        /// </summary>
        RoadGround GroundAt(HexCoord coord)
        {
            var top = views.TryGetValue(coord, out var view) ? view.SurfaceHeight : 0f;

            if (!state.Map.TryGetTile(coord, out var tile) || !tile.HasRiver)
                return new RoadGround(top);

            return new RoadGround(top, tile.Biome == BiomeType.Rocks ? BridgeKind.Stone : BridgeKind.Timber);
        }

        void OnProduced(TileData tile, ResourceType type)
        {
            OnTileChanged(tile);

            if (views.TryGetValue(tile.Coord, out var view))
                view.PlayExtraction(tile, type);

            if (state.Roads.TryFindPathToMetropolis(tile.Coord, path))
                deliveries.Send(type, new List<HexCoord>(path));
        }

        void OnDeliveryStarted(Delivery delivery)
        {
            var mover = Instantiate(moverPrefab, moversRoot);
            mover.Bind(delivery);
            movers.Add(delivery, mover);
        }

        void OnMerged(MergeReport report)
        {
            storageView.PlayMerge(report.ConsumedCells, report.ResultCells, report.Outcome.Source);

            // Удар временем — только на крупном слиянии. На тройке он звучал бы каждые несколько
            // секунд и из удара превратился бы в тик метронома.
            if (hitstop != null && report.Outcome.Consumed >= mergeRules.LargeCount)
                hitstop.Play();
        }

        /// <summary>
        /// Переполнение сожгло накал. Три эффекта на один момент, и это не украшательство:
        /// потеря накала не видна ни в одной цифре, кроме множителя, — вспышка говорит «на
        /// складе», провал цвета «в партии», удар по камере «прямо сейчас». Ресурс при этом
        /// теряется отдельно и мигает своим красным.
        /// </summary>
        void OnBurned()
        {
            storageView.PlayBurn();
            cameraRig.Shake();

            if (burnFlash != null)
                burnFlash.Play();
        }

        /// <summary>
        /// Крафт обменян на очки: плашка о прибавке, полёт ресурса в карточку контракта, потом
        /// зачёт. Порядок важен дважды: зачёт может закрыть контракт, и его награда должна лечь
        /// плашкой следом, а не перед тем, за что она пришла; полёт же запускается до зачёта,
        /// пока карточка того контракта, в который ресурс летит, ещё на экране.
        /// </summary>
        void OnConverted(int cell, ResourceType type, int points)
        {
            storageView.PunchCell(cell);
            hudView.Popups.ShowGain(points, type, PopupView.Anchor.On(storageView.CellRect(cell)));

            if (contracts.IsActive && contracts.Type == type)
                hudView.PlayContractDelivery(storageView.CellPoint(cell), type);

            contracts.Count(type);
        }

        /// <summary>
        /// Склад разгребли до чистого на полном накале: премия плашкой над той клеткой, которой
        /// его дочистили, и вспышка самого склада. Отдельной анимации у премии нет — празднует
        /// склад, потому что празднуется именно его состояние.
        /// </summary>
        void OnSwept(int cell, int points)
        {
            storageView.PlaySweep(cell);
            hudView.Popups.ShowSweep(points, PopupView.Anchor.On(storageView.CellRect(cell)));
        }

        /// <summary>
        /// Заработать больше нечем: поле замирает, на экране остаётся счёт. Попапы гаснут
        /// разом — висеть им теперь не над чем, поле под финальным экраном мертво.
        /// </summary>
        void OnGameEnded(FinalScore score)
        {
            hudView.Popups.Clear();
            pauseView.gameObject.SetActive(false);
            gameOverView.Show(score);
        }

        /// <summary>
        /// Кнопку нажали: пачка уходит не одним кадром, а волной по клетке за шаг. Двадцать
        /// прибавок, двадцать полётов в карточку контракта и двадцать ступеней накала в один
        /// кадр не читаются вовсе — а по шагам это ровно та же последовательность действий,
        /// какую бот делает мгновенно, только растянутая настолько, чтобы её было видно.
        /// </summary>
        void SellBatch()
        {
            if (selling != null)
                return;

            sale.Begin();
            selling = StartCoroutine(SellWave());
        }

        IEnumerator SellWave()
        {
            var step = new WaitForSeconds(sellStepSeconds);
            while (!end.HasEnded && sale.TrySellOne())
                yield return step;

            selling = null;
        }

        /// <summary>Доехавший ресурс перепрыгивает с Метрополии в свою клетку склада.</summary>
        void OnDeliveryArrived(Delivery delivery)
        {
            movers.Remove(delivery, out var mover);
            var stored = state.Storage.TryStore(delivery.Type, out var cell);

            if (mover == null)
                return;

            if (!stored)
            {
                // Потеря уже засчитана складом. Ресурс не исчезает в воздухе, а улетает за кромку
                // экрана: переполнение должно быть видно на самом ресурсе, а не только вспышкой.
                // Сторону выбираем жребием — два подряд потерянных не должны уходить одной дугой.
                var side = Random.value < 0.5f ? -1f : 1f;
                mover.MissTo(cameraRig.OffScreenPoint(side));
                return;
            }

            // Клетка занята сразу, иначе её перехватит следующая доставка, но показываем её
            // только когда кружок долетит.
            storageView.HoldCell(cell);
            mover.HopTo(storageView.CellWorldPoint(cell, Camera.main), () => OnResourceLanded(cell));
        }

        /// <summary>Кружок долетел: клетка склада проявляется.</summary>
        void OnResourceLanded(int cell) => storageView.ReleaseCell(cell);
    }
}
