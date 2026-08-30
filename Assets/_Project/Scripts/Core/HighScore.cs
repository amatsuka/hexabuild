using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Единственное, что переживает перезапуск: лучший счёт. Партия по-прежнему не сохраняется —
    /// закрыл вкладку, начал заново.
    /// </summary>
    public static class HighScore
    {
        const string Key = "HexColony.BestScore";

        public static int Best => PlayerPrefs.GetInt(Key, 0);

        /// <summary>Записывает счёт, если он лучше прежнего, и говорит, побит ли рекорд.</summary>
        public static bool Submit(int score)
        {
            if (score <= Best)
                return false;

            PlayerPrefs.SetInt(Key, score);
            PlayerPrefs.Save();
            return true;
        }
    }
}
