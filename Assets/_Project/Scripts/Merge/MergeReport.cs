using System.Collections.Generic;

namespace Game.Merge
{
    /// <summary>Что произошло на складе при слиянии: правило и задействованные клетки.</summary>
    public readonly struct MergeReport
    {
        public MergeReport(MergeOutcome outcome, IReadOnlyList<int> consumedCells, IReadOnlyList<int> resultCells)
        {
            Outcome = outcome;
            ConsumedCells = consumedCells;
            ResultCells = resultCells;
        }

        public MergeOutcome Outcome { get; }

        /// <summary>Клетки, из которых ресурсы ушли: из них летит анимация.</summary>
        public IReadOnlyList<int> ConsumedCells { get; }

        /// <summary>Клетки, куда лёг крафт: в них анимация приходит.</summary>
        public IReadOnlyList<int> ResultCells { get; }
    }
}
