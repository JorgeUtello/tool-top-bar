using System;
using System.IO;
using System.Text.Json;

namespace ToolTopBar
{
    public class SettingsService
    {
        private readonly string _dir;
        private readonly string _filePath;

        public SettingsService()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _dir = Path.Combine(appData, "ToolTopBar");
            _filePath = Path.Combine(_dir, "settings.json");
        }

        public Settings Load()
        {
            try
            {
                if (File.Exists(_filePath))
                {
                    var json = File.ReadAllText(_filePath);
                    var s = JsonSerializer.Deserialize<Settings>(json);
                    return s ?? Settings.Default();
                }
            }
            catch
            {
                // ignore and return defaults
            }
            return Settings.Default();
        }

        public void Save(Settings settings)
        {
            try
            {
                Directory.CreateDirectory(_dir);
                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_filePath, json);
            }
            catch
            {
                // ignore persistence errors
            }
        }
    }
}
