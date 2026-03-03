using System;
using System.IO;
using System.Text.Json;

namespace PaperManagementApp.Services
{
    public class AppSettings
    {
        public string PdfSavePath { get; set; }
    }

    public static class SettingsService
    {
        private static readonly string SettingsFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PaperManagementApp",
            "settings.json"
        );

        private static readonly string DefaultPdfSavePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PaperManagementApp",
            "PDFs"
        );

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    string json = File.ReadAllText(SettingsFilePath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json);
                    if (settings != null && !string.IsNullOrEmpty(settings.PdfSavePath))
                        return settings;
                }
            }
            catch
            {
                // 読み込み失敗時はデフォルト設定を返す
            }

            return new AppSettings { PdfSavePath = DefaultPdfSavePath };
        }

        public static void Save(AppSettings settings)
        {
            string dir = Path.GetDirectoryName(SettingsFilePath);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsFilePath, json);
        }
    }
}
