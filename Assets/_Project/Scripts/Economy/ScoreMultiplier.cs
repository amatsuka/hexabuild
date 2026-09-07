using System;

namespace Game.Economy
{
    /// <summary>
    /// Множитель очков партии: колония плюс серия. Колония растёт с каждой открытой игроком
    /// плиткой — `1 + шаг × открытых`, — серия прибавляет ступень за каждый закрытый подряд
    /// контракт до потолка, а провал снимает одну ступень, не всю серию. Итог — одно число:
    /// оно применяется ко всему, что приносит очки (обмен крафта и награда контракта), и оно же
    /// стоит на плашке прибавки. Стартовые очки, бонусы за пройденное поле и штраф за потерю
    /// под множитель не попадают.
    /// </summary>
    public sealed class ScoreMultiplier
    {
        /// <summary>
        /// Поправка перед округлением вниз: 20 × 1.15 в плавающей точке — это 22.999…,
        /// а игрок считает 23.
        /// </summary>
        const double Epsilon = 1e-6;

        public ScoreMultiplier(float colonyStep, float streakStep, float streakMax)
        {
            ColonyStep = colonyStep;
            StreakStep = streakStep;
            StreakMax = Math.Max(streakMax, 0f);
        }

        /// <summary>Колония или серия изменились: карточкам пора пересчитать числа.</summary>
        public event Action Changed;

        /// <summary>Сколько прибавляет к множителю каждая открытая плитка.</summary>
        public float ColonyStep { get; }

        /// <summary>Ступень серии: столько даёт закрытый контракт и столько снимает провал.</summary>
        public float StreakStep { get; }

        /// <summary>Потолок надбавки серии.</summary>
        public float StreakMax { get; }

        /// <summary>Сколько плиток открыл игрок: столько ступеней и у колонии.</summary>
        public int OpenedTiles { get; private set; }

        /// <summary>Надбавка серии: от нуля до <see cref="StreakMax"/>.</summary>
        public float Streak { get; private set; }

        public float Colony => 1f + ColonyStep * OpenedTiles;

        /// <summary>Итоговый множитель: колония плюс серия.</summary>
        public float Total => Colony + Streak;

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

        void SetStreak(float value)
        {
            if (Math.Abs(value - Streak) < 1e-6f)
                return;

            Streak = value;
            Changed?.Invoke();
        }
    }
}
