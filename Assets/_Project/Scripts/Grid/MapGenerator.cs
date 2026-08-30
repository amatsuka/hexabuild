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
        /// Какую долю поля обязан покрывать обход по проходимым плиткам от Метрополии. Отрезанных
        /// кусков не бывает — их соединяют перевалы, — поэтому порог считает ровно одно: сколько
        /// поля заняли сами горы. Ниже — карта негодная, играть будет не во что.
        /// </summary>
        const float MinPassableShare = 0.85f;

        /// <summary>Попыток перегенерации с новым смещением шума, прежде чем взять что вышло.</summary>
        const int MaxAttempts = 20;

        /// <summary>Сколько русел кладём на карту и сколько истоков перебираем ради них.</summary>
        const int RiverCount = 2;
        const int MaxSourceTries = 6;

        /// <summary>
        /// Короче этого русло не считается рекой и откатывается: исток берётся следующий. Гора у
        /// самой кромки иначе даёт ручеёк в две плитки, который болтается наверху карты.
        /// </summary>
        const int MinRiverTiles = 6;

        /// <summary>
        /// Ворота на запуск нового русла, а не нож посреди поля: начатое русло всегда доходит
        /// до края. Плитка с рекой не несёт месторождений и стоит моста, поэтому их число —
        /// вопрос баланса, а не вкуса.
        /// </summary>
        const int RiverBudget = 6;

        /// <summary>Предохранитель от бесконечного блуждания: поле — четырнадцать рядов.</summary>
        const int MaxRiverSteps = 18;

        /// <summary>
        /// Насколько шаг вниз по карте дешевле шага вбок, в единицах высоты шума. Река течёт
        /// сверху вниз: без этого русло уходило в ближайшую кромку и на верхних рядах кончалось
        /// через пару плиток.
        /// </summary>
        const float DownPull = 0.09f;

        /// <summary>
        /// Надбавка за шаг на кромочную плитку: на ней русло заканчивается устьем, и без надбавки
        /// река сворачивала бы к ближайшему боку вместо того, чтобы идти вниз. Больше высоты
        /// целого биома брать нельзя — тогда река перестанет доходить до края вовсе.
        /// </summary>
        const float BorderPenalty = 0.06f;

        /// <summary>Ближе этого истоки не ставятся: два русла из одного пятна гор — не река, а лужа.</summary>
        const int MinSourceDistance = 2;

        /// <summary>
        /// Развилок на русло, шаг, с которого они разрешены, вероятность на шаге и поводок рукава.
        /// Поводок короче главного русла: рукав отходит уже на полпути вниз, и с полной длиной он
        /// читался бы как вторая река.
        /// </summary>
        const int MaxBranchesPerRiver = 1;
        const int MinStepsBeforeBranch = 2;
        const float BranchChance = 0.4f;
        const int MaxBranchSteps = 12;
        const int BranchSalt = 47;

        public static HexMap Generate(MapGenerationSettings settings)
        {
            var random = settings.Seed == 0 ? new System.Random() : new System.Random(settings.Seed);

            HexMap fallback = null;
            for (var attempt = 0; attempt < MaxAttempts; attempt++)
            {
                var map = Build(settings, random);
                fallback ??= map;

                if (PassableShare(map) >= MinPassableShare && StoneGuaranteeIsReachable(map) && HasProperRiver(map))
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

            // Метрополия — город, а не ландшафт: горой она быть не может по правилам 2.1.
            biomes[HexCoord.Zero] = BiomeType.Meadow;

            // Перевалы пробиваются до рек: исток обязан остаться горой, а гора на пути перевала
            // становится скалой.
            OpenPassages(biomes);

            var rivers = CarveRivers(biomes, settings, noiseOrigin);

            // Река идёт по поверхности плитки, как дорога: месторождению на ней уже не место.
            var depositsByCoord = new Dictionary<HexCoord, List<Deposit>>();
            foreach (var pair in biomes)
                if (pair.Key != HexCoord.Zero)
                    depositsByCoord[pair.Key] = pair.Value == BiomeType.Mountains || rivers.ContainsKey(pair.Key)
                        ? new List<Deposit>()
                        : RollDeposits(random, settings);

            EnsureStoneNextToMetropolis(depositsByCoord, biomes, rivers, random, settings);

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
        /// связными пятнами. Значение шума читается как высота: песок внизу, горы наверху.
        /// </summary>
        static BiomeType RollBiome(HexCoord coord, MapGenerationSettings settings, Vector2 noiseOrigin)
        {
            var height = Height(coord, settings.BiomeNoiseScale, noiseOrigin);

            // Пороги подобраны на глаз: песок занимает низины, горы — вершины, между ними суша.
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
        /// Пробивает перевалы в грядах, пока поле не станет проходимым целиком. Гряда, замкнувшая
        /// кусок поля, — не препятствие, а вычеркнутая из партии территория: открыть её нельзя
        /// никогда, а цель «пройти всё поле» становится недостижимой. Горы на пути от Метрополии
        /// до отрезанного куска становятся скалами — узкий проход, обойти который всё ещё стоит
        /// шагов, но который существует.
        /// </summary>
        static void OpenPassages(Dictionary<HexCoord, BiomeType> biomes)
        {
            while (true)
            {
                var reachable = PassableComponent(biomes, HexCoord.Zero);
                var cutOff = FirstCutOff(biomes, reachable);
                if (cutOff == null)
                    return;

                CarvePassage(biomes, reachable, cutOff.Value);
            }
        }

        /// <summary>Проходимые плитки, до которых можно дойти от заданной по проходимым же.</summary>
        static HashSet<HexCoord> PassableComponent(IReadOnlyDictionary<HexCoord, BiomeType> biomes, HexCoord root)
        {
            var visited = new HashSet<HexCoord> { root };
            var frontier = new Queue<HexCoord>();
            frontier.Enqueue(root);

            while (frontier.Count > 0)
            {
                var current = frontier.Dequeue();
                for (var direction = 0; direction < HexCoord.Directions.Count; direction++)
                {
                    var neighbor = current.Neighbor(direction);
                    if (!biomes.TryGetValue(neighbor, out var biome) || biome == BiomeType.Mountains)
                        continue;

                    if (visited.Add(neighbor))
                        frontier.Enqueue(neighbor);
                }
            }

            return visited;
        }

        /// <summary>Первая проходимая плитка, отрезанная от Метрополии, или null, если таких нет.</summary>
        static HexCoord? FirstCutOff(IReadOnlyDictionary<HexCoord, BiomeType> biomes, HashSet<HexCoord> reachable)
        {
            foreach (var pair in biomes)
                if (pair.Value != BiomeType.Mountains && !reachable.Contains(pair.Key))
                    return pair.Key;

            return null;
        }

        /// <summary>
        /// Самый дешёвый путь до отрезанной плитки: шаг по проходимой стоит ноль, по горе — один,
        /// поэтому обход гряды выигрывает у пролома, пока обход вообще существует. Горы на найденном
        /// пути становятся скалами. Очередь с двумя концами: нулевой шаг идёт в голову, платный — в
        /// хвост, так дешёвые пути разбираются раньше дорогих.
        /// </summary>
        static void CarvePassage(
            Dictionary<HexCoord, BiomeType> biomes,
            HashSet<HexCoord> reachable,
            HexCoord cutOff)
        {
            var cost = new Dictionary<HexCoord, int>();
            var cameFrom = new Dictionary<HexCoord, HexCoord>();
            var frontier = new LinkedList<HexCoord>();

            foreach (var coord in reachable)
            {
                cost[coord] = 0;
                frontier.AddLast(coord);
            }

            while (frontier.Count > 0)
            {
                var current = frontier.First.Value;
                frontier.RemoveFirst();

                if (current == cutOff)
                    break;

                for (var direction = 0; direction < HexCoord.Directions.Count; direction++)
                {
                    var neighbor = current.Neighbor(direction);
                    if (!biomes.TryGetValue(neighbor, out var biome))
                        continue;

                    var step = cost[current] + (biome == BiomeType.Mountains ? 1 : 0);
                    if (cost.TryGetValue(neighbor, out var known) && known <= step)
                        continue;

                    cost[neighbor] = step;
                    cameFrom[neighbor] = current;

                    if (step == cost[current])
                        frontier.AddFirst(neighbor);
                    else
                        frontier.AddLast(neighbor);
                }
            }

            for (var coord = cutOff; cameFrom.ContainsKey(coord); coord = cameFrom[coord])
                if (biomes[coord] == BiomeType.Mountains)
                    biomes[coord] = BiomeType.Rocks;
        }

        /// <summary>
        /// Русло идёт по плиткам, как дорога: `RiverMask` держит грани, к которым лента выходит
        /// из центра. Исток — гора повыше на карте, шаг — переход на соседа ниже по склону и ниже
        /// по карте, конец — кромка поля.
        /// </summary>
        static Dictionary<HexCoord, int> CarveRivers(
            IReadOnlyDictionary<HexCoord, BiomeType> biomes,
            MapGenerationSettings settings,
            Vector2 noiseOrigin)
        {
            var rivers = new Dictionary<HexCoord, int>();
            var terrain = new RiverTerrain(biomes, settings, noiseOrigin);
            var candidates = new List<HexCoord>();

            // Гора на кромке истоком не годится: русло с неё сразу упрётся в край поля.
            foreach (var pair in biomes)
                if (pair.Value == BiomeType.Mountains && !terrain.IsBorder(pair.Key))
                    candidates.Add(pair.Key);

            if (candidates.Count == 0)
                return rivers;

            // Исток тем лучше, чем выше он на карте и чем дальше от боковых кромок: реке остаётся
            // длинный путь вниз, а не пара плиток до ближайшего края. Хеш разводит равные горы.
            candidates.Sort((a, b) => Promise(b).CompareTo(Promise(a)));

            var sources = new List<HexCoord>();
            foreach (var candidate in candidates)
            {
                if (sources.Count >= MaxSourceTries)
                    break;

                // Истоки разводятся по полю: соседние горы дали бы два русла в одном месте.
                var crowded = false;
                foreach (var source in sources)
                    crowded |= HexCoord.Distance(source, candidate) < MinSourceDistance;

                if (!crowded)
                    sources.Add(candidate);
            }

            // Русло прокладывается в копию и принимается, только если вышло достаточно длинным.
            // Короткое откатывается вместе с истоком: гора у кромки реки не даёт, даже если она
            // самая высокая на карте. Если длинного не вышло ни из одного истока, берём лучшее
            // из коротких — карта без реки хуже карты с ручьём.
            Dictionary<HexCoord, int> longest = null;
            var accepted = 0;

            foreach (var source in sources)
            {
                if (accepted >= RiverCount || rivers.Count >= RiverBudget)
                    break;

                var attempt = new Dictionary<HexCoord, int>(rivers);
                Carve(source, terrain, attempt);

                if (attempt.Count - rivers.Count >= MinRiverTiles)
                {
                    rivers = attempt;
                    accepted++;
                    continue;
                }

                if (longest == null || attempt.Count > longest.Count)
                    longest = attempt;
            }

            return rivers.Count > 0 ? rivers : longest ?? rivers;

            // Ряд весит меньше отдалённости от кромки: с верхнего ряда у самого бока русло
            // уходит за карту первым же шагом.
            float Promise(HexCoord coord) => coord.R + 2f * terrain.Inland(coord) + coord.Hash01(41);
        }

        /// <summary>
        /// Одно русло со всеми рукавами. Ходок спускается по склону, пока не выйдет к кромке поля;
        /// развилка отправляет второго ходока в следующего по низине соседа. Русло, вошедшее в уже
        /// проложенное, на этом заканчивается — это слияние, а не тупик.
        /// </summary>
        static void Carve(HexCoord source, RiverTerrain terrain, Dictionary<HexCoord, int> rivers)
        {
            var walkers = new Queue<(HexCoord Tile, HexCoord From, int Steps)>();
            var picks = new List<HexCoord>(2);
            var branches = MaxBranchesPerRiver;

            walkers.Enqueue((source, source, MaxRiverSteps));

            while (walkers.Count > 0)
            {
                var (tile, from, steps) = walkers.Dequeue();

                for (var step = 0; step < steps; step++)
                {
                    // Кромка — только устье. Дальше течь некуда, а идти вдоль края река не должна.
                    if (terrain.IsBorder(tile))
                    {
                        OpenMouth(tile, from, terrain, rivers);
                        break;
                    }

                    Downhill(tile, from, terrain, rivers, picks);
                    if (picks.Count == 0)
                        break;

                    var next = picks[0];
                    var merges = rivers.ContainsKey(next);
                    Link(tile, next, rivers);

                    if (!merges && branches > 0 && step >= MinStepsBeforeBranch && picks.Count > 1
                        && tile.Hash01(BranchSalt) < BranchChance)
                    {
                        branches--;
                        Link(tile, picks[1], rivers);
                        walkers.Enqueue((picks[1], tile, MaxBranchSteps));
                    }

                    if (merges)
                        break;

                    from = tile;
                    tile = next;
                }
            }
        }

        /// <summary>
        /// Самый низкий сосед и следующий за ним — кандидат под развилку. Метрополия и плитка,
        /// откуда пришли, исключены; уже проложенное русло берётся только тогда, когда свободных
        /// соседей не осталось вовсе.
        /// </summary>
        static void Downhill(
            HexCoord tile,
            HexCoord from,
            RiverTerrain terrain,
            IReadOnlyDictionary<HexCoord, int> rivers,
            List<HexCoord> picks)
        {
            picks.Clear();

            HexCoord best = default, second = default, confluence = default;
            var bestScore = float.MaxValue;
            var secondScore = float.MaxValue;
            var confluenceScore = float.MaxValue;

            for (var direction = 0; direction < HexCoord.Directions.Count; direction++)
            {
                var neighbor = tile.Neighbor(direction);
                if (neighbor == from || neighbor == HexCoord.Zero || !terrain.Contains(neighbor))
                    continue;

                var score = terrain.Slope(tile, neighbor);

                if (rivers.ContainsKey(neighbor))
                {
                    if (score < confluenceScore)
                    {
                        confluence = neighbor;
                        confluenceScore = score;
                    }

                    continue;
                }

                if (score < bestScore)
                {
                    second = best;
                    secondScore = bestScore;
                    best = neighbor;
                    bestScore = score;
                }
                else if (score < secondScore)
                {
                    second = neighbor;
                    secondScore = score;
                }
            }

            if (bestScore < float.MaxValue)
                picks.Add(best);
            if (secondScore < float.MaxValue)
                picks.Add(second);
            if (picks.Count == 0 && confluenceScore < float.MaxValue)
                picks.Add(confluence);
        }

        /// <summary>Сшивает две плитки руслом: бит в сторону соседа и ответный бит у него.</summary>
        static void Link(HexCoord tile, HexCoord next, Dictionary<HexCoord, int> rivers)
        {
            for (var direction = 0; direction < HexCoord.Directions.Count; direction++)
            {
                if (tile.Neighbor(direction) != next)
                    continue;

                rivers[tile] = rivers.GetValueOrDefault(tile) | (1 << direction);
                rivers[next] = rivers.GetValueOrDefault(next) | (1 << Opposite(direction));
                return;
            }
        }

        /// <summary>
        /// Устье: лента уходит за границу поля по той грани, что ближе к направлению течения.
        /// Ответного бита у неё нет — соседа за краем не существует.
        /// </summary>
        static void OpenMouth(
            HexCoord tile,
            HexCoord from,
            RiverTerrain terrain,
            Dictionary<HexCoord, int> rivers)
        {
            var flow = tile.ToWorld() - from.ToWorld();
            if (flow.sqrMagnitude < 1e-6f)
                return;

            var mouth = -1;
            var bestDot = float.MinValue;

            for (var direction = 0; direction < HexCoord.Directions.Count; direction++)
            {
                if (terrain.Contains(tile.Neighbor(direction)))
                    continue;

                var dot = Vector2.Dot(flow.normalized, HexCoord.Directions[direction].ToWorld().normalized);
                if (dot <= bestDot)
                    continue;

                mouth = direction;
                bestDot = dot;
            }

            if (mouth >= 0)
                rivers[tile] = rivers.GetValueOrDefault(tile) | (1 << mouth);
        }

        static int Opposite(int direction) => (direction + 3) % 6;

        /// <summary>
        /// Ландшафт глазами реки: где кончается поле, как высоко лежит плитка и сколько шагов
        /// от неё до кромки. Считается один раз на карту — блуждание опрашивает это на каждом шаге.
        /// </summary>
        readonly struct RiverTerrain
        {
            readonly IReadOnlyDictionary<HexCoord, BiomeType> biomes;
            readonly Dictionary<HexCoord, int> inland;
            readonly float noiseScale;
            readonly Vector2 noiseOrigin;

            public RiverTerrain(
                IReadOnlyDictionary<HexCoord, BiomeType> biomes,
                MapGenerationSettings settings,
                Vector2 noiseOrigin)
            {
                this.biomes = biomes;
                this.noiseOrigin = noiseOrigin;
                noiseScale = settings.BiomeNoiseScale;
                inland = MeasureInland(biomes);
            }

            public bool Contains(HexCoord coord) => biomes.ContainsKey(coord);

            /// <summary>Плитка на кромке поля: хотя бы один сосед лежит за его границей.</summary>
            public bool IsBorder(HexCoord coord) => inland[coord] == 0;

            /// <summary>Шагов от плитки до ближайшей кромки поля.</summary>
            public int Inland(HexCoord coord) => inland[coord];

            /// <summary>
            /// Во что обходится шаг: высота соседа, скидка за движение вниз по карте и надбавка за
            /// выход на кромку. Высота оставлена главным слагаемым — она и даёт извилистость,
            /// а два поправочных члена только не дают руслу свернуть вверх или в ближайший бок.
            /// </summary>
            public float Slope(HexCoord tile, HexCoord neighbor)
            {
                var world = neighbor.ToWorld() * noiseScale;
                var height = Mathf.PerlinNoise(noiseOrigin.x + world.x, noiseOrigin.y + world.y);

                return height - DownPull * (tile.R - neighbor.R) + (IsBorder(neighbor) ? BorderPenalty : 0f);
            }

            /// <summary>Шаги до кромки: волна от края поля внутрь.</summary>
            static Dictionary<HexCoord, int> MeasureInland(IReadOnlyDictionary<HexCoord, BiomeType> biomes)
            {
                var distances = new Dictionary<HexCoord, int>(biomes.Count);
                var frontier = new Queue<HexCoord>();

                foreach (var pair in biomes)
                {
                    var onBorder = false;
                    for (var direction = 0; direction < HexCoord.Directions.Count; direction++)
                        onBorder |= !biomes.ContainsKey(pair.Key.Neighbor(direction));

                    if (!onBorder)
                        continue;

                    distances[pair.Key] = 0;
                    frontier.Enqueue(pair.Key);
                }

                while (frontier.Count > 0)
                {
                    var current = frontier.Dequeue();
                    for (var direction = 0; direction < HexCoord.Directions.Count; direction++)
                    {
                        var neighbor = current.Neighbor(direction);
                        if (!biomes.ContainsKey(neighbor) || distances.ContainsKey(neighbor))
                            continue;

                        distances[neighbor] = distances[current] + 1;
                        frontier.Enqueue(neighbor);
                    }
                }

                return distances;
            }
        }

        /// <summary>Доля поля, до которой можно дойти от Метрополии по проходимым плиткам.</summary>
        static float PassableShare(HexMap map) => map.ReachableFromMetropolis().Count / (float)map.Count;

        /// <summary>
        /// На карте есть река, и она не ручеёк. Горы этим гарантируются заодно: русло берёт начало
        /// только в них. Карта, где все горы жмутся к кромке и русло из любой из них уходит за край
        /// через пару плиток, перегенерируется — река должна пересекать поле, а не болтаться в углу.
        /// </summary>
        static bool HasProperRiver(HexMap map)
        {
            var riverTiles = 0;
            foreach (var tile in map.Tiles.Values)
                if (tile.HasRiver)
                    riverTiles++;

            return riverTiles >= MinRiverTiles;
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
        /// не годится: месторождений она не несёт и дорогу не примет. Плитка с рекой — тоже:
        /// месторождений на ней не бывает вовсе.
        /// </summary>
        static void EnsureStoneNextToMetropolis(
            IReadOnlyDictionary<HexCoord, List<Deposit>> depositsByCoord,
            IReadOnlyDictionary<HexCoord, BiomeType> biomes,
            IReadOnlyDictionary<HexCoord, int> rivers,
            System.Random random,
            MapGenerationSettings settings)
        {
            var neighbors = new List<List<Deposit>>();
            foreach (var coord in HexCoord.Zero.Neighbors())
            {
                if (!depositsByCoord.TryGetValue(coord, out var deposits))
                    continue;

                if (biomes[coord] == BiomeType.Mountains || rivers.ContainsKey(coord))
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
