using System;

namespace FloatingNote
{
    /// <summary>
    /// A simple class to hold settings passed from the dashboard to the note window.
    /// </summary>
    public class Settings
    {
        public string[] Texts { get; set; }
        public int IntervalSeconds { get; set; }
        public double StartFontSize { get; set; }
        public bool IsGlowEnabled { get; set; }

        public Settings()
        {
            // Provide sensible defaults
            Texts = new[] { "Welcome!", "Configure me in the dashboard." };
            IntervalSeconds = 5;
            StartFontSize = 60;
            IsGlowEnabled = true;
        }
    }
}