using System.Collections.Generic;
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
        /// Сид этой партии — уже разрешённый, а не ноль из конфига. По нему живут и карта, и
        /// контракты, и его же забирает кнопка «Повторить карту» на финальном экране.
        /// </summary>
        int seed;

        /// <summary>Одно вью на всю дорожную сеть: меш у неё общий.</summary>
        RoadView roadView;

        GameState state;
        ProductionSystem production;
        DeliverySystem deliveries;
        MergeSystem merges;
        ContractSystem contracts;
        GameEndSystem end;

        void Awake()
        {
            seed = SessionSeed.Take(config.Seed);

            var map = MapGenerator.Generate(config.MapGenerationSettingsFor(seed));
            var wallet = new Wallet(config.StartingPoints);
            var storage = new StorageGrid(config.StorageSize);
            state = new GameState(map, wallet, storage, config.Prices);

            production = new ProductionSystem(map, state.Roads, config.ExtractionInterval);
            deliveries = new DeliverySystem(config.DeliverySecondsPerTile);
            merges = new MergeSystem(storage, wallet, mergeRules);
            contracts = new ContractSystem(
                wallet,
                mergeRules.CraftedTypes(),
                config.ContractGoal,
                config.ContractSeconds,
                config.ContractReward,
                config.ContractPauseMin,
                config.ContractPauseMax,
                seed);
            end = new GameEndSystem(
                state, mergeRules, deliveries, config.LossPenalty, config.FullFieldBonus, config.FullDepositBonus);

            SpawnTiles(map);
            SpawnWater(map);
            storageView.Bind(storage);
            hudView.Bind(state, contracts, storageView);
            gameOverView.Bind(storageView, production, contracts);
            pauseView.Bind(storageView);
        }

        void OnEnable()
        {
            input.Clicked += OnClicked;
            input.Dragged += OnDragged;
            input.Zoomed += OnZoomed;
            state.TileChanged += OnTileChanged;
            state.ActionRefused += ShowRefusal;
            state.Roads.Changed += OnRoadsChanged;
            production.Produced += OnProduced;
            production.TileDepleted += OnTileChanged;
            deliveries.Started += OnDeliveryStarted;
            deliveries.Arrived += OnDeliveryArrived;
            merges.Refused += ShowRefusal;
            merges.Merged += OnMerged;
            merges.Converted += OnConverted;
            end.Ended += OnGameEnded;
            gameOverView.RestartRequested += Restart;
            pauseView.RestartRequested += RestartSameMap;
            pauseView.ExitRequested += ExitToMenu;
        }

        void OnDisable()
        {
            input.Clicked -= OnClicked;
            input.Dragged -= OnDragged;
            input.Zoomed -= OnZoomed;
            state.TileChanged -= OnTileChanged;
            state.ActionRefused -= ShowRefusal;
            state.Roads.Changed -= OnRoadsChanged;
            production.Produced -= OnProduced;
            production.TileDepleted -= OnTileChanged;
            deliveries.Started -= OnDeliveryStarted;
            deliveries.Arrived -= OnDeliveryArrived;
            merges.Refused -= ShowRefusal;
            merges.Merged -= OnMerged;
            merges.Converted -= OnConverted;
            end.Ended -= OnGameEnded;
            gameOverView.RestartRequested -= Restart;
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

            if (end.HasEnded || pauseView.IsOpen)
                return;

            production.Tick(Time.deltaTime);
            deliveries.Tick(Time.deltaTime);
            contracts.Tick(Time.deltaTime);
            end.Tick();
        }

        void SpawnTiles(HexMap map)
        {
            foreach (var tile in map.Tiles.Values)
            {
                var view = Instantiate(tilePrefab, tilesRoot);
                view.Bind(tile);
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
        /// Клик разбирается по слоям: финальный экран, попап подтверждения, склад, поле под ним.
        /// После конца партии поле не принимает ничего, а кнопки экрана принимают — это и есть
        /// правило 3.10.
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

            // Пока висит подтверждение открытия, клик принадлежит ему: по галочке — открыть,
            // мимо — только закрыть, не выполняя того, по чему попали.
            if (hudView.Popups.TryClick(screenPosition))
                return;

            if (storageView.TryGetCellIndex(screenPosition, out var cell))
            {
                var content = state.Storage[cell];
                if (!content.HasValue)
                    return;

                refusalAnchor = PopupView.Anchor.On(storageView.CellRect(cell));

                // Базовый ресурс мержится, крафтовый превращается в очки.
                if (mergeRules.CanMerge(content.Value))
                    merges.TryMerge(content.Value);
                else
                    merges.TryConvert(cell);

                return;
            }

            if (storageView.ContainsScreenPoint(screenPosition))
                return;

            OnFieldClicked(TileUnderPointer(screenPosition));
        }

        /// <summary>
        /// Клик по полю. Открытие плитки стоит очков и потому спрашивает подтверждения попапом
        /// над самой плиткой; дорога и отказы идут сразу — щебень столько не весит, а отказ и
        /// есть ответ. Цену попап берёт до клика: она растёт по ходу партии (3.1), и показать
        /// её ровно там, где игрок целится, — единственное место, где она ему нужна.
        /// </summary>
        void OnFieldClicked(HexCoord coord)
        {
            if (!state.Map.TryGetTile(coord, out var tile))
                return;

            refusalAnchor = TileAnchor(coord);

            if (tile.State == TileState.Available && tile.IsPassable)
            {
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

        /// <summary>Отказ всплывает над тем, по чему кликнули: над плиткой или над клеткой склада.</summary>
        void ShowRefusal(string text) => hudView.Popups.ShowMessage(text, refusalAnchor);

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
        /// «Выход в главное меню» из паузы: сцена просто перезагружается, без заказа сида.
        /// `Game` в сохранённой сцене выключен по умолчанию, `MainMenu` включён — reload
        /// естественно возвращает на экран меню.
        /// </summary>
        void ExitToMenu() => SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);

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
        }

        /// <summary>
        /// Крафт обменян на очки: плашка о прибавке, полёт ресурса в карточку контракта, потом
        /// зачёт. Порядок важен дважды: зачёт может закрыть контракт, и его награда должна лечь
        /// плашкой следом, а не перед тем, за что она пришла; полёт же запускается до зачёта,
        /// пока карточка того контракта, в который ресурс летит, ещё на экране.
        /// </summary>
        void OnConverted(int cell, ResourceType type, int points)
        {
            hudView.Popups.ShowGain(points, type, PopupView.Anchor.On(storageView.CellRect(cell)));

            if (contracts.IsActive && contracts.Type == type)
                hudView.PlayContractDelivery(storageView.CellPoint(cell), type);

            contracts.Count(type);
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
