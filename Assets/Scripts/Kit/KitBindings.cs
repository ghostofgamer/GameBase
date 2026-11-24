using Base.Audio;
using Base.Settings;
using Base.UI;
using Zenject;

namespace Base
{
    /// <summary>
    /// Биндинги общих частей игры (Base.Kit). Проектные — из MonoInstaller игры в ProjectContext:
    /// <c>KitBindings.InstallProject(Container, soundConfig, uiConfig)</c>. Сценовые — KitSceneInstaller в SceneContext сцены с UI.
    /// </summary>
    public static class KitBindings
    {
        /// <summary>Настройки игрока и конфиги звука и интерфейса: живут всё время работы игры.</summary>
        public static void InstallProject(DiContainer container, SoundConfig sound, UiConfig ui)
        {
            container.Bind<IGameSettings>().To<GameSettings>().AsSingle();
            if (sound != null)
                container.BindInstance(sound).AsSingle();
            if (ui != null)
                container.BindInstance(ui).AsSingle();
        }
    }
}
