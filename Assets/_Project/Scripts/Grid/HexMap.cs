using System.Collections.Generic;
using UnityEngine;

namespace Game.Grid
{
    /// <summary>Поле гексов заданного радиуса: хранение плиток и доступ по координате.</summary>
    public sealed class HexMap
    {
        readonly Dictionary<HexCoord, TileData> tiles = new();

        public HexMap(int radius, IEnumerable<TileData> tiles)
        {
            Radius = radius;
            foreach (var tile in tiles)
                this.tiles.Add(tile.Coord, tile);
        }

        public int Radius { get; }

        public int Count => tiles.Count;

        public IReadOnlyDictionary<HexCoord, TileData> Tiles => tiles;

        public TileData Metropolis => tiles[HexCoord.Zero];

        /// <summary>Координаты поля-гекса радиуса <paramref name="radius"/> вокруг центра.</summary>
        public static IEnumerable<HexCoord> CoordsInRadius(int radius)
        {
            for (var q = -radius; q <= radius; q++)
            {
                var minR = Mathf.Max(-radius, -q - radius);
                var maxR = Mathf.Min(radius, -q + radius);
                for (var r = minR; r <= maxR; r++)
                    yield return new HexCoord(q, r);
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
