namespace Game.Tutorial
{
    /// <summary>Шаги обучения по порядку: каждый закрывается одним действием игрока.</summary>
    public enum TutorialStep
    {
        OpenTile,
        BuildRoad,
        WatchDelivery,
        Merge,
        Convert,
        Loop,
        Done
    }
}
