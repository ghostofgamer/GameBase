using System;
using Base.Services.Saves;

namespace Base.Settings
{
    /// <summary>Раздел сохранения общих настроек. Поля не переименовывать без ISaveMigration.</summary>
    [Serializable]
    public sealed class SettingsSave
    {
        public float SoundVolume = 1f;
        public float MusicVolume = 1f;
        public bool Vibration = true;
        public string Language = string.Empty;
    }

    /// <summary>Настройки игрока в разделе сохранения <see cref="SaveKey"/>. Каждая правка — отложенное сохранение.</summary>
    public sealed class GameSettings : IGameSettings
    {
        public const string SaveKey = "settings";

        /// <summary>Ниже — тишина (ползунок у самого края).</summary>
        private const float Silent = 0.001f;

        private readonly ISaveStore _saves;

        public GameSettings(ISaveStore saves)
        {
            _saves = saves;
        }

        public event Action Changed;

        private SettingsSave Data => _saves.Get<SettingsSave>(SaveKey);

        public float SoundVolume
        {
            get => Volume(Data.SoundVolume);
            set
            {
                Data.SoundVolume = Clamp(value);
                Saved();
            }
        }

        public float MusicVolume
        {
            get => Volume(Data.MusicVolume);
            set
            {
                Data.MusicVolume = Clamp(value);
                Saved();
            }
        }

        public bool Vibration
        {
            get => Data.Vibration;
            set
            {
                Data.Vibration = value;
                Saved();
            }
        }

        public string Language
        {
            get => Data.Language ?? string.Empty;
            set
            {
                Data.Language = value ?? string.Empty;
                Saved();
            }
        }

        private static float Volume(float value) => value <= Silent ? 0f : Clamp(value);

        private static float Clamp(float value) => value < 0f ? 0f : value > 1f ? 1f : value;

        private void Saved()
        {
            _saves.RequestSave();
            Changed?.Invoke();
        }
    }
}
