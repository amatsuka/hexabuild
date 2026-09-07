namespace Game.Core
{
    /// <summary>
    /// Какой уровень кампании сейчас играется. Живёт статикой по той же причине, что и
    /// <see cref="SessionSeed"/>: рестарт и переход на следующий уровень — это перезагрузка
    /// сцены, а сцена собирает всё заново. Сохранением это не является — прогресс кампании
    /// между запусками держит <see cref="CampaignProgress"/>.
    ///
    /// Пусто — партия вне кампании: «Случайная карта» и «Ввести сид».
    /// </summary>
    public static class CampaignSession
    {
        static CampaignConfig campaign;

        /// <summary>Уровень этой партии. Пусто — кампании нет.</summary>
        public static LevelConfig Level { get; private set; }

        /// <summary>Индекс уровня в кампании. Вне кампании — −1.</summary>
        public static int Index { get; private set; } = -1;

        public static bool IsCampaign => Level != null;

        /// <summary>Сколько уровней в кампании. Вне кампании — ноль.</summary>
        public static int Count => campaign != null ? campaign.Count : 0;

        /// <summary>Есть ли следующий уровень: на последнем кнопка «Следующий» не рисуется.</summary>
        public static bool HasNext => campaign != null && Index >= 0 && Index + 1 < campaign.Count;

        /// <summary>Партия по уровню кампании: сид берётся у уровня, а не у конфига.</summary>
        public static void Begin(CampaignConfig config, int index)
        {
            campaign = config;
            Index = index;
            Level = config != null ? config[index] : null;

            if (Level != null)
                SessionSeed.Repeat(Level.Seed);
        }

        /// <summary>Следующий уровень кампании. На последнем не делает ничего.</summary>
        public static void Advance()
        {
            if (HasNext)
                Begin(campaign, Index + 1);
        }

        /// <summary>Заново тот же уровень: сид уровня заказывается ещё раз.</summary>
        public static void Repeat()
        {
            if (Level != null)
                SessionSeed.Repeat(Level.Seed);
        }

        /// <summary>Партия вне кампании: «Случайная карта» и «Ввести сид» её и заказывают.</summary>
        public static void Clear()
        {
            campaign = null;
            Level = null;
            Index = -1;
        }
    }
}
