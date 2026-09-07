using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Кампания: упорядоченный список уровней. Порядок в массиве и есть порядок прохождения,
    /// он же — ключ прогресса в `PlayerPrefs`, поэтому уровни не переставляют местами после
    /// выхода сборки: игрок увидит свои звёзды на чужих уровнях.
    /// </summary>
    [CreateAssetMenu(fileName = "Campaign", menuName = "Hex Colony/Campaign Config")]
    public sealed class CampaignConfig : ScriptableObject
    {
        [SerializeField] LevelConfig[] levels = System.Array.Empty<LevelConfig>();

        public int Count => levels.Length;

        public LevelConfig this[int index] => index >= 0 && index < levels.Length ? levels[index] : null;
    }
}
