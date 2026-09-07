using Game.UI;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>Числа HUD: группы тысяч, знак прибавки и отсчёт секунд.</summary>
    public sealed class HudFormatTests
    {
        [TestCase(0, "0")]
        [TestCase(7, "7")]
        [TestCase(940, "940")]
        [TestCase(1000, "1 000")]
        [TestCase(3240, "3 240")]
        [TestCase(1234567, "1 234 567")]
        [TestCase(-4074, "−4 074")]
        public void Points_AreSplitIntoThousands(int value, string expected) =>
            Assert.That(HudFormat.Points(value), Is.EqualTo(expected));

        [TestCase(30, "+30")]
        [TestCase(0, "+0")]
        [TestCase(2115, "+2 115")]
        [TestCase(-5, "−5")]
        public void Gain_CarriesItsSign(int value, string expected) =>
            Assert.That(HudFormat.Gain(value), Is.EqualTo(expected));

        [Test]
        public void Seconds_AreSpelledWithTheLetter() => Assert.That(HudFormat.Seconds(45), Is.EqualTo("45 с"));

        [TestCase(1f, "×1.00")]
        [TestCase(1.35f, "×1.35")]
        [TestCase(6f, "×6.00")]
        public void Multiplier_HasTwoDecimalsAndADot(float value, string expected) =>
            Assert.That(HudFormat.Multiplier(value), Is.EqualTo(expected));

        [TestCase(0f, "+0.00")]
        [TestCase(0.25f, "+0.25")]
        [TestCase(-0.25f, "−0.25")]
        public void Bonus_CarriesItsSign(float value, string expected) =>
            Assert.That(HudFormat.Bonus(value), Is.EqualTo(expected));

        /// <summary>
        /// Вниз до целого: на 99.6% бар ещё не рекорд, и подпись «100%» спорила бы с ним самим.
        /// За 100% процент растёт дальше — этим и показан выход за потолок.
        /// </summary>
        [TestCase(0f, "0%")]
        [TestCase(0.25f, "25%")]
        [TestCase(0.786f, "78%")]
        [TestCase(0.996f, "99%")]
        [TestCase(1f, "100%")]
        [TestCase(1.124f, "112%")]
        [TestCase(-0.3f, "0%")]
        public void Percent_IsFlooredToWholes(float share, string expected) =>
            Assert.That(HudFormat.Percent(share), Is.EqualTo(expected));
    }
}
