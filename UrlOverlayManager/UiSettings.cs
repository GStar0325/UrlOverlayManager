using System;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;

namespace UrlOverlayManager
{
    public class UiSettings
    {
        public string ThemeName { get; set; } = "Light";
        public string FontMode { get; set; } = UiTheme.AppFontMode;
        public bool ShowMainWindowBorder { get; set; } = true;
        public string LyricsSearchDirectory { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        public bool LyricsOverlayClickThrough { get; set; } = true;
        public int LyricsOverlayX { get; set; } = 100;
        public int LyricsOverlayY { get; set; } = 100;
        public int LyricsOverlayWidth { get; set; } = 1280;
        public int LyricsOverlayHeight { get; set; } = 220;
    }

    public static class UiSettingsStore
    {
        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "UrlOverlayManager",
            "ui-settings.json");

        public static UiSettings Load()
        {
            if (!File.Exists(SettingsPath))
                return new UiSettings();

            try
            {
                string json = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize<UiSettings>(json) ?? new UiSettings();
            }
            catch
            {
                return new UiSettings();
            }
        }

        public static void Save(UiSettings settings)
        {
            string? directory = Path.GetDirectoryName(SettingsPath);

            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            JsonSerializerOptions options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, options));
        }

        public static void Apply(UiSettings settings)
        {
            UiTheme.ApplyTheme(settings.ThemeName);
            UiTheme.ApplyFont(settings.FontMode);
        }
    }
}
