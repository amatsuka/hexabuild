using System.Collections.Generic;
using Game.Economy;
using UnityEngine;

namespace Game.Grid
{
    /// <summary>Случайная раскладка месторождений по разделу 2.1 спеки.</summary>
    public static class MapGenerator
    {
        static readonly ResourceType[] DepositTypes = { ResourceType.Wood, ResourceType.Stone, ResourceType.Ore };

        public static HexMap Generate(MapGenerationSettings settings)
        {
            var random = settings.Seed == 0 ? new System.Random() : new System.Random(settings.Seed);
            var noiseOrigin = new Vector2(random.Next(0, 9999) * 0.37f, random.Next(0, 9999) * 0.41f);

            var depositsByCoord = new Dictionary<HexCoord, List<Deposit>>();
            foreach (var coord in HexMap.CoordsInFlare(settings.Rows))
                if (coord != HexCoord.Zero)
                    depositsByCoord[coord] = RollDeposits(random, settings);

            EnsureStoneNextToMetropolis(depositsByCoord, random, settings);

            var tiles = new List<TileData>(depositsByCoord.Count + 1)
            {
                new(HexCoord.Zero, true, null, RollBiome(HexCoord.Zero, settings, noiseOrigin), 0f)
            };
            foreach (var pair in depositsByCoord)
                tiles.Add(new TileData(
                    pair.Key,
                    false,
                    pair.Value,
                    RollBiome(pair.Key, settings, noiseOrigin),
                    RollShade(pair.Key, noiseOrigin)));

            return new HexMap(settings.Rows, tiles);
        }

        /// <summary>
        /// Ландшафт берётся из шума Перлина по мировым координатам плитки, поэтому биомы ложатся
        /// связными пятнами, а не вразнобой. Значение шума читается как высота: вода внизу, скалы
        /// наверху. На правила игры биом не влияет.
        /// </summary>
        static BiomeType RollBiome(HexCoord coord, MapGenerationSettings settings, Vector2 noiseOrigin)
        {
            var world = coord.ToWorld() * settings.BiomeNoiseScale;
            var height = Mathf.PerlinNoise(noiseOrigin.x + world.x, noiseOrigin.y + world.y);

            // Пороги подобраны на глаз: вода занимает низины, скалы — вершины, между ними суша.
            if (height < 0.27f)
                return BiomeType.Water;
            if (height < 0.36f)
                return BiomeType.Sand;
            if (height < 0.53f)
                return BiomeType.Meadow;

            return height < 0.68f ? BiomeType.Forest : BiomeType.Rocks;
        }

        /// <summary>Второй, более частый шум — мелкая разница тона внутри одного биома.</summary>
        static float RollShade(HexCoord coord, Vector2 noiseOrigin)
        {
            var world = coord.ToWorld() * 0.9f;
            // PerlinNoise изредка отдаёт значения чуть за пределами [0,1], поэтому зажимаем.
            return Mathf.Clamp(Mathf.PerlinNoise(noiseOrigin.y + world.x, noiseOrigin.x + world.y) * 2f - 1f, -1f, 1f);
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
