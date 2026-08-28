using System;
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

        public MergeSystem(StorageGrid storage, Wallet wallet, MergeRules rules)
        {
            this.storage = storage;
            this.wallet = wallet;
            this.rules = rules;
        }

        /// <summary>Слияние не состоялось: текст для HUD.</summary>
        public event Action<string> Refused;

        public event Action<MergeOutcome> Merged;

        /// <summary>Крафтовый ресурс превращён в очки.</summary>
        public event Action<ResourceType, int> Converted;

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

            storage.TryRemove(outcome.Source, outcome.Consumed);
            for (var i = 0; i < outcome.Produced; i++)
                storage.TryStore(outcome.Result);

            Merged?.Invoke(outcome);
            return true;
        }

        /// <summary>Клик по крафтовому ресурсу: клетка освобождается, игрок получает очки.</summary>
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

            storage.TryRemoveAt(cellIndex);
            wallet.AddPoints(rules.CraftedPoints);
            Converted?.Invoke(content.Value, rules.CraftedPoints);
            return true;
        }
    }
}
