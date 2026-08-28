using Game.Grid;
using Game.UI;
using UnityEngine;

namespace Game.Core
{
    /// <summary>Точка входа: создаёт поле, порождает визуалы плиток и связывает подписки.</summary>
    public sealed class GameSession : MonoBehaviour
    {
        [SerializeField] int fieldRadius = 6;
        [SerializeField] TileView tilePrefab;
        [SerializeField] Transform tilesRoot;
        [SerializeField] GameInput input;
        [SerializeField] CameraRig cameraRig;

        HexMap map;

        void Awake()
        {
            map = new HexMap(fieldRadius);
            SpawnTiles();
            cameraRig.SetFieldHalfExtents(FieldHalfExtents());
        }

        void OnEnable()
        {
            input.TileClicked += OnTileClicked;
            input.Dragged += cameraRig.Pan;
            input.Zoomed += cameraRig.Zoom;
        }

        void OnDisable()
        {
            input.TileClicked -= OnTileClicked;
            input.Dragged -= cameraRig.Pan;
            input.Zoomed -= cameraRig.Zoom;
        }

        void SpawnTiles()
        {
            foreach (var tile in map.Tiles.Values)
                Instantiate(tilePrefab, tilesRoot).Bind(tile);
        }

        /// <summary>Габариты поля с учётом вершин крайних гексов — для границ камеры.</summary>
        Vector2 FieldHalfExtents()
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

        void OnTileClicked(HexCoord coord)
        {
            if (map.Contains(coord))
                Debug.Log($"Tile {coord}");
        }
    }
}
