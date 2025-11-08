using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using System.Windows.Media; // Added for FontFamily

namespace FloatingNote // Corrected namespace
{
    public static class FontLoader
    {
        private static readonly string FontDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fonts");
        private static readonly string FontFilePath = Path.Combine(FontDir, "DMSans-Regular.ttf");

        // Static property to hold the loaded font family
        public static FontFamily NoteFontFamily { get; private set; }

        static FontLoader()
        {
            // Initialize with a fallback font
            NoteFontFamily = new FontFamily("Segoe UI");
        }

        public static async Task EnsureFontDownloadedAsync()
        {
            if (!Directory.Exists(FontDir))
                Directory.CreateDirectory(FontDir);

            if (!File.Exists(FontFilePath))
            {
                using (WebClient client = new WebClient())
                {
                    string fontUrl = "https://fonts.gstatic.com/s/dmsans/v15/rP2Hp2ywxg089UriCZSCHBeH.ttf";
                    await client.DownloadFileTaskAsync(new Uri(fontUrl), FontFilePath);
                }
            }

            // Try to load the font into the static property
            try
            {
                // The font name is often inside the file, but we can load it by its path.
                // The format is "File Path#Font Name"
                // We'll load the collection and find the font.
                var families = Fonts.GetFontFamilies(new Uri($"file:///{FontFilePath}"));
                foreach (var family in families)
                {
                    NoteFontFamily = family;
                    break; // Use the first font family in the file
                }
            }
            catch
            {
                // Fallback already set in static constructor
            }
        }
    }
}