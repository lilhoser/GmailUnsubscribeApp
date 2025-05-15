using GmailUnsubscribeApp.Models;
using GmailUnsubscribeApp.Services;
using GmailUnsubscribeApp.Helpers;
using Google.Apis.Gmail.v1;
using Newtonsoft.Json;

namespace GmailUnsubscribeApp
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var argParser = new ArgumentParser();
            var config = argParser.Parse(args);
            if (config.ShowHelp || config.HasError)
            {
                if (config.HasError)
                {
                    Console.WriteLine(config.ErrorMessage);
                    Console.WriteLine();
                }
                argParser.ShowHelp();
                return;
            }

            int emailsScanned = 0;
            string outputFile = null;
            List<(string Link, double Score)> scoredLinks = new List<(string Link, double Score)>();
            int visitedLinks = 0;
            int initialSuccessCount = 0;
            int confirmationSuccessCount = 0;
            int failedCount = 0;
            int alreadyVisitedCount = 0;
            var issues = new List<object>();
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GmailUnsubscribeApp", "logs", timestamp);
            string issuesFile = Path.Combine(logDir, $"unsubscribe_issues_{timestamp}.json");

            try
            {
                var quotaDisplay = new QuotaDisplay();
                if (config.ShowUsage)
                {
                    await quotaDisplay.DisplayServiceAndQuotasAsync(config.VirusTotalApiKey, config.HybridApiKey, config.NoLimit);
                    return;
                }

                // Updated constructor with isDryRun
                var gmailServiceWrapper = new GmailServiceWrapper(!string.IsNullOrEmpty(config.DryRunFile));
                var extractionResult = new LinkExtractionResult
                {
                    Links = new List<string>(),
                    EmailsScanned = 0,
                    LinkToEmailId = new Dictionary<string, string>()
                };

                if (!string.IsNullOrEmpty(config.DryRunFile))
                {
                    // Dry run mode: Load links from the specified HTML file
                    if (!File.Exists(config.DryRunFile))
                    {
                        Console.WriteLine($"Error: Dry run file '{config.DryRunFile}' not found.");
                        return;
                    }

                    var htmlGenerator = new HtmlGenerator();
                    scoredLinks = htmlGenerator.ParseHtmlFile(config.DryRunFile);
                    if (scoredLinks.Count == 0)
                    {
                        Console.WriteLine($"No valid unsubscribe links found in {config.DryRunFile}.");
                        return;
                    }

                    Console.WriteLine($"Loaded {scoredLinks.Count} unsubscribe links from {config.DryRunFile}.");
                }
                else
                {
                    await gmailServiceWrapper.AuthenticateAsync(config.CredentialsPath);
                    var linkExtractor = new LinkExtractor(gmailServiceWrapper);
                    if (config.CountItems)
                    {
                        int count = await linkExtractor.CountLabelItemsAsync(config.Label);
                        Console.WriteLine($"Total emails in label '{config.Label}': {count}");
                        return;
                    }

                    if (config.ListContents)
                    {
                        await linkExtractor.ListLabelContentsAsync(config.Label);
                        return;
                    }

                    if (string.IsNullOrEmpty(config.VirusTotalApiKey) && string.IsNullOrEmpty(config.HybridApiKey))
                    {
                        Console.WriteLine("Error: At least one API key (VirusTotal or Hybrid Analysis) is required for scanning.");
                        Console.WriteLine();
                        argParser.ShowHelp();
                        return;
                    }

                    await quotaDisplay.DisplayServiceAndQuotasAsync(config.VirusTotalApiKey, config.HybridApiKey, config.NoLimit);
                    Console.WriteLine();

                    Directory.CreateDirectory(logDir);
                    outputFile = Path.Combine(logDir, $"unsubscribe_links_{timestamp}.html");
                    extractionResult = await linkExtractor.GetUnsubscribeLinksAsync(config.Label, config.MaxResults);
                    emailsScanned = extractionResult.EmailsScanned;

                    var htmlGenerator = new HtmlGenerator();
                    if (extractionResult.Links.Count > 0)
                    {
                        var linkScanner = new LinkScanner();
                        string cacheFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GmailUnsubscribeApp", "visited_urls.txt");
                        var visitedUrls = Utility.LoadVisitedUrls(cacheFile);
                        var linksToScan = extractionResult.Links.Where(link => !visitedUrls.Contains(link)).ToList();
                        int alreadyVisited = extractionResult.Links.Count - linksToScan.Count;
                        Console.WriteLine($"Found {extractionResult.Links.Count} unsubscribe links.");
                        if (!string.IsNullOrEmpty(config.VirusTotalApiKey))
                        {
                            Console.WriteLine($"Submitting {linksToScan.Count} links to VirusTotal ({alreadyVisited} have already been visited)...");
                            scoredLinks = await linkScanner.ScanLinksWithVirusTotalAsync(linksToScan, config.VirusTotalApiKey, config.NoLimit, config.ForceYes);
                        }
                        else
                        {
                            Console.WriteLine($"Submitting {linksToScan.Count} links to Hybrid Analysis ({alreadyVisited} have already been visited)...");
                            scoredLinks = await linkScanner.ScanLinksWithHybridAnalysisAsync(linksToScan, config.HybridApiKey, config.NoLimit, config.ForceYes);
                        }

                        if (scoredLinks.Count > 0)
                        {
                            htmlGenerator.GenerateHtmlFile(scoredLinks, outputFile, true);
                            Console.WriteLine($"HTML file generated with {scoredLinks.Count} scored unsubscribe links: {Path.GetFullPath(outputFile)}");
                        }
                        else
                        {
                            Console.WriteLine($"No unsubscribe links were scored in {emailsScanned} emails scanned in label '{config.Label}'.");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"No unsubscribe links found in {emailsScanned} emails scanned in label '{config.Label}'.");
                    }
                }

                if (scoredLinks.Count > 0)
                {
                    // Updated constructor to include EnableMailto
                    var linkVisitor = new LinkVisitor(gmailServiceWrapper, config.EnableMailto);
                    var linkToEmailId = extractionResult.LinkToEmailId;
                    var visitResult = await linkVisitor.VisitUnsubscribeLinksAsync(scoredLinks, config.Threshold, linkToEmailId, config.ForceYes, timestamp, issues);
                    visitedLinks = visitResult.VisitedCount;
                    initialSuccessCount = visitResult.InitialSuccessCount;
                    confirmationSuccessCount = visitResult.ConfirmationSuccessCount;
                    failedCount = visitResult.FailedCount;
                    alreadyVisitedCount = visitResult.AlreadyVisitedCount;

                    // Write main issues log immediately after link visiting
                    try
                    {
                        Directory.CreateDirectory(logDir);
                        File.WriteAllText(issuesFile, JsonConvert.SerializeObject(issues, Formatting.Indented));
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Warning: Failed to write issues log file: {ex.Message}");
                    }

                    if (visitResult.SuccessfulEmailIds.Count > 0)
                    {
                        if (config.ForceYes)
                        {
                            try
                            {
                                var deletionResults = await gmailServiceWrapper.DeleteEmailsAsync(visitResult.SuccessfulEmailIds);
                                int deletedCount = deletionResults.Count(r => r.Error == null);
                                if (deletedCount > 0)
                                {
                                    Console.WriteLine($"{deletedCount} emails removed.");
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Email deletion cancelled due to error: {ex.Message}");
                            }
                        }
                        else
                        {
                            Console.Write($"\nRemove {visitResult.SuccessfulEmailIds.Count} successfully unsubscribed emails from '{config.Label}'? (y/n): ");
                            string userInput = Console.ReadLine()?.Trim().ToLower();
                            if (userInput == "y")
                            {
                                try
                                {
                                    var deletionResults = await gmailServiceWrapper.DeleteEmailsAsync(visitResult.SuccessfulEmailIds);
                                    int deletedCount = deletionResults.Count(r => r.Error == null);
                                    if (deletedCount > 0)
                                    {
                                        Console.WriteLine($"{deletedCount} emails removed.");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"Email deletion cancelled due to error: {ex.Message}");
                                }
                            }
                            else
                            {
                                Console.WriteLine("Email removal skipped.");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                issues.Add(new Dictionary<string, object>
                {
                    ["error"] = $"General error: {ex.Message}",
                    ["timestamp"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                });
            }
            finally
            {
                try
                {
                    Directory.CreateDirectory(logDir);
                    File.WriteAllText(issuesFile, JsonConvert.SerializeObject(issues, Formatting.Indented));
                    Console.WriteLine($"Issues log saved to: {Path.GetFullPath(issuesFile)}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Failed to write issues log file: {ex.Message}");
                }

                var summaryPrinter = new SummaryPrinter();
                summaryPrinter.PrintSummary((scoredLinks, emailsScanned, outputFile, visitedLinks, initialSuccessCount, confirmationSuccessCount, failedCount, alreadyVisitedCount));
            }
        }
    }
}