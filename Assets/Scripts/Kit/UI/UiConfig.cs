using System.Collections.Generic;
using Base.UI.Transitions;
using TMPro;
using UnityEngine;

namespace Base.UI
{
    /// <summary>Палитра интерфейса. Цвета, которые окна ставят кодом, берутся отсюда, а не из констант.</summary>
    [System.Serializable]
    public sealed class UiPalette
    {
        public Color Panel = new Color(0.08f, 0.07f, 0.06f, 0.88f);
        public Color PanelLight = new Color(0.16f, 0.13f, 0.1f, 0.92f);
        public Color Button = new Color(0.55f, 0.36f, 0.16f, 1f);
        public Color ButtonAccent = new Color(0.85f, 0.6f, 0.2f, 1f);
        public Color ButtonMuted = new Color(0.3f, 0.27f, 0.24f, 1f);
        public Color Text = new Color(0.97f, 0.93f, 0.85f, 1f);
        public Color TextMuted = new Color(0.75f, 0.7f, 0.62f, 1f);
        public Color Good = new Color(0.45f, 0.85f, 0.45f, 1f);
        public Color Bad = new Color(0.95f, 0.4f, 0.35f, 1f);
        public Color Gold = new Color(1f, 0.82f, 0.35f, 1f);

        [Tooltip("Неоткрытый предмет: серым")]
        public Color Locked = new Color(0.32f, 0.32f, 0.32f, 1f);
    }

    /// <summary>
    /// Общие настройки интерфейса: шрифты, палитра, стили появления окон по умолчанию, окна-префабы, касания.
    /// Окно со своими переходами в префабе использует их, без своих — берёт отсюда.
    /// Своё у игры (значки, картинки, подсказки загрузки) — в конфиге игры, не здесь.
    /// </summary>
    [CreateAssetMenu(menuName = "Base/UI/Ui Config", fileName = "UiConfig")]
    public sealed class UiConfig : ScriptableObject
    {
        [Header("Шрифты (TextMeshPro с кириллицей: встроенный шрифт Unity в WebGL её не рисует)")]
        public TMP_FontAsset Font;
        public TMP_FontAsset BoldFont;

        public UiPalette Palette = new UiPalette();

        [Header("Переходы по умолчанию")]
        public UiTransitionConfig ScreenShow;
        public UiTransitionConfig ScreenHide;
        public UiTransitionConfig PopupShow;
        public UiTransitionConfig PopupHide;

        [Tooltip("Акцент на ошибке: «не хватает монет»")]
        public UiTransitionConfig ErrorShake;

        [Header("Окна-префабы: IUiService находит окно по типу компонента")]
        [Tooltip("Папка префабов окон: список ниже собирается из неё сам (Base/UI/Rebuild Windows)")]
        public string WindowsFolder = "Assets/GameContent/Prefabs/UI";

        public List<UiWindow> Windows = new List<UiWindow>();

        [Header("Касания")]
        [Tooltip("Сколько миллиметров палец должен сдвинуться, чтобы нажатие стало прокруткой списка. " +
                 "Меньше — на телефоне нажатия кнопок в списках теряются от лёгкого дрожания пальца")]
        public float DragThresholdMm = 2.5f;

        public T FindWindow<T>() where T : UiWindow
        {
            for (var i = 0; i < Windows.Count; i++)
            {
                if (Windows[i] is T typed)
                    return typed;
            }

            return null;
        }
    }
}
