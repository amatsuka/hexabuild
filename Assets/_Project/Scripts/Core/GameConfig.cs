using Game.Economy;
using Game.Grid;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Только числа баланса из раздела 5 спеки. Цвета и параметры камеры сюда не переезжают.
    /// Пороги и очки merge живут в `MergeRules`, чтобы не держать два источника правды.
    /// </summary>
    [CreateAssetMenu(fileName = "GameConfig", menuName = "Hex Colony/Game Config")]
    public sealed class GameConfig : ScriptableObject
    {
        [Header("Поле")]
        [SerializeField] int fieldRows = 18;
        [SerializeField] int seed;

        [Header("Генерация месторождений")]
        [SerializeField] float emptyWeight = 30f;
        [SerializeField] float singleDepositWeight = 45f;
        [SerializeField] float twoDepositsWeight = 20f;
        [SerializeField] float threeDepositsWeight = 5f;
        [SerializeField] float biomeNoiseScale = 0.18f;
        [SerializeField] int minDepositReserve = 8;
        [SerializeField] int maxDepositReserve = 20;
        [Tooltip("На сколько растёт запас за каждый ряд от Метрополии: доля от базового диапазона")]
        [SerializeField] float reserveRowGrowth = 0.06f;

        [Header("Добыча и доставка")]
        [SerializeField] float extractionInterval = 3f;
        [SerializeField] float deliverySecondsPerTile = 1f;

        [Header("Склад")]
        [SerializeField] int storageSize = 24;

        [Header("Стоимости")]
        [SerializeField] int tileOpenCost = 20;
        [Tooltip("Во сколько раз дорожает открытие с каждой открытой плиткой")]
        [SerializeField] float openCostGrowth = 1.04f;
        [SerializeField] int roadCost = 1;
        [Tooltip("Надбавка к дороге за мост: через реку и по воде. Полная цена — roadCost + bridgeCost")]
        [SerializeField] int bridgeCost = 2;

        [Header("Множитель очков")]
        [Tooltip("Сколько прибавляет к множителю каждая открытая плитка")]
        [SerializeField] float multiplierStep = 0.05f;
        [Tooltip("Ступень серии за каждый закрытый подряд контракт; провал снимает одну ступень")]
        [SerializeField] float streakStep = 0.25f;
        [Tooltip("Потолок надбавки серии")]
        [SerializeField] float streakMax = 2f;

        [Header("Старт партии")]
        [SerializeField] int startingPoints = 40;
        [SerializeField] int startingGravel = 3;

        [Header("Контракты")]
        [Tooltip("Сколько крафтовых ресурсов одного типа просит контракт")]
        [SerializeField] int contractGoal = 3;
        [SerializeField] float contractSeconds = 45f;
        [Tooltip("Награда сверх обычных очков за обмен")]
        [SerializeField] int contractReward = 120;
        [Tooltip("Сколько Метрополия молчит между контрактами: случайно в этих границах")]
        [SerializeField] float contractPauseMin = 5f;
        [SerializeField] float contractPauseMax = 20f;

        [Header("Финальный счёт")]
        [Tooltip("Штраф за каждый ресурс, уничтоженный переполненным складом")]
        [SerializeField] int lossPenalty = 10;
        [Tooltip("Бонус за все открытые достижимые плитки")]
        [SerializeField] int fullFieldBonus = 500;
        [Tooltip("Бонус за все выработанные месторождения")]
        [SerializeField] int fullDepositBonus = 500;

        public int FieldRows => fieldRows;

        public int Seed => seed;

        public float ExtractionInterval => extractionInterval;

        public float DeliverySecondsPerTile => deliverySecondsPerTile;

        public int StorageSize => storageSize;

        public int StartingPoints => startingPoints;

        public int StartingGravel => startingGravel;

        public int ContractGoal => contractGoal;

        public float ContractSeconds => contractSeconds;

        public int ContractReward => contractReward;

        public float ContractPauseMin => contractPauseMin;

        public float ContractPauseMax => contractPauseMax;

        public int LossPenalty => lossPenalty;

        public int FullFieldBonus => fullFieldBonus;

        public int FullDepositBonus => fullDepositBonus;

        public float MultiplierStep => multiplierStep;

        public float StreakStep => streakStep;

        public float StreakMax => streakMax;

        public PriceSettings Prices => new(tileOpenCost, openCostGrowth, roadCost, bridgeCost);

        /// <summary>Множитель очков на партию: свой у каждой, как кошелёк.</summary>
        public ScoreMultiplier NewMultiplier() => new(multiplierStep, streakStep, streakMax);

        public MapGenerationSettings MapGenerationSettings => MapGenerationSettingsFor(seed);

        /// <summary>
        /// Та же генерация своим сидом. Нужна кнопкам финального экрана: «Повторить карту»
        /// перезапускает партию с сидом прошлой, а «Новая карта» — со свежим, и оба они не
        /// вправе править ассет конфига.
        /// </summary>
        public MapGenerationSettings MapGenerationSettingsFor(int mapSeed) => new(
            fieldRows,
            mapSeed,
            emptyWeight,
            singleDepositWeight,
            twoDepositsWeight,
            threeDepositsWeight,
            minDepositReserve,
            maxDepositReserve,
            reserveRowGrowth,
            biomeNoiseScale);
    }
}
