using System;

namespace Game.Economy
{
    /// <summary>
    /// Множитель очков партии: колония, серия контрактов и накал склада. Колония растёт с каждой
    /// открытой игроком плиткой — `1 + шаг × открытых`, — серия прибавляет ступень за каждый
    /// закрытый подряд контракт до потолка, а провал снимает одну ступень, не всю серию. Накал —
    /// третье слагаемое и единственное временнóе: он растёт за каждое действие на складе и утекает
    /// сам, стоит игроку замолчать. Итог — одно число: оно применяется ко всему, что приносит очки
    /// (обмен крафта, награда контракта и премия за чистый склад), и оно же стоит на плашке
    /// прибавки. Стартовые очки, бонусы за пройденное поле и штраф за потерю под множитель
    /// не попадают.
    /// </summary>
    public sealed class ScoreMultiplier
    {
        /// <summary>
        /// Поправка перед округлением вниз: 20 × 1.15 в плавающей точке — это 22.999…,
        /// а игрок считает 23.
        /// </summary>
        const double Epsilon = 1e-6;

        /// <summary>Ниже этого числа изменение никому не видно: два знака после запятой на плашке.</summary>
        const float Quantum = 1e-6f;

        /// <summary>Сколько секунд склад молчит: считается с последнего действия, а не с начала утечки.</summary>
        float silence;

        public ScoreMultiplier(
            float colonyStep, float streakStep, float streakMax,
            float heatStep, float heatMax, float heatHoldSeconds, float heatDrainSeconds)
        {
            ColonyStep = colonyStep;
            StreakStep = streakStep;
            StreakMax = Math.Max(streakMax, 0f);
            HeatStep = heatStep;
            HeatMax = Math.Max(heatMax, 0f);
            HeatHoldSeconds = Math.Max(heatHoldSeconds, 0f);
            HeatDrainSeconds = Math.Max(heatDrainSeconds, Quantum);
        }

        /// <summary>Колония, серия или накал изменились: карточкам пора пересчитать числа.</summary>
        public event Action Changed;

        /// <summary>
        /// Накал сгорел на переполнении, и было чему гореть. Отдельно от <see cref="Changed"/>
        /// затем, что это не «число стало другим», а момент: у него свой кадр — вспышка склада,
        /// провал цвета и удар по камере. Пустой накал события не даёт: переполнение на холодном
        /// складе игрок и так видит по улетевшему ресурсу, и бить его дважды не за что.
        /// </summary>
        public event Action Burned;

        /// <summary>Сколько прибавляет к множителю каждая открытая плитка.</summary>
        public float ColonyStep { get; }

        /// <summary>Ступень серии: столько даёт закрытый контракт и столько снимает провал.</summary>
        public float StreakStep { get; }

        /// <summary>Потолок надбавки серии.</summary>
        public float StreakMax { get; }

        /// <summary>Ступень накала: столько даёт одно действие на складе.</summary>
        public float HeatStep { get; }

        /// <summary>Потолок надбавки накала.</summary>
        public float HeatMax { get; }

        /// <summary>Сколько накал держится после последнего действия, прежде чем потечь.</summary>
        public float HeatHoldSeconds { get; }

        /// <summary>За сколько секунд утечка съедает полный накал.</summary>
        public float HeatDrainSeconds { get; }

        /// <summary>Сколько плиток открыл игрок: столько ступеней и у колонии.</summary>
        public int OpenedTiles { get; private set; }

        /// <summary>Надбавка серии: от нуля до <see cref="StreakMax"/>.</summary>
        public float Streak { get; private set; }

        /// <summary>Надбавка накала: от нуля до <see cref="HeatMax"/>.</summary>
        public float Heat { get; private set; }

        public float Colony => 1f + ColonyStep * OpenedTiles;

        /// <summary>Итоговый множитель: колония плюс серия плюс накал.</summary>
        public float Total => Colony + Streak + Heat;

        /// <summary>
        /// Сколько накала дало последнее действие на складе. Это и есть число на призраке,
        /// который вылетает от клетки: на потолке действие не платит ничего, и ноль здесь —
        /// не отсутствие данных, а честный ответ «эта ставка уже сыграна».
        /// </summary>
        public float LastBump { get; private set; }

        /// <summary>Накал долей от потолка: по ней греется рамка склада.</summary>
        public float HeatShare => HeatMax > 0f ? Heat / HeatMax : 0f;

        /// <summary>Накал на потолке: только тогда чистый склад платит премию.</summary>
        public bool HeatAtMax => HeatMax > 0f && Heat >= HeatMax - Quantum;

        /// <summary>
        /// Сколько осталось от паузы до начала утечки, долей: единица — только что действовал,
        /// ноль — накал уже течёт. Это и есть полоса под складом.
        /// </summary>
        public float HoldShare => HeatHoldSeconds > 0f
            ? Math.Max(0f, 1f - silence / HeatHoldSeconds)
            : 0f;

        /// <summary>Накал есть и он уже утекает: полосе пора погаснуть, а числу — падать.</summary>
        public bool HeatLeaking => Heat > Quantum && silence >= HeatHoldSeconds;

        /// <summary>Очки с множителем: базовые × итоговый, вниз до целого.</summary>
        public int Apply(int basePoints) => (int)Math.Floor(basePoints * (double)Total + Epsilon);

        /// <summary>Игрок открыл плитку: колония считает их у `GameState`, а не сама.</summary>
        public void TrackOpened(int openedTiles)
        {
            if (openedTiles == OpenedTiles)
                return;

            OpenedTiles = openedTiles;
            Changed?.Invoke();
        }

        /// <summary>Контракт закрыт: серия растёт на ступень, но не выше потолка.</summary>
        public void ExtendStreak() => SetStreak(Math.Min(Streak + StreakStep, StreakMax));

        /// <summary>Контракт провален: серия теряет одну ступень, а не всё.</summary>
        public void BreakStreak() => SetStreak(Math.Max(Streak - StreakStep, 0f));

        /// <summary>
        /// Действие на складе — мерж или обмен. Накал растёт на ступень, и пауза отсчитывается
        /// заново. Ступень достаётся следующему действию: очки за это уже посчитаны, как и
        /// у серии контрактов.
        /// </summary>
        public void Bump()
        {
            silence = 0f;
            var grown = Math.Min(Heat + HeatStep, HeatMax);
            LastBump = Math.Max(0f, grown - Heat);
            SetHeat(grown);
        }

        /// <summary>
        /// Склад переполнился: накал сгорает весь. Это и есть цена ставки «придержу до пятёрки» —
        /// потеря ресурса теперь стоит не только его самого.
        /// </summary>
        public void Burn()
        {
            var hadHeat = Heat > Quantum;
            silence = HeatHoldSeconds;
            SetHeat(0f);

            if (hadHeat)
                Burned?.Invoke();
        }

        /// <summary>
        /// Ход времени: пока пауза не вышла, накал держится, дальше течёт. Тикает тот же, кто
        /// тикает добычу и контракты, — партия или бот.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f)
                return;

            silence += deltaTime;
            if (Heat <= 0f || silence < HeatHoldSeconds)
                return;

            SetHeat(Math.Max(0f, Heat - HeatMax * deltaTime / HeatDrainSeconds));
        }

        void SetStreak(float value)
        {
            if (Math.Abs(value - Streak) < Quantum)
                return;

            Streak = value;
            Changed?.Invoke();
        }

        void SetHeat(float value)
        {
            if (Math.Abs(value - Heat) < Quantum)
                return;

            Heat = value;
            Changed?.Invoke();
        }
    }
}
