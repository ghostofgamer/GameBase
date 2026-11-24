using Base.Audio;
using Base.Testing;
using Base.Text;
using NUnit.Framework;
using UnityEngine;

namespace Base.Tests
{
    /// <summary>Проверки общих частей Base.Kit, которые не требуют сцены.</summary>
    public sealed class KitTests
    {
        [Test]
        public void NumberTemplateSplitsAroundPlaceholder()
        {
            NumberTemplate.Split("Монеты: {0} шт.", out var prefix, out var suffix);
            Assert.AreEqual("Монеты: ", prefix);
            Assert.AreEqual(" шт.", suffix);

            NumberTemplate.Split("Очки", out prefix, out suffix);
            Assert.AreEqual("Очки ", prefix);
            Assert.IsNull(suffix);
        }

        [Test]
        public void BrokenTranslationFormatShowsTemplate()
        {
            Assert.AreEqual("{0 сломано", UiFormat.Format(null, "{0 сломано", 5));
        }

        [Test]
        public void SoundConfigFindsEntryById()
        {
            var config = ScriptableObject.CreateInstance<SoundConfig>();
            config.Sounds.Add(new SoundConfig.Entry { Id = UiSfx.Click, Volume = 0.5f });
            Assert.AreEqual(0.5f, config.Find(UiSfx.Click).Volume);
            Assert.IsNull(config.Find("missing"));
            Assert.IsNull(config.Find(null));
            Object.DestroyImmediate(config);
        }

        [Test]
        public void VolumeCurveKeepsEnds()
        {
            var config = ScriptableObject.CreateInstance<SoundConfig>();
            Assert.AreEqual(0f, config.Curve(0f));
            Assert.AreEqual(1f, config.Curve(1f));
            Assert.AreEqual(0.25f, config.Curve(0.5f), 1e-4f);
            Object.DestroyImmediate(config);
        }

        [Test]
        public void UiSoundsHaveBuiltInPlaceholders()
        {
            Assert.IsNotNull(ProceduralSounds.CreateUi(UiSfx.Click));
            Assert.IsNotNull(ProceduralSounds.CreateUi(UiSfx.WindowOpen));
            Assert.IsNotNull(ProceduralSounds.CreateUi(UiSfx.Tab));
            Assert.IsNull(ProceduralSounds.CreateUi("game_sound"));
        }

        [Test]
        public void JsonFlattenerListsNestedFields()
        {
            var flat = JsonFlattener.Flatten("{\"a\":1,\"b\":{\"c\":\"x\"},\"d\":[2,3]}");
            Assert.AreEqual("1", flat["a"]);
            Assert.AreEqual("x", flat["b.c"]);
            Assert.AreEqual("3", flat["d[1]"]);
        }
    }
}
