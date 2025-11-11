using System;
using System.Windows.Media;
using System.Diagnostics;

namespace FloatingReminder
{
    public static class FontLoader
    {
        private static readonly Uri FontBaseUri =
            new Uri("pack://application:,,,/Fonts/#", UriKind.Absolute);

        public static FontFamily NoteFontFamily { get; private set; }

        static FontLoader()
        {
            try
            {
                NoteFontFamily = new FontFamily(FontBaseUri, "Instagram Sans");
                Log("[SUCCESS] Loaded embedded resource font: 'Instagram Sans'");
            }
            catch (Exception ex)
            {
                Log($"[ERROR] Failed to load embedded font: {ex.Message}");
                NoteFontFamily = new FontFamily("Segoe UI");
                Log("[WARNING] Falling back to default font 'Segoe UI'");
            }
        }

        public static System.Threading.Tasks.Task EnsureFontDownloadedAsync()
        {
            return System.Threading.Tasks.Task.CompletedTask;
        }

        private static void Log(string message)
        {
            string log = $"[FontLoader] {message}";
            Debug.WriteLine(log);
            Console.WriteLine(log);
        }
    }
}