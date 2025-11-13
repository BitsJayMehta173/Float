using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FloatingReminder
{
    public static class FriendService
    {
        // --- PASTE YOUR CONNECTION STRING HERE ---\r\n        
        // TODO: Move this to App.config
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

        // *** NEW: Method to get a user's full data (for friend list) ***
        public static async Task<User> GetUserAsync(string username)
        {
            var users = GetUsersCollection();
            var filter = Builders<User>.Filter.Eq(u => u.Username, username);
            return await users.Find(filter).FirstOrDefaultAsync();
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

        // *** NEW: Method to get all pending requests for the logged-in user ***
        // (This was missing from the original, but MainWindow.xaml.cs needs it)
        public static async Task<List<FriendRequest>> GetPendingRequestsAsync(string username)
        {
            var requests = GetFriendRequestsCollection();
            var filter = Builders<FriendRequest>.Filter.Eq(r => r.RecipientUsername, username)
                       & Builders<FriendRequest>.Filter.Eq(r => r.Status, RequestStatus.Pending);

            return await requests.Find(filter).ToListAsync();
        }

        /// <summary>
        /// Sends a new friend request.
        /// </summary>
        public static async Task<string> SendRequestAsync(string senderUsername, string recipientUsername)
        {
            if (senderUsername.Equals(recipientUsername, StringComparison.OrdinalIgnoreCase))
            {
                return "Error: You cannot add yourself as a friend.";
            }

            var users = GetUsersCollection();
            var requests = GetFriendRequestsCollection();

            // 1. Check if recipient exists
            var recipientFilter = Builders<User>.Filter.Eq(u => u.Username, recipientUsername);
            var recipient = await users.Find(recipientFilter).FirstOrDefaultAsync();
            if (recipient == null)
            {
                return "Error: User not found.";
            }

            // 2. Check if they are already friends (by checking sender's list)
            var senderFilter = Builders<User>.Filter.Eq(u => u.Username, senderUsername);
            var sender = await users.Find(senderFilter).FirstOrDefaultAsync();
            if (sender != null && sender.FriendUsernames.Contains(recipientUsername))
            {
                return "Error: You are already friends with this user.";
            }

            // 3. Check if a pending request already exists
            var existingReqFilter = (Builders<FriendRequest>.Filter.Eq(r => r.SenderUsername, senderUsername) &
                                     Builders<FriendRequest>.Filter.Eq(r => r.RecipientUsername, recipientUsername)) |
                                    (Builders<FriendRequest>.Filter.Eq(r => r.SenderUsername, recipientUsername) &
                                     Builders<FriendRequest>.Filter.Eq(r => r.RecipientUsername, senderUsername));

            var existingRequest = await requests.Find(existingReqFilter).FirstOrDefaultAsync();
            if (existingRequest != null)
            {
                return "Error: A friend request is already pending.";
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
        // *** MODIFIED: This now updates both User objects ***
        public static async Task AcceptRequestAsync(FriendRequest request)
        {
            var users = GetUsersCollection();
            var requests = GetFriendRequestsCollection();

            // 1. Add sender's name to recipient's friend list
            var recipientFilter = Builders<User>.Filter.Eq(u => u.Username, request.RecipientUsername);
            var recipientUpdate = Builders<User>.Update.AddToSet(u => u.FriendUsernames, request.SenderUsername);
            await users.UpdateOneAsync(recipientFilter, recipientUpdate);

            // 2. Add recipient's name to sender's friend list
            var senderFilter = Builders<User>.Filter.Eq(u => u.Username, request.SenderUsername);
            var senderUpdate = Builders<User>.Update.AddToSet(u => u.FriendUsernames, request.RecipientUsername);
            await users.UpdateOneAsync(senderFilter, senderUpdate);

            // 3. Update the request status to Accepted
            // (You could also delete it, but this is good for record-keeping)
            var requestFilter = Builders<FriendRequest>.Filter.Eq(r => r.Id, request.Id);
            var requestUpdate = Builders<FriendRequest>.Update.Set(r => r.Status, RequestStatus.Accepted);
            await requests.UpdateOneAsync(requestFilter, requestUpdate);
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