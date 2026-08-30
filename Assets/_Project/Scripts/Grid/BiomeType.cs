namespace Game.Grid
{
    /// <summary>
    /// Ландшафт плитки. С M9 это уже не только косметика: горы непроходимы, река на ребре
    /// требует моста. Порядок — по высоте шума: от низин к вершинам.
    /// </summary>
    public enum BiomeType
    {
        Sand,
        Meadow,
        Forest,
        Rocks,
        Mountains
    }
}
