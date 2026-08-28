using System;
using System.Collections.Generic;
using Game.Economy;
using Game.Grid;
using Game.Merge;
using Game.Roads;
using Game.Storage;

namespace Game.Tutorial
{
    /// <summary>
    /// Обучение первых минут: шесть шагов поверх обычной партии. Система ничего не решает за игрока
    /// и не трогает экономику — она слушает те же события, что и визуалы, и говорит, что подсветить.
    /// </summary>
    public sealed class TutorialSystem
    {
        readonly HexMap map;
        readonly StorageGrid storage;
        readonly RoadNetwork roads;
        readonly MergeRules rules;
        readonly List<int> cells = new();
        readonly HexCoord startTile;

        public TutorialSystem(HexMap map, StorageGrid storage, RoadNetwork roads, MergeRules rules)
        {
            this.map = map;
            this.storage = storage;
            this.roads = roads;
            this.rules = rules;
            startTile = PickStartTile(map);
            Refresh();
        }

        /// <summary>Шаг, цель или признак тупика изменились: визуалу пора перерисоваться.</summary>
        public event Action Changed;

        public TutorialStep Step { get; private set; }

        /// <summary>Плитка под подсветкой, если текущий шаг указывает на поле.</summary>
        public HexCoord? TargetTile { get; private set; }

        /// <summary>Клетки склада под подсветкой, если текущий шаг указывает на склад.</summary>
        public IReadOnlyList<int> TargetCells => cells;

        /// <summary>Щебень кончился раньше первой дороги: добывать нечем и заработать не на чем.</summary>
        public bool IsStuck { get; private set; }

        public bool IsRunning => Step != TutorialStep.Done;

        /// <summary>Событие партии. Чужое для текущего шага только пересчитывает цель.</summary>
        public void Notify(TutorialTrigger trigger)
        {
            if (IsRunning && trigger == TriggerOf(Step))
                Step++;

            Refresh();
        }

        /// <summary>Игрок снял обучение сам.</summary>
        public void Skip()
        {
            if (!IsRunning)
                return;

            Step = TutorialStep.Done;
            Refresh();
        }

        /// <summary>
        /// Пересчитать цель под текущее состояние партии: обучение следует за игроком, а не наоборот.
        /// </summary>
        public void Refresh()
        {
            cells.Clear();
            TargetTile = null;
            IsStuck = false;

            switch (Step)
            {
                case TutorialStep.OpenTile:
                case TutorialStep.Loop:
                    TargetTile = NearestTile(tile => tile.State == TileState.Available);
                    break;
                case TutorialStep.BuildRoad:
                    IsStuck = roads.Roads.Count == 0 && storage.CountOf(ResourceType.Gravel) == 0;
                    if (!IsStuck)
                        TargetTile = NearestTile(Buildable);
                    break;
                case TutorialStep.Merge:
                    CollectMergeable();
                    break;
                case TutorialStep.Convert:
                    CollectCrafted();
                    break;
            }

            Changed?.Invoke();
        }

        /// <summary>Открытая плитка без дороги: на ней и строят.</summary>
        bool Buildable(TileData tile) =>
            tile.State == TileState.Revealed && !tile.IsMetropolis && !roads.HasRoad(tile.Coord);

        /// <summary>
        /// Ближайшая к Метрополии подходящая плитка. Плитка с гарантированным камнем идёт вне
        /// очереди: обучение ведёт по маршруту, про который заранее известно, что он рабочий.
        /// </summary>
        HexCoord? NearestTile(Func<TileData, bool> match)
        {
            if (map.TryGetTile(startTile, out var start) && match(start))
                return startTile;

            HexCoord? best = null;
            var bestRank = int.MaxValue;

            foreach (var tile in map.Tiles.Values)
            {
                if (!match(tile))
                    continue;

                var rank = Rank(tile.Coord);
                if (rank >= bestRank)
                    continue;

                best = tile.Coord;
                bestRank = rank;
            }

            return best;
        }

        /// <summary>Сначала близость к Метрополии, потом Q — чтобы выбор не зависел от порядка обхода.</summary>
        static int Rank(HexCoord coord) => HexCoord.Distance(coord, HexCoord.Zero) * 100 + coord.Q + 50;

        /// <summary>Первый базовый тип, которого набралось на слияние: его клетки и подсвечиваем.</summary>
        void CollectMergeable()
        {
            for (var index = 0; index < storage.Capacity; index++)
            {
                var content = storage[index];
                if (!content.HasValue || !rules.CanMerge(content.Value))
                    continue;

                if (storage.CountOf(content.Value) < rules.SmallCount)
                    continue;

                CollectCells(content.Value);
                return;
            }
        }

        /// <summary>Крафтовые ресурсы: любой из них по тапу превращается в очки.</summary>
        void CollectCrafted()
        {
            for (var index = 0; index < storage.Capacity; index++)
            {
                var content = storage[index];
                if (content.HasValue && !rules.CanMerge(content.Value))
                    cells.Add(index);
            }
        }

        void CollectCells(ResourceType type)
        {
            for (var index = 0; index < storage.Capacity; index++)
                if (storage[index] == type)
                    cells.Add(index);
        }

        static TutorialTrigger TriggerOf(TutorialStep step)
        {
            switch (step)
            {
                case TutorialStep.BuildRoad:
                    return TutorialTrigger.RoadBuilt;
                case TutorialStep.WatchDelivery:
                    return TutorialTrigger.ResourceLanded;
                case TutorialStep.Merge:
                    return TutorialTrigger.Merged;
                case TutorialStep.Convert:
                    return TutorialTrigger.Converted;
                default:
                    return TutorialTrigger.TileRevealed;
            }
        }

        /// <summary>
        /// Соседняя с Метрополией плитка с камнем — генератор гарантирует, что она есть (2.1).
        /// Обучение смотрит в неоткрытую плитку намеренно: пустая плитка-транзит выпадает в 30%
        /// случаев, и шаг с доставкой на ней не наступил бы никогда.
        /// </summary>
        static HexCoord PickStartTile(HexMap map)
        {
            HexCoord? fallback = null;

            foreach (var tile in map.NeighborsOf(HexCoord.Zero))
            {
                fallback ??= tile.Coord;

                foreach (var deposit in tile.Deposits)
                    if (deposit.Type == ResourceType.Stone)
                        return tile.Coord;
            }

            return fallback ?? HexCoord.Zero;
        }
    }
}
