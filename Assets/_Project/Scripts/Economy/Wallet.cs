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

        public void AddPoints(int amount)
        {
            Points += amount;
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
