using System;
using Game.Economy;
using Game.Storage;

namespace Game.Merge
{
    /// <summary>Слияние ресурсов на складе: списание, выдача крафта и начисление очков.</summary>
    public sealed class MergeSystem
    {
        readonly StorageGrid storage;
        readonly Wallet wallet;
        readonly MergeRules rules;

        public MergeSystem(StorageGrid storage, Wallet wallet, MergeRules rules)
        {
            this.storage = storage;
            this.wallet = wallet;
            this.rules = rules;
        }

        /// <summary>Слияние не состоялось: текст для HUD.</summary>
        public event Action<string> Refused;

        public event Action<MergeOutcome> Merged;

        public bool TryMerge(ResourceType type)
        {
            if (!rules.CanMerge(type))
            {
                Refused?.Invoke("Крафтовый ресурс дальше не мержится");
                return false;
            }

            if (!rules.TryResolve(type, storage.CountOf(type), out var outcome))
            {
                Refused?.Invoke($"Для слияния нужно минимум {rules.SmallCount}");
                return false;
            }

            storage.TryRemove(outcome.Source, outcome.Consumed);
            for (var i = 0; i < outcome.Produced; i++)
                storage.TryStore(outcome.Result);

            wallet.AddPoints(outcome.Points);
            Merged?.Invoke(outcome);
            return true;
        }
    }
}
