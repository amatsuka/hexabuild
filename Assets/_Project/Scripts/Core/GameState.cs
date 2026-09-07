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

        /// <summary>Поправка перед округлением цены вниз: 20 × 1.04⁵ в плавающей точке чуть меньше себя.</summary>
        const double CostEpsilon = 1e-6;

        public GameState(
            HexMap map, Wallet wallet, StorageGrid storage, PriceSettings prices, ScoreMultiplier multiplier)
        {
            Map = map;
            Wallet = wallet;
            Storage = storage;
            Multiplier = multiplier;
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

        /// <summary>Множитель очков партии: колонию ему считает открытие плитки здесь.</summary>
        public ScoreMultiplier Multiplier { get; }

        /// <summary>Сколько плиток открыл игрок. Метрополия не в счёт: её открывать не пришлось.</summary>
        public int OpenedTiles { get; private set; }

        /// <summary>
        /// Цена следующего открытия. Растёт геометрически: `TileOpen × OpenGrowth^открытых`,
        /// вниз до целого. Доход плитки растёт вместе с множителем колонии, и линейная цена
        /// делала открытие безусловно выгодным — тапом, а не выбором; к концу поля отношение
        /// дохода к цене падает, и пустая плитка становится ставкой.
        /// </summary>
        public int NextTileCost =>
            (int)Math.Floor(prices.TileOpen * Math.Pow(prices.OpenGrowth, OpenedTiles) + CostEpsilon);

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
                ActionRefused?.Invoke(ImpassableReason(tile));
                return false;
            }

            var cost = NextTileCost;
            if (!Wallet.TrySpendPoints(cost))
            {
                ActionRefused?.Invoke($"Не хватает очков: нужно {cost}");
                return false;
            }

            OpenedTiles++;
            Multiplier.TrackOpened(OpenedTiles);
            tile.Reveal();
            TileChanged?.Invoke(tile);
            MakeNeighborsAvailable(tile);
            return true;
        }

        /// <summary>
        /// Стена ландшафта у горы и воды одна, а отказ разный: игроку надо сказать, во что он
        /// упёрся, иначе «гора непроходима» на заливе читается как ошибка игры.
        /// </summary>
        static string ImpassableReason(TileData tile) =>
            tile.Biome == BiomeType.Water ? "Вода непроходима" : "Гора непроходима";

        public bool TryBuildRoad(HexCoord coord)
        {
            if (!Map.TryGetTile(coord, out var tile) || tile.IsMetropolis)
                return false;

            if (!tile.IsPassable)
            {
                ActionRefused?.Invoke(ImpassableReason(tile));
                return false;
            }

            if (tile.State is TileState.Hidden or TileState.Available)
            {
                ActionRefused?.Invoke("Дорогу строят только на открытой плитке");
                return false;
            }

            if (Roads.HasRoad(coord))
                return false;

            if (!Roads.CanExtendTo(coord))
            {
                ActionRefused?.Invoke("Дорогу тянут от Метрополии: рядом нет подключённой дороги");
                return false;
            }

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
