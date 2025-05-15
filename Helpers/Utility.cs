using System.Globalization;
using System.Reflection;
using System.Text;

namespace GmailUnsubscribeApp.Helpers
{
    public static class Utility
    {
        private static readonly HashSet<string> SuccessKeywords;

        static Utility()
        {
            SuccessKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("GmailUnsubscribeApp.Resources.unsubscribe_keywords.txt"))
            {
                if (stream != null)
                {
                    using (var reader = new StreamReader(stream))
                    {
                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            if (!string.IsNullOrWhiteSpace(line))
                            {
                                SuccessKeywords.Add(line.Trim());
                            }
                        }
                    }
                }
            }
            if (!SuccessKeywords.Any())
            {
                throw new InvalidOperationException("Failed to load unsubscribe keywords: unsubscribe_keywords.txt is empty or missing.");
            }
        }

        public static HashSet<string> GetUnsubscribeButtonKeywords()
        {
            var keywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("GmailUnsubscribeApp.Resources.button_keywords.txt"))
            {
                if (stream != null)
                {
                    using (var reader = new StreamReader(stream))
                    {
                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            if (!string.IsNullOrWhiteSpace(line))
                            {
                                keywords.Add(line.Trim());
                            }
                        }
                    }
                }
            }
            if (!keywords.Any())
            {
                throw new InvalidOperationException("Failed to load button keywords: button_keywords.txt is empty or missing.");
            }
            return keywords;
        }

        public static string GenerateOutputFilePath(string outputFile)
        {
            string dir = Path.GetDirectoryName(outputFile);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string extension = Path.GetExtension(outputFile);
            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(outputFile);
            return Path.Combine(dir ?? "", $"{fileNameWithoutExtension}_{timestamp}{extension}");
        }

        public static bool IsUnsubscribeSuccessful(string htmlContent)
        {
            if (string.IsNullOrEmpty(htmlContent))
            {
                return true; // Empty body often indicates success for one-click unsubscribes
            }

            string lowerContent = htmlContent.Normalize(NormalizationForm.FormC).ToLowerInvariant();
            return SuccessKeywords.Any(keyword => lowerContent.Contains(keyword));
        }

        public static string SanitizeHtmlContent(string htmlContent)
        {
            if (string.IsNullOrEmpty(htmlContent))
            {
                return string.Empty;
            }

            return htmlContent; // Return raw HTML without sanitization
        }

        public static async Task<string> ReadResponseContentAsync(HttpResponseMessage response)
        {
            try
            {
                return await response.Content.ReadAsStringAsync();
            }
            catch
            {
                return string.Empty;
            }
        }

        public static void SaveVisitedUrl(string cacheFile, string url)
        {
            string dir = Path.GetDirectoryName(cacheFile);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.AppendAllText(cacheFile, url + Environment.NewLine);
        }

        public static HashSet<string> LoadVisitedUrls(string cacheFile)
        {
            var visitedUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (File.Exists(cacheFile))
            {
                foreach (var line in File.ReadAllLines(cacheFile))
                {
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        visitedUrls.Add(line.Trim());
                    }
                }
            }
            return visitedUrls;
        }

        public static void UpdateRequests(string requestFile)
        {
            string dir = Path.GetDirectoryName(requestFile);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var (minuteCount, hourCount, lastUpdate) = ReadRequests(requestFile);
            minuteCount++;
            hourCount++;

            // Reset counts based on elapsed time
            var now = DateTime.UtcNow;
            if ((now - lastUpdate).TotalMinutes >= 1)
            {
                minuteCount = 1; // Reset minute count
            }
            if ((now - lastUpdate).TotalHours >= 1)
            {
                hourCount = 1; // Reset hour count
            }

            File.WriteAllText(requestFile, $"{minuteCount},{hourCount},{now:yyyy-MM-ddTHH:mm:ssZ}");
        }

        public static (int MinuteCount, int HourCount, DateTime LastUpdate) ReadRequests(string requestFile)
        {
            if (!File.Exists(requestFile))
            {
                return (0, 0, DateTime.UtcNow);
            }

            string[] parts = File.ReadAllText(requestFile).Split(',');
            if (parts.Length != 3 || !int.TryParse(parts[0], out int minuteCount) || !int.TryParse(parts[1], out int hourCount) || !DateTime.TryParseExact(parts[2], "yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out DateTime lastUpdate))
            {
                return (0, 0, DateTime.UtcNow);
            }

            return (minuteCount, hourCount, lastUpdate);
        }
    }
}