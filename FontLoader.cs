using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;


namespace FloatingReminder
{
    public static class FontLoader
    {
        private static readonly string FontDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fonts");
        private static readonly string FontFilePath = Path.Combine(FontDir, "DMSans-Regular.ttf");

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
        }
    }
}
