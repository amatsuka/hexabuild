using System.Collections.Generic;
using Game.Economy;
using Game.Grid;
using Game.Merge;
using Game.Roads;
using Game.Storage;
using Game.Tutorial;
using Game.UI;
using UnityEngine;

namespace Game.Core
{
    /// <summary>Точка входа: создаёт партию и системы, порождает визуалы и связывает подписки.</summary>
    public sealed class GameSession : MonoBehaviour
    {
        const float RoadDepth = -0.06f;

        [SerializeField] GameConfig config;
        [SerializeField] MergeRules mergeRules;
        [SerializeField] TileView tilePrefab;
        [SerializeField] RoadView roadPrefab;
        [SerializeField] ResourceMover moverPrefab;
        [SerializeField] Transform tilesRoot;
        [SerializeField] Transform moversRoot;
        [SerializeField] GameInput input;
        [SerializeField] CameraRig cameraRig;
        [SerializeField] StorageView storageView;
        [SerializeField] HudView hudView;
        [SerializeField] TutorialView tutorialView;
        [SerializeField] GameOverView gameOverView;

        readonly Dictionary<HexCoord, TileView> views = new();
        readonly Dictionary<HexCoord, RoadView> roadViews = new();
        readonly Dictionary<Delivery, ResourceMover> movers = new();
        readonly List<HexCoord> path = new();

        GameState state;
        ProductionSystem production;
        DeliverySystem deliveries;
        MergeSystem merges;
        TutorialSystem tutorial;
        ContractSystem contracts;
        GameEndSystem end;

        void Awake()
        {
            var map = MapGenerator.Generate(config.MapGenerationSettings);
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
                config.Seed);
            end = new GameEndSystem(
                state, mergeRules, deliveries, config.LossPenalty, config.FullFieldBonus, config.FullDepositBonus);

            tutorial = new TutorialSystem(map, storage, state.Roads, mergeRules);

            SpawnTiles(map);
            storageView.Bind(storage);
            hudView.Bind(state, contracts);
            tutorialView.Bind(tutorial, views, storageView);
            cameraRig.SetFieldBounds(FieldBounds(map));
            cameraRig.FocusOnBottom();
        }

        void OnEnable()
        {
            input.Clicked += OnClicked;
            input.Dragged += cameraRig.Pan;
            input.Zoomed += cameraRig.Zoom;
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
            state.Storage.Changed += tutorial.Refresh;
            end.Ended += OnGameEnded;
        }

        void OnDisable()
        {
            input.Clicked -= OnClicked;
            input.Dragged -= cameraRig.Pan;
            input.Zoomed -= cameraRig.Zoom;
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
            state.Storage.Changed -= tutorial.Refresh;
            end.Ended -= OnGameEnded;
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
        /// Контракты ждут конца обучения: шесть шагов и так занимают весь экран, а первый контракт
        /// сгорел бы, пока игрок разбирается с первой дорогой. Дальше система выдаёт их сама,
        /// поэтому `Issue` срабатывает здесь ровно один раз — на самый первый контракт партии.
        /// </summary>
        void TickContracts()
        {
            if (tutorial.IsRunning)
                return;

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
            }
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
                var center = tile.Coord.ToWorld();
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

        /// <summary>Клик разбирается по слоям: сначала склад, потом поле под ним.</summary>
        void OnClicked(Vector2 screenPosition)
        {
            if (end.HasEnded)
                return;

            if (tutorialView.ContainsSkipPoint(screenPosition))
            {
                tutorial.Skip();
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

            state.HandleTileClick(input.CoordAt(screenPosition));
        }

        void OnTileChanged(TileData tile)
        {
            if (views.TryGetValue(tile.Coord, out var view))
                view.Apply(tile);

            // Открытая плитка закрывает шаг обучения, любая другая правка поля — только двигает цель.
            if (tile.State == TileState.Revealed && !tile.IsMetropolis)
                tutorial.Notify(TutorialTrigger.TileRevealed);
            else
                tutorial.Refresh();
        }

        /// <summary>Дороги перерисовываются целиком: их немного, а связность меняется всей цепочкой.</summary>
        void OnRoadsChanged()
        {
            foreach (var coord in state.Roads.Roads)
            {
                if (!roadViews.TryGetValue(coord, out var roadView))
                {
                    roadView = Instantiate(roadPrefab, views[coord].transform);
                    roadView.transform.localPosition = new Vector3(0f, 0f, RoadDepth);
                    roadViews.Add(coord, roadView);
                }

                var links = LinkMask(coord);
                roadView.Show(coord, state.Roads.IsConnected(coord), links, BridgeMask(coord, links));
            }

            tutorial.Notify(TutorialTrigger.RoadBuilt);
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
        /// Где маршрут идёт мостом: только там, где участок маршрута пересекает реку по ребру.
        /// </summary>
        int BridgeMask(HexCoord coord, int linkMask)
        {
            if (!state.Map.TryGetTile(coord, out var tile))
                return 0;

            return linkMask & tile.RiverMask;
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
            tutorial.Notify(TutorialTrigger.Merged);
        }

        void OnConverted(ResourceType type, int points)
        {
            contracts.Count(type);
            tutorial.Notify(TutorialTrigger.Converted);
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
                Destroy(mover.gameObject);
                return;
            }

            // Клетка занята сразу, иначе её перехватит следующая доставка, но показываем её
            // только когда кружок долетит.
            storageView.HoldCell(cell);
            mover.HopTo(storageView.CellWorldPoint(cell, Camera.main), () => OnResourceLanded(cell));
        }

        /// <summary>Кружок долетел: клетка проявляется, обучение засчитывает шаг с доставкой.</summary>
        void OnResourceLanded(int cell)
        {
            storageView.ReleaseCell(cell);
            tutorial.Notify(TutorialTrigger.ResourceLanded);
        }
    }
}
