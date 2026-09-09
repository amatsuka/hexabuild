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
        [Tooltip("До скольких занятых клеток надо разгрести склад, чтобы он считался чистым")]
        [SerializeField] int sweepCells = 2;
        [Tooltip("Базовая премия за чистый склад на полном накале. Идёт через множитель")]
        [SerializeField] int sweepBonus = 60;

        [Header("Стоимости")]
        [SerializeField] int tileOpenCost = 20;
        [Tooltip("Во сколько раз дорожает открытие с каждой открытой плиткой")]
        [SerializeField] float openCostGrowth = 1.04f;
        [SerializeField] int roadCost = 1;
        [Tooltip("Полная цена дороги на плитке с рекой: щебень")]
        [SerializeField] int bridgeGravel = 2;
        [Tooltip("Та же цена моста: доски. Единственное, за что доски платят помимо обмена на очки")]
        [SerializeField] int bridgeBoards = 2;

        [Header("Множитель очков")]
        [Tooltip("Сколько прибавляет к множителю каждая открытая плитка")]
        [SerializeField] float multiplierStep = 0.05f;
        [Tooltip("Ступень серии за каждый закрытый подряд контракт; провал снимает одну ступень")]
        [SerializeField] float streakStep = 0.25f;
        [Tooltip("Потолок надбавки серии")]
        [SerializeField] float streakMax = 2f;
        [Tooltip("Ступень накала: столько даёт одно действие на складе — мерж или обмен")]
        [SerializeField] float heatStep = 0.05f;
        [Tooltip("Потолок надбавки накала")]
        [SerializeField] float heatMax = 1f;
        [Tooltip("Сколько накал держится после последнего действия, прежде чем потечь")]
        [SerializeField] float heatHoldSeconds = 2.5f;
        [Tooltip("За сколько секунд утечка съедает полный накал")]
        [SerializeField] float heatDrainSeconds = 2f;

        [Header("Старт партии")]
        [SerializeField] int startingPoints = 40;
        [SerializeField] int startingGravel = 3;
        [Tooltip("Стартовые доски: на первый мост, пока лес ещё не подключён")]
        [SerializeField] int startingBoards = 3;

        [Header("Контракты")]
        [Tooltip("Сколько крафтовых ресурсов одного типа просит контракт")]
        [SerializeField] int contractGoal = 3;
        [SerializeField] float contractSeconds = 45f;
        [Tooltip("Награда сверх обычных очков за обмен")]
        [SerializeField] int contractReward = 120;
        [Tooltip("Сколько Метрополия молчит между контрактами: случайно в этих границах")]
        [SerializeField] float contractPauseMin = 5f;
        [SerializeField] float contractPauseMax = 20f;

        [Header("Вехи")]
        [Tooltip("Доли потолка, на которых партия выдаёт награду за веху. Шкала звёзд своя")]
        [SerializeField] float[] milestoneShares = { 0.25f, 0.5f, 0.75f };
        [Tooltip("Сколько щебня даёт пройденная веха")]
        [SerializeField] int milestoneGravel = 3;

        [Header("Продажа пачкой")]
        [Tooltip("Через сколько секунд после нажатия кнопка «Продать всё» приходит снова, минимум")]
        [SerializeField] float sellPauseMin = 10f;
        [Tooltip("Тот же приход, максимум: интервал берётся жребием между ними")]
        [SerializeField] float sellPauseMax = 20f;
        [Tooltip("Сколько щебня кнопка не продаёт: на дорогу и мост")]
        [SerializeField] int sellReserveGravel = 4;
        [Tooltip("Сколько досок кнопка не продаёт")]
        [SerializeField] int sellReserveBoards = 4;

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

        /// <summary>До скольких клеток надо разгрести склад, чтобы он считался чистым.</summary>
        public int SweepCells => sweepCells;

        /// <summary>Базовая премия за чистый склад: множитель ложится на неё сверху.</summary>
        public int SweepBonus => sweepBonus;

        public int StartingPoints => startingPoints;

        public int StartingGravel => startingGravel;

        public int StartingBoards => startingBoards;

        public int ContractGoal => contractGoal;

        public float ContractSeconds => contractSeconds;

        public int ContractReward => contractReward;

        public float ContractPauseMin => contractPauseMin;

        public float ContractPauseMax => contractPauseMax;

        public float SellPauseMin => sellPauseMin;

        public float SellPauseMax => sellPauseMax;

        public int SellReserveGravel => sellReserveGravel;

        public int SellReserveBoards => sellReserveBoards;

        public int LossPenalty => lossPenalty;

        public int FullFieldBonus => fullFieldBonus;

        public int FullDepositBonus => fullDepositBonus;

        public float MultiplierStep => multiplierStep;

        public float StreakStep => streakStep;

        public float StreakMax => streakMax;

        public float HeatStep => heatStep;

        public float HeatMax => heatMax;

        public float HeatHoldSeconds => heatHoldSeconds;

        public float HeatDrainSeconds => heatDrainSeconds;

        public PriceSettings Prices => new(tileOpenCost, openCostGrowth, roadCost, bridgeGravel, bridgeBoards);

        /// <summary>Сколько щебня приносит пройденная веха.</summary>
        public int MilestoneGravel => milestoneGravel;

        /// <summary>Множитель очков на партию: свой у каждой, как кошелёк.</summary>
        public ScoreMultiplier NewMultiplier() =>
            new(multiplierStep, streakStep, streakMax, heatStep, heatMax, heatHoldSeconds, heatDrainSeconds);

        /// <summary>
        /// Вехи на партию: доли отсюда, потолок — от уровня кампании или от бота, прогнанного
        /// на старте. Свои у каждой партии по той же причине, что и множитель: они помнят
        /// пройденное.
        /// </summary>
        public Milestones NewMilestones(int ceiling) => new(milestoneShares, ceiling);

        public MapGenerationSettings MapGenerationSettings => MapGenerationSettingsFor(seed);

        /// <summary>
        /// Числа, которые переопределяет уровень кампании. Ноль в уровне значит «взять
        /// дефолт»: ни рядов, ни секунд, ни цели, ни интервала со значением ноль не бывает.
        /// Вне кампании уровня нет, и все три отдают дефолт.
        /// </summary>
        public int ContractGoalFor(LevelConfig level) =>
            level != null && level.ContractGoal > 0 ? level.ContractGoal : contractGoal;

        public float ContractSecondsFor(LevelConfig level) =>
            level != null && level.ContractSeconds > 0f ? level.ContractSeconds : contractSeconds;

        public float ExtractionIntervalFor(LevelConfig level) =>
            level != null && level.ExtractionInterval > 0f ? level.ExtractionInterval : extractionInterval;

        /// <summary>
        /// Та же генерация своим сидом. Нужна кнопкам финального экрана: «Повторить карту»
        /// перезапускает партию с сидом прошлой, а «Новая карта» — со свежим, и оба они не
        /// вправе править ассет конфига. Уровень кампании накладывает свои рычаги поверх дефолтов.
        /// </summary>
        public MapGenerationSettings MapGenerationSettingsFor(int mapSeed, LevelConfig level = null) => new(
            level != null && level.FieldRows > 0 ? level.FieldRows : fieldRows,
            mapSeed,
            emptyWeight,
            singleDepositWeight,
            twoDepositsWeight,
            threeDepositsWeight,
            minDepositReserve,
            maxDepositReserve,
            reserveRowGrowth,
            biomeNoiseScale,
            level != null ? level.ReserveScale : 1f,
            level != null ? level.WaterCeiling : MapGenerator.WaterCeiling,
            level != null ? level.RocksCeiling : MapGenerator.RocksCeiling);
    }
}
