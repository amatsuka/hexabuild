using Game.Grid;
using UnityEngine;

namespace Game.Core
{
    /// <summary>Только числа баланса из раздела 5 спеки. Цвета и параметры камеры сюда не переезжают.</summary>
    [CreateAssetMenu(fileName = "GameConfig", menuName = "Hex Colony/Game Config")]
    public sealed class GameConfig : ScriptableObject
    {
        [Header("Поле")]
        [SerializeField] int fieldRadius = 6;
        [SerializeField] int seed;

        [Header("Генерация месторождений")]
        [SerializeField] float emptyWeight = 30f;
        [SerializeField] float singleDepositWeight = 45f;
        [SerializeField] float twoDepositsWeight = 20f;
        [SerializeField] float threeDepositsWeight = 5f;
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

        [Header("Merge")]
        [SerializeField] int smallMergeCount = 3;
        [SerializeField] int smallMergePoints = 10;
        [SerializeField] int largeMergeCount = 5;
        [SerializeField] int largeMergePoints = 25;

        [Header("Старт партии")]
        [SerializeField] int startingPoints = 40;
        [SerializeField] int startingGravel = 3;

        public int FieldRadius => fieldRadius;

        public int Seed => seed;

        public float ExtractionInterval => extractionInterval;

        public float DeliverySecondsPerTile => deliverySecondsPerTile;

        public int StorageSize => storageSize;

        public int TileOpenCost => tileOpenCost;

        public int RoadCost => roadCost;

        public int SmallMergeCount => smallMergeCount;

        public int SmallMergePoints => smallMergePoints;

        public int LargeMergeCount => largeMergeCount;

        public int LargeMergePoints => largeMergePoints;

        public int StartingPoints => startingPoints;

        public int StartingGravel => startingGravel;

        public MapGenerationSettings MapGenerationSettings => new(
            fieldRadius,
            seed,
            emptyWeight,
            singleDepositWeight,
            twoDepositsWeight,
            threeDepositsWeight,
            minDepositReserve,
            maxDepositReserve);
    }
}
