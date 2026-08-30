using System;
using System.Collections.Generic;

namespace Game.Economy
{
    /// <summary>
    /// Контракт Метрополии: сдать N крафтовых ресурсов одного типа за S секунд и получить награду
    /// сверх обычной цены за обмен. Активный контракт всегда один, следующий выдаётся сразу же.
    ///
    /// Провал ничем не штрафует: упущенный бонус и так стоит очков, а штраф поверх него превратил бы
    /// контракт из возможности в налог на невнимательность.
    /// </summary>
    public sealed class ContractSystem
    {
        readonly Wallet wallet;
        readonly IReadOnlyList<ResourceType> craftedTypes;
        readonly float seconds;
        readonly Random random;

        public ContractSystem(
            Wallet wallet, IReadOnlyList<ResourceType> craftedTypes, int goal, float seconds, int reward, int seed)
        {
            this.wallet = wallet;
            this.craftedTypes = craftedTypes;
            this.seconds = seconds;
            Goal = goal;
            Reward = reward;
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

        public int Reward { get; }

        public int Delivered { get; private set; }

        public float SecondsLeft { get; private set; }

        /// <summary>Первый контракт партии. Дальше система выдаёт их сама.</summary>
        public void Issue()
        {
            if (craftedTypes.Count == 0)
                return;

            Type = craftedTypes[random.Next(craftedTypes.Count)];
            Delivered = 0;
            SecondsLeft = seconds;
            IsActive = true;
            Issued?.Invoke();
        }

        public void Tick(float deltaTime)
        {
            if (!IsActive)
                return;

            SecondsLeft -= deltaTime;
            if (SecondsLeft > 0f)
                return;

            SecondsLeft = 0f;
            IsActive = false;
            Failed?.Invoke();
            Issue();
        }

        /// <summary>Игрок обменял крафт на очки: чужой тип контракту не засчитывается.</summary>
        public void Count(ResourceType type)
        {
            if (!IsActive || type != Type)
                return;

            Delivered++;
            Progressed?.Invoke();

            if (Delivered < Goal)
                return;

            IsActive = false;
            wallet.AddPoints(Reward);
            Completed?.Invoke(Reward);
            Issue();
        }
    }
}
