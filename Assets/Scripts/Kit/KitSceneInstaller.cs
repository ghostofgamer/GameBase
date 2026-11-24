using Base.Audio;
using Base.UI;
using UnityEngine;
using Zenject;

namespace Base
{
    /// <summary>
    /// Сценовые части Base.Kit: окна (IUiService) в корне UI сцены, кнопка «Назад», звуки и музыка сцены.
    /// Ставится в SceneContext сцены с интерфейсом. Ссылки проставляет генератор сцены игры (EditorSetup), не поиск по сцене.
    /// </summary>
    public sealed class KitSceneInstaller : MonoInstaller
    {
        [Tooltip("Корень UI сцены: объект с UiSafeArea, окна создаются внутри него")]
        [SerializeField] private RectTransform _uiRoot;

        [Tooltip("Звуки сцены. Пусто — окна и кнопки молчат")]
        [SerializeField] private SoundPlayer _sound;

        [Tooltip("Музыка и фон окружения сцены (MusicPlayer с каналами Music и Ambience). Можно пусто")]
        [SerializeField] private MusicPlayer[] _loops = new MusicPlayer[0];

#if UNITY_EDITOR
        public void EditorSetup(RectTransform uiRoot, SoundPlayer sound, params MusicPlayer[] loops)
        {
            _uiRoot = uiRoot;
            _sound = sound;
            _loops = loops ?? new MusicPlayer[0];
        }
#endif

        public override void InstallBindings()
        {
            if (_uiRoot != null)
            {
                Container.Bind<UnityEngine.RectTransform>().WithId(UiService.UiRootId).FromInstance(_uiRoot);
                Container.BindInterfacesTo<UiService>().AsSingle();
                Container.BindInterfacesTo<UiBackButton>().AsSingle();
            }

            if (_sound != null)
                Container.BindInstance(_sound).AsSingle();

            for (var i = 0; i < _loops.Length; i++)
            {
                var loop = _loops[i];
                if (loop != null)
                    Container.Bind<MusicPlayer>().WithId(loop.Channel).FromInstance(loop);
            }
        }
    }
}
