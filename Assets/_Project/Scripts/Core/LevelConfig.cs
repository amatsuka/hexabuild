using Game.Grid;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Уровень кампании: сид карты, переопределения дефолтов `GameConfig` и снятый ботом потолок
    /// в очках, от которого считаются звёзды. Числа, одинаковые для всей игры, сюда не переезжают —
    /// уровень трогает ровно те рычаги, которыми курс давит на игрока: размер поля, таймер и цель
    /// контракта, интервал добычи, щедрость месторождений и доли воды и гор.
    ///
    /// Ноль в переопределении значит «взять из `GameConfig`»: ни рядов, ни секунд, ни цели, ни
    /// интервала со значением ноль не бывает, поэтому отдельного флага не нужно. Пороги биомов
    /// ноль имеют осмысленный — «воды нет», — и потому задаются всегда.
    /// </summary>
    [CreateAssetMenu(fileName = "Level", menuName = "Hex Colony/Level Config")]
    public sealed class LevelConfig : ScriptableObject
    {
        [Header("Карта")]
        [Tooltip("Сид уровня. Ноль запрещён: кампания обязана быть воспроизводимой")]
        [SerializeField] int seed = 1;
        [Tooltip("Рядов поля. 0 — как в GameConfig")]
        [SerializeField] int fieldRows;
        [Tooltip("Доля шума под водой. 0 — воды нет; каноническое значение 0.27")]
        [Range(0f, 1f)]
        [SerializeField] float waterCeiling = MapGenerator.WaterCeiling;
        [Tooltip("Доля шума, выше которой горы. 1 — гор нет; каноническое значение 0.645")]
        [Range(0f, 1f)]
        [SerializeField] float rocksCeiling = MapGenerator.RocksCeiling;
        [Tooltip("Во сколько раз щедрее месторождения этого уровня")]
        [SerializeField] float reserveScale = 1f;

        [Header("Давление")]
        [Tooltip("Крафтов в контракте. 0 — как в GameConfig")]
        [SerializeField] int contractGoal;
        [Tooltip("Секунд на контракт. 0 — как в GameConfig")]
        [SerializeField] float contractSeconds;
        [Tooltip("Секунд между добычей. 0 — как в GameConfig")]
        [SerializeField] float extractionInterval;

        [Header("Потолок и звёзды")]
        [Tooltip("Счёт бота на этом уровне. Снимается пунктом контекстного меню, руками не писать")]
        [SerializeField] int botCeiling;
        [Tooltip("Доля потолка на одну звезду")]
        [SerializeField] float oneStarShare = 0.5f;
        [SerializeField] float twoStarShare = 0.75f;
        [SerializeField] float threeStarShare = 1f;

        public int Seed => seed;

        public int FieldRows => fieldRows;

        public float WaterCeiling => waterCeiling;

        public float RocksCeiling => rocksCeiling;

        public float ReserveScale => reserveScale;

        public int ContractGoal => contractGoal;

        public float ContractSeconds => contractSeconds;

        public float ExtractionInterval => extractionInterval;

        /// <summary>Счёт бота на этом уровне: 100% его и есть третья звезда.</summary>
        public int BotCeiling => botCeiling;

        public float OneStarShare => oneStarShare;

        public float TwoStarShare => twoStarShare;

        public float ThreeStarShare => threeStarShare;

        /// <summary>
        /// Сколько звёзд даёт счёт: доля от потолка бота. Потолок не снят — звёзд нет вовсе,
        /// иначе первая же партия получила бы три за деление на ноль.
        /// </summary>
        public int Stars(int total)
        {
            if (botCeiling <= 0)
                return 0;

            var share = total / (float)botCeiling;
            if (share >= threeStarShare)
                return 3;
            if (share >= twoStarShare)
                return 2;

            return share >= oneStarShare ? 1 : 0;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Снять потолок ботом. Собственного редактора у ассета нет — отдельной сборки `Editor`
        /// в проекте не заводится, — поэтому кнопка живёт пунктом контекстного меню инспектора.
        /// Прогон одной партии занимает десятки миллисекунд, а `CampaignTests` потом сверяет
        /// записанное с ботом и падает, если баланс уехал.
        /// </summary>
        [ContextMenu("Снять потолок ботом")]
        void MeasureCeiling()
        {
            var config = UnityEditor.AssetDatabase.LoadAssetAtPath<GameConfig>(
                "Assets/_Project/ScriptableObjects/GameConfig.asset");
            var rules = UnityEditor.AssetDatabase.LoadAssetAtPath<Game.Merge.MergeRules>(
                "Assets/_Project/ScriptableObjects/MergeRules.asset");

            if (config == null || rules == null)
            {
                Debug.LogError($"{name}: не нашёл ассеты баланса, потолок не снят");
                return;
            }

            var run = new Balance.BalanceBot(config, rules, seed, this).Play();
            botCeiling = run.Score.Total;
            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.AssetDatabase.SaveAssets();

            Debug.Log($"{name}: потолок {botCeiling}, поле {run.Score.FieldTiles} плиток, " +
                      $"{run.Seconds:0} c, тупик — {(run.Deadlock ? "да" : "нет")}");
        }
#endif
    }
}
