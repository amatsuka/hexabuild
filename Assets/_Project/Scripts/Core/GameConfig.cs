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
        [SerializeField] int fieldRows = 10;
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
        [SerializeField] int storageSize = 25;

        [Header("Стоимости")]
        [SerializeField] int tileOpenCost = 20;
        [SerializeField] int roadCost = 1;

        [Header("Старт партии")]
        [SerializeField] int startingPoints = 40;
        [SerializeField] int startingGravel = 3;

        public int FieldRows => fieldRows;

        public int Seed => seed;

        public float ExtractionInterval => extractionInterval;

        public float DeliverySecondsPerTile => deliverySecondsPerTile;

        public int StorageSize => storageSize;

        public int TileOpenCost => tileOpenCost;

        public int RoadCost => roadCost;

        public int StartingPoints => startingPoints;

        public int StartingGravel => startingGravel;

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
