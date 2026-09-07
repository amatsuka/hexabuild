using System;
using System.Collections.Generic;
using System.Diagnostics;
using Game.Economy;
using Game.Grid;
using Game.Merge;
using Game.Storage;

namespace Game.Core.Balance
{
    /// <summary>
    /// Партия без сцены: те же системы, что собирает `GameSession`, тем же порядком тиков и
    /// подписок, но без единого вью. Клей между системами повторён отсюда же — путь доставки от
    /// плитки к Метрополии, укладка приехавшего на склад, зачёт обмена в контракт, — потому что
    /// в игре он живёт в `GameSession`, а не в правилах.
    ///
    /// Играет жадная детерминированная политика: обмен → слияние → дорога → открытие, один вызов
    /// принимает все доступные действия по порядку. О карте бот знает ровно столько, сколько
    /// игрок: запас закрытой плитки не читает. Одна партия на экземпляр.
    /// </summary>
    public sealed class BalanceBot
    {
        /// <summary>Шаг симуляции. Грубее кадра, зато 200 сидов идут за секунды.</summary>
        public const float StepSeconds = 0.25f;

        /// <summary>Предел партии: дефект правил не должен подвесить редактор.</summary>
        public const float DefaultMaxSeconds = 3600f;

        /// <summary>
        /// Потолок действий бота в секунду. Без него бот кликал как машина — на уровнях 8–10
        /// выходило 8.6–10.5 действия в секунду, — и любой временнóй рычаг он выбирал на
        /// максимум, которого живой руке не достать. Пять — середина человеческого диапазона
        /// 4–6, замеренного по темпу M28; число решением человека 07.09.2026.
        /// </summary>
        public const float MaxActionsPerSecond = 5f;

        /// <summary>
        /// Тройка мержится раньше пятёрки только когда склад заполнен больше, чем на эту долю,
        /// либо тип нужен контракту или дороге; иначе бот ждёт пятёрку — она на единицу дороже.
        /// </summary>
        public const float MergeFillThreshold = 0.7f;

        readonly GameConfig config;
        readonly MergeRules rules;
        readonly int seed;

        /// <summary>Уровень кампании, чьи рычаги легли поверх дефолтов. Пусто — партия вне кампании.</summary>
        readonly LevelConfig level;

        readonly List<ResourceType> craftedTypes;
        readonly List<ResourceType> baseTypes = new();
        readonly List<HexCoord> path = new();

        // Обход поля. Буферы общие на все поиски: партия идёт тысячами шагов.
        readonly Queue<HexCoord> frontier = new();
        readonly Dictionary<HexCoord, HexCoord> parents = new();
        readonly List<HexCoord> sources = new();

        GameState state;
        ProductionSystem production;
        DeliverySystem deliveries;
        MergeSystem merges;
        ContractSystem contracts;
        GameEndSystem end;

        /// <summary>С последнего решения что-то поменялось: есть смысл решать заново.</summary>
        bool dirty;

        /// <summary>
        /// Сколько действий бот может сделать прямо сейчас. Копится по <see cref="MaxActionsPerSecond"/>
        /// в секунду и не превышает секундного запаса: человек тоже играет очередями, а не ровным
        /// метрономом, но за секунду успевает не больше своей скорости.
        /// </summary>
        float actions;

        /// <summary>Решению не хватило действий: доиграет на следующем шаге, а не когда что-то изменится.</summary>
        bool starved;

        bool played;
        int contractsFailed;

        /// <summary>Очки, пришедшие наградами контрактов: их доля в заработке — число стадии.</summary>
        int contractPoints;

        /// <summary>Очки, пришедшие премиями за чистый склад: ими калибруется база премии.</summary>
        int sweepPoints;

        public BalanceBot(GameConfig config, MergeRules rules, int seed, LevelConfig level = null)
        {
            if (seed == 0)
                throw new ArgumentException("сид 0 значит «случайный»: замер обязан быть воспроизводим", nameof(seed));

            this.config = config;
            this.rules = rules;
            this.seed = seed;
            this.level = level;
            craftedTypes = rules.CraftedTypes();

            foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
                if (rules.CanMerge(type))
                    baseTypes.Add(type);
        }

        /// <summary>Состояние партии после `Play`: тестам нужно заглянуть на поле.</summary>
        public GameState State => state;

        /// <summary>
        /// Сколько раз правила отказали боту. Бот проверяет всё до действия, поэтому любой отказ —
        /// его дефект, а не ход партии.
        /// </summary>
        public int Refusals { get; private set; }

        public BalanceRun Play(float maxSeconds = DefaultMaxSeconds)
        {
            if (played)
                throw new InvalidOperationException("одна партия на экземпляр: собери нового бота");

            played = true;
            var watch = Stopwatch.StartNew();

            var map = MapGenerator.Generate(config.MapGenerationSettingsFor(seed, level));
            var ceiling = BalanceCeiling.Of(map, rules, config.Prices);
            Assemble(map);

            var seconds = 0f;
            while (!end.HasEnded && seconds < maxSeconds)
            {
                production.Tick(StepSeconds);
                deliveries.Tick(StepSeconds);
                contracts.Tick(StepSeconds);
                state.Multiplier.Tick(StepSeconds);
                end.Tick();
                seconds += StepSeconds;

                actions = Math.Min(actions + MaxActionsPerSecond * StepSeconds, MaxActionsPerSecond);

                if (end.HasEnded)
                    continue;

                // Пройденное поле доигрывает склад само — так же, как `GameSession`. Без этого
                // бот виснет на придержанном щебне до предела времени: `GameEndSystem` считает
                // щебень обмениваемым и конца не объявляет, а `Decide` его бережёт под дорогу,
                // которой уже некуда идти. На счёт это не влияло — в хвосте не зарабатывается
                // ничего, — но `Seconds` уезжали в разы, и длину партии по ним читать было нельзя.
                // Доигрывание тоже стоит руки: иначе хвост партии шёл бы вчетверо быстрее,
                // чем игрок способен доклацать те же клетки.
                if (end.FieldPassed && actions >= 1f && merges.TryPlayOut())
                    actions -= 1f;

                Decide();
            }

            watch.Stop();
            var score = end.HasEnded ? end.Score : end.BuildScore();

            return new BalanceRun(
                seed,
                score,
                ceiling,
                contracts.CompletedCount,
                contractsFailed,
                contractPoints,
                sweepPoints,
                seconds,
                end.HasEnded,
                watch.ElapsedMilliseconds);
        }

        /// <summary>Сборка партии — `GameSession.Awake` и `Start` без вью.</summary>
        void Assemble(HexMap map)
        {
            var wallet = new Wallet(config.StartingPoints);
            var storage = new StorageGrid(config.StorageSize);
            var multiplier = config.NewMultiplier();
            state = new GameState(map, wallet, storage, config.Prices, multiplier);

            production = new ProductionSystem(map, state.Roads, config.ExtractionIntervalFor(level));
            deliveries = new DeliverySystem(config.DeliverySecondsPerTile);
            merges = new MergeSystem(storage, wallet, rules, multiplier, config.SweepCells, config.SweepBonus);
            contracts = new ContractSystem(
                wallet,
                craftedTypes,
                config.ContractGoalFor(level),
                config.ContractSecondsFor(level),
                config.ContractReward,
                config.ContractPauseMin,
                config.ContractPauseMax,
                seed,
                multiplier);
            end = new GameEndSystem(
                state, rules, deliveries, config.LossPenalty, config.FullFieldBonus, config.FullDepositBonus);

            state.ActionRefused += OnRefused;
            merges.Refused += OnRefused;
            state.TileChanged += _ => dirty = true;
            state.Roads.Changed += () => dirty = true;
            wallet.Changed += () => dirty = true;
            storage.Changed += () => dirty = true;
            production.Produced += OnProduced;
            deliveries.Arrived += OnDeliveryArrived;
            merges.Converted += OnConverted;
            merges.Swept += (_, points) => sweepPoints += points;
            contracts.Issued += () => dirty = true;
            contracts.Failed += () => contractsFailed++;
            contracts.Completed += reward => contractPoints += reward;

            state.Begin();
            for (var i = 0; i < config.StartingGravel; i++)
                storage.TryStore(ResourceType.Gravel);

            contracts.Issue();
            dirty = true;
        }

        void OnRefused(string _) => Refusals++;

        /// <summary>Добытое едет по дороге, которую плитка нашла в момент подключения.</summary>
        void OnProduced(TileData tile, ResourceType type)
        {
            if (state.Roads.TryFindPathToMetropolis(tile.Coord, path))
                deliveries.Send(type, new List<HexCoord>(path));
        }

        /// <summary>Клетка занимается сразу по прибытии; лишнее склад теряет сам.</summary>
        void OnDeliveryArrived(Delivery delivery)
        {
            state.Storage.TryStore(delivery.Type);
            dirty = true;
        }

        void OnConverted(int cell, ResourceType type, int points)
        {
            contracts.Count(type);
            dirty = true;
        }

        void Decide()
        {
            if (!dirty)
                return;

            dirty = false;
            starved = false;
            Exchange();
            MergeStep();
            BuildRoads();
            OpenTiles();

            // Решение оборвалось на пустом кошельке действий, а не на исчерпанном списке дел:
            // без этого бот ждал бы следующего изменения состояния и терял бы ход.
            if (starved)
                dirty = true;
        }

        /// <summary>Занять одно действие из секундного запаса. `false` — рука занята, ждём шага.</summary>
        bool TrySpend()
        {
            if (actions < 1f)
            {
                starved = true;
                return false;
            }

            actions -= 1f;
            return true;
        }

        // --- 1. Обмен ---

        /// <summary>
        /// Крафт меняется на очки сразу, тип активного контракта — первым. Щебень придерживается,
        /// пока он полезен: есть неподключённая плитка с запасом или очков хватает открыть ещё
        /// одну (на старте партии открытых плиток нет, а стартовый щебень — это первая дорога).
        /// Обменивается он, когда строить не для чего, либо когда на следующий шаг его не
        /// хватает и прийти ему неоткуда — иначе партия висела бы на нём вечно: `GameEndSystem`
        /// считает щебень обмениваемым ресурсом и конца не объявит.
        /// </summary>
        void Exchange()
        {
            var holdGravel = GravelIsUseful();

            if (contracts.IsActive)
                ConvertAll(contracts.Type, holdGravel);

            foreach (var type in craftedTypes)
                ConvertAll(type, holdGravel);
        }

        bool GravelIsUseful()
        {
            if (!TryNextRoadStep(out var step, out var towardStone))
                return CanAffordOpening();

            var gravel = state.Storage.CountOf(ResourceType.Gravel);
            var price = state.Map.TryGetTile(step, out var tile) ? state.RoadPrice(tile) : int.MaxValue;
            return gravel >= price + GravelReserve(towardStone) || GravelCanStillCome();
        }

        /// <summary>Очков хватает на следующую плитку и есть что открыть.</summary>
        bool CanAffordOpening()
        {
            if (state.Wallet.Points < state.NextTileCost)
                return false;

            foreach (var tile in state.Map.Tiles.Values)
                if (tile.State == TileState.Available && tile.IsPassable)
                    return true;

            return false;
        }

        void ConvertAll(ResourceType type, bool holdGravel)
        {
            if (type == ResourceType.Gravel && holdGravel)
                return;

            var storage = state.Storage;
            for (var cell = 0; cell < storage.Capacity; cell++)
                if (storage[cell] == type)
                {
                    if (!TrySpend())
                        return;

                    merges.TryConvert(cell);
                }
        }

        /// <summary>Щебень ещё придёт: работает подключённая плитка, ресурс в пути или камень на складе.</summary>
        bool GravelCanStillCome()
        {
            if (deliveries.Active.Count > 0 || HasProducingTile())
                return true;

            foreach (var type in baseTypes)
                if (rules.TryResolve(type, state.Storage.CountOf(type), out var outcome)
                    && outcome.Result == ResourceType.Gravel)
                    return true;

            return false;
        }

        bool HasProducingTile()
        {
            foreach (var coord in state.Roads.Roads)
                if (state.Roads.IsConnected(coord)
                    && state.Map.TryGetTile(coord, out var tile)
                    && tile.State == TileState.Revealed
                    && !tile.IsExhausted)
                    return true;

            return false;
        }

        // --- 2. Слияние ---

        /// <summary>
        /// Пятёрка мержится всегда. Тройка — только когда ждать пятёрку дороже: склад полон
        /// больше чем на порог, крафт нужен контракту или это щебень, которого не хватает на
        /// следующий шаг дороги.
        /// </summary>
        void MergeStep()
        {
            var storage = state.Storage;

            foreach (var type in baseTypes)
                while (rules.TryResolve(type, storage.CountOf(type), out var outcome))
                {
                    var large = outcome.Consumed >= rules.LargeCount;
                    var fill = storage.Count / (float)storage.Capacity;
                    var needed = fill > MergeFillThreshold
                        || (contracts.IsActive && contracts.Type == outcome.Result)
                        || (outcome.Result == ResourceType.Gravel && GravelIsShortForRoad());

                    if (!large && !needed)
                        break;

                    if (!TrySpend() || !merges.TryMerge(type))
                        break;
                }
        }

        bool GravelIsShortForRoad() =>
            TryNextRoadStep(out var coord, out var towardStone)
            && state.Map.TryGetTile(coord, out var tile)
            && state.Storage.CountOf(ResourceType.Gravel) < state.RoadPrice(tile) + GravelReserve(towardStone);

        // --- 3. Дорога ---

        /// <summary>Следующий шаг кратчайшего пути от сети к ближайшей плитке с запасом, пока хватает щебня.</summary>
        void BuildRoads()
        {
            while (TryNextRoadStep(out var coord, out var towardStone))
            {
                if (!state.Map.TryGetTile(coord, out var tile)
                    || state.Storage.CountOf(ResourceType.Gravel) < state.RoadPrice(tile) + GravelReserve(towardStone)
                    || !TrySpend()
                    || !state.TryBuildRoad(coord))
                    break;
            }
        }

        /// <summary>
        /// Сколько щебня не тратить на чужую плитку. Пока сеть не даёт камня, один щебень лежит
        /// под будущую плитку с камнем: камень генератор кладёт рядом с Метрополией, то есть в
        /// одной дороге от неё, а стартовый щебень иначе уходил на первые попавшиеся лес и руду —
        /// и камень, открытый следом, стоял с пустым складом до конца партии. К самому камню
        /// дорога идёт без резерва. Резерв на всю партию пробовался и дал хуже: тупиков 81
        /// против 75 на 200 сидах, пройденных полей 119 против 125 — придержанный щебень
        /// тормозит сеть сильнее, чем спасает.
        /// </summary>
        int GravelReserve(bool towardStone) => towardStone || NetworkHasStone() ? 0 : config.Prices.Road;

        /// <summary>Открытая плитка с остатком запаса, до которой дорога ещё не дошла.</summary>
        bool HasUnconnectedReserveTile()
        {
            foreach (var tile in state.Map.Tiles.Values)
                if (IsRoadTarget(tile))
                    return true;

            return false;
        }

        bool IsRoadTarget(TileData tile) =>
            tile.State == TileState.Revealed
            && !tile.IsMetropolis
            && tile.Deposits.Count > 0
            && !tile.IsExhausted
            && !state.Roads.HasRoad(tile.Coord);

        /// <summary>В сети есть работающая плитка с камнем: щебень придёт сам.</summary>
        bool NetworkHasStone()
        {
            foreach (var coord in state.Roads.Roads)
                if (state.Roads.IsConnected(coord)
                    && state.Map.TryGetTile(coord, out var tile)
                    && tile.State == TileState.Revealed
                    && HasLiveStone(tile))
                    return true;

            return false;
        }

        /// <summary>
        /// Щебень — единственное, из чего растёт сеть, и берётся он только из камня. Пока в сети
        /// нет работающей плитки с камнем, дорога идёт к ближайшей открытой плитке с камнем,
        /// а не просто к ближайшему запасу: иначе щебень уходил на лес и руду, а камень оставался
        /// стоять открытым в двух шагах. Открытая плитка показывает свои месторождения и игроку.
        /// </summary>
        bool StoneComesFirst()
        {
            if (NetworkHasStone())
                return false;

            foreach (var tile in state.Map.Tiles.Values)
                if (IsRoadTarget(tile) && HasLiveStone(tile))
                    return true;

            return false;
        }

        static bool HasLiveStone(TileData tile)
        {
            foreach (var deposit in tile.Deposits)
                if (deposit.Type == ResourceType.Stone && !deposit.IsExhausted)
                    return true;

            return false;
        }

        /// <summary>
        /// BFS от сети дорог по открытым проходимым плиткам до ближайшей цели; отдаёт первую
        /// плитку пути — она смежна с сетью, и дорогу на неё правила примут. Цель может быть
        /// и первым шагом. Открытая плитка всегда достижима по открытым: закрытую открывают
        /// только рядом с открытой, поэтому путь есть, пока есть цель.
        /// </summary>
        bool TryNextRoadStep(out HexCoord step) => TryNextRoadStep(out step, out _);

        /// <summary><paramref name="towardStone"/> — путь ведёт к плитке с камнем, резерв щебня на него не распространяется.</summary>
        bool TryNextRoadStep(out HexCoord step, out bool towardStone)
        {
            step = default;
            towardStone = false;
            if (!HasUnconnectedReserveTile())
                return false;

            var stoneFirst = StoneComesFirst();
            SeedFromNetwork();

            while (frontier.Count > 0)
            {
                var current = frontier.Dequeue();

                for (var direction = 0; direction < HexCoord.Directions.Count; direction++)
                {
                    var next = current.Neighbor(direction);
                    if (parents.ContainsKey(next) || !state.Map.TryGetTile(next, out var tile))
                        continue;

                    if (!IsOpened(tile) || !tile.IsPassable || state.Roads.HasRoad(next))
                        continue;

                    parents[next] = current;
                    if (IsRoadTarget(tile) && (!stoneFirst || HasLiveStone(tile)))
                    {
                        step = FirstStep(next);
                        towardStone = HasLiveStone(tile);
                        return true;
                    }

                    frontier.Enqueue(next);
                }
            }

            return false;
        }

        // --- 4. Открытие ---

        /// <summary>
        /// Плитка фронтира, ближайшая к сети дорог по шагам через открытые; при равенстве —
        /// с меньшим числом соседей-стен, дальше по координате, чтобы выбор не зависел от
        /// порядка перебора.
        /// </summary>
        void OpenTiles()
        {
            while (state.Wallet.Points >= state.NextTileCost && TryPickTileToOpen(out var coord))
                if (!TrySpend() || !state.TryRevealTile(coord))
                    break;
        }

        bool TryPickTileToOpen(out HexCoord best)
        {
            best = default;
            var found = false;
            var bestDistance = int.MaxValue;
            var bestWalls = int.MaxValue;

            SeedFromNetwork();
            var distances = new Dictionary<HexCoord, int>();
            foreach (var source in sources)
                distances[source] = 0;

            while (frontier.Count > 0)
            {
                var current = frontier.Dequeue();
                var distance = distances[current] + 1;

                for (var direction = 0; direction < HexCoord.Directions.Count; direction++)
                {
                    var next = current.Neighbor(direction);
                    if (parents.ContainsKey(next) || !state.Map.TryGetTile(next, out var tile) || !tile.IsPassable)
                        continue;

                    parents[next] = current;
                    distances[next] = distance;

                    if (IsOpened(tile))
                    {
                        frontier.Enqueue(next);
                        continue;
                    }

                    if (tile.State != TileState.Available)
                        continue;

                    var walls = WallNeighbors(next);
                    if (!found
                        || distance < bestDistance
                        || (distance == bestDistance && walls < bestWalls)
                        || (distance == bestDistance && walls == bestWalls && Before(next, best)))
                    {
                        found = true;
                        best = next;
                        bestDistance = distance;
                        bestWalls = walls;
                    }
                }
            }

            return found;
        }

        int WallNeighbors(HexCoord coord)
        {
            var walls = 0;
            for (var direction = 0; direction < HexCoord.Directions.Count; direction++)
                if (state.Map.TryGetTile(coord.Neighbor(direction), out var tile) && !tile.IsPassable)
                    walls++;

            return walls;
        }

        // --- Обход ---

        /// <summary>
        /// Старт обхода: Метрополия и все подключённые дороги. Порядок — по координате, а не по
        /// порядку хеш-множества: у `HexCoord.GetHashCode` зерно на процесс, и два запуска иначе
        /// могли бы разойтись в выборе при равных расстояниях.
        /// </summary>
        void SeedFromNetwork()
        {
            frontier.Clear();
            parents.Clear();
            sources.Clear();

            sources.Add(HexCoord.Zero);
            foreach (var road in state.Roads.Roads)
                if (state.Roads.IsConnected(road))
                    sources.Add(road);

            sources.Sort(CompareCoords);

            foreach (var source in sources)
            {
                parents[source] = source;
                frontier.Enqueue(source);
            }
        }

        /// <summary>Первая плитка пути после источника: по родителям назад до самого источника.</summary>
        HexCoord FirstStep(HexCoord target)
        {
            var current = target;
            while (parents[parents[current]] != parents[current])
                current = parents[current];

            return current;
        }

        static bool IsOpened(TileData tile) => tile.State is TileState.Revealed or TileState.Depleted;

        static bool Before(HexCoord a, HexCoord b) => CompareCoords(a, b) < 0;

        static int CompareCoords(HexCoord a, HexCoord b) => a.R != b.R ? a.R.CompareTo(b.R) : a.Q.CompareTo(b.Q);
    }
}
