using System;
using System.Collections.Generic;
using Game.Economy;
using Game.Storage;

namespace Game.Merge
{
    /// <summary>Действия на складе: слияние базовых ресурсов и обмен крафтовых на очки.</summary>
    public sealed class MergeSystem
    {
        readonly StorageGrid storage;
        readonly Wallet wallet;
        readonly MergeRules rules;
        readonly ScoreMultiplier multiplier;
        readonly int sweepCells;
        readonly int sweepBonus;

        /// <summary>
        /// Премия за чистый склад уже взята и ждёт, пока склад снова наполнится. Без защёлки
        /// она платила бы за каждый клик по пустому складу, а не за то, что его разгребли.
        /// </summary>
        bool sweptAlready;

        public MergeSystem(
            StorageGrid storage, Wallet wallet, MergeRules rules, ScoreMultiplier multiplier,
            int sweepCells = 0, int sweepBonus = 0)
        {
            this.storage = storage;
            this.wallet = wallet;
            this.rules = rules;
            this.multiplier = multiplier;
            this.sweepCells = sweepCells;
            this.sweepBonus = sweepBonus;

            // Переполнение сжигает накал здесь, а не в `GameSession`: тогда бот и партия
            // считали бы цену ставки по-разному, а бот на то и есть, чтобы считать ту же игру.
            storage.ResourceLost += OnResourceLost;
        }

        /// <summary>Слияние не состоялось: текст для HUD.</summary>
        public event Action<string> Refused;

        public event Action<MergeReport> Merged;

        /// <summary>
        /// Крафтовый ресурс превращён в очки: из какой клетки, что и на сколько. Клетка нужна
        /// не правилам, а виду: из неё ресурс улетает в карточку контракта.
        /// </summary>
        public event Action<int, ResourceType, int> Converted;

        /// <summary>
        /// Склад разгребли до чистого на полном накале: из какой клетки был последний ход
        /// и сколько за это дали. Клетка нужна виду — над ней встаёт плашка премии.
        /// </summary>
        public event Action<int, int> Swept;

        public bool TryMerge(ResourceType type)
        {
            if (!rules.CanMerge(type))
            {
                Refused?.Invoke("Крафтовый ресурс не мержится, кликните его ради очков");
                return false;
            }

            if (!rules.TryResolve(type, storage.CountOf(type), out var outcome))
            {
                Refused?.Invoke($"Для слияния нужно минимум {rules.SmallCount}");
                return false;
            }

            var consumedCells = new List<int>(outcome.Consumed);
            storage.TryRemove(outcome.Source, outcome.Consumed, consumedCells);

            var resultCells = new List<int>(outcome.Produced);
            for (var i = 0; i < outcome.Produced; i++)
                if (storage.TryStore(outcome.Result, out var cell))
                    resultCells.Add(cell);

            multiplier.Bump();
            Merged?.Invoke(new MergeReport(outcome, consumedCells, resultCells));
            TrySweep(resultCells.Count > 0 ? resultCells[0] : consumedCells[0]);
            return true;
        }

        /// <summary>
        /// Один шаг автодоигрывания склада: продать крафт, а если крафта нет — слить то, чего
        /// набралось на слияние. Решений в конце партии не остаётся, только доклик, и порядок
        /// здесь тот же, каким его вёл бы игрок: крафт уходит первым и освобождает клетки,
        /// а `TryResolve` сам предпочитает пятёрку тройке — счёт выходит тем же максимумом.
        /// `false` — доигрывать больше нечего.
        /// </summary>
        public bool TryPlayOut()
        {
            for (var cell = 0; cell < storage.Capacity; cell++)
            {
                var content = storage[cell];
                if (content.HasValue && !rules.CanMerge(content.Value))
                    return TryConvert(cell);
            }

            for (var cell = 0; cell < storage.Capacity; cell++)
            {
                var content = storage[cell];
                if (content.HasValue && storage.CountOf(content.Value) >= rules.SmallCount)
                    return TryMerge(content.Value);
            }

            return false;
        }

        /// <summary>
        /// Клик по крафтовому ресурсу: клетка освобождается, игрок получает очки — базовую цену
        /// крафта с множителем партии на момент клика.
        /// </summary>
        public bool TryConvert(int cellIndex)
        {
            var content = storage[cellIndex];
            if (!content.HasValue)
                return false;

            if (rules.CanMerge(content.Value))
            {
                Refused?.Invoke("Базовый ресурс сначала нужно смержить");
                return false;
            }

            var points = multiplier.Apply(rules.CraftedPoints);
            storage.TryRemoveAt(cellIndex);
            wallet.AddPoints(points);
            multiplier.Bump();
            Converted?.Invoke(cellIndex, content.Value, points);
            TrySweep(cellIndex);
            return true;
        }

        /// <summary>
        /// Склад опустел до <see cref="sweepCells"/> клеток, и накал стоит на потолке — премия.
        /// Она идёт через множитель, как награда контракта: платят именно за то, что разгребли
        /// на пике, а не когда придётся. Защёлка снимается, когда склад снова наберётся.
        /// </summary>
        void TrySweep(int cellIndex)
        {
            if (storage.Count > sweepCells)
            {
                sweptAlready = false;
                return;
            }

            if (sweptAlready || sweepBonus <= 0 || !multiplier.HeatAtMax)
                return;

            sweptAlready = true;
            var points = multiplier.Apply(sweepBonus);
            wallet.AddPoints(points);
            Swept?.Invoke(cellIndex, points);
        }

        void OnResourceLost(ResourceType _) => multiplier.Burn();
    }
}
