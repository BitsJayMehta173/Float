        
using MongoDB.Driver;
using System.Threading.Tasks;
using System;
using MongoDB.Bson;

namespace FloatingReminder
{
    public static class MongoSyncService
    {
        // --- PASTE YOUR CONNECTION STRING HERE ---
        private const string MongoConnectionString = "mongodb+srv://jacknnj8_db_user:v0zgx9ANbNrtdFhf@cluster0.w8ldldu.mongodb.net/?appName=Cluster0";
        // We'll give our settings a fixed ID so we always update the *same document*
        private const string UserSettingsId = "user_settings_v1";

        private static IMongoCollection<Settings> GetSettingsCollection()
        {
            try
            {
                var client = new MongoClient(MongoConnectionString);
                var database = client.GetDatabase("FloatingReminderDB"); // DB name
                var collection = database.GetCollection<Settings>("Settings"); // Collection name
                return collection;
            }
            catch (Exception ex)
            {
                // This will catch errors like a bad connection string
                Console.WriteLine($"MongoDB Connection Error: {ex.Message}");
                throw; // Re-throw to be caught by the button click
            }
        }

        public static async Task UploadSettingsAsync(Settings settings)
        {
            var collection = GetSettingsCollection();

            // Set the fixed ID on our settings object
            settings.Id = UserSettingsId;

            // Prepare the "upsert" operation
            var filter = Builders<Settings>.Filter.Eq(s => s.Id, UserSettingsId);
            var options = new ReplaceOptions { IsUpsert = true };

            // Execute the operation
            await collection.ReplaceOneAsync(filter, settings, options);
        }

        // --- NEW FUNCTION ---
        // This function will find the settings document in the cloud and return it.
        // If it doesn't exist, it will return 'null'.
        public static async Task<Settings> DownloadSettingsAsync()
        {
            var collection = GetSettingsCollection();
            var filter = Builders<Settings>.Filter.Eq(s => s.Id, UserSettingsId);

            // Find the single document with our ID
            return await collection.Find(filter).FirstOrDefaultAsync();
        }
    }
}