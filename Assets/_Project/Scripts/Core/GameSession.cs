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
        const float RoadHeight = 0.02f;

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

        readonly Dictionary<HexCoord, TileView> views = new();
        readonly Dictionary<HexCoord, RoadView> roadViews = new();
        readonly Dictionary<Delivery, ResourceMover> movers = new();
        readonly List<HexCoord> path = new();

        /// <summary>Самая высокая крышка поля: отсюда начинает спуск луч клика.</summary>
        float fieldCeiling;

        /// <summary>
        /// Сид этой партии — уже разрешённый, а не ноль из конфига. По нему живут и карта, и
        /// контракты, и его же забирает кнопка «Повторить карту» на финальном экране.
        /// </summary>
        int seed;

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
                seed);
            end = new GameEndSystem(
                state, mergeRules, deliveries, config.LossPenalty, config.FullFieldBonus, config.FullDepositBonus);

            SpawnTiles(map);
            SpawnWater(map);
            storageView.Bind(storage);
            hudView.Bind(state, contracts, storageView);
            gameOverView.Bind(storageView, production, contracts);
            cameraRig.SetFieldBounds(FieldBounds(map));
            cameraRig.FocusOnBottom();
        }

        void OnEnable()
        {
            input.Clicked += OnClicked;
            input.Dragged += OnDragged;
            input.Zoomed += OnZoomed;
            state.TileChanged += OnTileChanged;
            state.ActionRefused += hudView.ShowMessage;
            state.Roads.Changed += OnRoadsChanged;
            production.Produced += OnProduced;
            production.TileDepleted += OnTileChanged;
            deliveries.Started += OnDeliveryStarted;
            deliveries.Arrived += OnDeliveryArrived;
            merges.Refused += hudView.ShowMessage;
            merges.Merged += OnMerged;
            merges.Converted += OnConverted;
            end.Ended += OnGameEnded;
            gameOverView.RestartRequested += Restart;
        }

        void OnDisable()
        {
            input.Clicked -= OnClicked;
            input.Dragged -= OnDragged;
            input.Zoomed -= OnZoomed;
            state.TileChanged -= OnTileChanged;
            state.ActionRefused -= hudView.ShowMessage;
            state.Roads.Changed -= OnRoadsChanged;
            production.Produced -= OnProduced;
            production.TileDepleted -= OnTileChanged;
            deliveries.Started -= OnDeliveryStarted;
            deliveries.Arrived -= OnDeliveryArrived;
            merges.Refused -= hudView.ShowMessage;
            merges.Merged -= OnMerged;
            merges.Converted -= OnConverted;
            end.Ended -= OnGameEnded;
            gameOverView.RestartRequested -= Restart;
        }

        void Start()
        {
            state.Begin();
            for (var i = 0; i < config.StartingGravel; i++)
                state.Storage.TryStore(ResourceType.Gravel);
        }

        void Update()
        {
            if (end.HasEnded)
                return;

            production.Tick(Time.deltaTime);
            deliveries.Tick(Time.deltaTime);
            TickContracts();
            end.Tick();
        }

        /// <summary>
        /// Дальше контракты система выдаёт сама, поэтому `Issue` срабатывает здесь ровно один
        /// раз — на самый первый контракт партии.
        /// </summary>
        void TickContracts()
        {
            if (contracts.IsActive)
                contracts.Tick(Time.deltaTime);
            else
                contracts.Issue();
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
        /// Прямоугольник поля с учётом вершин крайних гексов. Снизу он расширен на высоту панели
        /// склада, иначе камера прижимает Метрополию под панель.
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

            // Доля панели от высоты экрана, зажатая на случай узкого или непортретного окна:
            // без ограничения панель выше экрана утащила бы камеру под поле.
            var camera = Camera.main;
            var panelShare = Mathf.Clamp(storageView.PanelHeightPixels / Screen.height, 0f, 0.4f);
            min.y -= panelShare * camera.orthographicSize * 2f;

            return new Rect(min, max - min);
        }

        /// <summary>
        /// Клик разбирается по слоям: финальный экран, склад, поле под ним. После конца партии
        /// поле не принимает ничего, а кнопки экрана принимают — это и есть правило 3.10.
        /// </summary>
        void OnClicked(Vector2 screenPosition)
        {
            if (end.HasEnded)
            {
                gameOverView.HandleClick(screenPosition);
                return;
            }

            if (storageView.TryGetCellIndex(screenPosition, out var cell))
            {
                var content = state.Storage[cell];
                if (!content.HasValue)
                    return;

                // Базовый ресурс мержится, крафтовый превращается в очки.
                if (mergeRules.CanMerge(content.Value))
                    merges.TryMerge(content.Value);
                else
                    merges.TryConvert(cell);

                return;
            }

            if (storageView.ContainsScreenPoint(screenPosition))
                return;

            state.HandleTileClick(TileUnderPointer(screenPosition));
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
            if (!end.HasEnded)
                cameraRig.Pan(delta);
        }

        void OnZoomed(float amount)
        {
            if (!end.HasEnded)
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

            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        void OnTileChanged(TileData tile)
        {
            if (views.TryGetValue(tile.Coord, out var view))
                view.Apply(tile);
        }

        /// <summary>Дороги перерисовываются целиком: их немного, а связность меняется всей цепочкой.</summary>
        void OnRoadsChanged()
        {
            foreach (var coord in state.Roads.Roads)
            {
                if (!roadViews.TryGetValue(coord, out var roadView))
                {
                    roadView = Instantiate(roadPrefab, views[coord].transform);
                    roadView.transform.localPosition = new Vector3(0f, RoadHeight, 0f);
                    roadViews.Add(coord, roadView);
                }

                var links = LinkMask(coord);
                roadView.Show(coord, state.Roads.IsConnected(coord), links, BridgeMask(coord, links));
            }
        }

        /// <summary>
        /// Биты направлений, по которым проходит маршрут: к своему родителю и к тем соседям, для
        /// которых родитель — эта плитка. Соседняя дорога сама по себе перемычку не рисует.
        /// </summary>
        int LinkMask(HexCoord coord)
        {
            var mask = 0;
            for (var direction = 0; direction < HexCoord.Directions.Count; direction++)
                if (state.Roads.IsRouteLink(coord, coord.Neighbor(direction)))
                    mask |= 1 << direction;

            return mask;
        }

        /// <summary>
        /// Где маршрут идёт мостом: на плитке с рекой настил лежит под всей лентой дороги — она
        /// проходит через центр плитки, где течёт русло, и другой дороги на этой плитке не бывает.
        /// </summary>
        int BridgeMask(HexCoord coord, int linkMask)
        {
            if (!state.Map.TryGetTile(coord, out var tile))
                return 0;

            return tile.HasRiver ? linkMask : 0;
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
        /// Крафт обменян на очки: сначала плашка о прибавке, потом зачёт контракту. Порядок
        /// важен — зачёт может закрыть контракт, и его награда должна лечь плашкой следом,
        /// а не перед тем, за что она пришла.
        /// </summary>
        void OnConverted(ResourceType type, int points)
        {
            hudView.ShowGain(points, type);
            contracts.Count(type);
        }

        /// <summary>Заработать больше нечем: поле замирает, на экране остаётся счёт.</summary>
        void OnGameEnded(FinalScore score) => gameOverView.Show(score);

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
