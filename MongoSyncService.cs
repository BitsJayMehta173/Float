using MongoDB.Driver;
using System.Threading.Tasks;
using System;
using MongoDB.Bson;
using System.Linq;
using System.Collections.Generic;
using MongoDB.Bson.Serialization;
using Newtonsoft.Json;
using MongoDB.Bson.IO;

namespace FloatingReminder
{
    public static class MongoSyncService
    {
        private const string MongoConnectionString = "mongodb+srv://jacknnj8_db_user:v0zgx9ANbNrtdFhf@cluster0.w8ldldu.mongodb.net/?appName=Cluster0";
        private const string UserSettingsId = "user_settings_v1";

        // --- Settings Collection (Preferences) ---
        private static IMongoCollection<Settings> GetSettingsCollection(string username)
        {
            var client = new MongoClient(MongoConnectionString);
            var database = client.GetDatabase("FloatingReminderDB");
            string collectionName = $"settings_{username}";
            return database.GetCollection<Settings>(collectionName);
        }

        // --- Collections Collection (Saved Lists) ---
        private static IMongoCollection<ReminderCollection> GetCollectionsDb(string username)
        {
            var client = new MongoClient(MongoConnectionString);
            var database = client.GetDatabase("FloatingReminderDB");
            string collectionName = $"collections_{username}";
            return database.GetCollection<ReminderCollection>(collectionName);
        }

        // --- Settings Sync ---
        public static async Task UploadSettingsAsync(Settings settings, string username)
        {
            if (username == SettingsService.GuestUsername) return;
            var collection = GetSettingsCollection(username);
            settings.Id = UserSettingsId;
            var filter = Builders<Settings>.Filter.Eq(s => s.Id, UserSettingsId);
            var options = new ReplaceOptions { IsUpsert = true };
            await collection.ReplaceOneAsync(filter, settings, options);
        }

        public static async Task<Settings> DownloadSettingsAsync(string username)
        {
            if (username == SettingsService.GuestUsername) return null;

            var client = new MongoClient(MongoConnectionString);
            var database = client.GetDatabase("FloatingReminderDB");
            string collectionName = $"settings_{username}";
            var rawCollection = database.GetCollection<BsonDocument>(collectionName);
            var filter = Builders<BsonDocument>.Filter.Eq("_id", UserSettingsId);
            var rawDoc = await rawCollection.Find(filter).FirstOrDefaultAsync();

            var settings = new Settings();

            if (rawDoc == null)
            {
                return settings;
            }

            settings.Id = rawDoc["_id"].AsString;

            if (rawDoc.Contains("StartFontSize"))
                settings.StartFontSize = rawDoc["StartFontSize"].AsDouble;
            if (rawDoc.Contains("IsGlowEnabled"))
                settings.IsGlowEnabled = rawDoc["IsGlowEnabled"].AsBoolean;

            BsonValue itemsListBson;
            if (rawDoc.TryGetValue("activeMessageList", out itemsListBson) && itemsListBson.IsBsonArray)
            {
                // FIX: Removed JsonWriterSettings entirely to bypass the JsonOutputMode.Strict error
                string json = itemsListBson.ToJson();
                settings.ActiveMessageList = Newtonsoft.Json.JsonConvert.DeserializeObject<List<ReminderItem>>(json);
            }
            else if (rawDoc.TryGetValue("Items", out itemsListBson) && itemsListBson.IsBsonArray)
            {
                // FIX: Removed JsonWriterSettings entirely to bypass the JsonOutputMode.Strict error
                string json = itemsListBson.ToJson();
                settings.ActiveMessageList = Newtonsoft.Json.JsonConvert.DeserializeObject<List<ReminderItem>>(json);
            }
            else
            {
                settings.ActiveMessageList = new List<ReminderItem>();
            }

            if (rawDoc.Contains("activeCollectionId") && rawDoc["activeCollectionId"].IsString)
            {
                settings.ActiveCollectionId = rawDoc["activeCollectionId"].AsString;
            }
            else if (settings.ActiveMessageList.Any())
            {
                var newCollection = new ReminderCollection
                {
                    Title = "My Imported List",
                    Items = settings.ActiveMessageList.Where(i => !i.IsDeleted).ToList(),
                    LastModified = DateTime.UtcNow
                };

                var collectionsDb = GetCollectionsDb(username);
                await collectionsDb.InsertOneAsync(newCollection);

                settings.ActiveCollectionId = newCollection.Id;
            }

            return settings;
        }

        // --- Collections Sync Methods ---
        public static List<ReminderCollection> LoadCollectionsFromLocal(string username)
        {
            return SettingsService.LoadCollections(username);
        }

        public static async Task<List<ReminderCollection>> LoadCollectionsFromCloudAsync(string username)
        {
            if (username == SettingsService.GuestUsername)
                return new List<ReminderCollection>();

            var db = GetCollectionsDb(username);
            return await db.Find(Builders<ReminderCollection>.Filter.Empty).ToListAsync();
        }

        public static void SaveCollectionsToLocal(List<ReminderCollection> collections, string username)
        {
            SettingsService.SaveCollections(collections, username);
        }

        public static async Task SaveCollectionsToCloudAsync(List<ReminderCollection> collections, string username)
        {
            if (username == SettingsService.GuestUsername) return;

            var db = GetCollectionsDb(username);

            var bulkOps = new List<WriteModel<ReminderCollection>>();
            foreach (var col in collections)
            {
                var filter = Builders<ReminderCollection>.Filter.Eq(r => r.Id, col.Id);
                var replaceOne = new ReplaceOneModel<ReminderCollection>(filter, col) { IsUpsert = true };
                bulkOps.Add(replaceOne);
            }

            if (bulkOps.Any())
            {
                await db.BulkWriteAsync(bulkOps);
            }
        }
    }
}