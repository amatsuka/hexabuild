using Game.Economy;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Game.UI
{
    /// <summary>
    /// Пульс виньетки на высоком накале: кадр слегка дышит по краям, пока склад разогрет, и
    /// перестаёт, как только накал упал. Это единственный эффект стадии, который выходит за
    /// пределы карточки склада, — камерой поле не трогаем, игрок сам его панит.
    ///
    /// Профиль заводится в памяти и своим `Volume` поверх сценного: править общий
    /// `GameVolumeProfile` из игры нельзя — в редакторе правка ушла бы прямо в ассет. Ловушка
    /// про пустые ссылки `VolumeProfile.Add` этого не касается: она про профиль, сохраняемый
    /// в ассет, а этот живёт ровно столько, сколько партия. Свой `Volume` вешается на
    /// собственный объект, а не на тот, где висит сценный: два тома на одном объекте
    /// неразличимы для `GetComponent`, и разбираться, какой из них чей, пришлось бы каждому,
    /// кто сюда заглянет.
    /// </summary>
    public sealed class HeatVignette : MonoBehaviour
    {
        [Tooltip("С какой доли накала виньетка начинает дышать. Ниже — её нет вовсе")]
        [SerializeField, Range(0f, 1f)] float threshold = 0.6f;

        [Tooltip("Насколько глубока виньетка на полном накале")]
        [SerializeField, Range(0f, 1f)] float maxIntensity = 0.28f;

        [Tooltip("Насколько глубина гуляет вокруг своего значения")]
        [SerializeField, Range(0f, 1f)] float pulseDepth = 0.35f;

        [Tooltip("Вдохов в секунду")]
        [SerializeField] float pulseSpeed = 2.2f;

        [Tooltip("Цвет виньетки: тёплый, тем же семейством, что нагрев рамки склада")]
        [SerializeField] Color tint = new(0.32f, 0.10f, 0.02f, 1f);

        [Tooltip("Приоритет тома: выше сценного, иначе поверх него ничего не ляжет")]
        [SerializeField] float priority = 10f;

        ScoreMultiplier multiplier;
        GameObject holder;
        Volume volume;
        Vignette vignette;
        VolumeProfile profile;
        float phase;

        public void Bind(ScoreMultiplier score)
        {
            multiplier = score;

            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            vignette = profile.Add<Vignette>();
            vignette.color.overrideState = true;
            vignette.color.value = tint;
            vignette.intensity.overrideState = true;
            vignette.intensity.value = 0f;
            vignette.smoothness.overrideState = true;
            vignette.smoothness.value = 0.6f;

            holder = new GameObject("Heat Vignette");
            holder.transform.SetParent(transform, false);

            volume = holder.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = priority;
            volume.profile = profile;
            volume.weight = 0f;
        }

        void Update()
        {
            if (multiplier == null)
                return;

            // Доля выше порога, растянутая обратно на 0..1: на пороге эффекта ещё нет, на
            // потолке он полный. Иначе виньетка включалась бы ступенькой.
            var over = Mathf.InverseLerp(threshold, 1f, multiplier.HeatShare);
            if (over <= 0f)
            {
                volume.weight = 0f;
                phase = 0f;
                return;
            }

            phase += Time.deltaTime * pulseSpeed;
            var breath = 1f - pulseDepth * (0.5f - 0.5f * Mathf.Cos(phase * Mathf.PI * 2f));

            volume.weight = 1f;
            vignette.intensity.value = maxIntensity * over * breath;
        }

        void OnDestroy()
        {
            if (profile != null)
                Destroy(profile);

            if (holder != null)
                Destroy(holder);
        }
    }
}
