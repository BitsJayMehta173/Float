using MongoDB.Driver;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FloatingReminder
{
    public static class MongoSyncService
    {
        private const string MongoConnectionString = "mongodb+srv://jacknnj8_db_user:v0zgx9ANbNrtdFhf@cluster0.w8ldldu.mongodb.net/?appName=Cluster0";
        private const string UserSettingsId = "user_settings_v1";

        // This is PUBLIC so SyncManager can use it
        public static IMongoCollection<ReminderCollection> GetGlobalCollectionsDb()
        {
            var client = new MongoClient(MongoConnectionString);
            var database = client.GetDatabase("FloatingReminderDB");
            return database.GetCollection<ReminderCollection>("AllCollections");
        }

        // --- Settings Collection (Preferences) ---
        private static IMongoCollection<Settings> GetSettingsCollection(string username)
        {
            var client = new MongoClient(MongoConnectionString);
            var database = client.GetDatabase("FloatingReminderDB");
            string collectionName = $"settings_{username}";
            return database.GetCollection<Settings>(collectionName);
        }

        // --- Collections Sync Logic ---

        public static List<ReminderCollection> LoadCollectionsFromLocal(string username)
        {
            return SettingsService.LoadCollections(username);
        }

        public static async Task<List<ReminderCollection>> LoadCollectionsFromCloudAsync(string username)
        {
            if (username == SettingsService.GuestUsername)
                return new List<ReminderCollection>();

            var db = GetGlobalCollectionsDb();

            var filter = Builders<ReminderCollection>.Filter.Eq(c => c.OwnerUsername, username) |
                         Builders<ReminderCollection>.Filter.AnyEq(c => c.SharedWithUsernames, username);

            return await db.Find(filter).ToListAsync();
        }

        public static void SaveCollectionsToLocal(List<ReminderCollection> collections, string username)
        {
            SettingsService.SaveCollections(collections, username);
        }

        // --- *** ALTERNATIVE SAVE METHOD *** ---
        // This version avoids "ReplaceOneOptions" completely.
        public static async Task SaveCollectionToCloudAsync(ReminderCollection collection, string username)
        {
            if (username == SettingsService.GuestUsername) return;

            // Permission check
            if (collection.OwnerUsername != username && !collection.SharedWithUsernames.Contains(username))
            {
                Console.WriteLine($"[Save Error] Permission denied for user '{username}' on collection '{collection.Title}'.");
                return;
            }

            var db = GetGlobalCollectionsDb();
            var filter = Builders<ReminderCollection>.Filter.Eq(r => r.Id, collection.Id);

            // 1. Check if the document already exists
            var existing = await db.Find(filter).FirstOrDefaultAsync();

            if (existing != null)
            {
                // 2. If it exists, REPLACE it (no options needed)
                await db.ReplaceOneAsync(filter, collection);
            }
            else
            {
                // 3. If it does not exist, INSERT it
                await db.InsertOneAsync(collection);
            }
        }
    }
}