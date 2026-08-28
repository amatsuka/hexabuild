using System.Collections.Generic;
using UnityEngine;

namespace Game.Grid
{
    /// <summary>Поле гексов заданного радиуса: хранение плиток и доступ по координате.</summary>
    public sealed class HexMap
    {
        readonly Dictionary<HexCoord, TileData> tiles = new();

        public HexMap(int radius)
        {
            Radius = radius;

            for (var q = -radius; q <= radius; q++)
            {
                var minR = Mathf.Max(-radius, -q - radius);
                var maxR = Mathf.Min(radius, -q + radius);
                for (var r = minR; r <= maxR; r++)
                {
                    var coord = new HexCoord(q, r);
                    tiles.Add(coord, new TileData(coord, coord == HexCoord.Zero));
                }
            }
        }

        public int Radius { get; }

        public int Count => tiles.Count;

        public IReadOnlyDictionary<HexCoord, TileData> Tiles => tiles;

        public TileData Metropolis => tiles[HexCoord.Zero];

        public bool Contains(HexCoord coord) => tiles.ContainsKey(coord);

        public bool TryGetTile(HexCoord coord, out TileData tile) => tiles.TryGetValue(coord, out tile);
    }
}
