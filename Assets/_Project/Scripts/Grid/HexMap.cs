using System.Collections.Generic;
using UnityEngine;

namespace Game.Grid
{
    /// <summary>Поле гексов заданного радиуса: хранение плиток и доступ по координате.</summary>
    public sealed class HexMap
    {
        readonly Dictionary<HexCoord, TileData> tiles = new();

        public HexMap(int rows, IEnumerable<TileData> tiles)
        {
            Rows = rows;
            foreach (var tile in tiles)
                this.tiles.Add(tile.Coord, tile);
        }

        public int Rows { get; }

        public int Count => tiles.Count;

        public IReadOnlyDictionary<HexCoord, TileData> Tiles => tiles;

        public TileData Metropolis => tiles[HexCoord.Zero];

        /// <summary>
        /// Координаты поля-раструба: Метрополия внизу, ряд r содержит 2r+1 плиток и центрируется
        /// над ней. Чётные и нечётные ряды смещены на полплитки — так гексы стыкуются.
        /// </summary>
        public static IEnumerable<HexCoord> CoordsInFlare(int rows)
        {
            for (var r = 0; r < rows; r++)
            {
                var firstQ = -Mathf.RoundToInt(1.5f * r);
                for (var i = 0; i <= 2 * r; i++)
                    yield return new HexCoord(firstQ + i, r);
            }
        }

        public bool Contains(HexCoord coord) => tiles.ContainsKey(coord);

        public bool TryGetTile(HexCoord coord, out TileData tile) => tiles.TryGetValue(coord, out tile);

        /// <summary>Существующие на поле соседи плитки.</summary>
        public IEnumerable<TileData> NeighborsOf(HexCoord coord)
        {
            foreach (var neighbor in coord.Neighbors())
                if (tiles.TryGetValue(neighbor, out var tile))
                    yield return tile;
        }
    }
}
