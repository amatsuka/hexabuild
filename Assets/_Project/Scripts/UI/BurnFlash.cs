using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Game.UI
{
    /// <summary>
    /// Провал цвета в момент, когда переполнение сожгло накал: кадр на четверть секунды теряет
    /// насыщенность и яркость и возвращается обратно. До M30 у этого события не было своего кадра
    /// вовсе — число множителя просто перерисовывалось меньшим, — а это самая дорогая потеря
    /// в игре после самого ресурса.
    ///
    /// Свой `Volume` поверх сценного, а не компонент в профиле <see cref="HeatVignette"/>: там
    /// вес идёт за накалом и держится единицей всё время, пока склад разогрет. Здесь вес обязан
    /// стоять в нуле всё остальное время, и это не вкусовщина, а условие безопасности. Сценный
    /// `GameVolumeProfile` уже несёт `ColorAdjustments` с насыщенностью, под которую решателем
    /// подобрана палитра биомов (`PROJECT_FACTS` §3); наш оверрайд при весе 1 подменил бы её
    /// целиком, а при весе 0 не значит ничего. То есть кадр гарантированно возвращается ровно
    /// к своей палитре, и знать её число нам не надо.
    ///
    /// Нового прохода рендера это не заводит: цветокоррекция в URP и так считается uber-проходом,
    /// добавляется только пересборка LUT на те кадры, пока вес не ноль.
    /// </summary>
    public sealed class BurnFlash : MonoBehaviour
    {
        [Tooltip("Сколько длится провал: удар и посадка вместе")]
        [SerializeField] float seconds = 0.24f;

        [Tooltip("До какой насыщенности проваливается кадр на пике")]
        [SerializeField, Range(-100f, 0f)] float saturation = -72f;

        [Tooltip("На сколько ступеней экспозиции темнеет кадр на пике")]
        [SerializeField] float exposure = -1.1f;

        [Tooltip("Приоритет тома: выше сценного и выше виньетки накала")]
        [SerializeField] float priority = 20f;

        GameObject holder;
        Volume volume;
        VolumeProfile profile;
        ColorAdjustments grading;
        float timer;

        /// <summary>
        /// Собирается в `Awake`, а не в `Bind`: зависимостей от партии у эффекта нет, и ловушка
        /// «вью в сцене живёт с загрузки, а собрано только после `Bind`» его тогда не касается.
        /// </summary>
        void Awake()
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();

            grading = profile.Add<ColorAdjustments>();
            grading.saturation.overrideState = true;
            grading.saturation.value = saturation;
            grading.postExposure.overrideState = true;
            grading.postExposure.value = exposure;

            holder = new GameObject("Burn Flash");
            holder.transform.SetParent(transform, false);

            volume = holder.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = priority;
            volume.profile = profile;
            volume.weight = 0f;
        }

        /// <summary>Накал сгорел: отыграть провал с начала, даже если предыдущий ещё не дошёл.</summary>
        public void Play() => timer = seconds;

        void Update()
        {
            if (timer <= 0f)
                return;

            // Нескалированное время по той же причине, что и у тряски: hitstop замедляет партию,
            // а удар обязан отыграть свои доли секунды целиком.
            timer = Mathf.Max(0f, timer - Time.unscaledDeltaTime);

            // `Hop` — резкий выброс и мягкая посадка. Симметричная кривая читалась бы как
            // «кадр поплыл», а нужно «кадру прилетело».
            volume.weight = timer <= 0f ? 0f : Anim.Hop(1f - timer / seconds);
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
