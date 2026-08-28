using System;
using Game.Economy;
using UnityEngine;

namespace Game.Merge
{
    /// <summary>Что получится из клика по ресурсу склада: пороги, рецепты и очки.</summary>
    [CreateAssetMenu(fileName = "MergeRules", menuName = "Hex Colony/Merge Rules")]
    public sealed class MergeRules : ScriptableObject
    {
        [Serializable]
        struct Recipe
        {
            public ResourceType Source;
            public ResourceType Result;
        }

        [Header("Пятёрка")]
        [SerializeField] int largeCount = 5;
        [SerializeField] int largeResultCount = 2;
        [SerializeField] int largePoints = 25;

        [Header("Тройка")]
        [SerializeField] int smallCount = 3;
        [SerializeField] int smallResultCount = 1;
        [SerializeField] int smallPoints = 10;

        [Header("Рецепты")]
        [SerializeField] Recipe[] recipes =
        {
            new() { Source = ResourceType.Wood, Result = ResourceType.Board },
            new() { Source = ResourceType.Stone, Result = ResourceType.Gravel },
            new() { Source = ResourceType.Ore, Result = ResourceType.Ingot }
        };

        public int LargeCount => largeCount;

        public int SmallCount => smallCount;

        /// <summary>У ресурса есть рецепт: крафтовые дальше не мержатся.</summary>
        public bool CanMerge(ResourceType type) => TryGetResult(type, out _);

        /// <summary>Пятёрка приоритетнее тройки; при недостатке ресурсов слияния нет.</summary>
        public bool TryResolve(ResourceType type, int available, out MergeOutcome outcome)
        {
            outcome = default;
            if (!TryGetResult(type, out var result))
                return false;

            if (available >= largeCount)
            {
                outcome = new MergeOutcome(type, result, largeCount, largeResultCount, largePoints);
                return true;
            }

            if (available >= smallCount)
            {
                outcome = new MergeOutcome(type, result, smallCount, smallResultCount, smallPoints);
                return true;
            }

            return false;
        }

        bool TryGetResult(ResourceType type, out ResourceType result)
        {
            foreach (var recipe in recipes)
                if (recipe.Source == type)
                {
                    result = recipe.Result;
                    return true;
                }

            result = default;
            return false;
        }
    }

    /// <summary>Итог одного слияния.</summary>
    public readonly struct MergeOutcome
    {
        public MergeOutcome(ResourceType source, ResourceType result, int consumed, int produced, int points)
        {
            Source = source;
            Result = result;
            Consumed = consumed;
            Produced = produced;
            Points = points;
        }

        public ResourceType Source { get; }

        public ResourceType Result { get; }

        public int Consumed { get; }

        public int Produced { get; }

        public int Points { get; }
    }
}
