using System.Collections.Generic;

namespace FloatingNote
{
    public class Settings
    {
        // Now holds a list of complex items instead of just strings
        public List<ReminderItem> Items { get; set; }
        public double StartFontSize { get; set; }
        public bool IsGlowEnabled { get; set; }

        public Settings()
        {
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