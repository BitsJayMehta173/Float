using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq; // Added for OrderBy

namespace FloatingReminder
{
    public static class SettingsService
    {
        public const string GuestUsername = "_guest";

        // --- NEW: Define the dedicated, safe storage folder ---
        private static readonly string StorageDirectory;

        // --- NEW: Static constructor ---
        // This code runs ONCE when the app first starts.
        // It finds the correct AppData folder and creates our storage directory.
        static SettingsService()
        {
            // 1. Get the C:\Users\<YourName>\AppData\Roaming folder
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

            // 2. Combine it with our app's name to get: ...\AppData\Roaming\FloatingReminder
            StorageDirectory = Path.Combine(appDataPath, "FloatingReminder");

            // 3. Create the folder if it doesn't exist
            if (!Directory.Exists(StorageDirectory))
            {
                Directory.CreateDirectory(StorageDirectory);
            }
        }

        // --- UPDATED: GetSettingsFilePath ---
        // This method now uses the new "StorageDirectory" instead of the .exe folder
        private static string GetSettingsFilePath(string username)
        {
            // Clean username to make it a valid filename
            string safeUsername = string.Join("_", username.Split(Path.GetInvalidFileNameChars()));
            string fileName = $"settings_{safeUsername}.json";

            // This now correctly points to:
            // ...\AppData\Roaming\FloatingReminder\settings_username.json
            return Path.Combine(StorageDirectory, fileName);
        }

        // --- UPDATED: LoadSettings ---
        // Added sorting logic here to keep it clean
        public static Settings LoadSettings(string username)
        {
            string settingsFilePath = GetSettingsFilePath(username);
            try
            {
                if (!File.Exists(settingsFilePath))
                {
                    var defaultSettings = new Settings();
                    if (username == GuestUsername)
                    {
                        SaveSettings(defaultSettings, username);
                    }
                    return defaultSettings;
                }

                string json = File.ReadAllText(settingsFilePath);
                var settings = JsonConvert.DeserializeObject<Settings>(json);

                if (settings == null)
                {
                    return new Settings();
                }

                if (settings.Items == null)
                {
                    settings.Items = new List<ReminderItem>();
                }

                // Centralize sorting: Always sort by time when loading
                settings.Items = settings.Items.OrderBy(item => item.CreatedAt).ToList();

                return settings;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading settings: {ex.Message}");
                return new Settings();
            }
        }

        // --- UPDATED: SaveSettings ---
        // Added sorting logic here to keep it clean
        public static void SaveSettings(Settings settings, string username)
        {
            string settingsFilePath = GetSettingsFilePath(username);
            try
            {
                // Centralize sorting: Always sort by time before saving
                settings.Items = settings.Items.OrderBy(item => item.CreatedAt).ToList();

                string json = JsonConvert.SerializeObject(settings, Formatting.Indented);
                File.WriteAllText(settingsFilePath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving settings: {ex.Message}");
            }
        }

        // --- (This method is unchanged) ---
        public static Settings GetGuestSettings()
        {
            return LoadSettings(GuestUsername);
        }
    }
}