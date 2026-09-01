using System.Collections.Generic;
using UnityEngine;

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
        /// Ширина верхнего ряда в плитках. Потолок поля по горизонтали: сколько бы рядов ни было,
        /// шире раструб не станет. Это число и держит поле под вертикальную ориентацию экрана.
        /// </summary>
        public const int TopRowTiles = 9;

        /// <summary>
        /// Как ширина ряда идёт от одной плитки внизу до <see cref="TopRowTiles"/> наверху.
        /// Единица — равномерно, ряд за рядом. Меньше единицы — поле раскрывается сразу у
        /// Метрополии и почти перестаёт расширяться наверху; больше — узкое горло внизу и
        /// раструб под самой кромкой.
        /// </summary>
        const float FlareCurve = 0.5f;

        /// <summary>
        /// Ширина ряда в плитках. Кривая обрезана ступенькой в одну плитку на ряд: ряд шире
        /// нижнего больше чем на плитку оставил бы крайние плитки висеть в воздухе, поэтому у
        /// самой Метрополии поле расширяется медленнее кривой.
        /// </summary>
        public static int RowWidth(int row, int rows)
        {
            var reach = rows > 1 ? Mathf.Pow(row / (float)(rows - 1), FlareCurve) : 0f;
            return Mathf.Min(row + 1, 1 + Mathf.RoundToInt((TopRowTiles - 1) * reach));
        }

        /// <summary>
        /// Координаты поля-раструба: Метрополия внизу, ряды расширяются вверх по
        /// <see cref="RowWidth"/>. Ряд шире нижнего расходится на полплитки в обе стороны и
        /// остаётся по центру, поэтому каждая плитка опирается хотя бы на одну плитку ряда ниже.
        /// Ряд той же ширины по центру не встаёт — соседние ряды гексов всегда смещены на
        /// полплитки, — и уходит на полплитки вбок; следующий такой ряд возвращается на ось,
        /// а сторона ухода чередуется. Край выходит зигзагом, но поле стоит по оси Метрополии.
        /// </summary>
        public static IEnumerable<HexCoord> CoordsInFlare(int rows)
        {
            var q = 0;
            var offset = 0;      // сдвиг ряда от оси Метрополии в полуплитках: 0, -1 или +1
            var leanLeft = true; // куда уйдёт следующий ряд, которому на оси места нет

            for (var r = 0; r < rows; r++)
            {
                var width = RowWidth(r, rows);

                // Ряд шире нижнего расходится на полплитки в обе стороны и сдвиг не меняет.
                if (r > 0 && width == RowWidth(r - 1, rows))
                {
                    if (offset != 0)
                    {
                        if (offset > 0)
                            q--;
                        offset = 0;
                    }
                    else
                    {
                        if (leanLeft)
                            q--;
                        offset = leanLeft ? -1 : 1;
                        leanLeft = !leanLeft;
                    }
                }
                else if (r > 0)
                    q--;

                for (var i = 0; i < width; i++)
                    yield return new HexCoord(q + i, r);
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

        /// <summary>
        /// Плитки, до которых можно дойти от Метрополии по проходимым. Отрезанных кусков поля
        /// генератор не выпускает — в грядах пробиты перевалы, — так что за пределами списка
        /// остаются только сами горы. Ландшафт за партию не меняется, поэтому список считают
        /// один раз и держат.
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
