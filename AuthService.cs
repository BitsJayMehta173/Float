using MongoDB.Driver;
using System;
using System.Threading.Tasks;

namespace FloatingReminder
{
    public static class AuthService
    {
        // --- PASTE YOUR CONNECTION STRING HERE ---
        // This is the same string from MongoSyncService.cs
        private const string MongoConnectionString = "mongodb+srv://jacknnj8_db_user:v0zgx9ANbNrtdFhf@cluster0.w8ldldu.mongodb.net/?appName=Cluster0";

        private static IMongoCollection<User> GetUsersCollection()
        {
            try
            {
                var client = new MongoClient(MongoConnectionString);
                var database = client.GetDatabase("FloatingReminderDB");
                // We create a new "collection" (like a table) just for users
                var collection = database.GetCollection<User>("Users");
                return collection;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"MongoDB Connection Error: {ex.Message}");
                throw;
            }
        }

        // --- New Sign Up Method ---
        public static async Task<string> RegisterUserAsync(string username, string password)
        {
            var users = GetUsersCollection();

            // 1. Check if user already exists
            var filter = Builders<User>.Filter.Eq(u => u.Username, username);
            var existingUser = await users.Find(filter).FirstOrDefaultAsync();
            if (existingUser != null)
            {
                return "Error: Username already exists.";
            }

            // 2. Hash the password
            string passwordHash = BCrypt.Net.BCrypt.HashPassword(password);

            // 3. Create and insert new user
            var newUser = new User
            {
                Username = username,
                PasswordHash = passwordHash
            };
            await users.InsertOneAsync(newUser);

            return "Success";
        }

        // --- New Login Method ---
        public static async Task<string> ValidateUserAsync(string username, string password)
        {
            var users = GetUsersCollection();

            // 1. Find the user
            var filter = Builders<User>.Filter.Eq(u => u.Username, username);
            var user = await users.Find(filter).FirstOrDefaultAsync();
            if (user == null)
            {
                return "Error: Invalid username or password.";
            }

            // 2. Verify the password
            bool isPasswordValid = BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
            if (isPasswordValid)
            {
                return "Success";
            }
            else
            {
                return "Error: Invalid username or password.";
            }
        }
    }
}