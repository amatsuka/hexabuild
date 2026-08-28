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

            var cameFrom = new Dictionary<HexCoord, HexCoord>();
            frontier.Clear();
            frontier.Enqueue(from);
            cameFrom[from] = from;

            while (frontier.Count > 0)
            {
                var current = frontier.Dequeue();
                if (current == HexCoord.Zero)
                    return BuildPath(cameFrom, from, path);

                foreach (var neighbor in current.Neighbors())
                {
                    if (neighbor != HexCoord.Zero && !roads.Contains(neighbor))
                        continue;

                    if (!cameFrom.TryAdd(neighbor, current))
                        continue;

                    frontier.Enqueue(neighbor);
                }
            }

            return false;
        }

        static bool BuildPath(IReadOnlyDictionary<HexCoord, HexCoord> cameFrom, HexCoord from, List<HexCoord> path)
        {
            var current = HexCoord.Zero;
            while (current != from)
            {
                path.Add(current);
                current = cameFrom[current];
            }

            path.Add(from);
            path.Reverse();
            return true;
        }

        /// <summary>BFS от Метрополии по плиткам с дорогами.</summary>
        void Recalculate()
        {
            connected.Clear();
            frontier.Clear();
            frontier.Enqueue(HexCoord.Zero);

            while (frontier.Count > 0)
            {
                var current = frontier.Dequeue();
                foreach (var neighbor in current.Neighbors())
                {
                    if (!roads.Contains(neighbor) || !connected.Add(neighbor))
                        continue;

                    frontier.Enqueue(neighbor);
                }
            }
        }
    }
}
