using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using Base.Platform;
using Base.Services.Saves;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Base.Testing
{
    /// <summary>
    /// Сторож обновлений: сохранение каждого выпуска (save_&lt;версия&gt;.json в Folder, снимает Base/Saves/Export Release Fixture)
    /// читается текущим кодом без потерь, а все ID контента выпуска (content_ids_&lt;версия&gt;.txt) на месте.
    /// Упал — значит обновление сотрёт игрокам прогресс: вернуть поле или ID, или написать ISaveMigration
    /// (раздел с миграцией сверяет тест миграции). Старые снимки не удалять.
    ///
    /// В игре: <c>public sealed class SaveCompatibilityTests : SaveCompatibilityTestBase</c> с папкой, разделами и ID.
    /// До первого выпуска снимков нет — ReleaseFixturesExist падает, это напоминание снять снимок в день выпуска.
    /// </summary>
    public abstract class SaveCompatibilityTestBase
    {
        /// <summary>Папка снимков выпусков (та же, что у IReleaseFixtureSource игры).</summary>
        protected abstract string Folder { get; }

        /// <summary>Все разделы сохранения и их классы. Новый раздел — строка сюда.</summary>
        protected abstract IReadOnlyList<(string Key, Type Type)> Sections { get; }

        /// <summary>
        /// Класс с константами ключей разделов (SaveKeys игры): каждый ключ обязан быть в Sections. null — без проверки.
        /// </summary>
        protected virtual Type SaveKeysType => null;

        /// <summary>Сборки, где лежат миграции (ISaveMigration с пустым конструктором). По умолчанию — сборка SaveKeysType.</summary>
        protected virtual IEnumerable<Assembly> MigrationAssemblies =>
            SaveKeysType != null ? new[] { SaveKeysType.Assembly } : Array.Empty<Assembly>();

        /// <summary>ID контента, которые есть сейчас. null — у игры нет контента с ID, проверка ID пропускается.</summary>
        protected virtual ISet<string> CurrentContentIds() => null;

        private IEnumerable<string> SaveFixtures() =>
            Directory.Exists(Folder) ? Directory.GetFiles(Folder, "save_*.json") : Enumerable.Empty<string>();

        private IEnumerable<string> IdFixtures() =>
            Directory.Exists(Folder) ? Directory.GetFiles(Folder, "content_ids_*.txt") : Enumerable.Empty<string>();

        [Test]
        public void EverySaveKeyIsListed()
        {
            if (SaveKeysType == null)
                return;

            var listed = new HashSet<string>(Sections.Select(s => s.Key));
            var keys = SaveKeysType.GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.IsLiteral && f.FieldType == typeof(string))
                .Select(f => (string)f.GetRawConstantValue());
            foreach (var key in keys)
                Assert.IsTrue(listed.Contains(key), $"Раздел '{key}' не внесён в Sections теста совместимости");
        }

        [Test]
        public void ReleaseFixturesExist()
        {
            Assert.IsNotEmpty(SaveFixtures(), "Нет снимка сохранения выпуска: Base/Saves/Export Release Fixture в день выпуска");
        }

        [Test]
        public void ReleasedContentIdsStillExist()
        {
            var ids = CurrentContentIds();
            if (ids == null)
                return;

            foreach (var file in IdFixtures())
            {
                var missing = File.ReadAllLines(file).Where(l => l.Length > 0 && !ids.Contains(l)).ToList();
                Assert.IsEmpty(missing, $"{Path.GetFileName(file)}: ID выпуска пропали (у игроков пропадут вещи): {string.Join(", ", missing)}");
            }
        }

        [UnityTest]
        public IEnumerator ReleasedSavesLoadWithoutLoss() => UniTask.ToCoroutine(async () =>
        {
            foreach (var file in SaveFixtures())
            {
                var json = File.ReadAllText(file);
                var store = new SaveStore(new FixedCloud(json), Migrations());
                await store.LoadAsync(CancellationToken.None);

                var envelope = JsonUtility.FromJson<Envelope>(json);
                Assert.IsNotNull(envelope?.sections, $"{Path.GetFileName(file)}: не конверт сохранения");

                foreach (var (key, type) in Sections)
                {
                    var old = envelope.sections.FirstOrDefault(s => s.key == key);
                    if (old == null || string.IsNullOrEmpty(old.json))
                        continue;

                    var section = typeof(SaveStore).GetMethod(nameof(SaveStore.Get)).MakeGenericMethod(type)
                        .Invoke(store, new object[] { key });
                    var now = JsonFlattener.Flatten(JsonUtility.ToJson(section));

                    // Раздел с миграцией сверяет сама миграция (её тест); без миграции каждое старое поле обязано остаться.
                    if (Math.Max(old.version, 1) < store.GetVersion(key))
                        continue;

                    foreach (var pair in JsonFlattener.Flatten(old.json))
                    {
                        Assert.IsTrue(now.TryGetValue(pair.Key, out var value),
                            $"{Path.GetFileName(file)}: в разделе '{key}' пропало поле '{pair.Key}' — переименовано или удалено без ISaveMigration");
                        Assert.AreEqual(pair.Value, value,
                            $"{Path.GetFileName(file)}: в разделе '{key}' поле '{pair.Key}' прочиталось иначе");
                    }
                }
            }
        });

        private List<ISaveMigration> Migrations()
        {
            // Те же миграции, что биндит игра: все классы с ISaveMigration и пустым конструктором.
            return MigrationAssemblies.SelectMany(a => a.GetTypes())
                .Where(t => typeof(ISaveMigration).IsAssignableFrom(t) && !t.IsAbstract && t.GetConstructor(Type.EmptyTypes) != null)
                .Select(t => (ISaveMigration)Activator.CreateInstance(t))
                .ToList();
        }

        [Serializable]
        private sealed class Envelope
        {
            public List<Section> sections;
        }

        [Serializable]
        private sealed class Section
        {
            public string key;
            public int version;
            public string json;
        }

        private sealed class FixedCloud : ICloudSaveService
        {
            private readonly string _json;

            public FixedCloud(string json)
            {
                _json = json;
            }

            public bool IsAvailable => true;

            public UniTask SaveAsync(string json, CancellationToken cancellationToken = default) => UniTask.CompletedTask;

            public UniTask<string> LoadAsync(CancellationToken cancellationToken = default) => UniTask.FromResult(_json);
        }
    }

    /// <summary>JSON раздела как плоский список «путь → значение»: так сравниваются старое и новое чтение.</summary>
    public static class JsonFlattener
    {
        public static Dictionary<string, string> Flatten(string json)
        {
            var result = new Dictionary<string, string>();
            if (string.IsNullOrEmpty(json))
                return result;

            var i = 0;
            ReadValue(json, ref i, string.Empty, result);
            return result;
        }

        private static void ReadValue(string s, ref int i, string path, Dictionary<string, string> output)
        {
            SkipSpaces(s, ref i);
            var c = s[i];
            if (c == '{')
            {
                i++;
                SkipSpaces(s, ref i);
                if (s[i] == '}')
                {
                    i++;
                    return;
                }

                while (true)
                {
                    SkipSpaces(s, ref i);
                    var name = ReadString(s, ref i);
                    SkipSpaces(s, ref i);
                    i++; // ':'
                    ReadValue(s, ref i, path.Length == 0 ? name : path + "." + name, output);
                    SkipSpaces(s, ref i);
                    if (s[i++] == '}')
                        return;
                }
            }

            if (c == '[')
            {
                i++;
                SkipSpaces(s, ref i);
                var index = 0;
                if (s[i] == ']')
                {
                    i++;
                    return;
                }

                while (true)
                {
                    ReadValue(s, ref i, path + "[" + index++ + "]", output);
                    SkipSpaces(s, ref i);
                    if (s[i++] == ']')
                        return;
                }
            }

            if (c == '"')
            {
                output[path] = ReadString(s, ref i);
                return;
            }

            var start = i;
            while (i < s.Length && s[i] != ',' && s[i] != '}' && s[i] != ']' && !char.IsWhiteSpace(s[i]))
                i++;
            output[path] = s.Substring(start, i - start);
        }

        private static string ReadString(string s, ref int i)
        {
            var builder = new StringBuilder();
            i++; // открывающая кавычка
            while (s[i] != '"')
            {
                if (s[i] == '\\')
                {
                    builder.Append(s[i]);
                    i++;
                }

                builder.Append(s[i]);
                i++;
            }

            i++;
            return builder.ToString();
        }

        private static void SkipSpaces(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i]))
                i++;
        }
    }
}
