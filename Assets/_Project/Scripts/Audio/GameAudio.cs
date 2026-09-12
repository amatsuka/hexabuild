using System.Collections.Generic;
using UnityEngine;

namespace Game.Audio
{
    /// <summary>
    /// Единственный проигрыватель звука в проекте. Клипы лежат в `Resources/Audio` и зовутся
    /// именем из `docs/audio_brief.md`: `Play("sfx_ui_click")` берёт `Audio/sfx_ui_click_01`.
    /// Суффикс варианта один — пулов `_02`, `_03` в поставке нет, и разнообразие держит питч.
    ///
    /// Хозяин звука — скрытый объект сцены, а не `DontDestroyOnLoad`: рестарт и выход в меню
    /// перезагружают сцену, и лупы обязаны уйти вместе с ней, а не тянуться в следующую партию.
    /// Кэш клипов при этом статический и переживает перезагрузку — грузить одно и то же
    /// по второму разу незачем.
    ///
    /// `AudioMixer`-ассета нет: дакинга и ползунков громкости в игре пока не просили, а
    /// громкость каждого звука стоит числом в точке вызова.
    /// </summary>
    public static class GameAudio
    {
        /// <summary>Сколько одновременных one-shot'ов. Питч живёт на источнике, поэтому их несколько.</summary>
        const int Voices = 8;

        const string Folder = "Audio/";
        const string Variant = "_01";

        static readonly Dictionary<string, AudioClip> clips = new();
        static readonly Dictionary<string, AudioSource> loops = new();

        static GameObject host;
        static AudioSource[] voices;
        static int nextVoice;

        /// <summary>Сыграть разово. Неизвестное имя молчит, а не падает: звук не стоит партии.</summary>
        public static void Play(string name, float volume = 1f, float pitch = 1f)
        {
            var clip = Clip(name);
            if (clip == null)
                return;

            EnsureHost();
            var voice = voices[nextVoice];
            nextVoice = (nextVoice + 1) % Voices;
            voice.pitch = pitch;
            voice.PlayOneShot(clip, volume);
        }

        /// <summary>Пустить луп. Уже играющий не перезапускается — зовут это каждый кадр.</summary>
        public static void Loop(string name, float volume = 1f)
        {
            if (loops.TryGetValue(name, out var source) && source != null)
            {
                source.volume = volume;
                return;
            }

            var clip = Clip(name);
            if (clip == null)
                return;

            EnsureHost();
            source = host.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = true;
            source.volume = volume;
            source.playOnAwake = false;
            source.Play();
            loops[name] = source;
        }

        public static void StopLoop(string name)
        {
            if (!loops.TryGetValue(name, out var source))
                return;

            if (source != null)
                Object.Destroy(source);

            loops.Remove(name);
        }

        static AudioClip Clip(string name)
        {
            if (clips.TryGetValue(name, out var clip))
                return clip;

            // Промах кэшируется вместе с попаданием: иначе отсутствующий клип ходил бы
            // в `Resources.Load` на каждом действии игрока.
            clip = Resources.Load<AudioClip>(Folder + name + Variant);
            clips[name] = clip;
            return clip;
        }

        static void EnsureHost()
        {
            if (host != null)
                return;

            host = new GameObject("Audio") { hideFlags = HideFlags.HideInHierarchy };

            // В сцене слушателя нет: камера собрана без него, а без `AudioListener` Unity
            // не играет вообще ничего. Звук в игре весь двумерный, поэтому где он стоит —
            // неважно, важно что он один.
            if (Object.FindFirstObjectByType<AudioListener>() == null)
                host.AddComponent<AudioListener>();

            voices = new AudioSource[Voices];
            for (var i = 0; i < Voices; i++)
            {
                var voice = host.AddComponent<AudioSource>();
                voice.playOnAwake = false;
                voices[i] = voice;
            }

            // Источники прошлой сцены умерли вместе с ней, а ключи в словаре остались.
            loops.Clear();
        }
    }
}
