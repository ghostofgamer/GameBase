using System.Threading;
using Base.Audio;
using Base.Core.Localization;
using Base.Settings;
using Base.Text;
using Base.UI.Transitions;
using Base.UI.Views;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Zenject;

namespace Base.UI
{
    /// <summary>
    /// Окно интерфейса как префаб. Появление и скрытие задаются ассетами UiTransitionConfig (поля в префабе),
    /// без них берутся стили по умолчанию из UiConfig. Окно открывает IUiService: создаёт из префаба при открытии
    /// и удаляет при закрытии, поэтому закрытые окна не занимают память.
    /// Корень окна растянут на весь родитель (затемнение), содержимое — дочерняя Panel по центру, не больше 1760×1000.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public abstract class UiWindow : MonoBehaviour
    {
        [SerializeField] private UiTransitionConfig _show;
        [SerializeField] private UiTransitionConfig _hide;

        [Tooltip("Модальное окно: использует переходы попапа по умолчанию")]
        [SerializeField] private bool _popup;

        private CanvasGroup _group;
        private UiTransitionTarget _target;
        private UniTaskCompletionSource _closeRequest = new UniTaskCompletionSource();

        /// <summary>Тексты окна: ключи из таблицы локализации.</summary>
        protected ILocalization Localization { get; private set; }

        /// <summary>Звуки сцены (щелчки кнопок окна — UiPress); в сцене без SoundPlayer — null.</summary>
        public SoundPlayer Sound { get; private set; }

        [Inject]
        public void InjectKit(ILocalization localization, [InjectOptional] SoundPlayer sound)
        {
            Localization = localization;
            Sound = sound;
        }

        /// <summary>
        /// Звук открытия окна (UiSfx.WindowOpen). Постоянные экраны (меню, HUD, загрузка, итоги со своим звуком,
        /// подсказки посреди игры) открываются молча.
        /// </summary>
        public virtual bool PlaysOpenSound => true;

        /// <summary>Корневой экран (главное меню): «Назад» на Android здесь спрашивает о выходе из игры.</summary>
        public virtual bool IsRootScreen => false;

        /// <summary>Окно открывается через IUiService (попадает в UiConfig.Windows). Экран загрузки в сцене — false.</summary>
        public virtual bool OpensThroughService => true;

        protected string T(string key)
        {
            return Localization != null ? Localization.Get(key) : key;
        }

        protected string T(string key, params object[] args)
        {
            return UiFormat.Format(Localization, key, args);
        }

        /// <summary>
        /// Ползунки громкости окна (настройки, пауза): звуки и музыка. 0 — выключено, отдельных выключателей нет.
        /// Отпустил ползунок звуков — пробный щелчок на новой громкости. Нет ползунка в префабе — строки просто нет.
        /// Тексты — ключи KitTexts.SoundVolume, MusicVolume, VolumePercent.
        /// </summary>
        protected void BindVolume(IGameSettings settings, VolumeSliderView sound, VolumeSliderView music)
        {
            if (settings == null)
                return;

            if (sound != null)
            {
                sound.Setup(T(KitTexts.SoundVolume), settings.SoundVolume, VolumePercent, v => settings.SoundVolume = v,
                    () =>
                    {
                        if (Sound != null)
                            Sound.Play(UiSfx.Click);
                    });
            }

            if (music != null)
                music.Setup(T(KitTexts.MusicVolume), settings.MusicVolume, VolumePercent, v => settings.MusicVolume = v, null);
        }

        private string VolumePercent(int percent) => T(KitTexts.VolumePercent, percent);

        protected CanvasGroup Group => _group != null ? _group : _group = GetComponent<CanvasGroup>();

        private UiTransitionTarget Target => _target ?? (_target = new UiTransitionTarget((RectTransform)transform, Group));

#if UNITY_EDITOR
        public void EditorSetupTransitions(UiTransitionConfig show, UiTransitionConfig hide, bool popup)
        {
            _show = show;
            _hide = hide;
            _popup = popup;
        }
#endif

        /// <summary>Стили по умолчанию для окна, у которого в префабе переходы не заданы.</summary>
        public void ApplyDefaults(UiConfig config)
        {
            if (config == null)
                return;

            if (_show == null)
                _show = _popup ? config.PopupShow : config.ScreenShow;
            if (_hide == null)
                _hide = _popup ? config.PopupHide : config.ScreenHide;
        }

        public async UniTask ShowAsync(CancellationToken ct)
        {
            gameObject.SetActive(true);
            _closeRequest = new UniTaskCompletionSource();
            Group.blocksRaycasts = true;
            Group.interactable = false;
            OnShowing();

            if (_show != null)
                await _show.PlayAsync(Target, ct);
            else
                Target.Reset();

            Group.interactable = true;
            OnShown();
        }

        public async UniTask HideAsync(CancellationToken ct)
        {
            Group.interactable = false;
            Group.blocksRaycasts = false;
            OnHiding();

            if (_hide != null)
                await _hide.PlayAsync(Target, ct);

            gameObject.SetActive(false);
            Target.Reset();
        }

        /// <summary>Кнопка, которую нажимает «Назад» Android и Escape. Нет — кнопка «Назад» в этом окне ничего не делает.</summary>
        protected virtual UiButtonView BackButton => null;

        /// <summary>Окно показано и принимает нажатия: на время анимации «Назад» не действует.</summary>
        public bool AcceptsBack => Group.interactable;

        /// <summary>Нажата кнопка «Назад». По умолчанию — как нажатие BackButton, если она видна и доступна.</summary>
        public virtual void OnBackPressed()
        {
            var button = BackButton;
            if (button != null && button.isActiveAndEnabled && button.Interactable)
                button.Button.onClick.Invoke();
        }

        /// <summary>Окно просит закрыть себя (кнопка «Закрыть»). Закрывает тот, кто открыл, через IUiService.</summary>
        public UniTask WaitCloseRequestAsync(CancellationToken ct)
        {
            return _closeRequest.Task.AttachExternalCancellation(ct);
        }

        protected void RequestClose()
        {
            _closeRequest.TrySetResult();
        }

        /// <summary>Вызов перед анимацией показа: заполнить тексты, подписаться.</summary>
        protected virtual void OnShowing()
        {
        }

        protected virtual void OnShown()
        {
        }

        /// <summary>Вызов перед анимацией скрытия: отписаться.</summary>
        protected virtual void OnHiding()
        {
        }
    }
}
