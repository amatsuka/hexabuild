using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Прогресс кампании между запусками: докуда открыто, сколько звёзд и какой счёт лучший
    /// на каждом уровне. Решением человека от 06.09.2026 персистентность расширена до этого —
    /// сама партия по-прежнему не сохраняется: закрыл вкладку посреди уровня, начал уровень заново.
    ///
    /// Ключ уровня — его индекс в `CampaignConfig`: список не переставляют после выхода сборки.
    /// </summary>
    public static class CampaignProgress
    {
        const string UnlockedKey = "HexColony.Campaign.Unlocked";
        const string StarsKey = "HexColony.Campaign.Stars.";
        const string BestKey = "HexColony.Campaign.Best.";
        const string TutorialKey = "HexColony.Campaign.Tutorial";

        /// <summary>
        /// Обучение уже пройдено. Флаг ставится последним шагом сценария или кнопкой
        /// «Пропустить», но не первым шагом: брошенное на середине обучение показывается снова.
        /// </summary>
        public static bool TutorialDone => PlayerPrefs.GetInt(TutorialKey, 0) != 0;

        /// <summary>Обучение доиграно или снято игроком: второй раз оно не придёт.</summary>
        public static void CompleteTutorial()
        {
            PlayerPrefs.SetInt(TutorialKey, 1);
            PlayerPrefs.Save();
        }

        /// <summary>Индекс последнего открытого уровня. Первый открыт всегда.</summary>
        public static int Unlocked => Mathf.Max(0, PlayerPrefs.GetInt(UnlockedKey, 0));

        public static bool IsUnlocked(int index) => index <= Unlocked;

        public static int StarsAt(int index) => PlayerPrefs.GetInt(StarsKey + index, 0);

        public static int BestAt(int index) => PlayerPrefs.GetInt(BestKey + index, 0);

        /// <summary>
        /// Итог партии на уровне: звёзды и счёт растут, но не падают, а следующий уровень
        /// открывается самим фактом доигранной партии. Звёзды на это не влияют: партия
        /// заканчивается и тупиком, и запирать за ним кампанию — значит запирать её насовсем.
        /// </summary>
        public static void Submit(int index, int total, int stars, int levels)
        {
            if (stars > StarsAt(index))
                PlayerPrefs.SetInt(StarsKey + index, stars);

            if (total > BestAt(index))
                PlayerPrefs.SetInt(BestKey + index, total);

            var next = Mathf.Min(index + 1, levels - 1);
            if (next > Unlocked)
                PlayerPrefs.SetInt(UnlockedKey, next);

            PlayerPrefs.Save();
        }

        /// <summary>Сбросить кампанию. Кнопки на это нет: пункт нужен тестам и отладке.</summary>
        public static void Clear(int levels)
        {
            PlayerPrefs.DeleteKey(UnlockedKey);
            PlayerPrefs.DeleteKey(TutorialKey);
            for (var i = 0; i < levels; i++)
            {
                PlayerPrefs.DeleteKey(StarsKey + i);
                PlayerPrefs.DeleteKey(BestKey + i);
            }

            PlayerPrefs.Save();
        }
    }
}
