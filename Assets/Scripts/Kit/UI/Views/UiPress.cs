using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Base.UI.Views
{
    /// <summary>
    /// Кнопка вдавливается под пальцем и пружинит обратно с перелётом: нажатие чувствуется, а не только темнеет.
    /// Вешается на корень кнопки (деталь Parts/Button), числа — в префабе. Выключенная кнопка не вдавливается.
    /// Каждый кадр ничего не выделяет; без анимации Update сразу выходит.
    /// </summary>
    public sealed class UiPress : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [Tooltip("Масштаб нажатой кнопки")]
        [SerializeField] private float _pressedScale = 0.92f;

        [Tooltip("Сдвиг вниз нажатой кнопки (в тень), единицы холста")]
        [SerializeField] private float _pressedDrop = 4f;

        [Tooltip("Сколько секунд кнопка уходит вниз")]
        [SerializeField] private float _pressTime = 0.05f;

        [Tooltip("Сколько секунд кнопка возвращается с перелётом")]
        [SerializeField] private float _releaseTime = 0.24f;

        [Tooltip("Звук нажатия (ID из SoundConfig). Вкладке — UiSfx.Tab")]
        [SerializeField] private string _sound = Base.Audio.UiSfx.Click;

        [Tooltip("Без звука нажатия")]
        [SerializeField] private bool _silent;

        private Button _button;
        private UiWindow _window;
        private bool _windowSearched;
        private RectTransform _rect;
        private Vector3 _baseScale = Vector3.one;
        private Vector2 _basePosition;
        private Graphic _hit;
        private Vector4 _basePadding;
        private bool _captured;
        private bool _drop;
        private bool _pressed;
        private float _time = -1f;
        private float _from;

        // Насколько кнопка нажата сейчас: 0 — отпущена, 1 — вдавлена (с перелётом бывает меньше 0).
        private float _depth;

#if UNITY_EDITOR
        public void EditorSetup(float pressedScale, float pressedDrop, float pressTime, float releaseTime)
        {
            _pressedScale = pressedScale;
            _pressedDrop = pressedDrop;
            _pressTime = pressTime;
            _releaseTime = releaseTime;
        }
#endif

        private void Awake()
        {
            _button = GetComponent<Button>();
            _rect = (RectTransform)transform;
        }

        private void OnDisable()
        {
            // Окно закрыли посреди нажатия: вернуть как было, иначе следующий показ начнётся вдавленным.
            if (_captured)
                Apply(0f);
            _pressed = false;
            _time = -1f;
            _depth = 0f;
            _captured = false;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_button != null && !_button.IsInteractable())
                return;

            Capture();
            _pressed = true;
            Begin();
            PlaySound();
        }

        /// <summary>Щелчок звучит на нажатии, вместе с вдавливанием. Звуки — у окна, в котором кнопка (SoundPlayer сцены).</summary>
        private void PlaySound()
        {
            if (_silent)
                return;

            if (!_windowSearched)
            {
                // Один раз на кнопку: окно, в котором она лежит, не меняется.
                _window = GetComponentInParent<UiWindow>(true);
                _windowSearched = true;
            }

            if (_window != null && _window.Sound != null)
                _window.Sound.Play(_sound, 1f, 0.04f);
        }

        /// <summary>
        /// Свой звук нажатия кнопке (вкладкам — UiSfx.Tab). Вызывать при создании окна (не каждый кадр): на шаблоне —
        /// копии получат звук сами. Кнопка без UiPress (старый префаб) — ничего.
        /// </summary>
        public static void UseSound(Component button, string sound)
        {
            if (button == null)
                return;
            var press = button.GetComponent<UiPress>();
            if (press != null)
                press._sound = sound;
        }

#if UNITY_EDITOR
        public void EditorSetupSound(string sound, bool silent)
        {
            _sound = sound;
            _silent = silent;
        }
#endif

        public void OnPointerUp(PointerEventData eventData)
        {
            Release();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            Release();
        }

        private void Release()
        {
            if (!_pressed)
                return;

            _pressed = false;
            Begin();
        }

        /// <summary>Исходные масштаб и место запоминаются в начале нажатия: раскладка могла их сменить.</summary>
        private void Capture()
        {
            if (_captured)
                return;

            _captured = true;
            _baseScale = _rect.localScale;
            _basePosition = _rect.anchoredPosition;
            // Что ловит касание: картинка корня, иначе цель кнопки. Её область расширяется на время нажатия (Apply).
            _hit = GetComponent<Graphic>();
            if (_hit == null && _button != null)
                _hit = _button.targetGraphic;
            if (_hit != null)
                _basePadding = _hit.raycastPadding;
            // В раскладке место задаёт она: сдвиг вниз только у свободной кнопки, иначе раскладка вернёт её рывком.
            _drop = _rect.parent == null || _rect.parent.GetComponent<LayoutGroup>() == null;
        }

        private void Begin()
        {
            _from = _depth;
            _time = 0f;
        }

        private void Update()
        {
            if (_time < 0f)
                return;

            _time += Time.unscaledDeltaTime;
            if (_pressed)
            {
                var t = _pressTime > 0f ? Mathf.Clamp01(_time / _pressTime) : 1f;
                _depth = Mathf.Lerp(_from, 1f, t);
                if (t >= 1f)
                    _time = -1f;
            }
            else
            {
                var t = _releaseTime > 0f ? Mathf.Clamp01(_time / _releaseTime) : 1f;
                // Пружина: проскакивает чуть выше покоя и успокаивается.
                _depth = _from * (1f - t) * Mathf.Cos(t * Mathf.PI * 1.5f);
                if (t >= 1f)
                {
                    _depth = 0f;
                    _time = -1f;
                }
            }

            Apply(_depth);
            if (_time < 0f && !_pressed)
                _captured = false;
        }

        private void Apply(float depth)
        {
            var scale = 1f + (_pressedScale - 1f) * depth;
            _rect.localScale = _baseScale * scale;
            if (_drop)
                _rect.anchoredPosition = _basePosition + new Vector2(0f, -_pressedDrop * depth);
            KeepHitArea(scale, depth);
        }

        /// <summary>
        /// Кнопка сжимается и уходит вниз, а касание засчитывается по отпусканию над ней: у края палец оказывался
        /// уже снаружи — щелчок звучал, нажатие терялось. Поэтому область касания на время нажатия расширяется
        /// (отрицательный raycastPadding) так, чтобы покрыть место кнопки до нажатия. Видимая кнопка не меняется.
        /// </summary>
        private void KeepHitArea(float scale, float depth)
        {
            if (_hit == null)
                return;
            if (depth <= 0f || scale <= 0f)
            {
                _hit.raycastPadding = _basePadding;
                return;
            }

            var size = _hit.rectTransform.rect.size;
            var grow = 1f / scale - 1f;
            var sideX = size.x * grow;
            var sideY = size.y * grow;
            var drop = _drop ? _pressedDrop * depth / Mathf.Max(0.01f, scale * Mathf.Abs(_baseScale.y)) : 0f;
            // Порядок raycastPadding: слева, снизу, справа, сверху; меньше — шире.
            _hit.raycastPadding = new Vector4(_basePadding.x - sideX, _basePadding.y - sideY,
                _basePadding.z - sideX, _basePadding.w - sideY - drop);
        }
    }
}
