using Game.Economy;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public sealed class ScoreMultiplierTests
    {
        /// <summary>Множитель кампании без накала: колония и серия проверяются им.</summary>
        static ScoreMultiplier Course() => new(0.05f, 0.25f, 2f, 0f, 0f, 0f, 1f);

        /// <summary>
        /// Он же с накалом по числам `GameConfig`: ступень 0.05, потолок +1.0, пауза 2.5 с,
        /// полная утечка за 2 с.
        /// </summary>
        static ScoreMultiplier Hot() => new(0.05f, 0.25f, 2f, 0.05f, 1f, 2.5f, 2f);

        [Test]
        public void FreshMultiplier_IsOne()
        {
            var multiplier = Course();
            Assert.AreEqual(1f, multiplier.Colony);
            Assert.AreEqual(0f, multiplier.Streak);
            Assert.AreEqual(1f, multiplier.Total);
            Assert.AreEqual(15, multiplier.Apply(15));
        }

        [Test]
        public void Colony_GrowsByAStepPerOpenedTile()
        {
            var multiplier = Course();
            multiplier.TrackOpened(60);
            Assert.AreEqual(4f, multiplier.Colony, 1e-5f, "×4 на 60-й плитке");
            Assert.AreEqual(60, multiplier.Apply(15));
            Assert.AreEqual(160, multiplier.Apply(40));
        }

        [Test]
        public void Streak_ClimbsToTheCapAndDropsOneStepAtATime()
        {
            var multiplier = Course();
            for (var i = 0; i < 20; i++)
                multiplier.ExtendStreak();
            Assert.AreEqual(2f, multiplier.Streak, 1e-6f);

            multiplier.BreakStreak();
            Assert.AreEqual(1.75f, multiplier.Streak, 1e-6f);

            for (var i = 0; i < 20; i++)
                multiplier.BreakStreak();
            Assert.AreEqual(0f, multiplier.Streak);
        }

        [Test]
        public void Total_IsColonyPlusStreak_AndAppliesToEverything()
        {
            var multiplier = Course();
            multiplier.TrackOpened(7);
            multiplier.ExtendStreak();

            Assert.AreEqual(1.6f, multiplier.Total, 1e-5f);
            Assert.AreEqual(24, multiplier.Apply(15));
            Assert.AreEqual(64, multiplier.Apply(40));
        }

        [Test]
        public void Apply_RoundsDown_ButSurvivesFloatingPoint()
        {
            var multiplier = Course();
            multiplier.TrackOpened(3);
            Assert.AreEqual(1.15f, multiplier.Total, 1e-5f);
            Assert.AreEqual(23, multiplier.Apply(20), "20 × 1.15 — это 23, а не 22.999…");
            Assert.AreEqual(17, multiplier.Apply(15), "15 × 1.15 = 17.25 → 17");
        }

        [Test]
        public void Changed_FiresOnRealChangesOnly()
        {
            var multiplier = Course();
            var changes = 0;
            multiplier.Changed += () => changes++;

            multiplier.TrackOpened(0);
            multiplier.BreakStreak();
            Assert.AreEqual(0, changes, "ничего не поменялось — события нет");

            multiplier.TrackOpened(1);
            multiplier.ExtendStreak();
            Assert.AreEqual(2, changes);
        }

        [Test]
        public void Heat_ClimbsAStepPerWarehouseActionUpToTheCap()
        {
            var multiplier = Hot();
            for (var i = 0; i < 5; i++)
                multiplier.Bump();
            Assert.AreEqual(0.25f, multiplier.Heat, 1e-5f);
            Assert.AreEqual(1.25f, multiplier.Total, 1e-5f, "накал — третье слагаемое к колонии и серии");

            for (var i = 0; i < 100; i++)
                multiplier.Bump();
            Assert.AreEqual(1f, multiplier.Heat, 1e-5f, "выше потолка накал не растёт");
            Assert.IsTrue(multiplier.HeatAtMax);
        }

        [Test]
        public void Heat_HoldsForThePauseAndThenLeaksToZero()
        {
            var multiplier = Hot();
            for (var i = 0; i < 20; i++)
                multiplier.Bump();

            multiplier.Tick(2.4f);
            Assert.AreEqual(1f, multiplier.Heat, 1e-5f, "пауза ещё не вышла — накал держится");
            Assert.IsFalse(multiplier.HeatLeaking);

            multiplier.Tick(0.2f);
            Assert.IsTrue(multiplier.HeatLeaking, "пауза вышла — накал потёк");
            Assert.Less(multiplier.Heat, 1f);

            multiplier.Tick(2f);
            Assert.AreEqual(0f, multiplier.Heat, 1e-5f, "полная утечка съедает потолок за две секунды");
        }

        [Test]
        public void Bump_RestartsThePause()
        {
            var multiplier = Hot();
            multiplier.Bump();
            multiplier.Tick(2.4f);
            Assert.AreEqual(0.04f, multiplier.HoldShare, 1e-2f, "пауза почти вышла");

            multiplier.Bump();
            Assert.AreEqual(1f, multiplier.HoldShare, 1e-5f, "действие отсчитывает паузу заново");
        }

        [Test]
        public void Burn_TakesTheWholeHeatAtOnce()
        {
            var multiplier = Hot();
            for (var i = 0; i < 20; i++)
                multiplier.Bump();

            multiplier.Burn();

            Assert.AreEqual(0f, multiplier.Heat, 1e-5f, "переполнение сжигает накал целиком");
            Assert.AreEqual(0f, multiplier.HoldShare, 1e-5f, "и паузу вместе с ним: копить придётся заново");
        }

        [Test]
        public void Burn_ReportsTheMomentOnceWhenThereWasHeat()
        {
            var multiplier = Hot();
            var burned = 0;
            multiplier.Burned += () => burned++;

            multiplier.Bump();
            multiplier.Burn();

            Assert.AreEqual(1, burned, "сгорел накал — у момента есть свой кадр");
        }

        [Test]
        public void Burn_OnColdStorage_ReportsNothing()
        {
            var multiplier = Hot();
            var burned = 0;
            multiplier.Burned += () => burned++;

            multiplier.Burn();
            multiplier.Burn();

            Assert.AreEqual(0, burned, "гореть было нечему: переполнение на холодном складе видно и без вспышки");
        }

        [Test]
        public void HeatShare_IsTheShareOfTheCap()
        {
            var multiplier = Hot();
            for (var i = 0; i < 10; i++)
                multiplier.Bump();
            Assert.AreEqual(0.5f, multiplier.HeatShare, 1e-5f);
        }

        [Test]
        public void ZeroHeat_LeavesTheMultiplierAsItWas()
        {
            var multiplier = Course();
            multiplier.Bump();
            multiplier.Tick(10f);

            Assert.AreEqual(0f, multiplier.Heat);
            Assert.AreEqual(1f, multiplier.Total, "накал выключен — множитель тот же, что до M29");
        }
    }
}
