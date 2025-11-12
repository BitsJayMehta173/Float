using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FloatingReminder
{
    public static class FriendService
    {
        // --- PASTE YOUR CONNECTION STRING HERE ---
        private const string MongoConnectionString = "mongodb+srv://jacknnj8_db_user:v0zgx9ANbNrtdFhf@cluster0.w8ldldu.mongodb.net/?appName=Cluster0";

        // Get the "Users" collection (read-only for searching)
        private static IMongoCollection<User> GetUsersCollection()
        {
            var client = new MongoClient(MongoConnectionString);
            var database = client.GetDatabase("FloatingReminderDB");
            return database.GetCollection<User>("Users");
        }

        // Get the new "FriendRequests" collection
        private static IMongoCollection<FriendRequest> GetFriendRequestsCollection()
        {
            var client = new MongoClient(MongoConnectionString);
            var database = client.GetDatabase("FloatingReminderDB");
            return database.GetCollection<FriendRequest>("FriendRequests");
        }

        /// <summary>
        /// Checks if a user exists in the database.
        /// </summary>
        public static async Task<bool> DoesUserExistAsync(string username)
        {
            var users = GetUsersCollection();
            var filter = Builders<User>.Filter.Eq(u => u.Username, username);
            var user = await users.Find(filter).FirstOrDefaultAsync();
            return user != null;
        }

        /// <summary>
        /// Gets all pending friend requests for the current user.
        /// </summary>
        public static async Task<List<FriendRequest>> GetPendingRequestsAsync(string myUsername)
        {
            var requests = GetFriendRequestsCollection();
            var filter = Builders<FriendRequest>.Filter.Eq(r => r.RecipientUsername, myUsername) &
                         Builders<FriendRequest>.Filter.Eq(r => r.Status, RequestStatus.Pending);
            return await requests.Find(filter).ToListAsync();
        }

        /// <summary>
        /// Sends a new friend request.
        /// </summary>
        public static async Task<string> SendFriendRequestAsync(string senderUsername, string recipientUsername)
        {
            // --- Edge Cases ---
            if (senderUsername.Equals(recipientUsername, StringComparison.OrdinalIgnoreCase))
            {
                return "You can't add yourself as a friend.";
            }

            if (!await DoesUserExistAsync(recipientUsername))
            {
                return "User not found.";
            }

            var requests = GetFriendRequestsCollection();

            // Check if a request is already pending (either way)
            var filter = (Builders<FriendRequest>.Filter.Eq(r => r.SenderUsername, senderUsername) &
                          Builders<FriendRequest>.Filter.Eq(r => r.RecipientUsername, recipientUsername)) |
                         (Builders<FriendRequest>.Filter.Eq(r => r.SenderUsername, recipientUsername) &
                          Builders<FriendRequest>.Filter.Eq(r => r.RecipientUsername, senderUsername));

            var existingRequest = await requests.Find(filter).FirstOrDefaultAsync();

            if (existingRequest != null)
            {
                if (existingRequest.Status == RequestStatus.Pending)
                    return "A request is already pending.";
                if (existingRequest.Status == RequestStatus.Accepted)
                    return "You are already friends.";
            }

            // All checks passed, create new request
            var newRequest = new FriendRequest
            {
                SenderUsername = senderUsername,
                RecipientUsername = recipientUsername
            };

            await requests.InsertOneAsync(newRequest);
            return "Success: Friend request sent!";
        }

        /// <summary>
        /// Accepts a friend request.
        /// </summary>
        public static async Task AcceptRequestAsync(FriendRequest request)
        {
            // Note: In a production app, we would add the usernames to each
            // User's "Friends" list. For now, we'll just update the status.

            var requests = GetFriendRequestsCollection();
            var filter = Builders<FriendRequest>.Filter.Eq(r => r.Id, request.Id);
            var update = Builders<FriendRequest>.Update.Set(r => r.Status, RequestStatus.Accepted);

            await requests.UpdateOneAsync(filter, update);
        }

        /// <summary>
        /// Declines (or deletes) a friend request.
        /// </summary>
        public static async Task DeclineRequestAsync(FriendRequest request)
        {
            // For simplicity, we'll just delete the request.
            var requests = GetFriendRequestsCollection();
            var filter = Builders<FriendRequest>.Filter.Eq(r => r.Id, request.Id);
            await requests.DeleteOneAsync(filter);
        }
    }
}