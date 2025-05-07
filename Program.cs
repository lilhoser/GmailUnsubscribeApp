using Google.Apis.Auth.OAuth2;
using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using System.Text;

namespace GmailUnsubscribeApp
{
    class Program
    {
        static string[] Scopes = { GmailService.Scope.GmailReadonly };
        static string ApplicationName = "Gmail Unsubscribe App";
        static string TokenPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GmailUnsubscribeApp", "token.json");

        static (bool ShowHelp, string Label, long MaxResults, string OutputFile, string VirusTotalApiKey, string HybridApiKey, bool ListContents, bool CountItems, string CredentialsPath, bool HasError, string ErrorMessage, bool NoLimit, double Threshold, bool ShowUsage) ParseArguments(string[] args)
        {
            bool showHelp = false;
            string label = "Promotions";
            long maxResults = 100;
            string outputFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GmailUnsubscribeApp", "unsubscribe_links.html");
            string virusTotalApiKey = null;
            string hybridApiKey = null;
            bool listContents = false;
            bool countItems = false;
            string credentialsPath = "credentials.json";
            bool hasError = false;
            string errorMessage = null;
            bool noLimit = false;
            double threshold = 0.0;
            bool showUsage = false;

            if (args.Length == 0)
            {
                hasError = true;
                errorMessage = "Error: No arguments provided.";
                return (showHelp, label, maxResults, outputFile, virusTotalApiKey, hybridApiKey, listContents, countItems, credentialsPath, hasError, errorMessage, noLimit, threshold, showUsage);
            }

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i].ToLower())
                {
                    case "-h":
                    case "--help":
                        showHelp = true;
                        break;
                    case "-l":
                    case "--label":
                        if (++i < args.Length)
                            label = args[i];
                        else
                        {
                            hasError = true;
                            errorMessage = $"Error: Missing value for {args[i - 1]}.";
                            return (showHelp, label, maxResults, outputFile, virusTotalApiKey, hybridApiKey, listContents, countItems, credentialsPath, hasError, errorMessage, noLimit, threshold, showUsage);
                        }
                        break;
                    case "-m":
                    case "--max-results":
                        if (++i < args.Length && long.TryParse(args[i], out long max))
                            maxResults = max;
                        else
                        {
                            hasError = true;
                            errorMessage = $"Error: Invalid or missing value for {args[i - 1]}.";
                            return (showHelp, label, maxResults, outputFile, virusTotalApiKey, hybridApiKey, listContents, countItems, credentialsPath, hasError, errorMessage, noLimit, threshold, showUsage);
                        }
                        break;
                    case "-o":
                    case "--output":
                        if (++i < args.Length)
                            outputFile = args[i];
                        else
                        {
                            hasError = true;
                            errorMessage = $"Error: Missing value for {args[i - 1]}.";
                            return (showHelp, label, maxResults, outputFile, virusTotalApiKey, hybridApiKey, listContents, countItems, credentialsPath, hasError, errorMessage, noLimit, threshold, showUsage);
                        }
                        break;
                    case "-v":
                    case "--virustotal-key":
                        if (++i < args.Length)
                            virusTotalApiKey = args[i];
                        else
                        {
                            hasError = true;
                            errorMessage = $"Error: Missing value for {args[i - 1]}.";
                            return (showHelp, label, maxResults, outputFile, virusTotalApiKey, hybridApiKey, listContents, countItems, credentialsPath, hasError, errorMessage, noLimit, threshold, showUsage);
                        }
                        break;
                    case "-a":
                    case "--hybrid-key":
                        if (++i < args.Length)
                            hybridApiKey = args[i];
                        else
                        {
                            hasError = true;
                            errorMessage = $"Error: Missing value for {args[i - 1]}.";
                            return (showHelp, label, maxResults, outputFile, virusTotalApiKey, hybridApiKey, listContents, countItems, credentialsPath, hasError, errorMessage, noLimit, threshold, showUsage);
                        }
                        break;
                    case "-t":
                    case "--threshold":
                        if (++i < args.Length && double.TryParse(args[i], out double thresh) && thresh >= 0)
                            threshold = thresh;
                        else
                        {
                            hasError = true;
                            errorMessage = $"Error: Invalid or missing value for {args[i - 1]}. Must be a non-negative number.";
                            return (showHelp, label, maxResults, outputFile, virusTotalApiKey, hybridApiKey, listContents, countItems, credentialsPath, hasError, errorMessage, noLimit, threshold, showUsage);
                        }
                        break;
                    case "--list":
                        listContents = true;
                        break;
                    case "--count":
                        countItems = true;
                        break;
                    case "-c":
                    case "--credentials":
                        if (++i < args.Length)
                            credentialsPath = args[i];
                        else
                        {
                            hasError = true;
                            errorMessage = $"Error: Missing value for {args[i - 1]}.";
                            return (showHelp, label, maxResults, outputFile, virusTotalApiKey, hybridApiKey, listContents, countItems, credentialsPath, hasError, errorMessage, noLimit, threshold, showUsage);
                        }
                        break;
                    case "--no-limit":
                        noLimit = true;
                        break;
                    case "--showusage":
                        showUsage = true;
                        break;
                    default:
                        hasError = true;
                        errorMessage = $"Error: Unrecognized argument '{args[i]}'.";
                        return (showHelp, label, maxResults, outputFile, virusTotalApiKey, hybridApiKey, listContents, countItems, credentialsPath, hasError, errorMessage, noLimit, threshold, showUsage);
                }
            }

            return (showHelp, label, maxResults, outputFile, virusTotalApiKey, hybridApiKey, listContents, countItems, credentialsPath, hasError, errorMessage, noLimit, threshold, showUsage);
        }

        static void ShowHelp()
        {
            Console.WriteLine("Gmail Unsubscribe App");
            Console.WriteLine("Scans emails in a Gmail label for unsubscribe links and generates an HTML file with links and VirusTotal or Hybrid Analysis scores.");
            Console.WriteLine("Can also list email contents, count emails, or show API usage.");
            Console.WriteLine();
            Console.WriteLine("Usage: GmailUnsubscribeApp [options]");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  -h, --help              Show this help message");
            Console.WriteLine("  -l, --label <name>      Gmail label to scan, list, or count (default: Promotions)");
            Console.WriteLine("  -m, --max-results <n>   Maximum number of emails to scan (default: 100)");
            Console.WriteLine("  -o, --output <file>     Output HTML file path (default: AppData\\GmailUnsubscribeApp\\unsubscribe_links.html)");
            Console.WriteLine("  -v, --virustotal-key <key> VirusTotal API key (required for VT scanning)");
            Console.WriteLine("  -a, --hybrid-key <key>  Hybrid Analysis API key (required for HA scanning)");
            Console.WriteLine("  -t, --threshold <value> Maliciousness score threshold for visiting links (default: 0)");
            Console.WriteLine("  -c, --credentials <file> Path to Google API credentials file (default: credentials.json)");
            Console.WriteLine("  --list                  List email subjects and IDs in the specified label");
            Console.WriteLine("  --count                 Count the number of emails in the specified label");
            Console.WriteLine("  --no-limit              Disable rate limits for selected API (VT: 4/min, 500/day, 15,500/month; HA: 200/min, 2000/hour)");
            Console.WriteLine("  --showusage             Show detected API usage and exit");
            Console.WriteLine();
            Console.WriteLine("Example:");
            Console.WriteLine("  GmailUnsubscribeApp -l INBOX -m 50 -o links.html -v your_virustotal_api_key -t 5 -c ./config/credentials.json");
            Console.WriteLine("  GmailUnsubscribeApp -l Promotions -m 50 -o links.html -a your_hybrid_api_key -t 10 -c ./credentials.json");
            Console.WriteLine("  GmailUnsubscribeApp -l Promotions --list -c ./credentials.json");
            Console.WriteLine("  GmailUnsubscribeApp -l INBOX --count -c ./credentials.json");
            Console.WriteLine("  GmailUnsubscribeApp --showusage -v your_virustotal_api_key");
        }

        static async Task<double> GetHybridAnalysisScoreAsync(HttpClient httpClient, string url, string apiKey)
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
                    return 0.0; // Treat as non-malicious
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

            return (double)malicious / total * 100.0; // Percentage of malicious detections
        }

        static async Task<List<(string Link, double Score)>> ScanLinksWithVirusTotalAsync(List<string> links, string virusTotalApiKey, bool noLimit)
        {
            var scoredLinks = new List<(string Link, double Score)>();
            string vtRequestFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GmailUnsubscribeApp", "vt_requests.txt");
            int dailyLimit = 500;
            int monthlyLimit = 15500;
            int rateLimit = 4; // Requests per minute
            var (requestsToday, requestsThisMonth) = ReadRequests(vtRequestFile);
            int remainingDailyRequests = noLimit ? int.MaxValue : Math.Max(0, dailyLimit - requestsToday);
            int remainingMonthlyRequests = noLimit ? int.MaxValue : Math.Max(0, monthlyLimit - requestsThisMonth);
            int linksToScan = Math.Min(links.Count, Math.Min(remainingDailyRequests, remainingMonthlyRequests));

            if (linksToScan == 0)
            {
                Console.WriteLine("VirusTotal daily or monthly request limit reached. No scans performed.");
                return scoredLinks;
            }

            double estimatedSeconds = noLimit ? linksToScan : Math.Ceiling((double)linksToScan / rateLimit) * 60;
            Console.WriteLine($"Found {links.Count} unsubscribe links. Scanning {linksToScan} links with VirusTotal.");
            Console.WriteLine($"Estimated time: {estimatedSeconds / 60:F1} minutes ({estimatedSeconds:F0} seconds).");
            Console.Write("Proceed with VirusTotal scanning? (y/n): ");
            string response = Console.ReadLine()?.Trim().ToLower();
            if (response != "y")
            {
                Console.WriteLine("VirusTotal scanning skipped.");
                return scoredLinks;
            }

            using (var httpClient = new HttpClient())
            {
                int requestsMade = 0;
                DateTime minuteStart = DateTime.Now;

                foreach (var link in links.Take(linksToScan))
                {
                    if (!noLimit)
                    {
                        var (currentDailyCount, currentMonthlyCount) = ReadRequests(vtRequestFile);
                        if (currentDailyCount >= dailyLimit || currentMonthlyCount >= monthlyLimit)
                        {
                            Console.WriteLine("VirusTotal daily or monthly request limit reached during scanning. Stopping.");
                            break;
                        }

                        if (requestsMade >= rateLimit)
                        {
                            double secondsElapsed = (DateTime.Now - minuteStart).TotalSeconds;
                            if (secondsElapsed < 60)
                            {
                                Console.WriteLine($"VirusTotal per-minute quota reached. Sleeping for {60 - secondsElapsed:F0} seconds.");
                                await Task.Delay((int)((60 - secondsElapsed) * 1000));
                            }
                            requestsMade = 0;
                            minuteStart = DateTime.Now;
                        }
                    }

                    try
                    {
                        double score = await GetVirusTotalScoreAsync(httpClient, link, virusTotalApiKey);
                        scoredLinks.Add((link, score));
                        requestsMade++;
                        UpdateRequests(vtRequestFile);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error: Failed to scan {link} with VirusTotal: {ex.Message}. Stopping scan.");
                        break;
                    }
                }
            }

            return scoredLinks;
        }

        static async Task<List<(string Link, double Score)>> ScanLinksWithHybridAnalysisAsync(List<string> links, string hybridApiKey, bool noLimit)
        {
            var scoredLinks = new List<(string Link, double Score)>();
            string haRequestFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GmailUnsubscribeApp", "ha_requests.txt");
            var (minuteLimit, hourLimit) = (200, 2000);
            if (minuteLimit == 0 || hourLimit == 0)
            {
                Console.WriteLine("Failed to retrieve valid Hybrid Analysis quota. No scans performed.");
                return scoredLinks;
            }

            var (minuteUsed, hourUsed) = ReadRequests(haRequestFile);
            int remainingMinuteRequests = noLimit ? int.MaxValue : Math.Max(0, minuteLimit - minuteUsed);
            int remainingHourRequests = noLimit ? int.MaxValue : Math.Max(0, hourLimit - hourUsed);
            int linksToScan = Math.Min(links.Count, Math.Min(remainingMinuteRequests, remainingHourRequests));

            if (linksToScan == 0)
            {
                Console.WriteLine("Hybrid Analysis minute or hour request limit reached. No scans performed.");
                return scoredLinks;
            }

            double estimatedSeconds = noLimit ? linksToScan : Math.Ceiling((double)linksToScan / minuteLimit) * 60;
            Console.WriteLine($"Found {links.Count} unsubscribe links. Scanning {linksToScan} links with Hybrid Analysis.");
            Console.WriteLine($"Estimated time: {estimatedSeconds / 60:F1} minutes ({estimatedSeconds:F0} seconds).");
            Console.Write("Proceed with Hybrid Analysis scanning? (y/n): ");
            string response = Console.ReadLine()?.Trim().ToLower();
            if (response != "y")
            {
                Console.WriteLine("Hybrid Analysis scanning skipped.");
                return scoredLinks;
            }

            using (var httpClient = new HttpClient())
            {
                int requestsMadeInMinute = 0;
                DateTime minuteStart = DateTime.Now;

                foreach (var link in links.Take(linksToScan))
                {
                    if (!noLimit)
                    {
                        var (currentMinuteCount, currentHourCount) = ReadRequests(haRequestFile);
                        if (currentHourCount >= hourLimit)
                        {
                            Console.WriteLine("Hybrid Analysis hour request limit reached during scanning. Stopping.");
                            break;
                        }

                        if (requestsMadeInMinute >= minuteLimit)
                        {
                            double secondsElapsed = (DateTime.Now - minuteStart).TotalSeconds;
                            if (secondsElapsed < 60)
                            {
                                Console.WriteLine($"Hybrid Analysis per-minute quota reached. Sleeping for {60 - secondsElapsed:F0} seconds.");
                                await Task.Delay((int)((60 - secondsElapsed) * 1000));
                            }
                            requestsMadeInMinute = 0;
                            minuteStart = DateTime.Now;
                        }
                    }

                    try
                    {
                        double score = await GetHybridAnalysisScoreAsync(httpClient, link, hybridApiKey);
                        scoredLinks.Add((link, score));
                        requestsMadeInMinute++;
                        UpdateRequests(haRequestFile);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error: Failed to scan {link} with Hybrid Analysis: {ex.Message}. Stopping scan.");
                        break;
                    }
                }
            }

            return scoredLinks;
        }

        static async Task Main(string[] args)
        {
            // Parse command-line arguments
            var config = ParseArguments(args);
            if (config.ShowHelp || config.HasError)
            {
                if (config.HasError)
                {
                    Console.WriteLine(config.ErrorMessage);
                    Console.WriteLine();
                }
                ShowHelp();
                return;
            }

            try
            {
                // Handle showusage
                if (config.ShowUsage)
                {
                    await DisplayServiceAndQuotasAsync(config.VirusTotalApiKey, config.HybridApiKey, config.NoLimit);
                    return;
                }

                // Authenticate and create Gmail service
                GmailService gmailService = await AuthenticateAsync(config.CredentialsPath);

                // Handle count items
                if (config.CountItems)
                {
                    int count = await CountLabelItemsAsync(gmailService, config.Label);
                    Console.WriteLine($"Total emails in label '{config.Label}': {count}");
                    return;
                }

                // Handle list contents
                if (config.ListContents)
                {
                    await ListLabelContentsAsync(gmailService, config.Label);
                    return;
                }

                // Proceed with unsubscribe link scanning if neither count nor list is specified
                if (string.IsNullOrEmpty(config.VirusTotalApiKey) && string.IsNullOrEmpty(config.HybridApiKey))
                {
                    Console.WriteLine("Error: At least one API key (VirusTotal or Hybrid Analysis) is required for scanning.");
                    Console.WriteLine();
                    ShowHelp();
                    return;
                }

                // Display selected service and quotas
                await DisplayServiceAndQuotasAsync(config.VirusTotalApiKey, config.HybridApiKey, config.NoLimit);
                Console.WriteLine();

                // Generate unique output filename with date/time and random string
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string randomString = Guid.NewGuid().ToString().Substring(0, 8);
                string outputFile = Path.Combine(
                    Path.GetDirectoryName(config.OutputFile),
                    $"unsubscribe_links_{timestamp}_{randomString}.html"
                );

                // Get unsubscribe links
                var result = await GetUnsubscribeLinksAsync(gmailService, config.Label, config.MaxResults);

                // Generate initial HTML file without scores
                if (result.Links.Count > 0)
                {
                    var initialLinks = result.Links.Select(link => (Link: link, Score: 0.0)).ToList();
                    GenerateHtmlFile(initialLinks, outputFile);
                    Console.WriteLine($"Initial HTML file generated with {result.Links.Count} unsubscribe links: {Path.GetFullPath(outputFile)}");
                }
                else
                {
                    Console.WriteLine($"No unsubscribe links found in {result.EmailsScanned} emails scanned in label '{config.Label}'.");
                    return;
                }

                // Scan links with selected API
                List<(string Link, double Score)> scoredLinks;
                if (!string.IsNullOrEmpty(config.VirusTotalApiKey))
                {
                    scoredLinks = await ScanLinksWithVirusTotalAsync(result.Links, config.VirusTotalApiKey, config.NoLimit);
                }
                else
                {
                    scoredLinks = await ScanLinksWithHybridAnalysisAsync(result.Links, config.HybridApiKey, config.NoLimit);
                }

                // Generate second HTML file with scores if scanning occurred
                if (scoredLinks.Count > 0)
                {
                    string scoredOutputFile = Path.ChangeExtension(outputFile, null) + "_scored.html";
                    GenerateHtmlFile(scoredLinks, scoredOutputFile, true);
                }

                // Visit unsubscribe links below threshold and track visited count
                int visitedLinks = await VisitUnsubscribeLinksAsync(scoredLinks, config.Threshold);

                // Print summary
                PrintSummary((scoredLinks, result.EmailsScanned, outputFile, visitedLinks));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        static async Task<int> VisitUnsubscribeLinksAsync(List<(string Link, double Score)> scoredLinks, double threshold)
        {
            var validLinks = scoredLinks
                .Where(l => l.Score >= 0 && l.Score <= threshold)
                .Where(l => Uri.TryCreate(l.Link, UriKind.Absolute, out Uri uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
                .Select(l => l.Link)
                .ToList();

            if (validLinks.Count == 0)
            {
                Console.WriteLine($"No unsubscribe links have a maliciousness score below or equal to {threshold}%.");
                return 0;
            }

            string cacheFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GmailUnsubscribeApp", "visited_urls.txt");
            var visitedUrls = LoadVisitedUrls(cacheFile);
            var linksToVisit = validLinks.Where(link => !visitedUrls.Contains(link)).ToList();

            if (linksToVisit.Count == 0)
            {
                Console.WriteLine($"All {validLinks.Count} unsubscribe links below or equal to {threshold}% have already been visited.");
                return 0;
            }

            Console.WriteLine($"Found {linksToVisit.Count} unvisited unsubscribe links with maliciousness score below or equal to {threshold}%.");
            Console.Write($"Proceed with visiting these links? (y/n): ");
            string response2 = Console.ReadLine()?.Trim().ToLower();
            if (response2 != "y")
            {
                Console.WriteLine("Visiting unsubscribe links skipped.");
                return 0;
            }

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GmailUnsubscribeApp", "logs", timestamp);
            if (!Directory.Exists(logDir))
            {
                Directory.CreateDirectory(logDir);
            }

            int visitedCount = 0;
            using (var httpClient = new HttpClient())
            {
                httpClient.DefaultRequestHeaders.Add("User-Agent", "GmailUnsubscribeApp");

                foreach (var link in linksToVisit)
                {
                    string abbreviatedLink = link.Length > 100 ? link.Substring(0, 97) + "..." : link;
                    Console.Write($"Visiting {abbreviatedLink}: ");
                    string safeUriPart = new Uri(link).Host.Replace(".", "_").Substring(0, Math.Min(20, new Uri(link).Host.Length));
                    string logFile = Path.Combine(logDir, $"visit_{timestamp}_{safeUriPart}.log");

                    try
                    {
                        var response = await httpClient.GetAsync(link);
                        Console.WriteLine($"{(int)response.StatusCode}");

                        // Log response details
                        var logContent = new StringBuilder();
                        logContent.AppendLine($"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                        logContent.AppendLine($"URL: {link}");
                        logContent.AppendLine($"Status: {(int)response.StatusCode} {response.StatusCode}");
                        logContent.AppendLine("Headers:");
                        foreach (var header in response.Headers)
                        {
                            logContent.AppendLine($"  {header.Key}: {string.Join(", ", header.Value)}");
                        }
                        logContent.AppendLine("Body:");
                        logContent.AppendLine(await response.Content.ReadAsStringAsync());

                        File.WriteAllText(logFile, logContent.ToString());

                        // Cache the visited URL
                        SaveVisitedUrl(cacheFile, link);
                        visitedUrls.Add(link);
                        visitedCount++;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error: {ex.Message}");
                        File.AppendAllText(logFile, $"Error visiting {link}: {ex.Message}\n");
                        visitedCount++;
                    }
                }
            }

            return visitedCount;
        }

        static HashSet<string> LoadVisitedUrls(string cacheFile)
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

                return new HashSet<string>(File.ReadAllLines(cacheFile).Where(line => !string.IsNullOrWhiteSpace(line)), StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Failed to load visited URLs cache: {ex.Message}");
                return new HashSet<string>();
            }
        }

        static void SaveVisitedUrl(string cacheFile, string url)
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

        static async Task DisplayServiceAndQuotasAsync(string virusTotalApiKey, string hybridApiKey, bool noLimit)
        {
            if (!string.IsNullOrEmpty(virusTotalApiKey))
            {
                Console.WriteLine("Service: VirusTotal");
                string vtRequestFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GmailUnsubscribeApp", "vt_requests.txt");
                int dailyLimit = 500;
                int monthlyLimit = 15500;
                var (requestsToday, requestsThisMonth) = ReadRequests(vtRequestFile);
                Console.WriteLine($"Detected Quotas:");
                Console.WriteLine($"  Daily Limit: {dailyLimit} requests");
                Console.WriteLine($"  Monthly Limit: {monthlyLimit} requests");
                Console.WriteLine($"Consumed:");
                Console.WriteLine($"  Daily: {requestsToday} requests");
                Console.WriteLine($"  Monthly: {requestsThisMonth} requests");
            }
            else if (!string.IsNullOrEmpty(hybridApiKey))
            {
                Console.WriteLine("Service: Hybrid Analysis");
                string haRequestFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GmailUnsubscribeApp", "ha_requests.txt");
                var (minuteLimit, hourLimit) = (200, 2000);
                var (minuteUsed, hourUsed) = ReadRequests(haRequestFile);
                Console.WriteLine($"Detected Quotas:");
                Console.WriteLine($"  Minute Limit: {minuteLimit} requests");
                Console.WriteLine($"  Hour Limit: {hourLimit} requests");
                Console.WriteLine($"Consumed:");
                Console.WriteLine($"  Minute: {minuteUsed} requests");
                Console.WriteLine($"  Hour: {hourUsed} requests");
            }
            else
            {
                Console.WriteLine("Service: None (no API key provided)");
            }
        }

        static void GenerateHtmlFile(List<(string Link, double Score)> links, string outputFile, bool includeScores = false)
        {
            string outputDir = Path.GetDirectoryName(outputFile);
            if (!Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }
            using (var writer = new StreamWriter(outputFile))
            {
                writer.WriteLine("<!DOCTYPE html>");
                writer.WriteLine("<html lang=\"en\">");
                writer.WriteLine("<head>");
                writer.WriteLine("<meta charset=\"UTF-8\">");
                writer.WriteLine("<title>Unsubscribe Links</title>");
                writer.WriteLine("<style>");
                writer.WriteLine("body { font-family: Arial, sans-serif; margin: 20px; }");
                writer.WriteLine("table { border-collapse: collapse; width: 100%; }");
                writer.WriteLine("th, td { border: 1px solid #ddd; padding: 8px; text-align: left; }");
                writer.WriteLine("th { background-color: #f2f2f2; }");
                writer.WriteLine("tr:nth-child(even) { background-color: #f9f9f9; }");
                writer.WriteLine("a { color: #007BFF; text-decoration: none; }");
                writer.WriteLine("a:hover { text-decoration: underline; }");
                if (includeScores)
                {
                    writer.WriteLine(".score-low { color: green; }");
                    writer.WriteLine(".score-medium { color: orange; }");
                    writer.WriteLine(".score-high { color: red; }");
                }
                writer.WriteLine("</style>");
                writer.WriteLine("</head>");
                writer.WriteLine("<body>");
                writer.WriteLine("<h1>Unsubscribe Links</h1>");
                writer.WriteLine("<table>");
                if (includeScores)
                {
                    writer.WriteLine("<tr><th>Maliciousness Score</th><th>Unsubscribe Link</th></tr>");
                    foreach (var (link, score) in links)
                    {
                        string scoreClass = score < 5.0 ? "score-low" : score < 20.0 ? "score-medium" : "score-high";
                        writer.WriteLine($"<tr><td class=\"{scoreClass}\">{score:F2}%</td><td><a href=\"{link}\" target=\"_blank\">{link}</a></td></tr>");
                    }
                }
                else
                {
                    writer.WriteLine("<tr><th>Unsubscribe Link</th></tr>");
                    foreach (var (link, _) in links)
                    {
                        writer.WriteLine($"<tr><td><a href=\"{link}\" target=\"_blank\">{link}</a></td></tr>");
                    }
                }
                writer.WriteLine("</table>");
                writer.WriteLine("</body>");
                writer.WriteLine("</html>");
            }
        }

        static void PrintSummary((List<(string Link, double Score)> Links, int EmailsScanned, string OutputFile, int VisitedLinks) result)
        {
            Console.WriteLine();
            Console.WriteLine("=== Run Summary ===");
            Console.WriteLine($"Emails scanned: {result.EmailsScanned}");
            Console.WriteLine($"Unsubscribe links found: {result.Links.Count}");
            Console.WriteLine($"Unsubscribe links visited: {result.VisitedLinks}");
            if (result.Links.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine($"Initial HTML file generated: {Path.GetFullPath(result.OutputFile)}");
            }
            if (result.Links.Any(l => l.Score >= 0))
            {
                Console.WriteLine();
                Console.WriteLine("Score Breakdown:");
                int lowRisk = result.Links.Count(l => l.Score >= 0 && l.Score < 5.0);
                int mediumRisk = result.Links.Count(l => l.Score >= 5.0 && l.Score < 20.0);
                int highRisk = result.Links.Count(l => l.Score >= 20.0);
                int failed = result.Links.Count(l => l.Score < 0);
                Console.WriteLine($"Low risk (< 5%): {lowRisk}");
                Console.WriteLine($"Medium risk (5-20%): {mediumRisk}");
                Console.WriteLine($"High risk (> 20%): {highRisk}");
                Console.WriteLine($"Failed scans: {failed}");
                Console.WriteLine();
                Console.WriteLine($"Scored HTML file generated: {Path.GetFullPath(Path.ChangeExtension(result.OutputFile, null) + "_scored.html")}");
            }
        }

        static async Task<(List<string> Links, int EmailsScanned)> GetUnsubscribeLinksAsync(GmailService service, string label, long maxResults)
        {
            var links = new List<string>();
            int emailsScanned = 0;
            string labelId = await GetLabelIdAsync(service, label);

            if (string.IsNullOrEmpty(labelId))
            {
                Console.WriteLine($"Label '{label}' not found.");
                return (links, emailsScanned);
            }

            // List messages in the specified label
            var request = service.Users.Messages.List("me");
            request.LabelIds = new[] { labelId }.ToList();
            request.MaxResults = Math.Min(maxResults, 100); // Gmail API limits to 100 per page

            // Get total messages in label for logging
            var labelInfo = await service.Users.Labels.Get("me", labelId).ExecuteAsync();
            int totalMessages = labelInfo.MessagesTotal ?? 0;
            Console.WriteLine($"Scanning up to {maxResults} emails in label '{label}' (total messages: {totalMessages}).");

            while (request != null && emailsScanned < maxResults)
            {
                var response = await request.ExecuteAsync();
                if (response.Messages != null)
                {
                    foreach (var message in response.Messages)
                    {
                        if (emailsScanned >= maxResults) break;
                        emailsScanned++;
                        var email = await service.Users.Messages.Get("me", message.Id).ExecuteAsync();
                        string subject = email.Payload?.Headers?.FirstOrDefault(h => h.Name == "Subject")?.Value ?? "(No Subject)";
                        string unsubscribeLink = ExtractUnsubscribeLink(email);
                        ConsoleColor originalColor = Console.ForegroundColor;
                        Console.ForegroundColor = !string.IsNullOrEmpty(unsubscribeLink) ? ConsoleColor.Red : ConsoleColor.Gray;
                        Console.WriteLine($"Scanning email: {subject}");
                        Console.ForegroundColor = originalColor;
                        if (!string.IsNullOrEmpty(unsubscribeLink) && !links.Contains(unsubscribeLink))
                        {
                            links.Add(unsubscribeLink);
                        }
                    }
                }

                request.PageToken = response.NextPageToken;
                if (string.IsNullOrEmpty(response.NextPageToken))
                    break;
            }

            return (links, emailsScanned);
        }

        static (int MinuteCount, int HourCount) ReadRequests(string filePath)
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

        static void UpdateRequests(string filePath)
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

        static async Task<GmailService> AuthenticateAsync(string credentialsPath)
        {
            UserCredential credential;
            using (var stream = new FileStream(credentialsPath, FileMode.Open, FileAccess.Read))
            {
                string tokenFolder = Path.GetDirectoryName(TokenPath);
                if (!Directory.Exists(tokenFolder))
                {
                    Directory.CreateDirectory(tokenFolder);
                }
                credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                    GoogleClientSecrets.FromStream(stream).Secrets,
                    Scopes,
                    "user",
                    CancellationToken.None,
                    new FileDataStore(TokenPath, true));
            }

            return new GmailService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = ApplicationName
            });
        }


        static async Task ListLabelContentsAsync(GmailService service, string label)
        {
            string labelId = await GetLabelIdAsync(service, label);
            if (string.IsNullOrEmpty(labelId))
            {
                Console.WriteLine($"Label '{label}' not found.");
                return;
            }

            Console.WriteLine($"Contents of label '{label}':");
            var request = service.Users.Messages.List("me");
            request.LabelIds = new[] { labelId }.ToList();
            request.MaxResults = 100; // Adjust as needed

            while (request != null)
            {
                var response = await request.ExecuteAsync();
                if (response.Messages != null)
                {
                    foreach (var message in response.Messages)
                    {
                        var email = await service.Users.Messages.Get("me", message.Id).ExecuteAsync();
                        string subject = email.Payload?.Headers?.FirstOrDefault(h => h.Name == "Subject")?.Value ?? "(No Subject)";
                        Console.WriteLine($"ID: {message.Id}, Subject: {subject}");
                    }
                }

                request.PageToken = response.NextPageToken;
                if (string.IsNullOrEmpty(response.NextPageToken))
                    break;
            }
        }

        static async Task<int> CountLabelItemsAsync(GmailService service, string label)
        {
            string labelId = await GetLabelIdAsync(service, label);
            if (string.IsNullOrEmpty(labelId))
            {
                Console.WriteLine($"Label '{label}' not found.");
                return 0;
            }

            int totalCount = 0;
            var request = service.Users.Messages.List("me");
            request.LabelIds = new[] { labelId }.ToList();
            request.MaxResults = 100;

            while (request != null)
            {
                var response = await request.ExecuteAsync();
                if (response.Messages != null)
                {
                    totalCount += response.Messages.Count;
                }

                request.PageToken = response.NextPageToken;
                if (string.IsNullOrEmpty(response.NextPageToken))
                    break;
            }

            return totalCount;
        }

        static async Task<string> GetLabelIdAsync(GmailService service, string labelName)
        {
            var labels = await service.Users.Labels.List("me").ExecuteAsync();
            var label = labels.Labels.FirstOrDefault(l => l.Name.Equals(labelName, StringComparison.OrdinalIgnoreCase));
            return label?.Id;
        }

        static string ExtractUnsubscribeLink(Message email)
        {
            // Check List-Unsubscribe header
            var headers = email.Payload?.Headers;
            if (headers != null)
            {
                var unsubscribeHeader = headers.FirstOrDefault(h => h.Name == "List-Unsubscribe");
                if (unsubscribeHeader != null)
                {
                    var match = Regex.Match(unsubscribeHeader.Value, @"<(.+?)>");
                    if (match.Success)
                    {
                        return match.Groups[1].Value;
                    }
                }
            }

            // Check email body for unsubscribe links
            string emailBody = GetEmailBody(email.Payload);
            if (!string.IsNullOrEmpty(emailBody))
            {
                var urlRegex = new Regex(@"https?://[^\s""]+unsubscribe[^\s""]*", RegexOptions.IgnoreCase);
                var match = urlRegex.Match(emailBody);
                if (match.Success)
                {
                    return match.Value;
                }
            }

            return null;
        }

        static string GetEmailBody(MessagePart part)
        {
            if (part == null) return string.Empty;

            string body = string.Empty;

            if (!string.IsNullOrEmpty(part.Body?.Data))
            {
                byte[] decodedBytes = Convert.FromBase64String(part.Body.Data.Replace('-', '+').Replace('_', '/'));
                body = System.Text.Encoding.UTF8.GetString(decodedBytes);
            }

            if (part.Parts != null)
            {
                foreach (var subPart in part.Parts)
                {
                    body += GetEmailBody(subPart);
                }
            }

            return body;
        }

        static async Task<double> GetVirusTotalScoreAsync(HttpClient httpClient, string url, string apiKey)
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

            return (double)malicious / total * 100.0; // Percentage of scanners flagging as malicious
        }

    }
}