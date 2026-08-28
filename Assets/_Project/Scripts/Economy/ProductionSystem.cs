using System;
using System.Collections.Generic;
using Game.Grid;
using Game.Roads;

namespace Game.Economy
{
    /// <summary>Добыча: один таймер на плитку, выдача по кругу, только на подключённых плитках.</summary>
    public sealed class ProductionSystem
    {
        readonly HexMap map;
        readonly RoadNetwork roads;
        readonly float interval;
        readonly Dictionary<HexCoord, float> timers = new();
        readonly List<HexCoord> stopped = new();

        public ProductionSystem(HexMap map, RoadNetwork roads, float interval)
        {
            this.map = map;
            this.roads = roads;
            this.interval = interval;
        }

        public event Action<TileData, ResourceType> Produced;

        /// <summary>Плитка исчерпана и больше не производит.</summary>
        public event Action<TileData> TileDepleted;

        public void Tick(float deltaTime)
        {
            stopped.Clear();

            foreach (var coord in roads.Roads)
            {
                if (!roads.IsConnected(coord) || !map.TryGetTile(coord, out var tile))
                    continue;

                if (tile.State != TileState.Revealed || tile.Deposits.Count == 0)
                {
                    stopped.Add(coord);
                    continue;
                }

                var timer = timers.GetValueOrDefault(coord) + deltaTime;
                while (timer >= interval)
                {
                    timer -= interval;
                    if (!tile.TryExtract(out var type))
                        break;

                    Produced?.Invoke(tile, type);
                    if (tile.State == TileState.Depleted)
                    {
                        TileDepleted?.Invoke(tile);
                        break;
                    }
                }

                timers[coord] = timer;
            }

            foreach (var coord in stopped)
                timers.Remove(coord);
        }
    }
}
