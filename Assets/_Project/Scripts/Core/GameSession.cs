using System.Collections.Generic;
using Game.Economy;
using Game.Grid;
using Game.Merge;
using Game.Roads;
using Game.Storage;
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

        readonly Dictionary<HexCoord, TileView> views = new();
        readonly Dictionary<HexCoord, RoadView> roadViews = new();
        readonly Dictionary<Delivery, ResourceMover> movers = new();
        readonly List<HexCoord> path = new();

        GameState state;
        ProductionSystem production;
        DeliverySystem deliveries;
        MergeSystem merges;

        void Awake()
        {
            var map = MapGenerator.Generate(config.MapGenerationSettings);
            var wallet = new Wallet(config.StartingPoints);
            var storage = new StorageGrid(config.StorageSize);
            state = new GameState(map, wallet, storage, config.TileOpenCost, config.RoadCost);

            production = new ProductionSystem(map, state.Roads, config.ExtractionInterval);
            deliveries = new DeliverySystem(config.DeliverySecondsPerTile);
            merges = new MergeSystem(storage, wallet, mergeRules);

            SpawnTiles(map);
            storageView.Bind(storage);
            hudView.Bind(wallet, storage);
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
        }

        void Start()
        {
            state.Begin();
            for (var i = 0; i < config.StartingGravel; i++)
                state.Storage.TryStore(ResourceType.Gravel);
        }

        void Update()
        {
            production.Tick(Time.deltaTime);
            deliveries.Tick(Time.deltaTime);
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

            var camera = Camera.main;
            var panelWorldHeight = storageView.PanelHeightPixels / Screen.height * camera.orthographicSize * 2f;
            min.y -= panelWorldHeight;

            return new Rect(min, max - min);
        }

        /// <summary>Клик разбирается по слоям: сначала склад, потом поле под ним.</summary>
        void OnClicked(Vector2 screenPosition)
        {
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

                roadView.Show(state.Roads.IsConnected(coord), LinkMask(coord));
            }
        }

        /// <summary>Биты направлений, в которых есть соседняя дорога или сама Метрополия.</summary>
        int LinkMask(HexCoord coord)
        {
            var mask = 0;
            for (var direction = 0; direction < HexCoord.Directions.Count; direction++)
            {
                var neighbor = coord.Neighbor(direction);
                if (neighbor == HexCoord.Zero || state.Roads.HasRoad(neighbor))
                    mask |= 1 << direction;
            }

            return mask;
        }

        void OnProduced(TileData tile, ResourceType type)
        {
            OnTileChanged(tile);

            if (state.Roads.TryFindPathToMetropolis(tile.Coord, path))
                deliveries.Send(type, new List<HexCoord>(path));
        }

        void OnDeliveryStarted(Delivery delivery)
        {
            var mover = Instantiate(moverPrefab, moversRoot);
            mover.Bind(delivery);
            movers.Add(delivery, mover);
        }

        void OnMerged(MergeReport report) =>
            storageView.PlayMerge(report.ConsumedCells, report.ResultCells, report.Outcome.Source);

        /// <summary>Доехавший ресурс перепрыгивает с Метрополии в свою клетку склада.</summary>
        void OnDeliveryArrived(Delivery delivery)
        {
            movers.Remove(delivery, out var mover);
            var stored = state.Storage.TryStore(delivery.Type, out var cell);

            if (mover == null)
                return;

            if (stored)
                mover.HopTo(storageView.CellWorldPoint(cell, Camera.main));
            else
                Destroy(mover.gameObject);
        }
    }
}
