namespace Game.Core
{
    /// <summary>
    /// Слагаемые финального счёта. Счёт — это заработанное за партию, а не остаток кошелька:
    /// иначе открытие плиток штрафовало бы само себя.
    /// </summary>
    public readonly struct FinalScore
    {
        public FinalScore(
            int earned,
            int lost,
            int lossPenalty,
            int openedTiles,
            int fieldTiles,
            int exhaustedDeposits,
            int fieldDeposits,
            int fieldBonus,
            int depositBonus)
        {
            Earned = earned;
            Lost = lost;
            LostPenalty = lost * lossPenalty;
            OpenedTiles = openedTiles;
            FieldTiles = fieldTiles;
            ExhaustedDeposits = exhaustedDeposits;
            FieldDeposits = fieldDeposits;
            FieldCleared = openedTiles >= fieldTiles;
            DepositsCleared = exhaustedDeposits >= fieldDeposits;
            FieldBonus = FieldCleared ? fieldBonus : 0;
            DepositBonus = DepositsCleared ? depositBonus : 0;
        }

        /// <summary>Всего заработано очков за партию.</summary>
        public int Earned { get; }

        /// <summary>Сколько ресурсов уничтожил переполненный склад.</summary>
        public int Lost { get; }

        public int LostPenalty { get; }

        public int OpenedTiles { get; }

        /// <summary>Достижимые проходимые плитки без Метрополии: только их и можно открыть.</summary>
        public int FieldTiles { get; }

        public int ExhaustedDeposits { get; }

        public int FieldDeposits { get; }

        public bool FieldCleared { get; }

        public bool DepositsCleared { get; }

        public int FieldBonus { get; }

        public int DepositBonus { get; }

        /// <summary>Поле пройдено полностью: и открыто, и выработано.</summary>
        public bool IsPerfect => FieldCleared && DepositsCleared;

        /// <summary>Уйти в минус можно: склад, переполняемый весь час, стоит именно столько.</summary>
        public int Total => Earned - LostPenalty + FieldBonus + DepositBonus;
    }
}
