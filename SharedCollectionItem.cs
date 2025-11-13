using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace FloatingReminder
{
    public class SharedCollectionItem
    {
        [BsonId]
        public ObjectId Id { get; set; }

        [BsonElement("senderUsername")]
        public string SenderUsername { get; set; }

        [BsonElement("recipientUsername")]
        public string RecipientUsername { get; set; }

        [BsonElement("sentAt")]
        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime SentAt { get; set; }

        // This embeds the entire collection, including all its items,
        // as a snapshot of the moment it was sent.
        [BsonElement("sharedCollectionData")]
        public ReminderCollection SharedCollectionData { get; set; }

        public SharedCollectionItem()
        {
            this.SentAt = DateTime.UtcNow;
        }
    }
}