using System;
using Base.Core.Localization;
using TMPro;

namespace Base.Text
{
    /// <summary>
    /// Текст по ключу с подстановкой («Монеты: {0}»). Ошибка формата в переводе не роняет окно: показывается шаблон.
    /// Для редкого (открытие окна, нажатие): string.Format выделяет память. Частые числа — NumberTemplate.
    /// </summary>
    public static class UiFormat
    {
        public static string Format(ILocalization localization, string key, params object[] args)
        {
            var format = localization != null ? localization.Get(key) : key;
            try
            {
                return string.Format(format, args);
            }
            catch (FormatException)
            {
                return format;
            }
        }
    }

    /// <summary>
    /// Шаблон с одним числом («Монеты: {0}», «Очки: {0}») для часто меняющегося текста: шаблон режется на префикс
    /// и суффикс один раз (и заново после смены языка), число пишется NumberText без выделения памяти.
    /// Текст перезаписывается, только если изменились число или шаблон.
    /// </summary>
    public sealed class NumberTemplate
    {
        private string _template;
        private string _prefix;
        private string _suffix;
        private int _shown;

        public void Set(TMP_Text text, string template, int value)
        {
            if (text == null)
                return;

            if (!ReferenceEquals(template, _template))
            {
                _template = template;
                Split(template, out _prefix, out _suffix);
            }
            else if (value == _shown)
            {
                return;
            }

            _shown = value;
            NumberText.Set(text, _prefix, value, _suffix);
        }

        /// <summary>«Очки: {0}» → «Очки: » и «». Без {0} число дописывается через пробел.</summary>
        public static void Split(string template, out string prefix, out string suffix)
        {
            template = template ?? string.Empty;
            var at = template.IndexOf("{0}", StringComparison.Ordinal);
            prefix = at >= 0 ? template.Substring(0, at) : template + " ";
            suffix = at >= 0 ? template.Substring(at + 3) : null;
        }
    }
}
