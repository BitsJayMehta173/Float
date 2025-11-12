using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Collections.Generic;

namespace FloatingReminder
{
    public class Settings
    {
        [BsonId]
        public string Id { get; set; }

        public List<ReminderItem> Items { get; set; }
        public double StartFontSize { get; set; }
        public bool IsGlowEnabled { get; set; }

        public Settings()
        {
            // Update to use the new constructor
            Items = new List<ReminderItem>
            {
                new ReminderItem { Message = "Welcome to your new dashboard! ✨", DurationSeconds = 5 },
                new ReminderItem { Message = "Add your own messages below 👇", DurationSeconds = 8 }
            };
            StartFontSize = 60;
            IsGlowEnabled = true;
        }
    }
}