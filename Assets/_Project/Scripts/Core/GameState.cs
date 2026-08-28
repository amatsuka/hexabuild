using System;
using Game.Economy;
using Game.Grid;
using Game.Roads;

namespace Game.Core
{
    /// <summary>Изменяемое состояние партии: поле, кошелёк и правило открытия плиток.</summary>
    public sealed class GameState
    {
        readonly int tileOpenCost;
        readonly int roadCost;

        public GameState(HexMap map, Wallet wallet, int tileOpenCost, int roadCost)
        {
            Map = map;
            Wallet = wallet;
            this.tileOpenCost = tileOpenCost;
            this.roadCost = roadCost;
            Roads = new RoadNetwork(map);
        }

        /// <summary>Плитка сменила состояние и её визуал пора обновить.</summary>
        public event Action<TileData> TileChanged;

        /// <summary>Действие не выполнено: текст для HUD.</summary>
        public event Action<string> ActionRefused;

        public HexMap Map { get; }

        public Wallet Wallet { get; }

        public RoadNetwork Roads { get; }

        /// <summary>Старт партии: Метрополия открыта, её соседи доступны.</summary>
        public void Begin()
        {
            var metropolis = Map.Metropolis;
            metropolis.Reveal();
            TileChanged?.Invoke(metropolis);
            MakeNeighborsAvailable(metropolis);
        }

        /// <summary>ЛКМ по плитке: закрытую открываем, открытую застраиваем дорогой.</summary>
        public void HandleTileClick(HexCoord coord)
        {
            if (!Map.TryGetTile(coord, out var tile))
                return;

            if (tile.State is TileState.Hidden or TileState.Available)
                TryRevealTile(coord);
            else
                TryBuildRoad(coord);
        }

        public bool TryRevealTile(HexCoord coord)
        {
            if (!Map.TryGetTile(coord, out var tile))
                return false;

            if (tile.State == TileState.Hidden)
            {
                ActionRefused?.Invoke("Плитка не смежна с открытой");
                return false;
            }

            if (tile.State != TileState.Available)
                return false;

            if (!Wallet.TrySpendPoints(tileOpenCost))
            {
                ActionRefused?.Invoke($"Не хватает очков: нужно {tileOpenCost}");
                return false;
            }

            tile.Reveal();
            TileChanged?.Invoke(tile);
            MakeNeighborsAvailable(tile);
            return true;
        }

        public bool TryBuildRoad(HexCoord coord)
        {
            if (!Map.TryGetTile(coord, out var tile) || tile.IsMetropolis)
                return false;

            if (tile.State is TileState.Hidden or TileState.Available)
            {
                ActionRefused?.Invoke("Дорогу строят только на открытой плитке");
                return false;
            }

            if (Roads.HasRoad(coord))
                return false;

            if (!Wallet.TrySpendMaterial(ResourceType.Gravel, roadCost))
            {
                ActionRefused?.Invoke($"Не хватает щебня: нужно {roadCost}");
                return false;
            }

            Roads.Build(coord);
            return true;
        }

        void MakeNeighborsAvailable(TileData tile)
        {
            foreach (var neighbor in Map.NeighborsOf(tile.Coord))
                if (neighbor.MakeAvailable())
                    TileChanged?.Invoke(neighbor);
        }
    }
}
