using System.Collections.Generic;
using System.Linq;
using Base.UI;
using UnityEditor;
using UnityEngine;

namespace Base.Editor
{
    /// <summary>
    /// Список окон UiConfig.Windows собирается сам: все префабы с компонентом UiWindow в папке UiConfig.WindowsFolder.
    /// Новое окно = новый префаб в этой папке, руками в список его добавлять не нужно.
    /// Окна с OpensThroughService = false (экран загрузки в сцене) в список не входят.
    /// </summary>
    public sealed class UiWindowRegistry : AssetPostprocessor
    {
        private static bool _scheduled;

        [MenuItem("Base/UI/Rebuild Windows", priority = 20)]
        public static void Rebuild()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:" + nameof(UiConfig)))
            {
                var config = AssetDatabase.LoadAssetAtPath<UiConfig>(AssetDatabase.GUIDToAssetPath(guid));
                if (config != null)
                    Rebuild(config);
            }
        }

        public static void Rebuild(UiConfig config)
        {
            if (string.IsNullOrEmpty(config.WindowsFolder) || !AssetDatabase.IsValidFolder(config.WindowsFolder))
                return;

            var windows = new List<UiWindow>();
            foreach (var prefabGuid in AssetDatabase.FindAssets("t:Prefab", new[] { config.WindowsFolder }))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(prefabGuid));
                var window = prefab != null ? prefab.GetComponent<UiWindow>() : null;
                if (window != null && window.OpensThroughService)
                    windows.Add(window);
            }

            windows.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            if (config.Windows.SequenceEqual(windows))
                return;

            config.Windows = windows;
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssetIfDirty(config);
            Debug.Log($"[UI] Windows registered in {config.name}: {string.Join(", ", windows.Select(w => w.name))}");
        }

        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (_scheduled || !imported.Concat(deleted).Concat(moved).Any(p => p.EndsWith(".prefab")))
                return;

            _scheduled = true;
            EditorApplication.delayCall += () =>
            {
                _scheduled = false;
                Rebuild();
            };
        }
    }
}
