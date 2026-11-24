using System;

namespace Base.Settings
{
    /// <summary>
    /// Общие настройки игрока, одинаковые во всех играх: громкость звуков и музыки, вибрация, выбранный язык.
    /// Свои настройки игры (скорость анимаций, подсказки) — отдельным сервисом игры в своём разделе сохранения.
    /// Читать только после загрузки сохранения (ISaveStore.IsLoaded): в Construct/Awake его может ещё не быть.
    /// </summary>
    public interface IGameSettings
    {
        /// <summary>Громкость звуков 0…1 (ползунок). 0 — звуки выключены, отдельного выключателя нет.</summary>
        float SoundVolume { get; set; }

        /// <summary>Громкость музыки и фона окружения 0…1 (ползунок). 0 — выключено.</summary>
        float MusicVolume { get; set; }

        /// <summary>Вибрация телефона. Где её нет (браузер, IHapticsService.IsAvailable = false), настройку не показывать.</summary>
        bool Vibration { get; set; }

        /// <summary>Язык, выбранный игроком. Пусто — язык площадки (определяется при запуске).</summary>
        string Language { get; set; }

        event Action Changed;
    }
}
