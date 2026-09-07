using System;
using System.Collections.Generic;

namespace Game.Economy
{
    /// <summary>
    /// Контракт Метрополии: сдать N крафтовых ресурсов одного типа за S секунд и получить награду
    /// сверх обычной цены за обмен. Активный контракт всегда один, а между контрактами Метрополия
    /// молчит случайную паузу: сплошная очередь контрактов не оставляла игроку ни одной минуты,
    /// когда он играет в своё, а не в чужой заказ. Первый контракт партии выдаётся без паузы (3.8).
    ///
    /// Провал не штрафует очками: упущенный бонус и так стоит очков, а штраф поверх него превратил бы
    /// контракт из возможности в налог на невнимательность. Он снимает ступень серии — той надбавки
    /// к множителю, которую дают закрытые подряд контракты; награда сама идёт через множитель.
    /// </summary>
    public sealed class ContractSystem
    {
        readonly Wallet wallet;
        readonly IReadOnlyList<ResourceType> craftedTypes;
        readonly float seconds;
        readonly float minPause;
        readonly float maxPause;
        readonly int baseReward;
        readonly ScoreMultiplier multiplier;
        readonly Random random;

        public ContractSystem(
            Wallet wallet, IReadOnlyList<ResourceType> craftedTypes, int goal, float seconds, int reward,
            float minPause, float maxPause, int seed, ScoreMultiplier multiplier)
        {
            this.wallet = wallet;
            this.craftedTypes = craftedTypes;
            this.seconds = seconds;
            this.minPause = minPause;
            this.maxPause = Math.Max(maxPause, minPause);
            this.multiplier = multiplier;
            Goal = goal;
            baseReward = reward;
            random = seed == 0 ? new Random() : new Random(seed);
        }

        /// <summary>Выдан новый контракт.</summary>
        public event Action Issued;

        /// <summary>Сдан ещё один ресурс по контракту.</summary>
        public event Action Progressed;

        /// <summary>Контракт закрыт, награда уже в кошельке.</summary>
        public event Action<int> Completed;

        /// <summary>Время вышло.</summary>
        public event Action Failed;

        public bool IsActive { get; private set; }

        /// <summary>Какой крафт просит Метрополия.</summary>
        public ResourceType Type { get; private set; }

        public int Goal { get; }

        /// <summary>
        /// Награда за закрытие сейчас: базовая с множителем партии. Меняется вместе с ним,
        /// поэтому карточка контракта перечитывает её на каждом изменении множителя.
        /// </summary>
        public int Reward => multiplier.Apply(baseReward);

        public int Delivered { get; private set; }

        public float SecondsLeft { get; private set; }

        /// <summary>
        /// Сколько ещё молчит Метрополия. Ноль — либо контракт уже висит, либо партия ещё не
        /// выдала первый: сама по себе система не начинает, её начинает `Issue`.
        /// </summary>
        public float SecondsToNext { get; private set; }

        /// <summary>Сколько контрактов закрыто за партию: это показывает финальный экран.</summary>
        public int CompletedCount { get; private set; }

        /// <summary>Первый контракт партии — без паузы. Дальше система выдаёт их сама.</summary>
        public void Issue()
        {
            if (craftedTypes.Count == 0)
                return;

            Type = craftedTypes[random.Next(craftedTypes.Count)];
            Delivered = 0;
            SecondsLeft = seconds;
            SecondsToNext = 0f;
            IsActive = true;
            Issued?.Invoke();
        }

        /// <summary>Тикает и активный контракт, и пауза между ними: живёт всегда что-то одно.</summary>
        public void Tick(float deltaTime)
        {
            if (!IsActive)
            {
                TickPause(deltaTime);
                return;
            }

            SecondsLeft -= deltaTime;
            if (SecondsLeft > 0f)
                return;

            SecondsLeft = 0f;
            IsActive = false;
            Failed?.Invoke();
            multiplier.BreakStreak();
            StartPause();
        }

        void TickPause(float deltaTime)
        {
            if (SecondsToNext <= 0f)
                return;

            SecondsToNext -= deltaTime;
            if (SecondsToNext <= 0f)
                Issue();
        }

        /// <summary>Молчание Метрополии между контрактами: случайное, но в своих границах.</summary>
        void StartPause() =>
            SecondsToNext = minPause + (float)random.NextDouble() * (maxPause - minPause);

        /// <summary>Игрок обменял крафт на очки: чужой тип контракту не засчитывается.</summary>
        public void Count(ResourceType type)
        {
            if (!IsActive || type != Type)
                return;

            Delivered++;
            Progressed?.Invoke();

            if (Delivered < Goal)
                return;

            // Награда считается по серии до этого контракта: ступень, которую он даёт,
            // достаётся следующему. Слушатели `Completed` видят множитель таким, каким он
            // сделал награду, — плашка показывает те же числа, что и кошелёк.
            IsActive = false;
            CompletedCount++;
            var reward = Reward;
            wallet.AddPoints(reward);
            Completed?.Invoke(reward);
            multiplier.ExtendStreak();
            StartPause();
        }
    }
}
