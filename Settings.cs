using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Collections.Generic;

namespace FloatingReminder
{
    public class Settings
    {
        [BsonId]
        public string Id { get; set; }

        // This is the "active" list, or "workspace"
        [BsonElement("activeMessageList")]
        public List<ReminderItem> ActiveMessageList { get; set; }

        // This is the ID of the collection we are currently editing
        [BsonElement("activeCollectionId")]
        public string ActiveCollectionId { get; set; }

        public double StartFontSize { get; set; }
        public bool IsGlowEnabled { get; set; }

        public Settings()
        {
            // The active list is now empty by default
            // A collection must be opened first
            ActiveMessageList = new List<ReminderItem>();
            ActiveCollectionId = null;
            StartFontSize = 60;
            IsGlowEnabled = true;
        }
    }
}