using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// Кривые коротких анимаций отклика. Заведены отдельно затем, что одни и те же три формы —
    /// пружина, подскок и затухающая тряска — нужны и плиткам поля, и клеткам склада, и кнопкам,
    /// а собраны эти объекты в разных местах. Ни твинов, ни корутин здесь нет: это чистые
    /// функции от прогресса 0..1, время считает вызывающий.
    /// </summary>
    public static class Anim
    {
        /// <summary>Насколько сильно пружина уходит за цель. Классические 1.70158 — это ~10%.</summary>
        const float BackOvershoot = 1.70158f;

        /// <summary>Сколько взмахов делает тряска, пока не затухнет.</summary>
        const float ShakeSwings = 3f;

        /// <summary>
        /// 0 → 1 с перелётом за единицу. Так возвращается отпущенное: линейный возврат читается
        /// как «отлипло», перелёт — как «спружинило».
        /// </summary>
        public static float OutBack(float progress)
        {
            var shifted = Mathf.Clamp01(progress) - 1f;
            return 1f + shifted * shifted * ((BackOvershoot + 1f) * shifted + BackOvershoot);
        }

        /// <summary>
        /// 0 → 1 → 0 с пиком в первой четверти: быстрый выброс и мягкая посадка. Симметричный
        /// синус читается вяло — подскок обязан быть резче падения, иначе это не рывок, а качание.
        /// </summary>
        public static float Hop(float progress) =>
            Mathf.Sin(Mathf.Sqrt(Mathf.Clamp01(progress)) * Mathf.PI);

        /// <summary>
        /// Затухающая тряска: <see cref="ShakeSwings"/> взмахов, амплитуда линейно сходит к нулю.
        /// Возвращает долю амплитуды со знаком, то есть от −1 до 1, и точный ноль на концах.
        /// </summary>
        public static float Shake(float progress)
        {
            var time = Mathf.Clamp01(progress);
            return Mathf.Sin(time * Mathf.PI * 2f * ShakeSwings) * (1f - time);
        }
    }
}
