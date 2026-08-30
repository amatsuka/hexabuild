using System.Collections.Generic;
using Game.Economy;
using UnityEngine;

namespace Game.Grid
{
    /// <summary>Случайная раскладка месторождений и препятствий по разделам 2.1 и 2.3 спеки.</summary>
    public static class MapGenerator
    {
        static readonly ResourceType[] DepositTypes = { ResourceType.Wood, ResourceType.Stone, ResourceType.Ore };

        /// <summary>
        /// Какую долю поля обязан покрывать обход по проходимым плиткам от Метрополии. Ниже —
        /// карта считается негодной: горы отрезали слишком много, играть будет не во что.
        /// </summary>
        const float MinPassableShare = 0.85f;

        /// <summary>Попыток перегенерации с новым смещением шума, прежде чем взять что вышло.</summary>
        const int MaxAttempts = 20;

        /// <summary>Русел на карте и максимальная длина каждого в рёбрах.</summary>
        const int RiverCount = 3;
        const int RiverLength = 16;

        public static HexMap Generate(MapGenerationSettings settings)
        {
            var random = settings.Seed == 0 ? new System.Random() : new System.Random(settings.Seed);

            HexMap fallback = null;
            for (var attempt = 0; attempt < MaxAttempts; attempt++)
            {
                var map = Build(settings, random);
                fallback ??= map;

                if (PassableShare(map) >= MinPassableShare && StoneGuaranteeIsReachable(map))
                    return map;
            }

            // Двадцать смещений шума подряд дали негодную карту — берём первую и не зацикливаемся.
            return fallback;
        }

        static HexMap Build(MapGenerationSettings settings, System.Random random)
        {
            var noiseOrigin = new Vector2(random.Next(0, 9999) * 0.37f, random.Next(0, 9999) * 0.41f);

            var biomes = new Dictionary<HexCoord, BiomeType>();
            foreach (var coord in HexMap.CoordsInFlare(settings.Rows))
                biomes[coord] = RollBiome(coord, settings, noiseOrigin);

            // Метрополия — город, а не ландшафт: горой или водой она быть не может по правилам 2.1.
            biomes[HexCoord.Zero] = BiomeType.Meadow;

            var rivers = CarveRivers(biomes, settings.Rows, noiseOrigin);

            var depositsByCoord = new Dictionary<HexCoord, List<Deposit>>();
            foreach (var pair in biomes)
                if (pair.Key != HexCoord.Zero)
                    depositsByCoord[pair.Key] = pair.Value == BiomeType.Mountains
                        ? new List<Deposit>()
                        : RollDeposits(random, settings);

            EnsureStoneNextToMetropolis(depositsByCoord, biomes, random, settings);

            var tiles = new List<TileData>(biomes.Count);
            foreach (var pair in biomes)
            {
                var coord = pair.Key;
                tiles.Add(new TileData(
                    coord,
                    coord == HexCoord.Zero,
                    coord == HexCoord.Zero ? null : depositsByCoord[coord],
                    pair.Value,
                    coord == HexCoord.Zero ? 0f : RollShade(coord, noiseOrigin),
                    rivers.TryGetValue(coord, out var mask) ? mask : 0));
            }

            return new HexMap(settings.Rows, tiles);
        }

        /// <summary>
        /// Ландшафт берётся из шума Перлина по мировым координатам плитки, поэтому биомы ложатся
        /// связными пятнами. Значение шума читается как высота: вода внизу, горы наверху.
        /// </summary>
        static BiomeType RollBiome(HexCoord coord, MapGenerationSettings settings, Vector2 noiseOrigin)
        {
            var height = Height(coord, settings.BiomeNoiseScale, noiseOrigin);

            // Пороги подобраны на глаз: вода занимает низины, горы — вершины, между ними суша.
            if (height < 0.27f)
                return BiomeType.Water;
            if (height < 0.36f)
                return BiomeType.Sand;
            if (height < 0.50f)
                return BiomeType.Meadow;
            if (height < 0.60f)
                return BiomeType.Forest;

            return height < 0.645f ? BiomeType.Rocks : BiomeType.Mountains;
        }

        static float Height(HexCoord coord, float noiseScale, Vector2 noiseOrigin)
        {
            var world = coord.ToWorld() * noiseScale;
            return Mathf.PerlinNoise(noiseOrigin.x + world.x, noiseOrigin.y + world.y);
        }

        /// <summary>Второй, более частый шум — мелкая разница тона внутри одного биома.</summary>
        static float RollShade(HexCoord coord, Vector2 noiseOrigin)
        {
            var world = coord.ToWorld() * 0.9f;
            // PerlinNoise изредка отдаёт значения чуть за пределами [0,1], поэтому зажимаем.
            return Mathf.Clamp(Mathf.PerlinNoise(noiseOrigin.y + world.x, noiseOrigin.x + world.y) * 2f - 1f, -1f, 1f);
        }

        /// <summary>
        /// Русла прокладываются блужданием по рёбрам: река течёт не по плиткам, а между ними.
        /// Шаг — переход на соседнее ребро через общую вершину; из четырёх соседних рёбер
        /// выбирается то, чья середина ниже, поэтому русло уходит от гор вниз к краю поля.
        /// </summary>
        static Dictionary<HexCoord, int> CarveRivers(
            IReadOnlyDictionary<HexCoord, BiomeType> biomes, int rows, Vector2 noiseOrigin)
        {
            var rivers = new Dictionary<HexCoord, int>();
            var sources = new List<HexCoord>();
            foreach (var pair in biomes)
                if (pair.Value == BiomeType.Mountains)
                    sources.Add(pair.Key);

            if (sources.Count == 0)
                return rivers;

            sources.Sort((a, b) => Rank(a).CompareTo(Rank(b)));

            var taken = new HashSet<(HexCoord Tile, int Direction)>();
            for (var i = 0; i < RiverCount && i < sources.Count; i++)
                Carve(sources[i * sources.Count / RiverCount], biomes, rivers, taken);

            return rivers;

            // Истоки разводятся по полю: подряд идущие горы дали бы три русла в одном месте.
            int Rank(HexCoord coord) => Mathf.RoundToInt(coord.Hash01(41) * 100000);
        }

        static void Carve(
            HexCoord source,
            IReadOnlyDictionary<HexCoord, BiomeType> biomes,
            Dictionary<HexCoord, int> rivers,
            HashSet<(HexCoord Tile, int Direction)> taken)
        {
            var edge = (Tile: source, Direction: (int)(source.Hash01(43) * 6f));
            var previous = (Tile: HexCoord.Zero, Direction: -1);

            for (var step = 0; step < RiverLength; step++)
            {
                if (!Mark(edge, biomes, rivers, taken))
                    return;

                var best = (Tile: HexCoord.Zero, Direction: -1);
                var bestHeight = float.MaxValue;

                foreach (var candidate in NeighborEdges(edge))
                {
                    if (Same(candidate, previous) || taken.Contains(Canonical(candidate)))
                        continue;

                    var height = EdgeMidpoint(candidate).y;
                    if (height >= bestHeight)
                        continue;

                    best = candidate;
                    bestHeight = height;
                }

                if (best.Direction < 0)
                    return;

                previous = edge;
                edge = best;
            }
        }

        /// <summary>Взводит бит на обеих плитках ребра. Возвращает false, если ребро уже вне поля.</summary>
        static bool Mark(
            (HexCoord Tile, int Direction) edge,
            IReadOnlyDictionary<HexCoord, BiomeType> biomes,
            Dictionary<HexCoord, int> rivers,
            HashSet<(HexCoord Tile, int Direction)> taken)
        {
            var other = edge.Tile.Neighbor(edge.Direction);
            var hasNear = biomes.ContainsKey(edge.Tile);
            var hasFar = biomes.ContainsKey(other);

            if (!hasNear && !hasFar)
                return false;

            taken.Add(Canonical(edge));

            if (hasNear)
                rivers[edge.Tile] = rivers.GetValueOrDefault(edge.Tile) | (1 << edge.Direction);
            if (hasFar)
                rivers[other] = rivers.GetValueOrDefault(other) | (1 << Opposite(edge.Direction));

            return true;
        }

        /// <summary>
        /// Четыре ребра, делящие вершину с этим: два поворотом вокруг своей плитки и два вокруг
        /// соседней. В каждой вершине гекс-сетки сходятся ровно три ребра, у ребра две вершины.
        /// </summary>
        static IEnumerable<(HexCoord Tile, int Direction)> NeighborEdges((HexCoord Tile, int Direction) edge)
        {
            var far = edge.Tile.Neighbor(edge.Direction);
            yield return (edge.Tile, (edge.Direction + 1) % 6);
            yield return (edge.Tile, (edge.Direction + 5) % 6);
            yield return (far, (edge.Direction + 2) % 6);
            yield return (far, (edge.Direction + 4) % 6);
        }

        static Vector2 EdgeMidpoint((HexCoord Tile, int Direction) edge) =>
            (edge.Tile.ToWorld() + edge.Tile.Neighbor(edge.Direction).ToWorld()) * 0.5f;

        static int Opposite(int direction) => (direction + 3) % 6;

        /// <summary>У ребра два представления — берём то, где плитка «меньше».</summary>
        static (HexCoord Tile, int Direction) Canonical((HexCoord Tile, int Direction) edge)
        {
            var other = edge.Tile.Neighbor(edge.Direction);
            if (edge.Tile.Q < other.Q || (edge.Tile.Q == other.Q && edge.Tile.R < other.R))
                return edge;

            return (other, Opposite(edge.Direction));
        }

        static bool Same((HexCoord Tile, int Direction) a, (HexCoord Tile, int Direction) b) =>
            b.Direction >= 0 && Canonical(a) == Canonical(b);

        /// <summary>Доля поля, до которой можно дойти от Метрополии по проходимым плиткам.</summary>
        static float PassableShare(HexMap map)
        {
            var visited = new HashSet<HexCoord> { HexCoord.Zero };
            var frontier = new Queue<HexCoord>();
            frontier.Enqueue(HexCoord.Zero);

            while (frontier.Count > 0)
                foreach (var neighbor in map.NeighborsOf(frontier.Dequeue()))
                    if (neighbor.IsPassable && visited.Add(neighbor.Coord))
                        frontier.Enqueue(neighbor.Coord);

            return visited.Count / (float)map.Count;
        }

        /// <summary>Гарантированный камень бесполезен, если до него не дойти.</summary>
        static bool StoneGuaranteeIsReachable(HexMap map)
        {
            foreach (var neighbor in map.NeighborsOf(HexCoord.Zero))
            {
                if (!neighbor.IsPassable)
                    continue;

                foreach (var deposit in neighbor.Deposits)
                    if (deposit.Type == ResourceType.Stone)
                        return true;
            }

            return false;
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

        /// <summary>
        /// Без камня рядом с Метрополией партия не стартует экономически. Гора в соседях
        /// не годится: месторождений она не несёт и дорогу не примет.
        /// </summary>
        static void EnsureStoneNextToMetropolis(
            IReadOnlyDictionary<HexCoord, List<Deposit>> depositsByCoord,
            IReadOnlyDictionary<HexCoord, BiomeType> biomes,
            System.Random random,
            MapGenerationSettings settings)
        {
            var neighbors = new List<List<Deposit>>();
            foreach (var coord in HexCoord.Zero.Neighbors())
            {
                if (!depositsByCoord.TryGetValue(coord, out var deposits))
                    continue;

                if (biomes[coord] == BiomeType.Mountains)
                    continue;

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
