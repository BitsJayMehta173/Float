using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;

namespace FloatingReminder
{
    public class ReminderCollection : IEquatable<ReminderCollection>
    {
        [BsonId]
        public string Id { get; set; }

        [BsonElement("title")]
        public string Title { get; set; }

        [BsonElement("createdAt")]
        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime CreatedAt { get; set; }

        [BsonElement("lastModified")]
        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime LastModified { get; set; }

        [BsonElement("isDeleted")]
        public bool IsDeleted { get; set; } = false;

        [BsonElement("items")]
        public List<ReminderItem> Items { get; set; } = new List<ReminderItem>();

        public ReminderCollection()
        {
            Id = Guid.NewGuid().ToString();
            CreatedAt = DateTime.UtcNow;
            LastModified = DateTime.UtcNow;
        }

        public bool Equals(ReminderCollection other)
        {
            if (other is null) return false;
            return this.Id == other.Id;
        }

        public override bool Equals(object obj) => Equals(obj as ReminderCollection);
        public override int GetHashCode() => Id.GetHashCode();
    }
}
