using UnityEngine;

namespace Base.Audio
{
    /// <summary>
    /// Синтез простых звуков: заглушки до настоящих, но уже дают отклик. Встроенные — только звуки интерфейса (UiSfx);
    /// игра делает свои заглушки этими же функциями и отдаёт их SoundPlayer.SetFallback при запуске.
    /// Создание клипа выделяет память: только при запуске или первом звуке, не каждый кадр.
    /// </summary>
    public static class ProceduralSounds
    {
        private const int Rate = 22050;

        /// <summary>Заглушка звука интерфейса; не звук интерфейса — null.</summary>
        public static AudioClip CreateUi(string id)
        {
            switch (id)
            {
                case UiSfx.Click: return Tone("click", 0.05f, 900f, 0.25f, 60f);
                case UiSfx.WindowOpen: return Whoosh("open", 0.2f, 0.25f);
                case UiSfx.Tab: return Tone("tab", 0.04f, 1400f, 0.15f, 80f);
                default: return null;
            }
        }

        public static AudioClip Noise(string name, float duration, float volume, float decay, int bursts)
        {
            var samples = new float[Mathf.CeilToInt(Rate * duration)];
            var random = new System.Random(name.GetHashCode());
            for (var b = 0; b < bursts; b++)
            {
                var start = bursts == 1 ? 0 : (int)(samples.Length * (b / (float)bursts + (float)random.NextDouble() * 0.04f));
                for (var i = start; i < samples.Length; i++)
                {
                    var t = (i - start) / (float)Rate;
                    var envelope = Mathf.Exp(-t * decay * (bursts > 1 ? 6f : 1f));
                    if (envelope < 0.01f)
                        break;
                    samples[i] += ((float)random.NextDouble() * 2f - 1f) * envelope * volume;
                }
            }

            return Build(name, samples);
        }

        /// <summary>Гром: «коричневый» шум (низкий гул) с быстрым нарастанием, раскатами и долгим затуханием.</summary>
        public static AudioClip Rumble(string name, float duration, float volume)
        {
            var samples = new float[Mathf.CeilToInt(Rate * duration)];
            var random = new System.Random(name.GetHashCode());
            var value = 0f;
            var peak = 0.0001f;
            for (var i = 0; i < samples.Length; i++)
            {
                var t = i / (float)Rate;
                value = Mathf.Clamp(value + ((float)random.NextDouble() * 2f - 1f) * 0.05f, -1f, 1f) * 0.995f;
                var attack = Mathf.Clamp01(t / 0.06f);
                var roll = 0.75f + 0.25f * Mathf.Sin(t * 9f + Mathf.Sin(t * 2.3f) * 3f);
                samples[i] = value * attack * roll * Mathf.Exp(-t * 1.6f);
                peak = Mathf.Max(peak, Mathf.Abs(samples[i]));
            }

            for (var i = 0; i < samples.Length; i++)
                samples[i] = samples[i] / peak * volume;
            return Build(name, samples);
        }

        /// <summary>«Вжух»: шум через простой фильтр, громкость нарастает и спадает, фильтр открывается к середине.</summary>
        public static AudioClip Whoosh(string name, float duration, float volume)
        {
            var samples = new float[Mathf.CeilToInt(Rate * duration)];
            var random = new System.Random(name.GetHashCode());
            var filtered = 0f;
            for (var i = 0; i < samples.Length; i++)
            {
                var t = i / (float)samples.Length;
                var envelope = Mathf.Sin(t * Mathf.PI);
                var cutoff = Mathf.Lerp(0.02f, 0.25f, envelope);
                filtered += (((float)random.NextDouble() * 2f - 1f) - filtered) * cutoff;
                samples[i] = filtered * envelope * volume * 2f;
            }

            return Build(name, samples);
        }

        public static AudioClip Tone(string name, float duration, float frequency, float volume, float decay)
        {
            var samples = new float[Mathf.CeilToInt(Rate * duration)];
            for (var i = 0; i < samples.Length; i++)
            {
                var t = i / (float)Rate;
                samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * Mathf.Exp(-t * decay) * volume;
            }

            return Build(name, samples);
        }

        public static AudioClip Sweep(string name, float duration, float from, float to, float volume)
        {
            var samples = new float[Mathf.CeilToInt(Rate * duration)];
            var phase = 0f;
            for (var i = 0; i < samples.Length; i++)
            {
                var t = i / (float)samples.Length;
                phase += 2f * Mathf.PI * Mathf.Lerp(from, to, t) / Rate;
                samples[i] = Mathf.Sin(phase) * (1f - t) * volume;
            }

            return Build(name, samples);
        }

        public static AudioClip Chord(string name, float duration, float[] notes, float volume)
        {
            var samples = new float[Mathf.CeilToInt(Rate * duration)];
            var step = samples.Length / (notes.Length + 1);
            for (var n = 0; n < notes.Length; n++)
            {
                for (var i = n * step; i < samples.Length; i++)
                {
                    var t = (i - n * step) / (float)Rate;
                    samples[i] += Mathf.Sin(2f * Mathf.PI * notes[n] * t) * Mathf.Exp(-t * 5f) * volume / notes.Length * 2f;
                }
            }

            return Build(name, samples);
        }

        private static AudioClip Build(string name, float[] samples)
        {
            var clip = AudioClip.Create(name, samples.Length, 1, Rate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
