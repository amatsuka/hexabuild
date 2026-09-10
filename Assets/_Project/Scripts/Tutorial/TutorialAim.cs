namespace Game.Tutorial
{
    /// <summary>Куда смотрит шаг: над этим встаёт подсказка и это же подсвечивается.</summary>
    public enum TutorialAim
    {
        /// <summary>Плитки поля из <see cref="TutorialSystem.TargetTiles"/>.</summary>
        Tiles,

        /// <summary>Клетки склада из <see cref="TutorialSystem.TargetCells"/>.</summary>
        Cells,

        /// <summary>Склад целиком: шаг говорит про него, а не про отдельную клетку.</summary>
        Storage,

        /// <summary>Карточка бара с потолком партии.</summary>
        Ceiling,

        /// <summary>Карточка контракта.</summary>
        Contract,

        /// <summary>Кнопка «Продать всё».</summary>
        SellButton
    }
}
