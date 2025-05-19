using Newtonsoft.Json.Linq;
using GmailUnsubscribeApp.Helpers;
using GmailUnsubscribeApp.Models;

namespace GmailUnsubscribeApp.Services
{
    public class LinkScanner
    {
        public async Task<List<(string Link, double Score)>> ScanLinksWithVirusTotalAsync(List<string> links, string apiKey, bool noLimit, bool forceYes)
        {
            var scoredLinks = new List<(string Link, double Score)>();
            string vtRequestFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GmailUnsubscribeApp", "vt_requests.txt");
            int dailyLimit = 500;
            int monthlyLimit = 15500;
            int rateLimit = 4;

            double estimatedSeconds = noLimit ? links.Count : Math.Ceiling((double)links.Count / rateLimit) * 90;
            Console.WriteLine($"Estimated time: {estimatedSeconds / 60:F1} minutes ({estimatedSeconds:F0} seconds).");
            if (!forceYes)
            {
                Console.Write("Proceed with VirusTotal scanning? (y/n): ");
                string response = Console.ReadLine()?.Trim().ToLower();
                if (response != "y")
                {
                    Console.WriteLine("VirusTotal scanning skipped.");
                    return scoredLinks;
                }
            }

            using (var httpClient = new HttpClient())
            {
                int requestsMade = 0;
                DateTime minuteStart = DateTime.Now;

                foreach (var link in links)
                {
                    var quotaResult = await CheckVirusTotalQuotaAsync(vtRequestFile, dailyLimit, monthlyLimit, rateLimit, requestsMade, minuteStart);
                    if (!noLimit && quotaResult.IsLimitReached)
                    {
                        break;
                    }
                    requestsMade = quotaResult.RequestsMade;
                    minuteStart = quotaResult.MinuteStart;

                    try
                    {
                        double score = await ScanSingleLinkWithVirusTotalAsync(httpClient, link, apiKey);
                        scoredLinks.Add((link, score));
                        requestsMade++;
                        Utility.UpdateRequests(vtRequestFile);
                    }
                    catch (UriFormatException)
                    {
                        //
                        // Likely a malformed link in the email, ignore
                        //
                        Console.WriteLine($"Warning: Skipping malformed URI: {link}");
                    }
                    catch (Exception ex2)
                    {
                        Console.WriteLine($"Error: Failed to scan {link} with VirusTotal: {ex2.Message}. Stopping scan.");
                        break;
                    }
                }
            }

            return scoredLinks;
        }

        public async Task<List<(string Link, double Score)>> ScanLinksWithHybridAnalysisAsync(List<string> links, string apiKey, bool noLimit, bool forceYes)
        {
            var scoredLinks = new List<(string Link, double Score)>();
            string haRequestFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GmailUnsubscribeApp", "ha_requests.txt");
            int minuteLimit = 200;
            int hourLimit = 2000;

            double estimatedSeconds = noLimit ? links.Count : Math.Ceiling((double)links.Count / minuteLimit) * 90;
            Console.WriteLine($"Estimated time: {estimatedSeconds / 60:F1} minutes ({estimatedSeconds:F0} seconds).");
            if (!forceYes)
            {
                Console.Write("Proceed with Hybrid Analysis scanning? (y/n): ");
                string response = Console.ReadLine()?.Trim().ToLower();
                if (response != "y")
                {
                    Console.WriteLine("Hybrid Analysis scanning skipped.");
                    return scoredLinks;
                }
            }

            using (var httpClient = new HttpClient())
            {
                int requestsMadeInMinute = 0;
                DateTime minuteStart = DateTime.Now;

                foreach (var link in links)
                {
                    var quotaResult = await CheckHybridAnalysisQuotaAsync(haRequestFile, minuteLimit, hourLimit, requestsMadeInMinute, minuteStart);
                    if (!noLimit && quotaResult.IsLimitReached)
                    {
                        break;
                    }
                    requestsMadeInMinute = quotaResult.RequestsMade;
                    minuteStart = quotaResult.MinuteStart;

                    try
                    {
                        double score = await ScanSingleLinkWithHybridAnalysisAsync(httpClient, link, apiKey);
                        scoredLinks.Add((link, score));
                        requestsMadeInMinute++;
                        Utility.UpdateRequests(haRequestFile);
                    }
                    catch (UriFormatException)
                    {
                        //
                        // Likely a malformed link in the email, ignore
                        //
                        Console.WriteLine($"Warning: Skipping malformed URI: {link}");
                    }
                    catch (Exception ex2)
                    {
                        //
                        // To avoid wasting quota, stop the scan.
                        //
                        Console.WriteLine($"Error: Failed to scan {link} with Hybrid Analysis: {ex2.Message}. Stopping scan.");
                        break;
                    }
                }
            }

            return scoredLinks;
        }

        private async Task<QuotaCheckResult> CheckVirusTotalQuotaAsync(string vtRequestFile, int dailyLimit, int monthlyLimit, int rateLimit, int requestsMade, DateTime minuteStart)
        {
            var result = new QuotaCheckResult
            {
                RequestsMade = requestsMade,
                MinuteStart = minuteStart
            };

            var (currentDailyCount, currentMonthlyCount, _) = Utility.ReadRequests(vtRequestFile);
            if (currentDailyCount >= dailyLimit || currentMonthlyCount >= monthlyLimit)
            {
                Console.WriteLine("VirusTotal daily or monthly request limit reached during scanning. Stopping.");
                result.IsLimitReached = true;
                return result;
            }

            if (requestsMade >= rateLimit)
            {
                Console.WriteLine("VirusTotal per-minute quota reached. Sleeping for 90 seconds.");
                await Task.Delay(90 * 1000);
                result.RequestsMade = 0;
                result.MinuteStart = DateTime.Now;
            }

            return result;
        }

        private async Task<QuotaCheckResult> CheckHybridAnalysisQuotaAsync(string haRequestFile, int minuteLimit, int hourLimit, int requestsMadeInMinute, DateTime minuteStart)
        {
            var result = new QuotaCheckResult
            {
                RequestsMade = requestsMadeInMinute,
                MinuteStart = minuteStart
            };

            var (currentMinuteCount, currentHourCount, _) = Utility.ReadRequests(haRequestFile);
            if (currentHourCount >= hourLimit)
            {
                Console.WriteLine("Hybrid Analysis hour request limit reached during scanning. Stopping.");
                result.IsLimitReached = true;
                return result;
            }

            if (requestsMadeInMinute >= minuteLimit)
            {
                Console.WriteLine("Hybrid Analysis per-minute quota reached. Sleeping for 90 seconds.");
                await Task.Delay(90 * 1000);
                result.RequestsMade = 0;
                result.MinuteStart = DateTime.Now;
            }

            return result;
        }

        private async Task<double> ScanSingleLinkWithVirusTotalAsync(HttpClient httpClient, string url, string apiKey)
        {
            var uri = new Uri(url);
            string domain = uri.Host;

            string vtUrl = $"https://www.virustotal.com/api/v3/domains/{domain}";
            httpClient.DefaultRequestHeaders.Add("x-apikey", apiKey);

            var response = await httpClient.GetAsync(vtUrl);
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"VirusTotal API error for {domain}: {response.ReasonPhrase}");
            }

            var json = await response.Content.ReadAsStringAsync();
            var data = JObject.Parse(json);

            var stats = data["data"]?["attributes"]?["last_analysis_stats"];
            if (stats == null)
            {
                return 0.0;
            }

            int malicious = stats["malicious"]?.Value<int>() ?? 0;
            int total = stats["malicious"]?.Value<int>() + stats["suspicious"]?.Value<int>() + stats["undetected"]?.Value<int>() + stats["harmless"]?.Value<int>() ?? 1;

            return (double)malicious / total * 100.0;
        }

        private async Task<double> ScanSingleLinkWithHybridAnalysisAsync(HttpClient httpClient, string url, string apiKey)
        {
            var uri = new Uri(url);
            string domain = uri.Host;

            string haUrl = "https://www.hybrid-analysis.com/api/v2/search/terms";
            httpClient.DefaultRequestHeaders.Clear();
            httpClient.DefaultRequestHeaders.Add("api-key", apiKey);
            httpClient.DefaultRequestHeaders.Add("User-Agent", "GmailUnsubscribeApp");
            httpClient.DefaultRequestHeaders.Add("Accept", "application/json");

            var requestBody = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("domain", $"{domain}")
            });

            var response = await httpClient.PostAsync(haUrl, requestBody);
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    Console.WriteLine($"Info: No Hybrid Analysis results found for {domain}.");
                    return 0.0;
                }
                throw new HttpRequestException($"Hybrid Analysis API error for {domain}: {response.ReasonPhrase}");
            }

            var json = await response.Content.ReadAsStringAsync();
            var data = JObject.Parse(json);

            var results = data["result"]?.ToObject<JArray>();
            if (results == null || results.Count == 0)
            {
                return 0.0;
            }

            int malicious = 0;
            int total = results.Count;
            foreach (var result in results)
            {
                var verdict = result["verdict"]?.ToString().ToLower();
                if (verdict == "malicious")
                {
                    malicious++;
                }
            }

            return (double)malicious / total * 100.0;
        }
    }
}