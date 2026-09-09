using System;
using Game.Core;
using Game.Economy;
using Game.Storage;

namespace Game.Merge
{
    /// <summary>
    /// Кнопка «Продать всё»: приходит по таймеру и одним нажатием меняет накопленный крафт
    /// на очки. Она не отменяет клик по клетке, а снимает с него доклик: держать темп руками
    /// игрок по-прежнему может и на поздних уровнях обязан, но разгребать полный склад
    /// поштучно ему больше не приходится.
    ///
    /// Резерв щебня и досок кнопка не трогает: продать под ноль то, чем строят дорогу и мост,
    /// значит подарить игроку тупик за собственную аккуратность.
    ///
    /// Накал двигает каждая проданная единица — решение человека. Следствие принято сознательно:
    /// полный склад под кнопкой сам выносит накал к потолку и продаётся по нарастающему
    /// множителю, то есть копить выгоднее, чем кликать.
    /// </summary>
    public sealed class BatchSale
    {
        readonly StorageGrid storage;
        readonly MergeSystem merges;
        readonly MergeRules rules;
        readonly GameEndSystem end;
        readonly Random random;
        readonly float pauseMin;
        readonly float pauseMax;
        readonly int gravelReserve;
        readonly int boardReserve;

        /// <summary>Сколько ещё ждать прихода кнопки. Ноль и ниже — она на экране.</summary>
        float wait;

        public BatchSale(
            StorageGrid storage, MergeSystem merges, MergeRules rules, GameEndSystem end,
            float pauseMin, float pauseMax, int gravelReserve, int boardReserve, int seed)
        {
            this.storage = storage;
            this.merges = merges;
            this.rules = rules;
            this.end = end;
            this.pauseMin = pauseMin;
            this.pauseMax = pauseMax;
            this.gravelReserve = gravelReserve;
            this.boardReserve = boardReserve;
            random = new Random(seed);
            wait = NextPause();
        }

        /// <summary>
        /// Заработать на поле больше нечем, и всё, что осталось, — это склад. Кнопка в этом
        /// состоянии приходит без таймера и продаёт без резерва: строить уже некуда, беречь
        /// щебень не для чего, а докликивать хвост партии игрок не должен.
        /// </summary>
        public bool Everything => end.NothingLeftOnField;

        /// <summary>Таймер вышел или партия доиграна: кнопка на экране.</summary>
        public bool Ready => Everything || wait <= 0f;

        /// <summary>Кнопка на экране и ей есть что продать. Пустая кнопка не показывается.</summary>
        public bool CanSell => Ready && HasStock;

        /// <summary>Нажатие принято: следующий приход отсчитывается с этого момента.</summary>
        public void Begin() => wait = NextPause();

        public void Tick(float deltaTime)
        {
            if (wait > 0f)
                wait -= deltaTime;
        }

        /// <summary>Продать всю пачку разом: так её видит бот, у которого нет кадров.</summary>
        public void Sell()
        {
            while (TrySellOne())
            {
            }
        }

        /// <summary>
        /// Одна единица пачки. Партия продаёт их шагами — двадцать прибавок в один кадр
        /// не читаются, — но правило и порядок здесь те же, что у бота: разница только
        /// в темпе, как у добычи.
        /// </summary>
        public bool TrySellOne()
        {
            // Хвост партии доигрывается целиком: крафт в очки, остатки базового — в крафт.
            // Иначе три бревна на складе не дали бы `GameEndSystem` объявить конец.
            if (Everything)
                return merges.TryPlayOut();

            for (var cell = 0; cell < storage.Capacity; cell++)
            {
                var content = storage[cell];
                if (!content.HasValue || rules.CanMerge(content.Value))
                    continue;

                // Счёт проверяется на каждой единице, а не один раз на тип: так резервом
                // остаются последние клетки, а продажа идёт слева направо — волной.
                if (storage.CountOf(content.Value) <= Reserve(content.Value))
                    continue;

                return merges.TryConvert(cell);
            }

            return false;
        }

        bool HasStock
        {
            get
            {
                if (Everything)
                    return end.HasCashableResource();

                for (var cell = 0; cell < storage.Capacity; cell++)
                {
                    var content = storage[cell];
                    if (content.HasValue
                        && !rules.CanMerge(content.Value)
                        && storage.CountOf(content.Value) > Reserve(content.Value))
                        return true;
                }

                return false;
            }
        }

        int Reserve(ResourceType type) => type switch
        {
            ResourceType.Gravel => gravelReserve,
            ResourceType.Board => boardReserve,
            _ => 0
        };

        float NextPause() => pauseMin + (float)random.NextDouble() * (pauseMax - pauseMin);
    }
}
