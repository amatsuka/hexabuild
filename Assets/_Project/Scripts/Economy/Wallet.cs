using System;

namespace Game.Economy
{
    /// <summary>Очки игрока. Ресурсы лежат на складе, а не здесь.</summary>
    public sealed class Wallet
    {
        public Wallet(int startingPoints)
        {
            Points = startingPoints;
        }

        public event Action Changed;

        public int Points { get; private set; }

        /// <summary>
        /// Всего заработано за партию. Именно это идёт в финальный счёт: остаток кошелька
        /// штрафовал бы игрока за открытие плиток, то есть за то, ради чего партия и играется.
        /// Стартовые очки не в счёт — их не заработали.
        /// </summary>
        public int TotalEarned { get; private set; }

        public void AddPoints(int amount)
        {
            Points += amount;
            TotalEarned += amount;
            Changed?.Invoke();
        }

        public bool TrySpendPoints(int amount)
        {
            if (Points < amount)
                return false;

            Points -= amount;
            Changed?.Invoke();
            return true;
        }
    }
}
