using System;
using Game.Economy;

namespace Game.Storage
{
    /// <summary>Склад Метрополии: ресурс кладётся в первую свободную клетку, лишний уничтожается.</summary>
    public sealed class StorageGrid
    {
        readonly ResourceType?[] cells;

        public StorageGrid(int capacity)
        {
            cells = new ResourceType?[capacity];
        }

        public event Action Changed;

        /// <summary>Ресурс не поместился и уничтожен.</summary>
        public event Action<ResourceType> ResourceLost;

        public int Capacity => cells.Length;

        public int Count { get; private set; }

        public int LostCount { get; private set; }

        public ResourceType? this[int index] => cells[index];

        public bool TryStore(ResourceType type)
        {
            for (var i = 0; i < cells.Length; i++)
            {
                if (cells[i].HasValue)
                    continue;

                cells[i] = type;
                Count++;
                Changed?.Invoke();
                return true;
            }

            LostCount++;
            ResourceLost?.Invoke(type);
            Changed?.Invoke();
            return false;
        }

        public int CountOf(ResourceType type)
        {
            var count = 0;
            foreach (var cell in cells)
                if (cell == type)
                    count++;

            return count;
        }

        public bool TryRemove(ResourceType type, int amount)
        {
            if (CountOf(type) < amount)
                return false;

            var left = amount;
            for (var i = 0; i < cells.Length && left > 0; i++)
            {
                if (cells[i] != type)
                    continue;

                cells[i] = null;
                Count--;
                left--;
            }

            Changed?.Invoke();
            return true;
        }
    }
}
