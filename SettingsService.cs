using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;

namespace FloatingReminder
{
    public static class SettingsService
    {
        private static readonly string SettingsFilePath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "settings.json"
        );

        public static Settings LoadSettings()
        {
            try
            {
                if (!File.Exists(SettingsFilePath))
                {
                    var defaultSettings = new Settings();
                    SaveSettings(defaultSettings);
                    return defaultSettings;
                }

                string json = File.ReadAllText(SettingsFilePath);
                var settings = JsonConvert.DeserializeObject<Settings>(json);

                if (settings == null)
                {
                    return new Settings();
                }

                if (settings.Items == null)
                {
                    settings.Items = new List<ReminderItem>();
                }

                return settings;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading settings: {ex.Message}");
                return new Settings();
            }
        }

        public static void SaveSettings(Settings settings)
        {
            try
            {
                string json = JsonConvert.SerializeObject(settings, Formatting.Indented);
                File.WriteAllText(SettingsFilePath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving settings: {ex.Message}");
            }
        }
    }
}