using System.Collections.Generic;

namespace Game.Grid
{
    /// <summary>Поле гексов из заданного числа рядов: хранение плиток и доступ по координате.</summary>
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
        /// Координаты поля-раструба: Метрополия внизу, ряд r содержит r+1 плиток и центрируется
        /// над ней. Ряд шире нижнего на одну плитку, поэтому каждая плитка опирается хотя бы на
        /// одну плитку ряда ниже, а крайние — ровно на одну. Чётные и нечётные ряды смещены на
        /// полплитки — так гексы стыкуются.
        /// </summary>
        public static IEnumerable<HexCoord> CoordsInFlare(int rows)
        {
            for (var r = 0; r < rows; r++)
                for (var i = 0; i <= r; i++)
                    yield return new HexCoord(-r + i, r);
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

        /// <summary>
        /// Плитки, до которых можно дойти от Метрополии по проходимым: гряда гор обрывает путь,
        /// и поле за ней не открыть никогда. Ландшафт за партию не меняется, поэтому список
        /// считают один раз и держат.
        /// </summary>
        public List<TileData> ReachableFromMetropolis()
        {
            var reachable = new List<TileData> { Metropolis };
            var visited = new HashSet<HexCoord> { HexCoord.Zero };
            var frontier = new Queue<HexCoord>();
            frontier.Enqueue(HexCoord.Zero);

            while (frontier.Count > 0)
                foreach (var neighbor in NeighborsOf(frontier.Dequeue()))
                    if (neighbor.IsPassable && visited.Add(neighbor.Coord))
                    {
                        reachable.Add(neighbor);
                        frontier.Enqueue(neighbor.Coord);
                    }

            return reachable;
        }
    }
}
