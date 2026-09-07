using System.Collections.Generic;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// Вехи партии: доли потолка с наградой. Проверяется то, на чём они могут сломаться, —
    /// порядок срабатывания, разовость каждой вехи и поведение без потолка.
    /// </summary>
    public sealed class MilestonesTests
    {
        static readonly float[] Course = { 0.25f, 0.5f, 0.75f };

        static Milestones WithLog(int ceiling, out List<float> reached, params float[] shares)
        {
            var milestones = new Milestones(shares, ceiling);
            var log = new List<float>();
            milestones.Reached += log.Add;
            reached = log;
            return milestones;
        }

        [Test]
        public void Report_BelowFirstShare_ReachesNothing()
        {
            var milestones = WithLog(1000, out var reached, Course);

            milestones.Report(249);

            Assert.That(reached, Is.Empty);
            Assert.That(milestones.Passed, Is.Zero);
        }

        [Test]
        public void Report_OnShare_ReachesIt()
        {
            var milestones = WithLog(1000, out var reached, Course);

            milestones.Report(250);

            Assert.That(reached, Is.EqualTo(new[] { 0.25f }));
        }

        [Test]
        public void Report_JumpingOverTwoShares_ReachesBothInOrder()
        {
            var milestones = WithLog(1000, out var reached, Course);

            milestones.Report(600);

            Assert.That(reached, Is.EqualTo(new[] { 0.25f, 0.5f }));
            Assert.That(milestones.Passed, Is.EqualTo(2));
        }

        [Test]
        public void Report_Again_DoesNotPayTheSameShareTwice()
        {
            var milestones = WithLog(1000, out var reached, Course);

            milestones.Report(300);
            milestones.Report(320);

            Assert.That(reached, Is.EqualTo(new[] { 0.25f }));
        }

        /// <summary>Штраф за потерю проседает счёт: пройденная веха от этого не отменяется.</summary>
        [Test]
        public void Report_AfterScoreDropped_DoesNotPayAgain()
        {
            var milestones = WithLog(1000, out var reached, Course);

            milestones.Report(300);
            milestones.Report(200);
            milestones.Report(300);

            Assert.That(reached, Is.EqualTo(new[] { 0.25f }));
        }

        [Test]
        public void Report_PastEveryShare_StopsAtTheLast()
        {
            var milestones = WithLog(1000, out var reached, Course);

            milestones.Report(4000);

            Assert.That(reached, Is.EqualTo(Course));
            Assert.That(milestones.Passed, Is.EqualTo(3));
        }

        [Test]
        public void Shares_ComeSortedRegardlessOfInspectorOrder()
        {
            var milestones = WithLog(1000, out var reached, 0.75f, 0.25f, 0.5f);

            milestones.Report(1000);

            Assert.That(reached, Is.EqualTo(Course));
            Assert.That(milestones.ShareAt(0), Is.EqualTo(0.25f));
        }

        /// <summary>Непозитивная доля сработала бы на нулевом счёте, то есть до первого действия.</summary>
        [Test]
        public void Shares_ThatAreNotPositive_AreDropped()
        {
            var milestones = WithLog(1000, out var reached, 0f, -0.5f, 0.5f);

            Assert.That(milestones.Count, Is.EqualTo(1));

            milestones.Report(0);
            Assert.That(reached, Is.Empty);

            milestones.Report(500);
            Assert.That(reached, Is.EqualTo(new[] { 0.5f }));
        }

        /// <summary>Потолка нет — вех нет: делить счёт не на что.</summary>
        [TestCase(0)]
        [TestCase(-100)]
        public void WithoutCeiling_ThereAreNoMilestones(int ceiling)
        {
            var milestones = WithLog(ceiling, out var reached, Course);

            milestones.Report(100000);

            Assert.That(milestones.Count, Is.Zero);
            Assert.That(reached, Is.Empty);
        }

        [Test]
        public void WithoutShares_ThereAreNoMilestones()
        {
            Assert.That(new Milestones(null, 1000).Count, Is.Zero);
            Assert.That(new Milestones(new float[0], 1000).Count, Is.Zero);
        }

        /// <summary>
        /// Числа курса стоят в самом ассете, а не только в инициализаторе поля: отметки 25 / 50 / 75%
        /// и три щебня за веху. Тест сторожит именно ассет — поле в коде до него не доходит,
        /// если в YAML лежит что-то другое.
        /// </summary>
        [Test]
        public void GameConfig_CarriesTheMilestonesOfTheCourse()
        {
            var config = UnityEditor.AssetDatabase.LoadAssetAtPath<GameConfig>(BalanceBotTests.ConfigPath);
            var milestones = config.NewMilestones(1000);
            var reached = new List<float>();

            milestones.Reached += reached.Add;
            milestones.Report(1000);

            Assert.That(reached, Is.EqualTo(Course));
            Assert.That(config.MilestoneGravel, Is.EqualTo(3));
        }
    }
}
