using System.Globalization;

namespace Game.UI
{
    /// <summary>Числа HUD в том виде, в каком их читают: тысячи разделены, секунды с буквой.</summary>
    public static class HudFormat
    {
        /// <summary>
        /// Счёт группами по три: «3 240». Разделитель — обычный пробел, а не узкий: узкого
        /// пробела может не оказаться в глифах SDF-атласа, и число рассыпалось бы на квадратики.
        /// </summary>
        public static string Points(int value)
        {
            var negative = value < 0;
            var digits = (negative ? -(long)value : value).ToString();

            var text = string.Empty;
            for (var i = 0; i < digits.Length; i++)
            {
                if (i > 0 && (digits.Length - i) % 3 == 0)
                    text += ' ';

                text += digits[i];
            }

            return negative ? "−" + text : text;
        }

        /// <summary>Прибавка со знаком: «+30».</summary>
        public static string Gain(int value) => value < 0 ? "−" + Points(-value) : "+" + Points(value);

        /// <summary>Остаток времени контракта, целыми секундами вверх.</summary>
        public static string Seconds(int value) => value + " с";

        /// <summary>Множитель с двумя знаками: «×1.35». Точка, а не запятая: так он записан в плане и на плашке.</summary>
        public static string Multiplier(float value) => "×" + value.ToString("0.00", CultureInfo.InvariantCulture);

        /// <summary>Надбавка к множителю со знаком: «+0.25».</summary>
        public static string Bonus(float value) =>
            (value < 0f ? "−" : "+") + System.Math.Abs(value).ToString("0.00", CultureInfo.InvariantCulture);
    }
}
