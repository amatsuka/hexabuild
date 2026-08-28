using System;
using System.Collections.Generic;

namespace Game.Economy
{
    /// <summary>Очки и материалы игрока.</summary>
    public sealed class Wallet
    {
        readonly Dictionary<ResourceType, int> materials = new();

        public Wallet(int startingPoints, int startingGravel)
        {
            Points = startingPoints;
            materials[ResourceType.Gravel] = startingGravel;
        }

        public event Action Changed;

        public int Points { get; private set; }

        public int GetMaterial(ResourceType type) => materials.GetValueOrDefault(type, 0);

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

        public void AddMaterial(ResourceType type, int amount)
        {
            materials[type] = GetMaterial(type) + amount;
            Changed?.Invoke();
        }

        public bool TrySpendMaterial(ResourceType type, int amount)
        {
            if (GetMaterial(type) < amount)
                return false;

            materials[type] = GetMaterial(type) - amount;
            Changed?.Invoke();
            return true;
        }
    }
}
