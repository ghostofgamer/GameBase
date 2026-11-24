#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace Base.Platform.Web
{
    /// <summary>
    /// Аналитика web-площадок (Яндекс Игры, VK) через Яндекс Метрику. Счётчик подключает страница игры
    /// (номер — ProjectIdentity.MetrikaCounterId). Событие уходит «параметром визита»: имя события — верхняя папка,
    /// дальше дерево из JSON, как в AppMetrica (отчёт «Посетители» → «Параметры визитов»). Площадка — свойство игрока
    /// platform (отчёт «Параметры посетителей»), по нему Яндекс и VK смотрятся отдельно.
    /// В редакторе ничего не отправляет.
    /// </summary>
    public sealed class WebMetrikaAnalyticsService : IAnalyticsService
    {
        private readonly string _platform;
        private bool _platformSent;

        public WebMetrikaAnalyticsService(PlatformId platform)
        {
            _platform = platform.ToString().ToLowerInvariant();
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void WebMetrikaEvent(string name, string json);
        [DllImport("__Internal")] private static extern void WebMetrikaUserParam(string key, string value, double number, int isNumber);
        [DllImport("__Internal")] private static extern int WebMetrikaIsAvailable();

        public bool IsAvailable => WebMetrikaIsAvailable() != 0;
#else
        public bool IsAvailable => false;
#endif

        public void ReportEvent(string name, string parametersJson)
        {
            if (string.IsNullOrEmpty(name))
                return;

            SendPlatformOnce();
#if UNITY_WEBGL && !UNITY_EDITOR
            WebMetrikaEvent(name, string.IsNullOrEmpty(parametersJson) ? null : parametersJson);
#endif
        }

        public void SetUserProperty(string key, string value)
        {
            if (string.IsNullOrEmpty(key))
                return;
#if UNITY_WEBGL && !UNITY_EDITOR
            WebMetrikaUserParam(key, value ?? string.Empty, 0, 0);
#endif
        }

        public void SetUserProperty(string key, double value)
        {
            if (string.IsNullOrEmpty(key))
                return;
#if UNITY_WEBGL && !UNITY_EDITOR
            WebMetrikaUserParam(key, null, value, 1);
#endif
        }

        /// <summary>Метрика отправляет сама и при закрытии страницы.</summary>
        public void Flush()
        {
        }

        private void SendPlatformOnce()
        {
            if (_platformSent)
                return;
            _platformSent = true;
            SetUserProperty("platform", _platform);
        }
    }
}
