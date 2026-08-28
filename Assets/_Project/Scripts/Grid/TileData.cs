using System;
using System.Collections.Generic;
using Game.Economy;

namespace Game.Grid
{
    /// <summary>Данные одной плитки: координата, месторождения и состояние.</summary>
    public sealed class TileData
    {
        static readonly Deposit[] NoDeposits = Array.Empty<Deposit>();

        int nextDeposit;

        public TileData(HexCoord coord, bool isMetropolis, IReadOnlyList<Deposit> deposits = null)
        {
            Coord = coord;
            IsMetropolis = isMetropolis;
            Deposits = deposits ?? NoDeposits;
        }

        public HexCoord Coord { get; }

        public bool IsMetropolis { get; }

        public IReadOnlyList<Deposit> Deposits { get; }

        public TileState State { get; private set; } = TileState.Hidden;

        /// <summary>Все месторождения плитки исчерпаны.</summary>
        public bool IsExhausted
        {
            get
            {
                foreach (var deposit in Deposits)
                    if (!deposit.IsExhausted)
                        return false;

                return true;
            }
        }

        /// <summary>Плитка стала смежной с открытой и доступна для открытия.</summary>
        public bool MakeAvailable()
        {
            if (State != TileState.Hidden)
                return false;

            State = TileState.Available;
            return true;
        }

        public void Reveal() => State = TileState.Revealed;

        /// <summary>Выдать единицу ресурса по кругу, пропуская исчерпанные месторождения.</summary>
        public bool TryExtract(out ResourceType type)
        {
            type = default;
            if (Deposits.Count == 0)
                return false;

            for (var step = 0; step < Deposits.Count; step++)
            {
                var index = (nextDeposit + step) % Deposits.Count;
                var deposit = Deposits[index];
                if (deposit.IsExhausted)
                    continue;

                deposit.Extract();
                type = deposit.Type;
                nextDeposit = (index + 1) % Deposits.Count;

                if (IsExhausted)
                    State = TileState.Depleted;

                return true;
            }

            State = TileState.Depleted;
            return false;
        }
    }
}
