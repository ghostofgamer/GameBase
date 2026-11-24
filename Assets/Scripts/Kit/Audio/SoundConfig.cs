using System;
using System.Collections.Generic;
using UnityEngine;

namespace Base.Audio
{
    /// <summary>
    /// Звуки игры: на каждое событие (строковый ID) — один или несколько клипов и громкость. Несколько клипов играют
    /// вразнобой (не одинаковый щелчок подряд). Событие без строки или без клипов звучит синтезом из кода
    /// (ProceduralSounds) — заглушка, пока свой звук не выбран. Общие числа музыки — здесь же.
    /// </summary>
    [CreateAssetMenu(menuName = "Base/Audio/Sound Config", fileName = "Sound")]
    public sealed class SoundConfig : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            [Tooltip("ID события: UiSfx.* для интерфейса, константы игры для остального")]
            public string Id;

            [Tooltip("Клипы события. Несколько — играют вразнобой, без повтора подряд")]
            public AudioClip[] Clips = new AudioClip[0];

            [Tooltip("Громкость события (умножается на громкость в месте вызова)")]
            [Range(0f, 1f)] public float Volume = 1f;

            [Tooltip("Музыка на время звука: 1 — не трогать, 0.3 — тише до 30% (победа, новый уровень)")]
            [Range(0f, 1f)] public float MusicDuck = 1f;
        }

        [Tooltip("Один и тот же звук не повторяется чаще (секунды): кнопка и код окна не дают двойной щелчок")]
        [Range(0f, 0.2f)] public float RepeatGuard = 0.06f;

        public List<Entry> Sounds = new List<Entry>();

        [Tooltip("Ползунок громкости -> громкость: ползунок в степени этого числа. 2 — середина ползунка на слух как половина " +
                 "(слух логарифмический), 1 — как есть")]
        [Range(1f, 3f)] public float VolumeCurve = 2f;

        [Header("Музыка")]
        [Tooltip("Общая громкость музыки: тише звуков, чтобы не мешала")]
        [Range(0f, 1f)] public float MusicVolume = 0.45f;

        [Tooltip("Общая громкость фона окружения: тише музыки. Ползунок — тот же, «Музыка»")]
        [Range(0f, 1f)] public float AmbienceVolume = 0.3f;

        [Tooltip("Переход между треками (секунды)")]
        [Range(0f, 5f)] public float MusicCrossfade = 1.5f;

        [Tooltip("Первый трек после запуска нарастает (секунды)")]
        [Range(0f, 5f)] public float MusicFadeIn = 2.5f;

        [Tooltip("Как быстро музыка стихает под звук (секунды)")]
        [Range(0f, 1f)] public float MusicDuckAttack = 0.12f;

        [Tooltip("Как быстро музыка возвращается после звука (секунды)")]
        [Range(0f, 3f)] public float MusicDuckRelease = 1.2f;

        private Dictionary<string, Entry> _byId;

        /// <summary>Положение ползунка (0…1) -> множитель громкости.</summary>
        public float Curve(float slider) => slider <= 0f ? 0f : slider >= 1f ? 1f : Mathf.Pow(slider, VolumeCurve);

        /// <summary>Строка события; нет — null. Таблица строится один раз, дальше без выделения памяти.</summary>
        public Entry Find(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;

            if (_byId == null)
            {
                _byId = new Dictionary<string, Entry>();
                for (var i = 0; i < Sounds.Count; i++)
                {
                    var entry = Sounds[i];
                    if (entry != null && !string.IsNullOrEmpty(entry.Id))
                        _byId[entry.Id] = entry;
                }
            }

            return _byId.TryGetValue(id, out var found) ? found : null;
        }

        private void OnValidate()
        {
            // Правка в инспекторе: таблица соберётся заново.
            _byId = null;
        }
    }
}
