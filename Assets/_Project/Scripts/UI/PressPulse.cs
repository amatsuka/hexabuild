using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Отклик объекта на палец: под нажатием он проседает или ужимается, на отпускании
    /// возвращается пружиной, на отказе коротко дрожит поперёк. Компонент навешивается лениво,
    /// первым же нажатием: на поле шестьдесят плиток, а нажата всегда одна, и держать
    /// шестьдесят тикающих компонентов ради неё незачем. Дойдя до покоя, компонент гасит сам
    /// себя — выключенный `Update` Unity не зовёт.
    /// </summary>
    public sealed class PressPulse : MonoBehaviour
    {
        /// <summary>За сколько объект успевает просесть под пальцем.</summary>
        const float DownSeconds = 0.07f;

        /// <summary>За сколько он возвращается. Дольше просадки: это пружина, а не щелчок.</summary>
        const float UpSeconds = 0.16f;

        const float ShakeSeconds = 0.22f;

        /// <summary>Насколько карточка интерфейса ужимается под пальцем.</summary>
        const float CardSqueeze = 0.05f;

        /// <summary>
        /// Во что карточка темнеет. Красится вершинный цвет, то есть шейдер домножает на него
        /// разом и градиент, и кромку, и свечение — тот же приём, что у вспышки потери склада.
        /// </summary>
        static readonly Color CardTint = new(0.80f, 0.80f, 0.80f, 1f);

        /// <summary>
        /// Насколько прижатым считается объект, отпущенный мгновенно. Тап короче кадра-двух
        /// снимается раньше, чем просадка успела набраться, и без нижней границы самое частое
        /// действие в игре не давало бы вообще никакого отклика.
        /// </summary>
        const float MinRelease = 0.6f;

        Graphic tinted;
        Color pressedTint;
        Color restTint;

        Vector3 restPosition;
        Vector3 restScale;

        float squeeze;
        float sink;
        float shakeWidth;

        bool held;
        float press;
        float releaseFrom;
        float releaseTime = -1f;
        float shakeTime = -1f;

        /// <summary>
        /// Прижать карточку интерфейса: она ужимается и темнеет. Форма отклика одна на все
        /// карточки — кнопки, шестерёнку, клетки склада: разные числа читались бы как разные
        /// по отзывчивости элементы, чего в интерфейсе нет.
        /// </summary>
        public static void HoldCard(Graphic card)
        {
            var pulse = Of(card);
            pulse.tinted = card;
            pulse.pressedTint = CardTint;
            pulse.squeeze = CardSqueeze;
            pulse.sink = 0f;
            pulse.Hold();
        }

        /// <summary>Прижать объект сцены: он проседает по высоте на <paramref name="sink"/> юнитов.</summary>
        public static void HoldSolid(Component target, float sink)
        {
            var pulse = Of(target);
            pulse.squeeze = 0f;
            pulse.sink = sink;
            pulse.Hold();
        }

        /// <summary>Отпустить. Объект, которого не держали, молчит.</summary>
        public static void Release(Component target)
        {
            var pulse = target.GetComponent<PressPulse>();
            if (pulse == null || !pulse.held)
                return;

            pulse.held = false;
            pulse.releaseFrom = Mathf.Max(pulse.press, MinRelease);
            pulse.releaseTime = 0f;
        }

        /// <summary>
        /// Отказ: объект дрожит поперёк. Ширина — в единицах его родителя, то есть в юнитах
        /// на поле и в пикселях канваса в интерфейсе.
        /// </summary>
        public static void ShakeSideways(Component target, float width)
        {
            var pulse = Of(target);
            pulse.shakeWidth = width;
            pulse.shakeTime = 0f;
            pulse.Wake();
        }

        /// <summary>
        /// Оборвать отклик и вернуть покой в этом же кадре. Нужно там, где объект тут же
        /// забирает себе другая анимация: масштаб клетки склада на слиянии, высота плитки
        /// на открытии — иначе затухающая пружина дорисовывала бы поверх них своё.
        /// </summary>
        /// <summary>
        /// Нажатие уже ведёт цвет и положение этого объекта. Спрашивают те, кто красит те же
        /// карточки по своему поводу: пока пружина не улеглась, она пишет цвет каждый кадр,
        /// и второй хозяин цвета дал бы мерцание, а не два эффекта.
        /// </summary>
        public static bool IsBusy(Component target) =>
            target != null && target.TryGetComponent<PressPulse>(out var pulse) && pulse.enabled;

        public static void Cancel(Component target)
        {
            var pulse = target.GetComponent<PressPulse>();
            if (pulse == null || !pulse.enabled)
                return;

            pulse.held = false;
            pulse.press = 0f;
            pulse.releaseTime = -1f;
            pulse.shakeTime = -1f;
            pulse.Settle();
        }

        static PressPulse Of(Component target)
        {
            var pulse = target.GetComponent<PressPulse>();
            if (pulse != null)
                return pulse;

            pulse = target.gameObject.AddComponent<PressPulse>();
            // Свежий компонент включён по умолчанию. Гасим сразу: покой он запоминает на входе
            // в работу, а до первого нажатия ему нечего делать.
            pulse.enabled = false;
            return pulse;
        }

        void Hold()
        {
            Wake();
            held = true;
            releaseTime = -1f;
        }

        /// <summary>Войти в работу, запомнив покой. Уже работающий помнит его с прошлого раза.</summary>
        void Wake()
        {
            if (enabled)
                return;

            restPosition = transform.localPosition;
            restScale = transform.localScale;
            if (tinted != null)
                restTint = tinted.color;

            enabled = true;
        }

        void Update()
        {
            if (held)
            {
                press = Mathf.MoveTowards(press, 1f, Time.deltaTime / DownSeconds);
            }
            else if (releaseTime >= 0f)
            {
                releaseTime += Time.deltaTime;
                var progress = Mathf.Clamp01(releaseTime / UpSeconds);

                // Пружина: `OutBack` перелетает за единицу, доля прижатия уходит в минус, и
                // объект на мгновение оказывается чуть выше и крупнее покоя.
                press = releaseFrom * (1f - Anim.OutBack(progress));
                if (progress >= 1f)
                {
                    press = 0f;
                    releaseTime = -1f;
                }
            }

            var shift = 0f;
            if (shakeTime >= 0f)
            {
                shakeTime += Time.deltaTime;
                var progress = Mathf.Clamp01(shakeTime / ShakeSeconds);
                shift = Anim.Shake(progress) * shakeWidth;
                if (progress >= 1f)
                    shakeTime = -1f;
            }

            transform.localScale = restScale * (1f - squeeze * press);
            transform.localPosition = restPosition + new Vector3(shift, -sink * press, 0f);
            if (tinted != null)
                tinted.color = Color.Lerp(restTint, pressedTint, press);

            if (!held && releaseTime < 0f && shakeTime < 0f)
                Settle();
        }

        /// <summary>Вернуть точные числа покоя и замолчать до следующего нажатия.</summary>
        void Settle()
        {
            transform.localScale = restScale;
            transform.localPosition = restPosition;
            if (tinted != null)
                tinted.color = restTint;

            enabled = false;
        }
    }
}
