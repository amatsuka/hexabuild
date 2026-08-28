namespace Game.Grid
{
    /// <summary>Данные одной плитки поля. В M1 хранит только координату и признак Метрополии.</summary>
    public sealed class TileData
    {
        public TileData(HexCoord coord, bool isMetropolis)
        {
            Coord = coord;
            IsMetropolis = isMetropolis;
        }

        public HexCoord Coord { get; }

        public bool IsMetropolis { get; }
    }
}
