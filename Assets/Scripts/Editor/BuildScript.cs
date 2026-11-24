using System.IO;
using System.Linq;
using Base.Platform;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Base.Editor
{
    /// <summary>
    /// Сборка под площадку одной командой: переключает площадку и запускает BuildPipeline.
    /// Меню Base/Build или из командной строки:
    ///   Unity -batchmode -quit -projectPath . -buildTarget WebGL -executeMethod Base.Editor.BuildScript.BuildYandex
    ///   Unity -batchmode -quit -projectPath . -buildTarget WebGL -executeMethod Base.Editor.BuildScript.BuildVKGames
    ///   Unity -batchmode -quit -projectPath . -buildTarget WebGL -executeMethod Base.Editor.BuildScript.BuildVKPlay
    ///   Unity -batchmode -quit -projectPath . -buildTarget Android -executeMethod Base.Editor.BuildScript.BuildRuStore
    /// Результат кладётся в Builds/&lt;площадка&gt;.
    /// </summary>
    public static class BuildScript
    {
        [MenuItem("Base/Build/Yandex Games (WebGL)", priority = 0)]
        public static void BuildYandex() => Build(PlatformId.Yandex);

        [MenuItem("Base/Build/VK Games (WebGL)", priority = 1)]
        public static void BuildVKGames() => Build(PlatformId.VKGames);

        [MenuItem("Base/Build/VK Play (WebGL)", priority = 2)]
        public static void BuildVKPlay() => Build(PlatformId.VKPlay);

        [MenuItem("Base/Build/RuStore (Android APK)", priority = 3)]
        public static void BuildRuStore() => Build(PlatformId.RuStore);

        /// <summary>Тестовая сцена с кнопкой на каждый метод площадки (реклама, покупки, сохранение). Создаёт PlatformTestSceneCreator.</summary>
        public const string TestPanelScene = "Assets/Scenes/PlatformTest.unity";

        /// <summary>
        /// Тестовая сборка: только сцена с кнопками площадки, без игры. Для проверки рекламы и покупок на площадке
        /// без прохождения партий. Кладётся туда же, куда обычная (Builds/&lt;площадка&gt;): перед выпуском пересобрать обычную.
        /// </summary>
        [MenuItem("Base/Build/Test Panel/Yandex Games (WebGL)", priority = 40)]
        public static void BuildYandexTestPanel() => TryBuild(PlatformId.Yandex, true);

        [MenuItem("Base/Build/Test Panel/VK Games (WebGL)", priority = 41)]
        public static void BuildVKGamesTestPanel() => TryBuild(PlatformId.VKGames, true);

        [MenuItem("Base/Build/Test Panel/RuStore (Android APK)", priority = 42)]
        public static void BuildRuStoreTestPanel() => TryBuild(PlatformId.RuStore, true);

        /// <summary>Define тестовой сборки: игра включает свои читы (мгновенная победа и т. п.). В обычную сборку не попадает.</summary>
        public const string CheatsDefine = "GAME_CHEATS";

        /// <summary>
        /// Тестовая сборка игры с читами (define GAME_CHEATS): что включается — решает игра. Кладётся туда же, куда обычная
        /// (Builds/&lt;площадка&gt;): перед выпуском пересобрать обычную. CLI: BuildYandexCheats, BuildVKGamesCheats, BuildRuStoreCheats.
        /// </summary>
        [MenuItem("Base/Build/Cheats/Yandex Games (WebGL)", priority = 60)]
        public static void BuildYandexCheats() => Build(PlatformId.Yandex, true);

        [MenuItem("Base/Build/Cheats/VK Games (WebGL)", priority = 61)]
        public static void BuildVKGamesCheats() => Build(PlatformId.VKGames, true);

        [MenuItem("Base/Build/Cheats/RuStore (Android APK)", priority = 62)]
        public static void BuildRuStoreCheats() => Build(PlatformId.RuStore, true);

        /// <summary>Сборка из меню или CLI. В batchmode завершает Unity с кодом результата.</summary>
        public static void Build(PlatformId platform, bool cheats = false)
        {
            var ok = TryBuild(platform, false, cheats);
            if (Application.isBatchMode)
                EditorApplication.Exit(ok ? 0 : 1);
        }

        /// <summary>Сборка без выхода из Unity: для цепочек вроде "собрать и выложить".</summary>
        public static bool TryBuild(PlatformId platform, bool testPanel = false, bool cheats = false)
        {
            if (testPanel && !File.Exists(TestPanelScene))
            {
                EditorUtility.DisplayDialog("Base", $"Нет {TestPanelScene}. Создать: Base/Setup/Create Platform Test Scene " +
                                                    "(потом убрать её из первых в Build Settings).", "OK");
                return false;
            }

            PlatformSwitcher.Apply(platform);

            var target = PlatformTargets.BuildTargetFor(platform);
            var scenes = testPanel
                ? new[] { TestPanelScene }
                : EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0)
            {
                Debug.LogError("[Base] No scenes enabled in Build Settings");
                return false;
            }

            var location = target == BuildTarget.Android
                ? $"Builds/{platform}/{ProjectIdentity.BuildFileName}.apk"
                : $"Builds/{platform}";

            if (target == BuildTarget.Android)
                EditorUserBuildSettings.buildAppBundle = false;

            // Папка WebGL-сборки заливается на хостинг целиком (выкладка VK упаковывает её всю),
            // поэтому файлы прошлой сборки, например .br после смены сжатия, удаляем заранее.
            if (target == BuildTarget.WebGL && Directory.Exists(location))
                FileUtil.DeleteFileOrDirectory(location);

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                target = target,
                targetGroup = BuildPipeline.GetBuildTargetGroup(target),
                locationPathName = location,
                options = BuildOptions.None,
            };
            if (cheats)
            {
                options.extraScriptingDefines = new[] { CheatsDefine };
                Debug.LogWarning("[Base] CHEATS build (" + CheatsDefine + "): not for release, rebuild without cheats before publishing");
            }

            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[Base] Build {platform} succeeded: {summary.outputPath} ({summary.totalSize / (1024 * 1024)} MB, {summary.totalTime:mm\\:ss})");
                return true;
            }

            Debug.LogError($"[Base] Build {platform} finished with {summary.result}: {summary.totalErrors} errors");
            return false;
        }
    }
}
