using System;
using Game.Economy;
using Game.Grid;
using Game.Roads;
using Game.Storage;

namespace Game.Core
{
    /// <summary>Изменяемое состояние партии: поле, кошелёк и правило открытия плиток.</summary>
    public sealed class GameState
    {
        readonly PriceSettings prices;

        public GameState(HexMap map, Wallet wallet, StorageGrid storage, PriceSettings prices)
        {
            Map = map;
            Wallet = wallet;
            Storage = storage;
            this.prices = prices;
            Roads = new RoadNetwork(map);
        }

        /// <summary>Плитка сменила состояние и её визуал пора обновить.</summary>
        public event Action<TileData> TileChanged;

        /// <summary>Действие не выполнено: текст для HUD.</summary>
        public event Action<string> ActionRefused;

        public HexMap Map { get; }

        public Wallet Wallet { get; }

        public StorageGrid Storage { get; }

        public RoadNetwork Roads { get; }

        /// <summary>Сколько плиток открыл игрок. Метрополия не в счёт: её открывать не пришлось.</summary>
        public int OpenedTiles { get; private set; }

        /// <summary>
        /// Цена следующего открытия. Растёт ступенями: чем больше поля позади, тем дороже шаг
        /// вперёд. Иначе доход от новых плиток обгоняет расход и партия перестаёт быть выбором.
        /// </summary>
        public int NextTileCost => prices.TileOpen + prices.OpenStep * (OpenedTiles / prices.OpenGroup);

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

            if (!tile.IsPassable)
            {
                ActionRefused?.Invoke("Гора непроходима");
                return false;
            }

            var cost = NextTileCost;
            if (!Wallet.TrySpendPoints(cost))
            {
                ActionRefused?.Invoke($"Не хватает очков: нужно {cost}");
                return false;
            }

            OpenedTiles++;
            tile.Reveal();
            TileChanged?.Invoke(tile);
            MakeNeighborsAvailable(tile);
            return true;
        }

        public bool TryBuildRoad(HexCoord coord)
        {
            if (!Map.TryGetTile(coord, out var tile) || tile.IsMetropolis)
                return false;

            if (!tile.IsPassable)
            {
                ActionRefused?.Invoke("Гора непроходима");
                return false;
            }

            if (tile.State is TileState.Hidden or TileState.Available)
            {
                ActionRefused?.Invoke("Дорогу строят только на открытой плитке");
                return false;
            }

            if (Roads.HasRoad(coord))
                return false;

            var price = RoadPrice(tile);
            if (!Storage.TryRemove(ResourceType.Gravel, price))
            {
                ActionRefused?.Invoke(price > prices.Road
                    ? $"Нужен мост: {price} щебня"
                    : $"Не хватает щебня: нужно {price}");
                return false;
            }

            Roads.Build(coord);
            return true;
        }

        /// <summary>
        /// Цена дороги: обычная плюс надбавка за мост на плитке с рекой. Русло идёт через центр
        /// плитки, лента дороги — тоже, обойти реку внутри гекса нельзя, поэтому цена локальна
        /// и родителя спрашивать не нужно.
        /// </summary>
        public int RoadPrice(TileData tile) => tile.HasRiver ? prices.Road + prices.Bridge : prices.Road;

        void MakeNeighborsAvailable(TileData tile)
        {
            foreach (var neighbor in Map.NeighborsOf(tile.Coord))
                if (neighbor.MakeAvailable())
                    TileChanged?.Invoke(neighbor);
        }
    }
}
