using System;
using Game.Economy;
using Game.Grid;

namespace Game.Core
{
    /// <summary>Изменяемое состояние партии: поле, кошелёк и правило открытия плиток.</summary>
    public sealed class GameState
    {
        readonly int tileOpenCost;

        public GameState(HexMap map, Wallet wallet, int tileOpenCost)
        {
            Map = map;
            Wallet = wallet;
            this.tileOpenCost = tileOpenCost;
        }

        /// <summary>Плитка сменила состояние и её визуал пора обновить.</summary>
        public event Action<TileData> TileChanged;

        /// <summary>Действие не выполнено: текст для HUD.</summary>
        public event Action<string> ActionRefused;

        public HexMap Map { get; }

        public Wallet Wallet { get; }

        /// <summary>Старт партии: Метрополия открыта, её соседи доступны.</summary>
        public void Begin()
        {
            var metropolis = Map.Metropolis;
            metropolis.Reveal();
            TileChanged?.Invoke(metropolis);
            MakeNeighborsAvailable(metropolis);
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

        void MakeNeighborsAvailable(TileData tile)
        {
            foreach (var neighbor in Map.NeighborsOf(tile.Coord))
                if (neighbor.MakeAvailable())
                    TileChanged?.Invoke(neighbor);
        }
    }
}
