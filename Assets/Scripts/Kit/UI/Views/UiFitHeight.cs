using UnityEngine;
using UnityEngine.UI;

namespace Base.UI.Views
{
    /// <summary>
    /// Колонка окна с раскладкой (VerticalLayoutGroup), растянутая на панель: если строки не влезают по высоте,
    /// колонка уменьшается целиком (масштабом), чтобы нижние кнопки остались в окне. Влезает — масштаб 1.
    /// Окно зовёт <see cref="SetDirty"/> после того, как добавило строки; пересчёт — в ближайшем LateUpdate, один раз.
    /// Объект должен быть растянут на родителя (якоря stretch, центр 0.5).
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class UiFitHeight : MonoBehaviour
    {
        [Tooltip("Меньше этого масштаб не опускается: текст должен оставаться читаемым")]
        [SerializeField] private float _minScale = 0.6f;

        private RectTransform _rect;
        private bool _dirty = true;
        private bool _fitting;

        public void SetDirty()
        {
            _dirty = true;
        }

        private void Awake()
        {
            _rect = (RectTransform)transform;
        }

        private void OnEnable()
        {
            _dirty = true;
        }

        private void OnRectTransformDimensionsChange()
        {
            // Своё изменение размера из Fit сюда тоже попадёт: флаг снимается после Fit, повторно не зациклится,
            // потому что после подгонки нужная высота уже влезает.
            if (_rect != null && !_fitting)
                _dirty = true;
        }

        private void LateUpdate()
        {
            if (!_dirty)
                return;

            _dirty = false;
            Fit();
        }

        private void Fit()
        {
            var parent = _rect.parent as RectTransform;
            if (parent == null)
                return;

            _fitting = true;
            _rect.localScale = Vector3.one;
            _rect.sizeDelta = Vector2.zero;
            LayoutRebuilder.ForceRebuildLayoutImmediate(_rect);

            var available = parent.rect.height;
            var needed = LayoutUtility.GetPreferredHeight(_rect);
            if (needed > available && available > 0f)
            {
                var scale = Mathf.Max(_minScale, available / needed);
                var size = parent.rect.size;
                _rect.localScale = new Vector3(scale, scale, 1f);
                // Растянутый прямоугольник увеличивается так, чтобы после уменьшения занять ту же панель.
                _rect.sizeDelta = new Vector2(size.x / scale - size.x, size.y / scale - size.y);
                LayoutRebuilder.ForceRebuildLayoutImmediate(_rect);
            }

            _fitting = false;
        }
    }
}
