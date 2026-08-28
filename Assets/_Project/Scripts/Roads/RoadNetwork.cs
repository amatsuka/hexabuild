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
        /// Кратчайший путь по дорогам от плитки до Метрополии, включая обе крайние плитки.
        /// Путь нужен доставке и фиксируется в момент отправки.
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
        /// BFS от Метрополии по плиткам с дорогами. Попутно каждой дороге назначается родитель —
        /// первая плитка, из которой её нашли, то есть шаг кратчайшего пути к Метрополии.
        /// Оторванные от Метрополии куски тоже раскладываются в деревья, каждый от своего корня,
        /// иначе кольцо дорог рисовалось бы замкнутым.
        /// </summary>
        void Recalculate()
        {
            connected.Clear();
            parents.Clear();

            Traverse(HexCoord.Zero, connected);

            foreach (var road in roads)
                if (!parents.ContainsKey(road))
                    Traverse(road, null);
        }

        void Traverse(HexCoord root, HashSet<HexCoord> reached)
        {
            frontier.Clear();
            frontier.Enqueue(root);

            while (frontier.Count > 0)
            {
                var current = frontier.Dequeue();
                foreach (var neighbor in current.Neighbors())
                {
                    if (!roads.Contains(neighbor) || parents.ContainsKey(neighbor))
                        continue;

                    parents[neighbor] = current;
                    reached?.Add(neighbor);
                    frontier.Enqueue(neighbor);
                }
            }
        }
    }
}
