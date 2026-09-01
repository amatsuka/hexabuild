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

        [Header("Добыча и доставка")]
        [SerializeField] float extractionInterval = 3f;
        [SerializeField] float deliverySecondsPerTile = 1f;

        [Header("Склад")]
        [SerializeField] int storageSize = 24;

        [Header("Стоимости")]
        [SerializeField] int tileOpenCost = 20;
        [Tooltip("Насколько дорожает открытие за каждую группу уже открытых плиток")]
        [SerializeField] int openCostStep = 2;
        [Tooltip("Сколько открытых плиток поднимают цену на один шаг")]
        [SerializeField] int openCostGroup = 5;
        [SerializeField] int roadCost = 1;
        [Tooltip("Надбавка к дороге за мост: через реку и по воде. Полная цена — roadCost + bridgeCost")]
        [SerializeField] int bridgeCost = 2;

        [Header("Старт партии")]
        [SerializeField] int startingPoints = 40;
        [SerializeField] int startingGravel = 3;

        [Header("Контракты")]
        [Tooltip("Сколько крафтовых ресурсов одного типа просит контракт")]
        [SerializeField] int contractGoal = 3;
        [SerializeField] float contractSeconds = 45f;
        [Tooltip("Награда сверх обычных очков за обмен")]
        [SerializeField] int contractReward = 40;

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

        public int LossPenalty => lossPenalty;

        public int FullFieldBonus => fullFieldBonus;

        public int FullDepositBonus => fullDepositBonus;

        public PriceSettings Prices => new(tileOpenCost, openCostStep, openCostGroup, roadCost, bridgeCost);

        public MapGenerationSettings MapGenerationSettings => new(
            fieldRows,
            seed,
            emptyWeight,
            singleDepositWeight,
            twoDepositsWeight,
            threeDepositsWeight,
            minDepositReserve,
            maxDepositReserve,
            biomeNoiseScale);
    }
}
