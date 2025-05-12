using HtmlAgilityPack;
using System.Text.RegularExpressions;
using System.Text;

namespace GmailUnsubscribeApp.Helpers
{
    public static class Utility
    {
        public static string SanitizeHtmlContent(string content)
        {
            try
            {
                content = Regex.Replace(content, @"<script\b[^<]*(?:(?!<\/script>)<[^<]*)*<\/script>", "", RegexOptions.IgnoreCase);
                content = Regex.Replace(content, @"[^\x20-\x7E\n\r\t]", "");
                content = Regex.Replace(content, @"\s+", " ").Trim();
                return content;
            }
            catch
            {
                return "Error sanitizing content";
            }
        }

        public static HashSet<string> LoadVisitedUrls(string cacheFile)
        {
            try
            {
                string dir = Path.GetDirectoryName(cacheFile);
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                if (!File.Exists(cacheFile))
                {
                    return new HashSet<string>();
                }

                return new HashSet<string>(File.ReadAllLines(cacheFile).Where(line => !string.IsNullOrEmpty(line)), StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Failed to load visited URLs cache: {ex.Message}");
                return new HashSet<string>();
            }
        }

        public static void SaveVisitedUrl(string cacheFile, string url)
        {
            try
            {
                string dir = Path.GetDirectoryName(cacheFile);
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                File.AppendAllText(cacheFile, url + Environment.NewLine);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Failed to save URL to visited cache: {ex.Message}");
            }
        }

        public static async Task<string> ReadResponseContentAsync(HttpResponseMessage response)
        {
            try
            {
                return await response.Content.ReadAsStringAsync();
            }
            catch (InvalidOperationException)
            {
                try
                {
                    byte[] contentBytes = await response.Content.ReadAsByteArrayAsync();
                    string contentType = response.Content.Headers.ContentType?.ToString() ?? "unknown";
                    try
                    {
                        return Encoding.UTF8.GetString(contentBytes);
                    }
                    catch
                    {
                        try
                        {
                            return Encoding.GetEncoding("ISO-8859-1").GetString(contentBytes);
                        }
                        catch
                        {
                            return $"[Unreadable content; Content-Type: {contentType}; Hex: {BitConverter.ToString(contentBytes).Replace("-", "")}]";
                        }
                    }
                }
                catch (Exception ex)
                {
                    return $"[Error reading content; Content-Type: {response.Content.Headers.ContentType?.ToString() ?? "unknown"}; Error: {ex.Message}]";
                }
            }
        }

        public static (int MinuteCount, int HourCount) ReadRequests(string filePath)
        {
            try
            {
                string dir = Path.GetDirectoryName(filePath);
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                if (!File.Exists(filePath))
                {
                    return (0, 0);
                }

                string[] lines = File.ReadAllLines(filePath);
                int minuteCount = 0;
                int hourCount = 0;

                foreach (var line in lines)
                {
                    string[] parts = line.Split(',');
                    if (parts.Length == 3 && DateTime.TryParse(parts[0], out DateTime date))
                    {
                        if (date >= DateTime.Now.AddMinutes(-1))
                        {
                            minuteCount += int.TryParse(parts[2], out int count) ? count : 0;
                        }
                        if (date >= DateTime.Now.AddHours(-1))
                        {
                            hourCount += int.TryParse(parts[2], out int count) ? count : 0;
                        }
                    }
                }

                return (minuteCount, hourCount);
            }
            catch
            {
                return (0, 0);
            }
        }

        public static void UpdateRequests(string filePath)
        {
            try
            {
                string dir = Path.GetDirectoryName(filePath);
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                File.AppendAllText(filePath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss},1,1{Environment.NewLine}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Failed to update request count: {ex.Message}");
            }
        }

        public static bool IsUnsubscribeSuccessful(string htmlContent)
        {
            try
            {
                var doc = new HtmlDocument();
                doc.LoadHtml(htmlContent);

                var forms = doc.DocumentNode.SelectNodes("//form[@action]");
                var submitButtons = doc.DocumentNode.SelectNodes("//input[@type='submit']");
                if (submitButtons == null)
                {
                    return true;
                }

                var successWords = new[]
                {
                    "unsubscribed", "success", "confirmed", "removed",
                    "取消订阅", "成功", "确认",
                    "सदस्यता रद्द", "सफलता", "पुष्टि",
                    "desuscrito", "éxito", "confirmado",
                    "désabonné", "succès", "confirmé",
                    "إلغاء الاشتراك", "نجاح", "مؤكد",
                    "সাবস্ক্রিপশন বাতিল", "সাফল্য", "নিশ্চিত",
                    "cancelado", "sucesso", "confirmado",
                    "отписан", "успех", "подтверждено",
                    "رکنیت منسوخ", "کامیابی", "تصدیق شدہ",
                    "berhenti berlangganan", "sukses", "dikonfirmasi",
                    "abgemeldet", "erfolg", "bestätigt",
                    "登録解除", "成功", "確認済み",
                    "kujiondoa", "mafanikio", "imethibitishwa",
                    "सदस्यता रद्द", "यश", "पुष्टी",
                    "చందా రద్దు", "విజయం", "నిర్ధారించబడింది",
                    "abonelikten çık", "başarı", "onaylandı",
                    "பதிவு நீக்கப்பட்டது", "வெற்றி", "உறுதிப்படுத்தப்பட்டது",
                    "取消订阅", "成功", "确认",
                    "구독 취소", "성공", "확인됨"
                };
                var textElements = doc.DocumentNode.SelectNodes("//div|//p|//span|//h1");
                if (textElements != null)
                {
                    foreach (var element in textElements)
                    {
                        string text = element.InnerText.ToLower();
                        if (successWords.Any(word => text.Contains(word)))
                        {
                            return true;
                        }
                    }
                }

                var successElements = doc.DocumentNode.SelectNodes("//div[@class or @id]|//p[@class or @id]|//span[@class or @id]");
                if (successElements != null)
                {
                    foreach (var element in successElements)
                    {
                        string classAttr = element.GetAttributeValue("class", "").ToLower();
                        string idAttr = element.GetAttributeValue("id", "").ToLower();
                        if (classAttr.Contains("success") || classAttr.Contains("confirmation") ||
                            classAttr.Contains("unsubscribed") || classAttr.Contains("complete") ||
                            idAttr.Contains("success") || idAttr.Contains("confirmation") ||
                            idAttr.Contains("unsubscribed") || idAttr.Contains("complete"))
                        {
                            return true;
                        }
                    }
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        public static string GenerateOutputFilePath(string baseOutputFile)
        {
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string randomString = Guid.NewGuid().ToString().Substring(0, 8);
            return Path.Combine(
                Path.GetDirectoryName(baseOutputFile),
                $"unsubscribe_links_{timestamp}_{randomString}.html"
            );
        }
    }
}