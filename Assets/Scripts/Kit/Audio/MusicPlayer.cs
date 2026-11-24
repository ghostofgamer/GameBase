using System;
using System.Threading;
using Base.Services.Saves;
using Base.Settings;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Zenject;

namespace Base.Audio
{
    /// <summary>Что играет плеер петель: музыку или фон окружения.</summary>
    public enum LoopChannel
    {
        Music,
        Ambience,
    }

    /// <summary>
    /// Фоновая петля: музыка или фон окружения (второй компонент с каналом Ambience). Игра говорит, что должно играть
    /// (Play с ссылкой Addressables или Stop), плеер делает остальное: новый трек нарастает, старый стихает
    /// (SoundConfig.MusicCrossfade), потом выгружается — в памяти один трек, на время перехода два.
    /// Тот же трек не перезапускается. Ползунок «Музыка» на 0 — трек не грузится.
    /// Приглушение под важный звук — Duck (зовёт SoundPlayer по SoundConfig.Entry.MusicDuck).
    /// Пауза на время рекламы и при свёрнутой вкладке — AudioListener.pause (AdPauseController), здесь ничего не нужно.
    /// Дорожка поверх петли (дождь поверх фона) — PlayOverlay. Треки — в группе Addressables игры.
    /// Производительность: Update — только арифметика громкости, без выделений; загрузка — по смене трека.
    /// </summary>
    public sealed class MusicPlayer : MonoBehaviour
    {
        [SerializeField] private AudioSource _a;
        [SerializeField] private AudioSource _b;

        [Tooltip("Музыка или фон окружения")]
        [SerializeField] private LoopChannel _channel = LoopChannel.Music;

        [Tooltip("Дорожка поверх петли (дождь). Пусто — без неё")]
        [SerializeField] private AudioSource _overlay;

        private sealed class Track
        {
            public AudioSource Source;
            public AsyncOperationHandle<AudioClip> Handle;
            public string Key;
            public float Volume;

            /// <summary>Доля громкости трека 0…1 (нарастание и затухание).</summary>
            public float Fade;

            public float FadeSpeed;
        }

        private readonly Track _current = new Track();
        private readonly Track _previous = new Track();
        private IGameSettings _settings;
        private SoundConfig _config;
        private ISaveStore _saves;

        // Что должно играть (может не играть: музыка выключена, трек ещё грузится).
        private AssetReferenceT<AudioClip> _wanted;
        private float _wantedVolume = 1f;
        private int _request;
        private string _pendingKey;
        private bool _started;
        private bool _waitingForSave;

        private float _duck = 1f;
        private float _duckTarget = 1f;
        private float _duckUntil;

        // Ползунок «Музыка» по кривой громкости: читается при смене настроек, не каждый кадр.
        private float _user = 1f;
        private float _overlayVolume;
        private float _overlayFade;

        /// <summary>
        /// Настройки читаются только после загрузки сохранения (в Construct его может ещё не быть) — до этого плеер ждёт.
        /// </summary>
        [Inject]
        public void Construct([InjectOptional] IGameSettings settings, [InjectOptional] SoundConfig config,
            [InjectOptional] ISaveStore saves)
        {
            _settings = settings;
            _config = config;
            _saves = saves;
        }

#if UNITY_EDITOR
        public void EditorSetup(AudioSource a, AudioSource b, LoopChannel channel = LoopChannel.Music, AudioSource overlay = null)
        {
            _a = a;
            _b = b;
            _channel = channel;
            _overlay = overlay;
        }
#endif

        public LoopChannel Channel => _channel;

        /// <summary>Играть трек (громкость трека 0…1 поверх общей). null — тишина. Тот же трек не перезапускается.</summary>
        public void Play(AssetReferenceT<AudioClip> track, float volume = 1f)
        {
            _wanted = track != null && track.RuntimeKeyIsValid() ? track : null;
            _wantedVolume = volume;
            Refresh();
        }

        /// <summary>Затихнуть и выгрузить трек.</summary>
        public void Stop()
        {
            Play(null);
        }

        private bool SaveReady => _saves == null || _saves.IsLoaded;

        private bool MusicOn => _user > 0f;

        private float Master => (_channel == LoopChannel.Ambience
            ? (_config != null ? _config.AmbienceVolume : 0.3f)
            : (_config != null ? _config.MusicVolume : 0.45f)) * _user;

        private float Crossfade => _config != null ? _config.MusicCrossfade : 1.5f;

        private float FadeIn => _config != null ? _config.MusicFadeIn : 2.5f;

        private void Awake()
        {
            _current.Source = _a;
            _previous.Source = _b;
        }

        private void OnEnable()
        {
            if (_settings != null)
                _settings.Changed += Refresh;
        }

        private void Start()
        {
            // Зависимости приходят до Start: подписка на настройки, если OnEnable был раньше Construct.
            if (_settings != null)
            {
                _settings.Changed -= Refresh;
                _settings.Changed += Refresh;
            }
        }

        private void OnDisable()
        {
            if (_settings != null)
                _settings.Changed -= Refresh;
        }

        private void OnDestroy()
        {
            Unload(_current);
            Unload(_previous);
            StopOverlay();
        }

        /// <summary>Включить дорожку поверх петли (дождь): нарастает за время перехода. Клип грузит и отпускает тот, кто её включил.</summary>
        public void PlayOverlay(AudioClip clip, float volume)
        {
            if (_overlay == null || clip == null)
                return;

            _overlay.clip = clip;
            _overlay.loop = true;
            _overlay.volume = 0f;
            _overlayVolume = volume;
            _overlayFade = 0f;
            _overlay.Play();
        }

        /// <summary>Выключить дорожку сразу: её клип сейчас выгрузят.</summary>
        public void StopOverlay()
        {
            if (_overlay == null)
                return;

            _overlay.Stop();
            _overlay.clip = null;
            _overlayFade = 0f;
        }

        /// <summary>Приглушить петлю на время звука: level — доля громкости (1 — не трогать), hold — длина звука.</summary>
        public void Duck(float level, float hold)
        {
            if (level >= 1f)
                return;

            _duckTarget = Mathf.Min(_duckTarget, Mathf.Clamp01(level));
            _duckUntil = Mathf.Max(_duckUntil, Time.unscaledTime + hold);
        }

        private void ReadUserVolume()
        {
            if (_settings == null)
            {
                _user = 1f;
                return;
            }

            var slider = _settings.MusicVolume;
            _user = _config != null ? _config.Curve(slider) : slider * slider;
        }

        private async UniTaskVoid RefreshWhenLoadedAsync(CancellationToken ct)
        {
            if (_waitingForSave)
                return;

            _waitingForSave = true;
            try
            {
                await _saves.WaitLoadedAsync(ct);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            finally
            {
                _waitingForSave = false;
            }

            Refresh();
        }

        /// <summary>Привести играющее к желаемому: трек, если музыка включена, иначе тишина.</summary>
        private void Refresh()
        {
            if (!SaveReady)
            {
                RefreshWhenLoadedAsync(this.GetCancellationTokenOnDestroy()).Forget();
                return;
            }

            ReadUserVolume();
            if (_current.Source == null || _previous.Source == null)
                return;

            var key = MusicOn && _wanted != null ? _wanted.AssetGUID : null;
            if (key != null && key == _pendingKey)
                return;

            // Что грузилось раньше, уже не нужно: по номеру запроса загрузка выбросит свой трек.
            _request++;
            _pendingKey = null;
            if (key == _current.Key)
            {
                _current.Volume = _wantedVolume;
                return;
            }

            if (key == null)
            {
                FadeOutCurrent();
                return;
            }

            _pendingKey = key;
            SwitchAsync(_wanted, key, _wantedVolume, _request, this.GetCancellationTokenOnDestroy()).Forget();
        }

        private async UniTaskVoid SwitchAsync(AssetReferenceT<AudioClip> reference, string key, float volume, int request,
            CancellationToken ct)
        {
            var handle = default(AsyncOperationHandle<AudioClip>);
            AudioClip clip;
            try
            {
                handle = Addressables.LoadAssetAsync<AudioClip>(reference.RuntimeKey);
                clip = await handle.ToUniTask(cancellationToken: ct);
            }
            catch (OperationCanceledException)
            {
                if (handle.IsValid())
                    Addressables.Release(handle);
                return;
            }
            catch (Exception e)
            {
                // Нет сборки Addressables или битая ссылка: без музыки, игра идёт дальше.
                Debug.LogError($"[{_channel}] Track {key} failed to load: {e.Message}");
                if (handle.IsValid())
                    Addressables.Release(handle);
                if (request == _request)
                    _pendingKey = null;
                return;
            }

            // Пока грузилось, трек или настройка сменились: этот уже не нужен.
            if (request != _request || clip == null)
            {
                if (handle.IsValid())
                    Addressables.Release(handle);
                return;
            }

            _pendingKey = null;
            FadeOutCurrent();
            var fade = _started ? Crossfade : FadeIn;
            _started = true;

            _current.Handle = handle;
            _current.Key = key;
            _current.Volume = volume;
            _current.Fade = 0f;
            _current.FadeSpeed = fade > 0f ? 1f / fade : 1000f;
            _current.Source.clip = clip;
            _current.Source.loop = true;
            _current.Source.volume = 0f;
            _current.Source.Play();
            // Фон — петля по кругу: каждый раз с нового места, повтор рисунка не так заметен.
            if (_channel == LoopChannel.Ambience && clip.length > 1f)
                _current.Source.time = UnityEngine.Random.Range(0f, clip.length * 0.95f);
        }

        /// <summary>Играющий трек уходит во второй источник и стихает; то, что стихало там, выгружается сразу.</summary>
        private void FadeOutCurrent()
        {
            if (_current.Key == null)
                return;

            Unload(_previous);
            var source = _previous.Source;
            _previous.Source = _current.Source;
            _previous.Handle = _current.Handle;
            _previous.Key = _current.Key;
            _previous.Volume = _current.Volume;
            _previous.Fade = _current.Fade;
            var fade = Crossfade;
            _previous.FadeSpeed = fade > 0f ? 1f / fade : 1000f;

            _current.Source = source;
            _current.Handle = default;
            _current.Key = null;
            _current.Fade = 0f;
        }

        private static void Unload(Track track)
        {
            if (track.Source != null)
            {
                track.Source.Stop();
                track.Source.clip = null;
            }

            if (track.Handle.IsValid())
                Addressables.Release(track.Handle);
            track.Handle = default;
            track.Key = null;
            track.Fade = 0f;
        }

        private void Update()
        {
            var dt = Time.unscaledDeltaTime;
            if (_duckTarget < 1f && Time.unscaledTime >= _duckUntil)
                _duckTarget = 1f;

            if (_duck != _duckTarget)
            {
                var time = _duck > _duckTarget
                    ? (_config != null ? _config.MusicDuckAttack : 0.12f)
                    : (_config != null ? _config.MusicDuckRelease : 1.2f);
                _duck = time > 0f ? Mathf.MoveTowards(_duck, _duckTarget, dt / time) : _duckTarget;
            }

            var master = Master * _duck;
            if (_current.Key != null)
            {
                _current.Fade = Mathf.Min(1f, _current.Fade + dt * _current.FadeSpeed);
                _current.Source.volume = master * _current.Volume * _current.Fade;
            }

            if (_previous.Key != null)
            {
                _previous.Fade -= dt * _previous.FadeSpeed;
                if (_previous.Fade <= 0f)
                    Unload(_previous);
                else
                    _previous.Source.volume = master * _previous.Volume * _previous.Fade;
            }

            if (_overlay != null && _overlay.clip != null)
            {
                var fade = Crossfade;
                _overlayFade = Mathf.Min(1f, _overlayFade + (fade > 0f ? dt / fade : 1f));
                _overlay.volume = master * _overlayVolume * _overlayFade;
            }
        }
    }
}
