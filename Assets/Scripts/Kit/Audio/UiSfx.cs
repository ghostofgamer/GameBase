namespace Base.Audio
{
    /// <summary>
    /// Звуки интерфейса, которые шаблон играет сам: нажатие кнопки (UiPress), открытие окна (UiService), вкладка.
    /// Звуки игры — свои строковые ID в коде игры (класс констант), клипы к ним — строками SoundConfig.
    /// </summary>
    public static class UiSfx
    {
        public const string Click = "ui_click";
        public const string WindowOpen = "ui_window_open";
        public const string Tab = "ui_tab";
    }
}
