using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace FloatingReminder
{
    public class User
    {
        [BsonId]
        public ObjectId Id { get; set; } // MongoDB's unique ID

        [BsonElement("username")]
        public string Username { get; set; }

        [BsonElement("passwordHash")]
        public string PasswordHash { get; set; }
    }
}