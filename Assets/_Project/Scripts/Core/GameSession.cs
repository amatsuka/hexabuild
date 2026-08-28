using System.Collections.Generic;
using Game.Economy;
using Game.Grid;
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

        void Awake()
        {
            var map = MapGenerator.Generate(config.MapGenerationSettings);
            var wallet = new Wallet(config.StartingPoints);
            var storage = new StorageGrid(config.StorageSize);
            state = new GameState(map, wallet, storage, config.TileOpenCost, config.RoadCost);

            production = new ProductionSystem(map, state.Roads, config.ExtractionInterval);
            deliveries = new DeliverySystem(config.DeliverySecondsPerTile);

            SpawnTiles(map);
            cameraRig.SetFieldHalfExtents(FieldHalfExtents(map));
            storageView.Bind(storage);
            hudView.Bind(wallet, storage);
        }

        void OnEnable()
        {
            input.TileClicked += OnTileClicked;
            input.Dragged += cameraRig.Pan;
            input.Zoomed += cameraRig.Zoom;
            state.TileChanged += OnTileChanged;
            state.ActionRefused += hudView.ShowMessage;
            state.Roads.Changed += OnRoadsChanged;
            production.Produced += OnProduced;
            production.TileDepleted += OnTileChanged;
            deliveries.Started += OnDeliveryStarted;
            deliveries.Arrived += OnDeliveryArrived;
        }

        void OnDisable()
        {
            input.TileClicked -= OnTileClicked;
            input.Dragged -= cameraRig.Pan;
            input.Zoomed -= cameraRig.Zoom;
            state.TileChanged -= OnTileChanged;
            state.ActionRefused -= hudView.ShowMessage;
            state.Roads.Changed -= OnRoadsChanged;
            production.Produced -= OnProduced;
            production.TileDepleted -= OnTileChanged;
            deliveries.Started -= OnDeliveryStarted;
            deliveries.Arrived -= OnDeliveryArrived;
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

        /// <summary>Габариты поля с учётом вершин крайних гексов — для границ камеры.</summary>
        Vector2 FieldHalfExtents(HexMap map)
        {
            var halfExtents = Vector2.zero;
            foreach (var tile in map.Tiles.Values)
            {
                var center = tile.Coord.ToWorld();
                halfExtents.x = Mathf.Max(halfExtents.x, Mathf.Abs(center.x) + HexCoord.Width * 0.5f);
                halfExtents.y = Mathf.Max(halfExtents.y, Mathf.Abs(center.y) + HexCoord.Size);
            }

            return halfExtents;
        }

        void OnTileClicked(HexCoord coord) => state.HandleTileClick(coord);

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

        void OnDeliveryArrived(Delivery delivery)
        {
            if (movers.Remove(delivery, out var mover))
                Destroy(mover.gameObject);

            state.Storage.TryStore(delivery.Type);
        }
    }
}
