using System.Collections.Generic;
using Game.Economy;
using Game.Grid;
using Game.Roads;
using Game.UI;
using UnityEngine;

namespace Game.Core
{
    /// <summary>Точка входа: создаёт партию, порождает визуалы плиток и связывает подписки.</summary>
    public sealed class GameSession : MonoBehaviour
    {
        [SerializeField] GameConfig config;
        [SerializeField] TileView tilePrefab;
        [SerializeField] RoadView roadPrefab;
        [SerializeField] Transform tilesRoot;
        [SerializeField] GameInput input;
        [SerializeField] CameraRig cameraRig;

        const float RoadDepth = -0.06f;

        readonly Dictionary<HexCoord, TileView> views = new();
        readonly Dictionary<HexCoord, RoadView> roadViews = new();

        GameState state;

        void Awake()
        {
            var map = MapGenerator.Generate(config.MapGenerationSettings);
            var wallet = new Wallet(config.StartingPoints, config.StartingGravel);
            state = new GameState(map, wallet, config.TileOpenCost, config.RoadCost);

            SpawnTiles(map);
            cameraRig.SetFieldHalfExtents(FieldHalfExtents(map));
        }

        void OnEnable()
        {
            input.TileClicked += OnTileClicked;
            input.Dragged += cameraRig.Pan;
            input.Zoomed += cameraRig.Zoom;
            state.TileChanged += OnTileChanged;
            state.ActionRefused += OnActionRefused;
            state.Roads.Changed += OnRoadsChanged;
        }

        void OnDisable()
        {
            input.TileClicked -= OnTileClicked;
            input.Dragged -= cameraRig.Pan;
            input.Zoomed -= cameraRig.Zoom;
            state.TileChanged -= OnTileChanged;
            state.ActionRefused -= OnActionRefused;
            state.Roads.Changed -= OnRoadsChanged;
        }

        void Start() => state.Begin();

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

        // HUD появится в M4, до тех пор отказ виден в Console.
        void OnActionRefused(string message) => Debug.Log(message);
    }
}
