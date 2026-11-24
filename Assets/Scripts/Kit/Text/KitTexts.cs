namespace Base.Text
{
    /// <summary>
    /// Ключи текстов, которые ставит сам шаблон (ползунки громкости). Русский и английский — в LocalizationTable шаблона,
    /// остальные языки и свои формулировки — строкой с тем же ключом в таблице игры (она важнее).
    /// </summary>
    public static class KitTexts
    {
        public const string SoundVolume = "settings.soundVolume";
        public const string MusicVolume = "settings.musicVolume";

        /// <summary>Подпись значения ползунка: {0} — проценты.</summary>
        public const string VolumePercent = "settings.volumePercent";
    }
}
