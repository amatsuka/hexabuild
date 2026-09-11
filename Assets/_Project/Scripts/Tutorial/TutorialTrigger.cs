namespace Game.Tutorial
{
    /// <summary>
    /// Чем закрывается шаг обучения. Всё, кроме <see cref="Next"/>, — события, которые партия
    /// и так рассылает визуалам: своих событий обучение не заводит.
    /// </summary>
    public enum TutorialTrigger
    {
        /// <summary>Шагу делать нечего: его закрывает кнопка «Дальше» на карточке.</summary>
        Next,
        TileRevealed,
        RoadBuilt,
        ResourceLanded,
        Merged,
        Converted,
        ContractClosed,
        Sold
    }
}
