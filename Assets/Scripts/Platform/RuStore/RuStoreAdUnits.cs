namespace Base.Platform.RuStore
{
    /// <summary>
    /// Рекламные блоки Яндекс Рекламы для Android-сборки.
    ///
    /// Свои блоки каждой игры вида R-M-XXXXXX-Y из кабинета Рекламной сети Яндекса (partner.yandex.ru):
    /// там заводится приложение и по блоку на каждый формат. Приложение РСЯ находит только опубликованное в магазине,
    /// поэтому до публикации здесь демо-блоки: они не требуют регистрации и всегда отдают тестовую рекламу.
    /// </summary>
    internal static class RuStoreAdUnits
    {
        public const string Interstitial = "demo-interstitial-yandex";
        public const string Rewarded = "demo-rewarded-yandex";
    }
}
