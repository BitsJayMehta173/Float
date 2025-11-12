using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FloatingReminder
{
    public static class SettingsService
    {
        public const string GuestUsername = "_guest";
        private static readonly string StorageDirectory;

        static SettingsService()
        {
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            StorageDirectory = Path.Combine(appDataPath, "FloatingReminder");
            if (!Directory.Exists(StorageDirectory))
            {
                Directory.CreateDirectory(StorageDirectory);
            }
        }

        // --- Settings File (Preferences) ---

        private static string GetSettingsFilePath(string username)
        {
            string safeUsername = string.Join("_", username.Split(Path.GetInvalidFileNameChars()));
            string fileName = $"settings_{safeUsername}.json";
            return Path.Combine(StorageDirectory, fileName);
        }

        public static Settings LoadSettings(string username)
        {
            string settingsFilePath = GetSettingsFilePath(username);
            var defaultSettings = new Settings();
            try
            {
                if (!File.Exists(settingsFilePath))
                {
                    if (username == GuestUsername)
                    {
                        // For guests, we create one "default" collection
                        var collections = new List<ReminderCollection>
                        {
                            new ReminderCollection { Title = "My First List" }
                        };
                        SaveCollections(collections, username);
                        // And set it as active
                        defaultSettings.ActiveCollectionId = collections[0].Id;
                        SaveSettings(defaultSettings, username);
                    }
                    return defaultSettings;
                }

                string json = File.ReadAllText(settingsFilePath);
                var settings = JsonConvert.DeserializeObject<Settings>(json);

                if (settings == null) return defaultSettings;

                // --- THIS IS THE FIX for the "Element 'Items' does not match" error ---
                // We check if ActiveCollectionId is null, which means it's an old file
                if (settings.ActiveCollectionId == null)
                {
                    // This user has old local data. We must migrate it.
                    dynamic rawJson = JsonConvert.DeserializeObject(json);
                    List<ReminderItem> oldItems = null;

                    if (rawJson.ActiveMessageList != null)
                    {
                        oldItems = rawJson.ActiveMessageList.ToObject<List<ReminderItem>>();
                    }
                    else if (rawJson.Items != null)
                    {
                        oldItems = rawJson.Items.ToObject<List<ReminderItem>>();
                    }

                    if (oldItems != null)
                    {
                        // Create a new collection for their old "active" list
                        var newCollection = new ReminderCollection
                        {
                            Title = "My Imported List",
                            Items = oldItems.Where(i => !i.IsDeleted).ToList(),
                            LastModified = DateTime.UtcNow
                        };

                        // Save this new collection
                        var collections = LoadCollections(username);
                        collections.Add(newCollection);
                        SaveCollections(collections, username);

                        // Set this as their active collection
                        settings.ActiveCollectionId = newCollection.Id;
                        SaveSettings(settings, username);
                    }
                }

                return settings;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading settings: {ex.Message}");
                return defaultSettings;
            }
        }

        public static void SaveSettings(Settings settings, string username)
        {
            string settingsFilePath = GetSettingsFilePath(username);
            try
            {
                string json = JsonConvert.SerializeObject(settings, Formatting.Indented);
                File.WriteAllText(settingsFilePath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving settings: {ex.Message}");
            }
        }

        // --- Collections File (Saved Lists) ---

        private static string GetCollectionsFilePath(string username)
        {
            string safeUsername = string.Join("_", username.Split(Path.GetInvalidFileNameChars()));
            string fileName = $"collections_{safeUsername}.json";
            return Path.Combine(StorageDirectory, fileName);
        }

        public static List<ReminderCollection> LoadCollections(string username)
        {
            string filePath = GetCollectionsFilePath(username);
            try
            {
                if (!File.Exists(filePath))
                {
                    return new List<ReminderCollection>();
                }
                string json = File.ReadAllText(filePath);
                var collections = JsonConvert.DeserializeObject<List<ReminderCollection>>(json);
                return collections?.OrderBy(c => c.Title).ToList() ?? new List<ReminderCollection>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading collections: {ex.Message}");
                return new List<ReminderCollection>();
            }
        }

        public static void SaveCollections(List<ReminderCollection> collections, string username)
        {
            string filePath = GetCollectionsFilePath(username);
            try
            {
                var sorted = collections.OrderBy(c => c.Title).ToList();
                string json = JsonConvert.SerializeObject(sorted, Formatting.Indented);
                File.WriteAllText(filePath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving collections: {ex.Message}");
            }
        }
    }
}