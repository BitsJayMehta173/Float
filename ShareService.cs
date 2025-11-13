using MongoDB.Driver;
using System.Collections.Generic;
using System.Threading.Tasks;
using MongoDB.Bson;

namespace FloatingReminder
{
    public static class ShareService
    {
        // We can reuse the connection string from AuthService
        private const string MongoConnectionString = "mongodb+srv://jacknnj8_db_user:v0zgx9ANbNrtdFhf@cluster0.w8ldldu.mongodb.net/?appName=Cluster0";

        private static IMongoCollection<SharedCollectionItem> GetShareCollection()
        {
            var client = new MongoClient(MongoConnectionString);
            var database = client.GetDatabase("FloatingReminderDB");
            // A new collection for all users, to act as the global inbox
            return database.GetCollection<SharedCollectionItem>("SharedCollections");
        }

        // --- CALLED BY THE SENDER ---
        public static async Task SendCollectionAsync(ReminderCollection collection, string senderUsername, string recipientUsername)
        {
            var shares = GetShareCollection();

            var newItem = new SharedCollectionItem
            {
                SenderUsername = senderUsername,
                RecipientUsername = recipientUsername,
                // We create a deep copy here to be safe, though embedding should do this.
                SharedCollectionData = new ReminderCollection
                {
                    Title = collection.Title,
                    Items = new List<ReminderItem>(collection.Items)
                    // Note: We'll give these new IDs when the recipient receives them
                }
            };

            await shares.InsertOneAsync(newItem);
        }

        // --- CALLED BY THE RECIPIENT ---
        public static async Task<List<SharedCollectionItem>> GetNewSharesAsync(string recipientUsername)
        {
            var shares = GetShareCollection();
            var filter = Builders<SharedCollectionItem>.Filter.Eq(s => s.RecipientUsername, recipientUsername);
            return await shares.Find(filter).ToListAsync();
        }

        // --- CALLED BY RECIPIENT AFTER DOWNLOADING ---
        public static async Task DeleteShareAsync(ObjectId shareId)
        {
            var shares = GetShareCollection();
            var filter = Builders<SharedCollectionItem>.Filter.Eq(s => s.Id, shareId);
            await shares.DeleteOneAsync(filter);
        }
    }
}