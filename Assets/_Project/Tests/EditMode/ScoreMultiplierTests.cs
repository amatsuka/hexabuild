using Game.Economy;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public sealed class ScoreMultiplierTests
    {
        static ScoreMultiplier Course() => new(0.05f, 0.25f, 2f);

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
    }
}
