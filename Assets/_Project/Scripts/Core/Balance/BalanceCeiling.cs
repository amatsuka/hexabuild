using Game.Economy;
using Game.Grid;
using Game.Merge;

namespace Game.Core.Balance
{
    /// <summary>
    /// Аналитический потолок заработка на карте — тот же расчёт, которым M17 сняла 4074 на 200
    /// сидах: весь запас достижимых месторождений идёт в пятёрки (единица запаса стоит
    /// `CraftedPoints × LargeResultCount / LargeCount` очков), а щебень, который уйдёт в дорогу на
    /// каждую достижимую плитку, из запаса вычитается. Это верхняя оценка, а не достижимый счёт:
    /// остаток от пятёрок, потери склада, время и стартовый щебень она не видит. Нужна как
    /// знаменатель для доли достижимого — и боту в замере, и бару в M26.
    /// </summary>
    public static class BalanceCeiling
    {
        public static int Of(HexMap map, MergeRules rules, PriceSettings prices)
        {
            var units = 0;
            var gravel = 0;

            foreach (var tile in map.ReachableFromMetropolis())
            {
                foreach (var deposit in tile.Deposits)
                    units += deposit.Reserve;

                if (!tile.IsMetropolis)
                    gravel += tile.HasRiver ? prices.Road + prices.Bridge : prices.Road;
            }

            return (int)((units - gravel) * PointsPerUnit(rules));
        }

        /// <summary>Очков за единицу базового ресурса при слиянии пятёрками.</summary>
        public static float PointsPerUnit(MergeRules rules)
        {
            // Один рецепт даёт `largeResultCount` крафта из `largeCount` единиц; результат
            // у всех рецептов одинаково стоит `CraftedPoints`.
            rules.TryResolve(ResourceType.Wood, rules.LargeCount, out var outcome);
            return rules.CraftedPoints * outcome.Produced / (float)rules.LargeCount;
        }
    }
}
