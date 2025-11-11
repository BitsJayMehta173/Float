using MongoDB.Bson.Serialization.Attributes;
using System;

namespace FloatingReminder
{
    // Implement IEquatable to help with finding duplicates
    public class ReminderItem : IEquatable<ReminderItem>
    {
        // This is now the unique key for MongoDB
        [BsonId]
        public string Id { get; set; }

        [BsonElement("message")]
        public string Message { get; set; }

        [BsonElement("durationSeconds")]
        public int DurationSeconds { get; set; } = 5;

        // NEW: Add timestamp and set it to UTC for consistency
        [BsonElement("createdAt")]
        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime CreatedAt { get; set; }

        // NEW: Constructor to assign unique ID and timestamp on creation
        public ReminderItem()
        {
            this.Id = Guid.NewGuid().ToString(); // Assign a new unique ID
            this.CreatedAt = DateTime.UtcNow; // Assign the current time
        }

        // --- Equality checks now use the unique ID ---

        public bool Equals(ReminderItem other)
        {
            if (other is null)
                return false;

            // Two items are the same if their IDs are the same.
            return this.Id == other.Id;
        }

        public override bool Equals(object obj)
        {
            return this.Equals(obj as ReminderItem);
        }

        public override int GetHashCode()
        {
            // Use the Id's hash code
            return this.Id.GetHashCode();
        }
    }
}