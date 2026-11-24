using System.Collections.Generic;
using Base.Services.Saves;
using Base.Settings;
using UnityEngine;
using Zenject;

namespace Base.Audio
{
    /// <summary>
    /// Проигрывает звуки по строковому ID. Клипы и громкость — SoundConfig; событие без клипов звучит заглушкой:
    /// своей (SetFallback, синтез ProceduralSounds) или встроенной для звуков интерфейса. Нет ни того ни другого — тишина.
    /// Один и тот же звук не звучит чаще SoundConfig.RepeatGuard: кнопка (UiPress) и код окна не дают двойной щелчок.
    /// Громкость — ползунок «Звуки» (IGameSettings.SoundVolume) по кривой SoundConfig.VolumeCurve.
    /// Все звуки идут мимо AudioMixer: в WebGL его нет. Новый звук мимо SoundPlayer не заводить.
    /// Производительность: Play без выделения памяти (словари по строке, клипы кэшируются).
    /// </summary>
    public sealed class SoundPlayer : MonoBehaviour
    {
        [SerializeField] private AudioSource _source;

        [Tooltip("Петли, которые приглушаются на время звуков с SoundConfig.Entry.MusicDuck < 1 (музыка, фон окружения)")]
        [SerializeField] private MusicPlayer[] _ducked = new MusicPlayer[0];

        private readonly Dictionary<string, AudioClip> _fallbacks = new Dictionary<string, AudioClip>();
        private readonly Dictionary<string, float> _lastPlayed = new Dictionary<string, float>();
        private readonly Dictionary<string, int> _lastClip = new Dictionary<string, int>();
        private IGameSettings _settings;
        private SoundConfig _config;
        private ISaveStore _saves;

        [Inject]
        public void Construct([InjectOptional] IGameSettings settings, [InjectOptional] SoundConfig config,
            [InjectOptional] ISaveStore saves)
        {
            _settings = settings;
            _config = config;
            _saves = saves;
        }

#if UNITY_EDITOR
        public void EditorSetup(AudioSource source, params MusicPlayer[] ducked)
        {
            _source = source;
            _ducked = ducked ?? new MusicPlayer[0];
        }
#endif

        /// <summary>Заглушка для события без клипов в SoundConfig (например, синтез ProceduralSounds). Вызывать при запуске, не каждый кадр.</summary>
        public void SetFallback(string id, AudioClip clip)
        {
            if (!string.IsNullOrEmpty(id))
                _fallbacks[id] = clip;
        }

        /// <param name="pitch">Высота относительно записи; сверху добавляется случайный сдвиг pitchJitter.</param>
        public void Play(string id, float volume = 1f, float pitchJitter = 0.08f, float pitch = 1f)
        {
            var user = UserVolume;
            if (_source == null || user <= 0f || string.IsNullOrEmpty(id))
                return;

            var now = Time.unscaledTime;
            if (_config != null && _config.RepeatGuard > 0f && _lastPlayed.TryGetValue(id, out var last) &&
                now - last < _config.RepeatGuard)
                return;

            var entry = _config != null ? _config.Find(id) : null;
            var clip = entry != null ? Pick(id, entry.Clips) : null;
            if (clip != null)
                volume *= entry.Volume;
            else
                clip = Fallback(id);
            if (clip == null)
                return;

            _lastPlayed[id] = now;
            _source.pitch = pitch * (1f + Random.Range(-pitchJitter, pitchJitter));
            _source.PlayOneShot(clip, volume * user);
            if (entry != null && entry.MusicDuck < 1f)
                DuckLoops(entry.MusicDuck, clip.length);
        }

        /// <summary>
        /// Свой клип мимо таблицы событий (длинный звук одной локации, загруженный вместе с ней): громкость ползунка
        /// «Звуки» учитывается, повтор не ограничивается.
        /// </summary>
        public void PlayClip(AudioClip clip, float volume, float pitch = 1f, float musicDuck = 1f)
        {
            var user = UserVolume;
            if (_source == null || clip == null || user <= 0f)
                return;

            _source.pitch = pitch;
            _source.PlayOneShot(clip, volume * user);
            if (musicDuck < 1f)
                DuckLoops(musicDuck, clip.length);
        }

        private void DuckLoops(float level, float hold)
        {
            for (var i = 0; i < _ducked.Length; i++)
            {
                if (_ducked[i] != null)
                    _ducked[i].Duck(level, hold);
            }
        }

        /// <summary>Громкость ползунка «Звуки» по кривой; 0 — звуки выключены. До загрузки сохранения — полная.</summary>
        private float UserVolume
        {
            get
            {
                if (_settings == null || (_saves != null && !_saves.IsLoaded))
                    return 1f;
                var slider = _settings.SoundVolume;
                return _config != null ? _config.Curve(slider) : slider * slider;
            }
        }

        /// <summary>Клип события из конфига: несколько — случайный, но не тот же, что прозвучал прошлым.</summary>
        private AudioClip Pick(string id, AudioClip[] clips)
        {
            if (clips == null || clips.Length == 0)
                return null;
            if (clips.Length == 1)
                return clips[0];

            _lastClip.TryGetValue(id, out var previous);
            var index = Random.Range(0, clips.Length - 1);
            if (index >= previous)
                index++;
            _lastClip[id] = index;
            return clips[index];
        }

        private AudioClip Fallback(string id)
        {
            if (_fallbacks.TryGetValue(id, out var cached))
                return cached;

            var clip = ProceduralSounds.CreateUi(id);
            _fallbacks[id] = clip;
            return clip;
        }
    }
}
