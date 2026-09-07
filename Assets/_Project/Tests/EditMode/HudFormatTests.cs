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
    }
}
