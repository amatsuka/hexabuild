namespace Game.Tutorial
{
    /// <summary>
    /// Шаги обучения по порядку. Каждый закрывается одним игровым событием, кроме последнего:
    /// премию за чистый склад подсказкой не подвести — она требует двадцати действий подряд, —
    /// поэтому шаг её называет и гаснет сам.
    /// </summary>
    public enum TutorialStep
    {
        OpenStone,
        BuildRoad,
        WatchDelivery,
        Merge,
        Convert,
        Goal,
        Contract,
        CraftBoard,
        SellButton,
        Heat,
        Wall,
        Bridge,
        Sweep,
        Done
    }
}
