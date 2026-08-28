using Game.Economy;

namespace Game.Grid
{
    /// <summary>Месторождение на плитке: тип ресурса и остаток запаса.</summary>
    public sealed class Deposit
    {
        public Deposit(ResourceType type, int reserve)
        {
            Type = type;
            Reserve = reserve;
        }

        public ResourceType Type { get; }

        public int Reserve { get; private set; }

        public bool IsExhausted => Reserve <= 0;
    }
}
