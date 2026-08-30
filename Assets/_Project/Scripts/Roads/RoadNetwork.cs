using System;
using System.Collections.Generic;
using Game.Grid;

namespace Game.Roads
{
    /// <summary>Дороги на поле и их связность с Метрополией. Пересчёт — по событию постройки.</summary>
    public sealed class RoadNetwork
    {
        readonly HexMap map;
        readonly HashSet<HexCoord> roads = new();
        readonly HashSet<HexCoord> connected = new();
        readonly Dictionary<HexCoord, HexCoord> parents = new();
        readonly Queue<HexCoord> frontier = new();
        readonly HashSet<HexCoord> visited = new();

        public RoadNetwork(HexMap map)
        {
            this.map = map;
        }

        /// <summary>Дороги построены или переподключены, визуал пора обновить.</summary>
        public event Action Changed;

        public IReadOnlyCollection<HexCoord> Roads => roads;

        public bool HasRoad(HexCoord coord) => roads.Contains(coord);

        /// <summary>Есть непрерывная цепочка дорог от плитки до Метрополии.</summary>
        public bool IsConnected(HexCoord coord) => connected.Contains(coord);

        /// <summary>Плитка, через которую эта дорога уходит к Метрополии.</summary>
        public bool TryGetParent(HexCoord coord, out HexCoord parent) => parents.TryGetValue(coord, out parent);

        /// <summary>
        /// Две плитки соединены участком маршрута. У каждой дороги ровно один родитель, поэтому
        /// сеть рисуется деревом: перемычек между соседями по кругу не возникает.
        /// </summary>
        public bool IsRouteLink(HexCoord from, HexCoord to) =>
            (parents.TryGetValue(from, out var fromParent) && fromParent == to)
            || (parents.TryGetValue(to, out var toParent) && toParent == from);

        public bool Build(HexCoord coord)
        {
            if (!map.Contains(coord) || coord == HexCoord.Zero)
                return false;

            if (!roads.Add(coord))
                return false;

            Recalculate();
            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// Путь по дорогам от плитки до Метрополии вдоль назначенных родителей, включая обе
        /// крайние плитки. Родитель липкий, поэтому путь — не обязательно кратчайший: это тот
        /// маршрут, который дорога нашла в момент подключения и который игрок видит нарисованным.
        /// Доставка фиксирует его в момент отправки.
        /// </summary>
        public bool TryFindPathToMetropolis(HexCoord from, List<HexCoord> path)
        {
            path.Clear();
            if (!IsConnected(from))
                return false;

            var current = from;
            path.Add(current);
            while (current != HexCoord.Zero)
            {
                current = parents[current];
                path.Add(current);
            }

            return true;
        }

        /// <summary>
        /// BFS от Метрополии по плиткам с дорогами. Родитель — плитка, из которой дорогу нашли,
        /// то есть её шаг вниз по маршруту.
        ///
        /// Родитель липкий: дорога, однажды дотянувшаяся до Метрополии, его больше не меняет.
        /// Дороги не сносятся, поэтому найденный маршрут остаётся валидным навсегда, а перебор
        /// заново перекладывал бы уже построенную сеть при каждой новой дороге — игрок видел бы,
        /// как старые дороги переползают. Цена — маршрут иногда на шаг длиннее кратчайшего.
        ///
        /// Оторванные от Метрополии куски пересчитываются целиком: их деревья растут от
        /// собственных корней и после подключения смотрели бы не в ту сторону.
        /// </summary>
        void Recalculate()
        {
            foreach (var road in roads)
                if (!connected.Contains(road))
                    parents.Remove(road);

            Traverse(HexCoord.Zero, connected);

            foreach (var road in roads)
                if (!parents.ContainsKey(road))
                    Traverse(road, null);
        }

        /// <summary>
        /// Обход от корня. `visited` отдельно от `parents`: подключённые дороги родителя уже
        /// имеют, но пройти сквозь них нужно — за ними лежат те, которых обход ещё не касался.
        /// </summary>
        void Traverse(HexCoord root, HashSet<HexCoord> reached)
        {
            frontier.Clear();
            visited.Clear();
            frontier.Enqueue(root);
            visited.Add(root);

            while (frontier.Count > 0)
            {
                var current = frontier.Dequeue();
                foreach (var neighbor in current.Neighbors())
                {
                    if (!roads.Contains(neighbor) || !visited.Add(neighbor))
                        continue;

                    if (!parents.ContainsKey(neighbor))
                        parents[neighbor] = current;

                    reached?.Add(neighbor);
                    frontier.Enqueue(neighbor);
                }
            }
        }
    }
}
