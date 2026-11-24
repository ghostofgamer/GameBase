using System;
using System.Collections.Generic;
using System.Threading;
using Base.Audio;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Zenject;

namespace Base.UI
{
    /// <summary>
    /// Окна-префабы: открыть по типу, закрыть. Окно создаётся из префаба только при открытии
    /// и удаляется при закрытии. Префабы перечислены в UiConfig.Windows (собирает UiWindowRegistry в редакторе).
    /// Окна — прямые ссылки (лёгкие; шрифты и значки иначе копировались бы в каждый пакет Addressables).
    /// </summary>
    public interface IUiService
    {
        /// <summary>Создать окно из префаба и показать. setup вызывается до анимации показа: заполнить окно данными.</summary>
        UniTask<T> OpenAsync<T>(CancellationToken ct, Action<T> setup = null) where T : UiWindow;

        UniTask CloseAsync(UiWindow window, CancellationToken ct);

        /// <summary>Открытое последним окно; нет окон — null.</summary>
        UiWindow Top { get; }
    }

    public sealed class UiService : IUiService, IDisposable
    {
        /// <summary>ID биндинга корневого RectTransform (объект с UiSafeArea), в котором создаются окна.</summary>
        public const string UiRootId = "UiRoot";

        private readonly UiConfig _config;
        private readonly RectTransform _root;
        private readonly DiContainer _container;
        private readonly SoundPlayer _sound;
        private readonly List<UiWindow> _open = new List<UiWindow>();
        private bool _dragThresholdSet;

        public UiService(UiConfig config, [Inject(Id = UiRootId)] RectTransform root, DiContainer container,
            [InjectOptional] SoundPlayer sound = null)
        {
            _config = config;
            _root = root;
            _container = container;
            _sound = sound;
        }

        public UiWindow Top => _open.Count > 0 ? _open[_open.Count - 1] : null;

        /// <summary>
        /// Порог прокрутки в миллиметрах, а не в пикселях: у Unity 10 px, на плотном экране телефона это меньше миллиметра,
        /// и дрожание пальца превращает нажатие кнопки в списке в прокрутку. Один раз, при первом окне (EventSystem уже есть).
        /// </summary>
        private void ApplyDragThreshold()
        {
            if (_dragThresholdSet)
                return;
            var system = UnityEngine.EventSystems.EventSystem.current;
            if (system == null || _config == null)
                return;

            _dragThresholdSet = true;
            var dpi = Screen.dpi > 0f ? Screen.dpi : 96f;
            var pixels = Mathf.RoundToInt(_config.DragThresholdMm / 25.4f * dpi);
            system.pixelDragThreshold = Mathf.Max(system.pixelDragThreshold, pixels);
        }

        public async UniTask<T> OpenAsync<T>(CancellationToken ct, Action<T> setup = null) where T : UiWindow
        {
            var prefab = _config != null ? _config.FindWindow<T>() : null;
            if (prefab == null)
                throw new InvalidOperationException($"Window {typeof(T).Name} is not in UiConfig.Windows: " +
                                                    "its prefab is missing in UiConfig.WindowsFolder (Base/UI/Rebuild Windows)");

            ApplyDragThreshold();
            var window = _container.InstantiatePrefabForComponent<T>(prefab, _root);
            window.ApplyDefaults(_config);
            setup?.Invoke(window);
            _open.Add(window);
            if (window.PlaysOpenSound && _sound != null)
                _sound.Play(UiSfx.WindowOpen, 1f, 0.03f);
            await window.ShowAsync(ct);
            return window;
        }

        public async UniTask CloseAsync(UiWindow window, CancellationToken ct)
        {
            if (window == null)
                return;

            _open.Remove(window);
            try
            {
                await window.HideAsync(ct);
            }
            finally
            {
                if (window != null)
                    UnityEngine.Object.Destroy(window.gameObject);
            }
        }

        public void Dispose()
        {
            for (var i = 0; i < _open.Count; i++)
            {
                if (_open[i] != null)
                    UnityEngine.Object.Destroy(_open[i].gameObject);
            }

            _open.Clear();
        }
    }
}
