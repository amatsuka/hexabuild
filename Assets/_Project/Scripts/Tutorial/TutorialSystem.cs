using System;
using System.Collections.Generic;
using Game.Core;
using Game.Economy;
using Game.Grid;
using Game.Merge;
using Game.Roads;
using Game.Storage;

namespace Game.Tutorial
{
    /// <summary>
    /// Обучение первой партии кампании: тринадцать шагов поверх обычной игры. Система ничего
    /// не делает за игрока и не трогает экономику — она слушает те же события, что и визуалы,
    /// и говорит, что подсветить и что разрешить.
    ///
    /// **Ввод блокируется** (решение человека 10.09.2026, прежнее «ввод не блокируется» снято):
    /// на каждом шаге доступно только то, чем он закрывается, остальное поле и склад затенены.
    /// Разрешённое — не всегда сама цель: шаг про бар закрывается следующим слиянием, шаг про
    /// доски — обменом трёх досок, а шаг про обход гряды вообще открывает всё поле, потому что
    /// дорогу к реке игрок ищет сам. Где разрешение шире цели, там оно и написано отдельно.
    ///
    /// Карта первого уровня зафиксирована сидом 284 (M35), поэтому цели шагов — её координаты,
    /// а не результат поиска по полю: камень, дерево, стена и переправа лежат в трёх шагах
    /// от Метрополии, и обучение ведёт по известной раскладке.
    /// </summary>
    public sealed class TutorialSystem
    {
        /// <summary>Сосед Метрополии с камнем: одно месторождение, значит три камня подряд.</summary>
        public static readonly HexCoord StoneTile = new(-1, 1);

        /// <summary>Второй сосед, с лесом: из него доски на первый мост.</summary>
        public static readonly HexCoord WoodTile = new(0, 1);

        /// <summary>Речная скальная плитка: первый мост в игре — каменная арка.</summary>
        public static readonly HexCoord RiverTile = new(-2, 3);

        /// <summary>Гряда гор в третьем ряду: непроходимая стена прямо перед игроком.</summary>
        static readonly HexCoord[] WallTiles = { new(-1, 3), new(0, 3) };

        /// <summary>Сколько висит последний шаг: он ничего не ждёт, а называет и гаснет.</summary>
        const float LastStepSeconds = 9f;

        readonly HexMap map;
        readonly StorageGrid storage;
        readonly RoadNetwork roads;
        readonly MergeRules rules;
        readonly List<HexCoord> tiles = new();
        readonly List<int> cells = new();

        /// <summary>Клетки склада, по которым на этом шаге проходит тап.</summary>
        readonly List<int> allowed = new();

        float remaining = LastStepSeconds;

        public TutorialSystem(HexMap map, StorageGrid storage, RoadNetwork roads, MergeRules rules)
        {
            this.map = map;
            this.storage = storage;
            this.roads = roads;
            this.rules = rules;
            Refresh();
        }

        /// <summary>Шаг или его цель изменились: визуалу пора перерисоваться.</summary>
        public event Action Changed;

        /// <summary>
        /// Обучение доиграно или снято игроком. Флаг в <see cref="PlayerPrefs"/> ставит партия,
        /// а не система: в тестах ей на диске делать нечего.
        /// </summary>
        public event Action Finished;

        /// <summary>
        /// Обучение живёт только в кампании и только на первом уровне, один раз. Флаг ставится
        /// последним шагом, а не первым: брошенное на середине обучение показывается снова.
        /// </summary>
        public static bool ShouldRun => CampaignSession.Index == 0 && !CampaignProgress.TutorialDone;

        public TutorialStep Step { get; private set; }

        public bool IsRunning => Step != TutorialStep.Done;

        /// <summary>Куда смотрит текущий шаг.</summary>
        public TutorialAim Aim => AimOf(Step);

        /// <summary>Плитки под подсветкой, если шаг указывает на поле.</summary>
        public IReadOnlyList<HexCoord> TargetTiles => tiles;

        /// <summary>Клетки склада под подсветкой, если шаг указывает на склад.</summary>
        public IReadOnlyList<int> TargetCells => cells;

        /// <summary>
        /// Клетки склада, доступные на этом шаге. Остальные затенены и тапа не принимают;
        /// пустой список значит, что склад закрыт целиком.
        /// </summary>
        public IReadOnlyList<int> AllowedCells => allowed;

        /// <summary>Проходит ли тап по клетке склада.</summary>
        public bool AllowsCell(int index) => allowed.Contains(index);

        /// <summary>
        /// Проходит ли тап по плитке поля. Шаг про обход гряды открывает поле целиком: дорогу
        /// к реке игрок ищет сам, и подсказать её, ничего не открыв, нельзя — а гора откажет
        /// сама, в этом и урок.
        /// </summary>
        public bool AllowsTile(HexCoord coord) => Step switch
        {
            TutorialStep.OpenStone or TutorialStep.BuildRoad or TutorialStep.WatchDelivery =>
                coord == StoneTile,
            TutorialStep.CraftBoard => coord == WoodTile,
            // С шага про гряду поле открыто целиком и больше не закрывается. Отбирать у игрока
            // уже открытые плитки на следующем шаге нельзя: партия к этому моменту идёт своим
            // ходом, и остальные уроки — про склад, а его кормит именно поле.
            >= TutorialStep.Wall => true,
            _ => false
        };

        /// <summary>
        /// Кнопка «Продать всё» до своего шага не приходит: жребий 10–20 с выдал бы её посреди
        /// первых уроков. Это одна из двух вещей, которые обучение выдаёт событием сценария,
        /// вторая — первый контракт. Сам шаг стоит после моста: до него склад пуст, и кнопке
        /// нечего продавать — резерв 4 щебня и 4 досок выше всего, что к тому моменту нажито.
        /// </summary>
        public bool HoldsSale => IsRunning && Step < TutorialStep.SellButton;

        /// <summary>
        /// Ждёт ли шаг именно этого события. По нему партия понимает, когда выдать контракт
        /// на доски и когда начать опрашивать утечку накала. Шаг, который не ждёт ничего,
        /// не ждёт и <see cref="TutorialTrigger.None"/>: он гаснет по времени.
        /// </summary>
        public bool Waits(TutorialTrigger trigger) =>
            IsRunning && trigger != TutorialTrigger.None && TriggerOf(Step) == trigger;

        /// <summary>Событие партии. Чужое для текущего шага только пересчитывает цель.</summary>
        public void Notify(TutorialTrigger trigger)
        {
            if (Waits(trigger) && IsSatisfied(Step))
                Advance();
            else
                Refresh();
        }

        /// <summary>Последний шаг ничего не ждёт: он гаснет сам.</summary>
        public void Tick(float deltaTime)
        {
            if (Step != TutorialStep.Sweep)
                return;

            remaining -= deltaTime;
            if (remaining <= 0f)
                Advance();
        }

        /// <summary>
        /// Игрок пропустил шаг крестиком на подсказке: следующий встаёт на его место. Это не то
        /// же, что <see cref="Skip"/>, — обучение продолжается, просто без этого урока.
        /// </summary>
        public void SkipStep()
        {
            if (IsRunning)
                Advance();
        }

        /// <summary>Игрок снял обучение целиком: флаг ставится тот же, что и на последнем шаге.</summary>
        public void Skip()
        {
            if (!IsRunning)
                return;

            Step = TutorialStep.Done;
            Finish();
        }

        /// <summary>Пересчитать цель под текущее состояние партии: склад меняется под шагом.</summary>
        public void Refresh()
        {
            tiles.Clear();
            cells.Clear();
            allowed.Clear();

            switch (Step)
            {
                case TutorialStep.OpenStone:
                case TutorialStep.BuildRoad:
                case TutorialStep.WatchDelivery:
                    AimAt(StoneTile);
                    break;
                case TutorialStep.Merge:
                    CollectMergeable(cells);
                    break;
                case TutorialStep.Convert:
                    CollectCrafted(cells);
                    break;
                case TutorialStep.CraftBoard:
                    AimAt(WoodTile);
                    break;
                case TutorialStep.Wall:
                    foreach (var coord in WallTiles)
                        AimAt(coord);

                    break;
                case TutorialStep.Bridge:
                    AimAt(RiverTile);
                    break;
            }

            CollectAllowed();
            Changed?.Invoke();
        }

        /// <summary>
        /// Что открыто на этом шаге. Часть шагов открывает ровно свою цель, часть — больше:
        /// склад целиком открыт там, где шаг иначе встал бы намертво. С девятого шага кнопка
        /// продажи ждёт запаса сверх резерва, а набрать его можно только слияниями; после него
        /// накал, обход гряды и мост тоже требуют свободных рук на складе.
        /// </summary>
        void CollectAllowed()
        {
            switch (Step)
            {
                case TutorialStep.Merge:
                case TutorialStep.Convert:
                    allowed.AddRange(cells);
                    break;
                case TutorialStep.Goal:
                case TutorialStep.CraftBoard:
                    CollectMergeable(allowed);
                    break;
                case TutorialStep.Contract:
                    CollectCells(ResourceType.Board, allowed);
                    break;
                case TutorialStep.Wall:
                case TutorialStep.Bridge:
                case TutorialStep.SellButton:
                case TutorialStep.Heat:
                case TutorialStep.Sweep:
                case TutorialStep.Done:
                    CollectOccupied();
                    break;
            }
        }

        void CollectOccupied()
        {
            for (var index = 0; index < storage.Capacity; index++)
                if (storage[index].HasValue)
                    allowed.Add(index);
        }

        void Advance()
        {
            Step++;
            remaining = LastStepSeconds;

            if (Step == TutorialStep.Done)
                Finish();
            else
                Refresh();
        }

        void Finish()
        {
            Refresh();
            Finished?.Invoke();
        }

        /// <summary>Цель на поле. Координаты чужой карты в списке не окажется.</summary>
        void AimAt(HexCoord coord)
        {
            if (map.Contains(coord))
                tiles.Add(coord);
        }

        /// <summary>
        /// Событие пришло, но шагу нужно ещё и состояние партии. Иначе «обойди гряду» закрылся
        /// бы любой открытой плиткой, а «слей бревно в доску» — любым слиянием.
        /// </summary>
        bool IsSatisfied(TutorialStep step) => step switch
        {
            TutorialStep.CraftBoard => storage.CountOf(ResourceType.Board) > 0,
            TutorialStep.Wall => HasRevealedRiver(),
            TutorialStep.Bridge => HasBridge(),
            _ => true
        };

        /// <summary>Игрок дошёл до реки, то есть обошёл гряду: гора не открывается никогда.</summary>
        bool HasRevealedRiver()
        {
            foreach (var tile in map.Tiles.Values)
                if (tile.HasRiver && tile.State == TileState.Revealed)
                    return true;

            return false;
        }

        /// <summary>Дорога легла на речную плитку — это и есть мост.</summary>
        bool HasBridge()
        {
            foreach (var coord in roads.Roads)
                if (map.TryGetTile(coord, out var tile) && tile.HasRiver)
                    return true;

            return false;
        }

        /// <summary>Первый базовый тип, которого набралось на слияние: его клетки и подсвечиваем.</summary>
        void CollectMergeable(List<int> into)
        {
            for (var index = 0; index < storage.Capacity; index++)
            {
                var content = storage[index];
                if (!content.HasValue || !rules.CanMerge(content.Value))
                    continue;

                if (storage.CountOf(content.Value) < rules.SmallCount)
                    continue;

                CollectCells(content.Value, into);
                return;
            }
        }

        /// <summary>
        /// Крафт, который шаг про очки разрешает обменять. Доски в него не входят: они лежат
        /// на складе под контракт седьмого шага, и обменянные здесь оставили бы его без цели —
        /// а мост потом без оплаты.
        /// </summary>
        void CollectCrafted(List<int> into)
        {
            for (var index = 0; index < storage.Capacity; index++)
            {
                var content = storage[index];
                if (content.HasValue && !rules.CanMerge(content.Value) && content.Value != ResourceType.Board)
                    into.Add(index);
            }
        }

        void CollectCells(ResourceType type, List<int> into)
        {
            for (var index = 0; index < storage.Capacity; index++)
                if (storage[index] == type)
                    into.Add(index);
        }

        static TutorialTrigger TriggerOf(TutorialStep step) => step switch
        {
            TutorialStep.OpenStone => TutorialTrigger.TileRevealed,
            TutorialStep.BuildRoad => TutorialTrigger.RoadBuilt,
            TutorialStep.WatchDelivery => TutorialTrigger.ResourceLanded,
            TutorialStep.Merge => TutorialTrigger.Merged,
            TutorialStep.Convert => TutorialTrigger.Converted,
            // Бар двигает не отдельное действие, а следующий виток цикла: шаг закрывается
            // тем же слиянием, с которого цикл и начинается.
            TutorialStep.Goal => TutorialTrigger.Merged,
            TutorialStep.Contract => TutorialTrigger.ContractClosed,
            TutorialStep.CraftBoard => TutorialTrigger.Merged,
            TutorialStep.Wall => TutorialTrigger.TileRevealed,
            TutorialStep.Bridge => TutorialTrigger.RoadBuilt,
            TutorialStep.SellButton => TutorialTrigger.Sold,
            TutorialStep.Heat => TutorialTrigger.HeatLeaked,
            _ => TutorialTrigger.None
        };

        static TutorialAim AimOf(TutorialStep step) => step switch
        {
            TutorialStep.Merge or TutorialStep.Convert => TutorialAim.Cells,
            TutorialStep.Goal => TutorialAim.Ceiling,
            TutorialStep.Contract => TutorialAim.Contract,
            TutorialStep.SellButton => TutorialAim.SellButton,
            TutorialStep.Heat or TutorialStep.Sweep => TutorialAim.Storage,
            _ => TutorialAim.Tiles
        };
    }
}
