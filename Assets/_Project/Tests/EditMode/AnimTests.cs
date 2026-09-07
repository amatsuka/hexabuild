using Game.UI;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// Кривые отклика. Проверяются ровно те их свойства, на которых стоят вызывающие: концы
    /// закреплены (иначе объект не вернётся точно в покой), пружина перелетает за цель, подскок
    /// резче падения, тряска затухает и уходит в обе стороны.
    /// </summary>
    public sealed class AnimTests
    {
        [Test]
        public void OutBack_IsPinnedAtBothEnds()
        {
            Assert.That(Anim.OutBack(0f), Is.EqualTo(0f).Within(1e-5f));
            Assert.That(Anim.OutBack(1f), Is.EqualTo(1f).Within(1e-5f));
        }

        [Test]
        public void OutBack_OvershootsItsTarget() =>
            Assert.That(Peak(Anim.OutBack), Is.GreaterThan(1.05f));

        [Test]
        public void OutBack_ClampsBeyondItsRange()
        {
            Assert.That(Anim.OutBack(-1f), Is.EqualTo(0f).Within(1e-5f));
            Assert.That(Anim.OutBack(2f), Is.EqualTo(1f).Within(1e-5f));
        }

        [Test]
        public void Hop_StartsAndLandsAtZero()
        {
            Assert.That(Anim.Hop(0f), Is.EqualTo(0f).Within(1e-5f));
            Assert.That(Anim.Hop(1f), Is.EqualTo(0f).Within(1e-5f));
        }

        [Test]
        public void Hop_ReachesFullHeight() => Assert.That(Peak(Anim.Hop), Is.EqualTo(1f).Within(1e-3f));

        /// <summary>Выброс резче посадки: пик стоит в первой половине, иначе это качание, а не рывок.</summary>
        [Test]
        public void Hop_PeaksEarly() => Assert.That(PeakAt(Anim.Hop), Is.LessThan(0.4f));

        [Test]
        public void Shake_StartsAndEndsStill()
        {
            Assert.That(Anim.Shake(0f), Is.EqualTo(0f).Within(1e-5f));
            Assert.That(Anim.Shake(1f), Is.EqualTo(0f).Within(1e-5f));
        }

        [Test]
        public void Shake_SwingsBothWays()
        {
            var lowest = 0f;
            var highest = 0f;
            for (var step = 0; step <= 1000; step++)
            {
                var value = Anim.Shake(step / 1000f);
                lowest = Mathf.Min(lowest, value);
                highest = Mathf.Max(highest, value);
            }

            Assert.That(highest, Is.GreaterThan(0.5f));
            Assert.That(lowest, Is.LessThan(-0.3f));
        }

        /// <summary>Амплитуда затухает: второй взмах слабее первого, и так до конца.</summary>
        [Test]
        public void Shake_FadesOut()
        {
            for (var step = 0; step <= 1000; step++)
            {
                var progress = step / 1000f;
                Assert.That(Mathf.Abs(Anim.Shake(progress)), Is.LessThanOrEqualTo(1f - progress + 1e-5f));
            }
        }

        static float Peak(System.Func<float, float> curve)
        {
            var highest = float.MinValue;
            for (var step = 0; step <= 1000; step++)
                highest = Mathf.Max(highest, curve(step / 1000f));

            return highest;
        }

        static float PeakAt(System.Func<float, float> curve)
        {
            var highest = float.MinValue;
            var at = 0f;
            for (var step = 0; step <= 1000; step++)
            {
                var progress = step / 1000f;
                var value = curve(progress);
                if (value <= highest)
                    continue;

                highest = value;
                at = progress;
            }

            return at;
        }
    }
}
