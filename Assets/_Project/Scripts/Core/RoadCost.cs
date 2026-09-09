namespace Game.Core
{
    /// <summary>
    /// Цена одной дороги: щебень и доски. Досок просит только мост — с M33 переправа платится
    /// не одним щебнем, а двумя ресурсами, и списываются они вместе или никак.
    /// </summary>
    public readonly struct RoadCost
    {
        public RoadCost(int gravel, int boards)
        {
            Gravel = gravel;
            Boards = boards;
        }

        public int Gravel { get; }

        public int Boards { get; }
    }
}
