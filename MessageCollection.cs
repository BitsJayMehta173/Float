using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;

namespace FloatingReminder
{
    public class MessageCollection : IEquatable<MessageCollection>
    {
        [BsonId]
        public string Id { get; set; }

        [BsonElement("title")]
        public string Title { get; set; }

        [BsonElement("lastModified")]
        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime LastModified { get; set; }

        [BsonElement("isDeleted")]
        public bool IsDeleted { get; set; } = false;

        // This list is a "snapshot" of the messages in this collection
        [BsonElement("items")]
        public List<ReminderItem> Items { get; set; } = new List<ReminderItem>();

        public MessageCollection()
        {
            this.Id = Guid.NewGuid().ToString();
            this.LastModified = DateTime.UtcNow;
        }

        // --- Equality check is based on the unique ID ---
        public bool Equals(MessageCollection other)
        {
            if (other is null)
                return false;

            return this.Id == other.Id;
        }

        public override bool Equals(object obj)
        {
            return this.Equals(obj as MessageCollection);
        }

        public override int GetHashCode()
        {
            return this.Id.GetHashCode();
        }
    }
}