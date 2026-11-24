using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Base.UI.Views
{
    /// <summary>
    /// Ползунок громкости (деталь Parts/VolumeSlider): подпись слева, ползунок, число процентов справа.
    /// Changed — на каждый сдвиг (громкость меняется сразу, сохранение отложенное), Released — когда игрок отпустил
    /// (окно даёт пробный звук, чтобы было слышно, какая громкость). Компонент стоит рядом со Slider: отпускание
    /// приходит тому объекту, на котором нажали.
    /// </summary>
    public sealed class VolumeSliderView : MonoBehaviour, IPointerUpHandler
    {
        [SerializeField] private Slider _slider;
        [SerializeField] private TMP_Text _label;
        [SerializeField] private TMP_Text _value;

        [Tooltip("Значок строки (динамик, нота): картинку ставит копия детали в окне")]
        [SerializeField] private Image _icon;

        [Tooltip("Заливка (Image Filled, слева направо) на всю длину жёлоба: заполняется до середины ручки. " +
                 "Пусто — заливку ведёт сам Slider (fillRect)")]
        [SerializeField] private Image _fill;

        private Action<float> _changed;
        private Action _released;
        private int _shownPercent = -1;
        private Func<int, string> _format;

        public Slider Slider => _slider;
        public Image Icon => _icon;

#if UNITY_EDITOR
        public void EditorSetup(Slider slider, TMP_Text label, TMP_Text value, Image icon, Image fill)
        {
            _slider = slider;
            _label = label;
            _value = value;
            _icon = icon;
            _fill = fill;
        }
#endif

        private void Awake()
        {
            if (_slider != null)
                _slider.onValueChanged.AddListener(OnValueChanged);
        }

        /// <param name="format">Подпись значения по процентам (ключ локализации у окна); null — без числа.</param>
        public void Setup(string label, float value, Func<int, string> format, Action<float> changed, Action released)
        {
            _changed = changed;
            _released = released;
            _format = format;
            if (_label != null)
                _label.text = label;
            if (_slider != null)
            {
                _slider.minValue = 0f;
                _slider.maxValue = 1f;
                _slider.SetValueWithoutNotify(value);
            }

            _shownPercent = -1;
            ShowValue(value);
            UpdateFill(value);
        }

        private void OnValueChanged(float value)
        {
            ShowValue(value);
            UpdateFill(value);
            _changed?.Invoke(value);
        }

        /// <summary>Ширина ползунка стала известна (вёрстка окна) или сменилась: заливка пересчитывается.</summary>
        private void OnRectTransformDimensionsChange()
        {
            if (_slider != null)
                UpdateFill(_slider.value);
        }

        /// <summary>
        /// Заливка до середины ручки: ручка ходит в жёлобе, отступив на полручки от краёв, поэтому доля заливки —
        /// (полручки + значение × (жёлоб − ручка)) / жёлоб. Край заливки всегда под ручкой, без зазора и выступа.
        /// </summary>
        private void UpdateFill(float value)
        {
            if (_fill == null)
                return;

            var width = _fill.rectTransform.rect.width;
            var knob = _slider != null && _slider.handleRect != null ? _slider.handleRect.rect.width : 0f;
            _fill.fillAmount = width > knob && width > 0f
                ? Mathf.Clamp01((knob * 0.5f + value * (width - knob)) / width)
                : value;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _released?.Invoke();
        }

        /// <summary>Число меняется, только если изменились проценты.</summary>
        private void ShowValue(float value)
        {
            if (_value == null || _format == null)
                return;

            var percent = Mathf.RoundToInt(value * 100f);
            if (percent == _shownPercent)
                return;

            _shownPercent = percent;
            _value.text = _format(percent);
        }
    }
}
