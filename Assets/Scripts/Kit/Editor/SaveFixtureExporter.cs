using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Base.Editor
{
    /// <summary>
    /// Что игра кладёт в снимок выпуска: папка снимков и все ID контента (в сохранении игрока лежат только ID,
    /// пропавший ID — пропавшая у игрока вещь). Игра реализует один такой класс в своей Editor-сборке.
    /// </summary>
    public interface IReleaseFixtureSource
    {
        /// <summary>Папка снимков, например Assets/MyGameContent/Scripts/Tests/Saves.</summary>
        string Folder { get; }

        /// <summary>Все ID контента выпуска. Контента с ID нет — пусто.</summary>
        IEnumerable<string> ContentIds();
    }

    /// <summary>
    /// Снимок выпуска для теста совместимости сохранений (SaveCompatibilityTestBase): сохранение игрока из редактора
    /// (save_&lt;версия&gt;.json) и список всех ID контента (content_ids_&lt;версия&gt;.txt). Запускать в день каждого выпуска
    /// (меню Base/Saves/Export Release Fixture или CLI ExportCli), файлы — в git, старые снимки не удалять.
    /// Сохранение берётся из заглушки площадки (PlayerPrefs редактора): сначала поиграть в редакторе.
    /// </summary>
    public static class SaveFixtureExporter
    {
        // Ключ StubCloudSaveService (Base.Platform.Stub): сохранение редактора.
        private const string StubSaveKey = "base.stub.save";

        [MenuItem("Base/Saves/Export Release Fixture", priority = 40)]
        public static void ExportMenu()
        {
            Export();
        }

        /// <summary>Unity -batchmode -quit -projectPath . -executeMethod Base.Editor.SaveFixtureExporter.ExportCli</summary>
        public static void ExportCli()
        {
            if (!Export())
                EditorApplication.Exit(1);
        }

        private static bool Export()
        {
            var sourceType = TypeCache.GetTypesDerivedFrom<IReleaseFixtureSource>()
                .FirstOrDefault(t => !t.IsAbstract && t.GetConstructor(Type.EmptyTypes) != null);
            if (sourceType == null)
            {
                Debug.LogError("[Saves] No IReleaseFixtureSource in the game's Editor assembly: nothing says where fixtures go");
                return false;
            }

            var source = (IReleaseFixtureSource)Activator.CreateInstance(sourceType);
            var save = PlayerPrefs.GetString(StubSaveKey, string.Empty);
            if (string.IsNullOrEmpty(save))
            {
                Debug.LogError("[Saves] No editor save (PlayerPrefs '" + StubSaveKey + "'): play in the editor first");
                return false;
            }

            var version = PlayerSettings.bundleVersion;
            var ids = (source.ContentIds() ?? Enumerable.Empty<string>())
                .Where(i => !string.IsNullOrEmpty(i)).Distinct().OrderBy(i => i, StringComparer.Ordinal);

            Directory.CreateDirectory(source.Folder);
            File.WriteAllText(Path.Combine(source.Folder, "save_" + version + ".json"), save);
            File.WriteAllLines(Path.Combine(source.Folder, "content_ids_" + version + ".txt"), ids);
            AssetDatabase.Refresh();
            Debug.Log("[Saves] Release fixture " + version + " exported to " + source.Folder);
            return true;
        }
    }
}
