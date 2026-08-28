using System.Collections.Generic;
using Game.Economy;

namespace Game.Grid
{
    /// <summary>Случайная раскладка месторождений по разделу 2.1 спеки.</summary>
    public static class MapGenerator
    {
        static readonly ResourceType[] DepositTypes = { ResourceType.Wood, ResourceType.Stone, ResourceType.Ore };

        public static HexMap Generate(MapGenerationSettings settings)
        {
            var random = settings.Seed == 0 ? new System.Random() : new System.Random(settings.Seed);

            var depositsByCoord = new Dictionary<HexCoord, List<Deposit>>();
            foreach (var coord in HexMap.CoordsInFlare(settings.Rows))
                if (coord != HexCoord.Zero)
                    depositsByCoord[coord] = RollDeposits(random, settings);

            EnsureStoneNextToMetropolis(depositsByCoord, random, settings);

            var tiles = new List<TileData>(depositsByCoord.Count + 1)
            {
                new(HexCoord.Zero, true)
            };
            foreach (var pair in depositsByCoord)
                tiles.Add(new TileData(pair.Key, false, pair.Value));

            return new HexMap(settings.Rows, tiles);
        }

        static List<Deposit> RollDeposits(System.Random random, MapGenerationSettings settings)
        {
            var count = RollDepositCount(random, settings);
            var deposits = new List<Deposit>(count);
            var available = new List<ResourceType>(DepositTypes);

            for (var i = 0; i < count; i++)
            {
                var index = random.Next(available.Count);
                deposits.Add(new Deposit(available[index], RollReserve(random, settings)));
                available.RemoveAt(index);
            }

            return deposits;
        }

        static int RollDepositCount(System.Random random, MapGenerationSettings settings)
        {
            var total = settings.EmptyWeight + settings.SingleDepositWeight
                                             + settings.TwoDepositsWeight + settings.ThreeDepositsWeight;
            var roll = random.NextDouble() * total;

            if (roll < settings.EmptyWeight)
                return 0;
            roll -= settings.EmptyWeight;

            if (roll < settings.SingleDepositWeight)
                return 1;
            roll -= settings.SingleDepositWeight;

            return roll < settings.TwoDepositsWeight ? 2 : 3;
        }

        static int RollReserve(System.Random random, MapGenerationSettings settings) =>
            random.Next(settings.MinReserve, settings.MaxReserve + 1);

        /// <summary>Без камня рядом с Метрополией партия не стартует экономически.</summary>
        static void EnsureStoneNextToMetropolis(
            IReadOnlyDictionary<HexCoord, List<Deposit>> depositsByCoord,
            System.Random random,
            MapGenerationSettings settings)
        {
            var neighbors = new List<List<Deposit>>();
            foreach (var coord in HexCoord.Zero.Neighbors())
                if (depositsByCoord.TryGetValue(coord, out var deposits))
                {
                    foreach (var deposit in deposits)
                        if (deposit.Type == ResourceType.Stone)
                            return;

                    neighbors.Add(deposits);
                }

            if (neighbors.Count == 0)
                return;

            var chosen = neighbors[random.Next(neighbors.Count)];
            var stone = new Deposit(ResourceType.Stone, RollReserve(random, settings));

            if (chosen.Count == 0)
                chosen.Add(stone);
            else
                chosen[random.Next(chosen.Count)] = stone;
        }
    }
}
