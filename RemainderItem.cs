using MongoDB.Bson.Serialization.Attributes;
using System;

namespace FloatingReminder
{
    public class ReminderItem : IEquatable<ReminderItem>
    {
        [BsonId]
        public string Id { get; set; }

        [BsonElement("message")]
        public string Message { get; set; }

        [BsonElement("durationSeconds")]
        public int DurationSeconds { get; set; } = 5;

        [BsonElement("createdAt")]
        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime CreatedAt { get; set; }

        [BsonElement("lastModified")]
        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime LastModified { get; set; }

        // --- NEW ---
        [BsonElement("ownerUsername")]
        public string OwnerUsername { get; set; }

        [BsonElement("isDeleted")]
        public bool IsDeleted { get; set; } = false;


        public ReminderItem()
        {
            this.Id = Guid.NewGuid().ToString();
            this.CreatedAt = DateTime.UtcNow;
            this.LastModified = this.CreatedAt;
        }

        public bool Equals(ReminderItem other)
        {
            if (other is null)
                return false;

            return this.Id == other.Id;
        }

        public override bool Equals(object obj)
        {
            return this.Equals(obj as ReminderItem);
        }

        public override int GetHashCode()
        {
            return this.Id.GetHashCode();
        }
    }
}