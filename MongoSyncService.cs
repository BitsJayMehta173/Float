using MongoDB.Driver;
using System.Threading.Tasks;
using System;
using MongoDB.Bson;
using System.Linq; // Added for .Concat .GroupBy
using System.Collections.Generic; // Added for List<>

namespace FloatingReminder
{
    public static class MongoSyncService
    {
        // --- PASTE YOUR CONNECTION STRING HERE ---
        private const string MongoConnectionString = "mongodb+srv://jacknnj8_db_user:v0zgx9ANbNrtdFhf@cluster0.w8ldldu.mongodb.net/?appName=Cluster0";

        private const string UserSettingsId = "user_settings_v1";

        private static IMongoCollection<Settings> GetSettingsCollection(string username)
        {
            var client = new MongoClient(MongoConnectionString);
            var database = client.GetDatabase("FloatingReminderDB");
            // Use a unique collection name for each user's settings
            string collectionName = $"settings_{username}";
            var collection = database.GetCollection<Settings>(collectionName);
            return collection;
        }

        // --- UPDATED ---
        // We now pass the username to get their specific collection
        public static async Task UploadSettingsAsync(Settings settings, string username)
        {
            // Guests cannot upload
            if (username == SettingsService.GuestUsername) return;

            var collection = GetSettingsCollection(username);
            settings.Id = UserSettingsId;

            var filter = Builders<Settings>.Filter.Eq(s => s.Id, UserSettingsId);
            var options = new ReplaceOptions { IsUpsert = true };

            await collection.ReplaceOneAsync(filter, settings, options);
        }

        // --- UPDATED ---
        // We now pass the username to get their specific collection
        public static async Task<Settings> DownloadSettingsAsync(string username)
        {
            if (username == SettingsService.GuestUsername) return null;

            var collection = GetSettingsCollection(username);
            var filter = Builders<Settings>.Filter.Eq(s => s.Id, UserSettingsId);

            return await collection.Find(filter).FirstOrDefaultAsync();
        }

        // --- NEW MERGE FUNCTION ---
        public static async Task<Settings> MergeGuestDataToUserAsync(string username)
        {
            // 1. Get Guest's local data
            var guestSettings = SettingsService.LoadSettings(SettingsService.GuestUsername);
            var localItems = guestSettings?.Items ?? new List<ReminderItem>();

            // 2. Get User's cloud data
            var cloudSettings = await DownloadSettingsAsync(username);
            var cloudItems = cloudSettings?.Items ?? new List<ReminderItem>();

            // 3. Perform the merge
            var mergedList = localItems.Concat(cloudItems)
                                       .GroupBy(item => item.Id) // Group by unique ID
                                       .Select(group => group.First()) // Take the first (removes duplicates)
                                       .OrderBy(item => item.CreatedAt) // Sort by time
                                       .ToList();

            // 4. Create new settings for the user
            // We use the cloud settings as a base, or create new ones
            var finalSettings = cloudSettings ?? new Settings();
            finalSettings.Items = mergedList;

            // 5. Save the new merged list to the cloud
            await UploadSettingsAsync(finalSettings, username);

            // 6. Save the new merged list locally for the user
            SettingsService.SaveSettings(finalSettings, username);

            return finalSettings;
        }
    }
}