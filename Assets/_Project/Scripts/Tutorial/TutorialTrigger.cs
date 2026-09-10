namespace Game.Tutorial
{
    /// <summary>
    /// События партии, которыми закрываются шаги обучения. Своих событий обучение не заводит:
    /// всё это партия и так рассылает визуалам.
    /// </summary>
    public enum TutorialTrigger
    {
        /// <summary>Шаг не ждёт ничего: он гаснет по времени.</summary>
        None,
        TileRevealed,
        RoadBuilt,
        ResourceLanded,
        Merged,
        Converted,
        ContractClosed,
        Sold,

        /// <summary>Накал пошёл на убыль. Событием партия об этом не сообщает — его опрашивают.</summary>
        HeatLeaked
    }
}
