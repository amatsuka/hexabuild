using System;
using System.Collections.Generic;

namespace Game.Grid
{
    /// <summary>Данные одной плитки: координата, месторождения и состояние.</summary>
    public sealed class TileData
    {
        static readonly Deposit[] NoDeposits = Array.Empty<Deposit>();

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

        /// <summary>Плитка стала смежной с открытой и доступна для открытия.</summary>
        public bool MakeAvailable()
        {
            if (State != TileState.Hidden)
                return false;

            State = TileState.Available;
            return true;
        }

        public void Reveal() => State = TileState.Revealed;
    }
}
