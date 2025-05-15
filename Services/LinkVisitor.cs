using HtmlAgilityPack;
using System.Web;
using GmailUnsubscribeApp.Helpers;
using GmailUnsubscribeApp.Models;
using System.Text;
using System.Linq;

namespace GmailUnsubscribeApp.Services
{
    public class LinkVisitor
    {
        private readonly GmailServiceWrapper _gmailService;
        private readonly bool _enableMailto;

        public LinkVisitor(GmailServiceWrapper gmailService, bool enableMailto)
        {
            _gmailService = gmailService;
            _enableMailto = enableMailto;
        }

        public async Task<LinkVisitResult> VisitUnsubscribeLinksAsync(List<(string Link, double Score)> scoredLinks, double threshold, Dictionary<string, string> linkToEmailId, bool forceYes, string timestamp, List<object> issues)
        {
            var validLinks = scoredLinks
                .Where(l => l.Score >= 0 && l.Score <= threshold)
                .Where(l => Uri.TryCreate(l.Link, UriKind.Absolute, out Uri uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
                .Select(l => l.Link)
                .ToList();

            if (validLinks.Count == 0)
            {
                Console.WriteLine($"No unsubscribe links have a maliciousness score below or equal to {threshold}%.");
                return new LinkVisitResult();
            }

            string cacheFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GmailUnsubscribeApp", "visited_urls.txt");
            var visitedUrls = Utility.LoadVisitedUrls(cacheFile);
            var linksToVisit = validLinks.Where(link => !visitedUrls.Contains(link)).ToList();
            int alreadyVisitedCount = validLinks.Count - linksToVisit.Count;

            if (linksToVisit.Count == 0)
            {
                Console.WriteLine($"All {validLinks.Count} unsubscribe links below or equal to {threshold}% have already been visited.");
                return new LinkVisitResult { AlreadyVisitedCount = alreadyVisitedCount };
            }

            Console.WriteLine($"Found {linksToVisit.Count} unvisited unsubscribe links with maliciousness score below or equal to {threshold}%.");
            if (!forceYes)
            {
                Console.Write($"Proceed with visiting these links? (y/n): ");
                string userInput = Console.ReadLine()?.Trim().ToLower();
                if (userInput != "y")
                {
                    Console.WriteLine("Visiting unsubscribe links skipped.");
                    return new LinkVisitResult { AlreadyVisitedCount = alreadyVisitedCount };
                }
            }

            return await ProcessLinksAsync(linksToVisit, linkToEmailId, cacheFile, alreadyVisitedCount, timestamp, issues);
        }

        private async Task<LinkVisitResult> ProcessLinksAsync(List<string> linksToVisit, Dictionary<string, string> linkToEmailId, string cacheFile, int alreadyVisitedCount, string timestamp, List<object> issues)
        {
            var result = new LinkVisitResult
            {
                VisitedCount = 0,
                InitialSuccessCount = 0,
                ConfirmationSuccessCount = 0,
                FailedCount = 0,
                SuccessfulEmailIds = new List<string>(),
                AlreadyVisitedCount = alreadyVisitedCount
            };
            string logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GmailUnsubscribeApp", "logs", timestamp);
            var tldFailures = new Dictionary<string, Dictionary<string, int>>();

            using (var httpClient = new HttpClient())
            {
                httpClient.DefaultRequestHeaders.Add("User-Agent", "GmailUnsubscribeApp");

                foreach (var link in linksToVisit)
                {
                    var singleResult = await VisitSingleLinkAsync(httpClient, link, linkToEmailId, cacheFile, logDir, timestamp, issues, tldFailures);
                    result.VisitedCount += singleResult.VisitedCount;
                    result.InitialSuccessCount += singleResult.InitialSuccessCount;
                    result.ConfirmationSuccessCount += singleResult.ConfirmationSuccessCount;
                    result.FailedCount += singleResult.FailedCount;
                    result.SuccessfulEmailIds.AddRange(singleResult.SuccessfulEmailIds);
                }
            }

            // Output top 10 TLDs with failure reasons
            var topTlds = tldFailures
                .OrderByDescending(kv => kv.Value.Sum(v => v.Value))
                .Take(10)
                .Select(kv => $"{kv.Key}: {string.Join(", ", kv.Value.Select(v => $"{v.Key} ({v.Value})"))}");
            Console.WriteLine("\nTop 10 TLDs with inconclusive or failure outcomes:");
            foreach (var tld in topTlds)
            {
                Console.WriteLine($"  {tld}");
            }
            issues.Add(new { top_tld_failures = topTlds });

            return result;
        }

        private async Task<(HttpResponseMessage Response, string Body)> VisitInitialLinkAsync(HttpClient httpClient, string link, StringBuilder logContent, Dictionary<string, object> issueEntry)
        {
            var initialResponse = await httpClient.GetAsync(link);
            string initialBody = await Utility.ReadResponseContentAsync(initialResponse);

            logContent.AppendLine($"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            logContent.AppendLine($"Initial URL: {link}");
            logContent.AppendLine($"Method: GET");
            logContent.AppendLine($"Status: {(int)initialResponse.StatusCode} {initialResponse.StatusCode}");
            logContent.AppendLine($"Success: {(Utility.IsUnsubscribeSuccessful(initialBody) ? "Yes" : "No")}");
            logContent.AppendLine("Headers:");
            foreach (var header in initialResponse.Headers)
            {
                logContent.AppendLine($"  {header.Key}: {string.Join(", ", header.Value)}");
            }
            logContent.AppendLine("Body:");
            logContent.AppendLine(initialBody);

            issueEntry["initial_url"] = link;
            issueEntry["initial_status"] = $"{(int)initialResponse.StatusCode} {initialResponse.StatusCode}";
            issueEntry["initial_headers"] = initialResponse.Headers.ToDictionary(h => h.Key, h => string.Join(", ", h.Value));
            issueEntry["initial_body"] = initialBody;

            return (initialResponse, initialBody);
        }

        private async Task<(HttpResponseMessage Response, string Body)> RetryWithPostAsync(HttpClient httpClient, string link, StringBuilder logContent, Dictionary<string, object> issueEntry)
        {
            var uri = new Uri(link);
            var queryParams = HttpUtility.ParseQueryString(uri.Query);
            var retryFormData = queryParams.AllKeys.ToDictionary(k => k, k => queryParams[k]);
            var content = new FormUrlEncodedContent(retryFormData);
            var postResponse = await httpClient.PostAsync(link, content);
            string postBody = await Utility.ReadResponseContentAsync(postResponse);

            logContent.AppendLine();
            logContent.AppendLine($"Retry URL: {link}");
            logContent.AppendLine($"Method: POST");
            logContent.AppendLine("Form Data:");
            foreach (var kvp in retryFormData)
            {
                logContent.AppendLine($"  {kvp.Key}: {kvp.Value}");
            }
            logContent.AppendLine($"Status: {(int)postResponse.StatusCode} {postResponse.StatusCode}");
            logContent.AppendLine($"Success: {(Utility.IsUnsubscribeSuccessful(postBody) ? "Yes" : "No")}");
            logContent.AppendLine("Headers:");
            foreach (var header in postResponse.Headers)
            {
                logContent.AppendLine($"  {header.Key}: {string.Join(", ", header.Value)}");
            }
            logContent.AppendLine("Body:");
            logContent.AppendLine(postBody);

            issueEntry["retry_status"] = $"{(int)postResponse.StatusCode} {postResponse.StatusCode}";
            issueEntry["retry_headers"] = postResponse.Headers.ToDictionary(h => h.Key, h => string.Join(", ", h.Value));
            issueEntry["retry_body"] = postBody;

            return (postResponse, postBody);
        }

        private async Task<LinkVisitResult> VisitSingleLinkAsync(HttpClient httpClient, string link, Dictionary<string, string> linkToEmailId, string cacheFile, string logDir, string timestamp, List<object> issues, Dictionary<string, Dictionary<string, int>> tldFailures)
        {
            string abbreviatedLink = link.Length > 76 ? link.Substring(0, 35) + "..." + link.Substring(link.Length - 38) : link;
            Console.WriteLine($"Visiting {abbreviatedLink}:");

            string safeUriPart = new Uri(link).Host.Replace(".", "_").Substring(0, Math.Min(20, new Uri(link).Host.Length));
            string logFile = Path.Combine(logDir, $"visit_{timestamp}_{safeUriPart}.log");
            var logContent = new StringBuilder();
            var issueEntry = new Dictionary<string, object>();
            var successfulEmailIds = new List<string>();
            string tld = GetTld(link);

            try
            {
                var (result, body) = await ProcessInitialLinkAsync(httpClient, link, logContent, issueEntry, tld, tldFailures, linkToEmailId, cacheFile);
                if (result != null)
                {
                    return result;
                }

                return await ProcessFormAndConfirmationAsync(httpClient, link, body, logContent, issueEntry, tld, tldFailures, linkToEmailId, cacheFile, issues);
            }
            catch (Exception ex)
            {
                LogError(logContent, issueEntry, tld, tldFailures, link, ex);
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("  Exception: Failed");
                Console.ResetColor();
                issues.Add(issueEntry);
                return new LinkVisitResult
                {
                    VisitedCount = 1,
                    InitialSuccessCount = 0,
                    ConfirmationSuccessCount = 0,
                    FailedCount = 1,
                    SuccessfulEmailIds = successfulEmailIds
                };
            }
            finally
            {
                File.WriteAllText(logFile, logContent.ToString());
                httpClient.DefaultRequestHeaders.Remove("Accept");
            }
        }

        private string GetTld(string link)
        {
            var tldParts = new Uri(link).Host.Split('.');
            if (tldParts.Length > 2)
            {
                return $"{tldParts[tldParts.Length - 2]}.{tldParts[tldParts.Length - 1]}";
            }
            return new Uri(link).Host;
        }

        private async Task<(LinkVisitResult Result, string Body)> ProcessInitialLinkAsync(HttpClient httpClient, string link, StringBuilder logContent, Dictionary<string, object> issueEntry, string tld, Dictionary<string, Dictionary<string, int>> tldFailures, Dictionary<string, string> linkToEmailId, string cacheFile)
        {
            var successfulEmailIds = new List<string>();
            var (initialResponse, initialBody) = await VisitInitialLinkAsync(httpClient, link, logContent, issueEntry);

            // Check success keywords first
            bool initialSuccess = (initialResponse.StatusCode == System.Net.HttpStatusCode.OK || initialResponse.StatusCode == System.Net.HttpStatusCode.Accepted) && Utility.IsUnsubscribeSuccessful(initialBody);

            Console.ForegroundColor = initialSuccess ? ConsoleColor.Green : ConsoleColor.Yellow;
            Console.WriteLine($"  Initial link ({(int)initialResponse.StatusCode}): {(initialSuccess ? "Successful" : "Unsuccessful")}");
            Console.ResetColor();

            if (initialSuccess)
            {
                Utility.SaveVisitedUrl(cacheFile, link);
                if (linkToEmailId.ContainsKey(link))
                {
                    successfulEmailIds.Add(linkToEmailId[link]);
                }
                return (new LinkVisitResult
                {
                    VisitedCount = 1,
                    InitialSuccessCount = 1,
                    ConfirmationSuccessCount = 0,
                    FailedCount = 0,
                    SuccessfulEmailIds = successfulEmailIds
                }, initialBody);
            }

            // Handle 400 BadRequest explicitly
            if (initialResponse.StatusCode == System.Net.HttpStatusCode.BadRequest)
            {
                LogError(logContent, issueEntry, tld, tldFailures, link, new Exception("Bad request response"), "Bad request response");
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("  Initial link: Failed");
                Console.ResetColor();
                return (new LinkVisitResult
                {
                    VisitedCount = 1,
                    InitialSuccessCount = 0,
                    ConfirmationSuccessCount = 0,
                    FailedCount = 1,
                    SuccessfulEmailIds = successfulEmailIds
                }, initialBody);
            }

            // Check for redirect to error page
            if (initialResponse.StatusCode is >= System.Net.HttpStatusCode.Moved and <= System.Net.HttpStatusCode.UseProxy && !Utility.IsUnsubscribeSuccessful(initialBody))
            {
                var redirectUrl = initialResponse.Headers.Location?.ToString();
                if (redirectUrl != null && redirectUrl.Contains("error", StringComparison.OrdinalIgnoreCase))
                {
                    LogError(logContent, issueEntry, tld, tldFailures, link, new Exception($"Redirect to error page: {redirectUrl}"), "Redirect to error page");
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("  Redirect: Failed");
                    Console.ResetColor();
                    return (new LinkVisitResult
                    {
                        VisitedCount = 1,
                        InitialSuccessCount = 0,
                        ConfirmationSuccessCount = 0,
                        FailedCount = 1,
                        SuccessfulEmailIds = successfulEmailIds
                    }, initialBody);
                }
            }

            // Process mailto links if enabled and initial response is OK
            if (_enableMailto && !_gmailService.IsDryRun && initialResponse.StatusCode == System.Net.HttpStatusCode.OK)
            {
                var doc = new HtmlDocument();
                doc.LoadHtml(initialBody);
                var mailtoLinks = doc.DocumentNode.SelectNodes("//a[starts-with(@href, 'mailto:')]");
                if (mailtoLinks != null && mailtoLinks.Any())
                {
                    string mailtoAddress = mailtoLinks.First().GetAttributeValue("href", "").Replace("mailto:", "").Split('?')[0];
                    try
                    {
                        await _gmailService.SendEmailAsync(mailtoAddress, "Unsubscribe Request", "");
                        logContent.AppendLine($"Sent mailto unsubscribe email to {mailtoAddress}");
                        issueEntry["mailto_sent"] = mailtoAddress;
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("  Mailto link: Successful");
                        Console.ResetColor();
                        Utility.SaveVisitedUrl(cacheFile, link);
                        if (linkToEmailId.ContainsKey(link))
                        {
                            successfulEmailIds.Add(linkToEmailId[link]);
                        }
                        return (new LinkVisitResult
                        {
                            VisitedCount = 1,
                            InitialSuccessCount = 1,
                            ConfirmationSuccessCount = 0,
                            FailedCount = 0,
                            SuccessfulEmailIds = successfulEmailIds
                        }, initialBody);
                    }
                    catch (Exception ex)
                    {
                        logContent.AppendLine($"Error sending mailto email to {mailtoAddress}: {ex.Message}");
                        issueEntry["mailto_error"] = ex.Message;
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("  Mailto link: Failed");
                        Console.ResetColor();
                    }
                }
            }

            return (null, initialBody); // Continue to form and confirmation processing
        }

        private async Task<LinkVisitResult> ProcessFormAndConfirmationAsync(HttpClient httpClient, string link, string initialBody, StringBuilder logContent, Dictionary<string, object> issueEntry, string tld, Dictionary<string, Dictionary<string, int>> tldFailures, Dictionary<string, string> linkToEmailId, string cacheFile, List<object> issues)
        {
            var successfulEmailIds = new List<string>();
            bool initialSuccess = false;
            bool confirmationSuccess = false;

            // Process form submission
            var (formResult, postBody) = await TryProcessFormAsync(httpClient, link, initialBody, logContent, issueEntry, tld, tldFailures, linkToEmailId, cacheFile);
            if (formResult != null)
            {
                if (formResult.InitialSuccessCount > 0)
                {
                    initialSuccess = true;
                    successfulEmailIds.AddRange(formResult.SuccessfulEmailIds);
                }
                else
                {
                    issues.Add(issueEntry);
                    return formResult;
                }
            }

            // Process confirmation link
            var confirmationResult = await TryProcessConfirmationAsync(httpClient, link, postBody ?? initialBody, logContent, issueEntry, tld, tldFailures, linkToEmailId, cacheFile);
            if (confirmationResult != null)
            {
                confirmationSuccess = confirmationResult.ConfirmationSuccessCount > 0;
                if (confirmationSuccess)
                {
                    successfulEmailIds.AddRange(confirmationResult.SuccessfulEmailIds);
                }
                else
                {
                    issues.Add(issueEntry);
                    return confirmationResult;
                }
            }

            // Final result
            if (initialSuccess || confirmationSuccess)
            {
                Utility.SaveVisitedUrl(cacheFile, link);
                if (linkToEmailId.ContainsKey(link))
                {
                    successfulEmailIds.Add(linkToEmailId[link]);
                }
                return new LinkVisitResult
                {
                    VisitedCount = 1,
                    InitialSuccessCount = initialSuccess ? 1 : 0,
                    ConfirmationSuccessCount = confirmationSuccess ? 1 : 0,
                    FailedCount = 0,
                    SuccessfulEmailIds = successfulEmailIds
                };
            }

            issueEntry["failure_reason"] = "Initial response not successful";
            issues.Add(issueEntry);
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("  Confirmation link: Indeterminate");
            Console.ResetColor();
            return new LinkVisitResult
            {
                VisitedCount = 1,
                InitialSuccessCount = 0,
                ConfirmationSuccessCount = 0,
                FailedCount = 1,
                SuccessfulEmailIds = successfulEmailIds
            };
        }

        private async Task<(LinkVisitResult Result, string PostBody)> TryProcessFormAsync(HttpClient httpClient, string link, string initialBody, StringBuilder logContent, Dictionary<string, object> issueEntry, string tld, Dictionary<string, Dictionary<string, int>> tldFailures, Dictionary<string, string> linkToEmailId, string cacheFile)
        {
            var successfulEmailIds = new List<string>();
            var doc = new HtmlDocument();
            doc.LoadHtml(initialBody);
            var buttonKeywords = Utility.GetUnsubscribeButtonKeywords();
            HtmlNode formNode = null;
            string buttonText = "Unknown";
            string formAction = link;
            string elementType = "Unknown";

            try
            {
                var forms = doc.DocumentNode.SelectNodes("//form[descendant::button or descendant::input[@type='submit']]");
                if (forms != null)
                {
                    foreach (var form in forms)
                    {
                        var buttons = form.SelectNodes(".//button");
                        var submitInputs = form.SelectNodes(".//input[@type='submit']");
                        if (buttons != null)
                        {
                            foreach (var button in buttons)
                            {
                                buttonText = button.InnerText?.Trim()?.Normalize(NormalizationForm.FormC)?.ToLowerInvariant() ?? "";
                                if (buttonKeywords.Any(k => buttonText.Contains(k.Normalize(NormalizationForm.FormC).ToLowerInvariant())))
                                {
                                    formNode = form;
                                    formAction = form.GetAttributeValue("action", link);
                                    elementType = "button";
                                    break;
                                }
                            }
                        }
                        if (formNode == null && submitInputs != null)
                        {
                            foreach (var input in submitInputs)
                            {
                                buttonText = input.GetAttributeValue("value", "")?.Trim()?.Normalize(NormalizationForm.FormC)?.ToLowerInvariant() ?? "";
                                if (buttonKeywords.Any(k => buttonText.Contains(k.Normalize(NormalizationForm.FormC).ToLowerInvariant())))
                                {
                                    formNode = form;
                                    formAction = form.GetAttributeValue("action", link);
                                    elementType = "input";
                                    break;
                                }
                            }
                        }
                        if (formNode != null) break;
                    }
                }
            }
            catch (Exception ex)
            {
                logContent.AppendLine($"Error detecting form: {ex.Message}");
                issueEntry["form_detection_error"] = ex.Message;
                issueEntry["form_button_text"] = buttonText;
                if (!tldFailures.ContainsKey(tld)) tldFailures[tld] = new Dictionary<string, int>();
                tldFailures[tld]["Form detection error"] = tldFailures[tld].ContainsKey("Form detection error") ? tldFailures[tld]["Form detection error"] + 1 : 1;
            }

            if (formNode == null)
            {
                logContent.AppendLine("No form with unsubscribe button detected.");
                issueEntry["form_detection_result"] = "No form with unsubscribe button found";
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("  Form action: Indeterminate");
                Console.ResetColor();
                if (!tldFailures.ContainsKey(tld)) tldFailures[tld] = new Dictionary<string, int>();
                tldFailures[tld]["No confirmation link found"] = tldFailures[tld].ContainsKey("No confirmation link found") ? tldFailures[tld]["No confirmation link found"] + 1 : 1;
                return (new LinkVisitResult
                {
                    VisitedCount = 1,
                    InitialSuccessCount = 0,
                    ConfirmationSuccessCount = 0,
                    FailedCount = 1,
                    SuccessfulEmailIds = successfulEmailIds
                }, initialBody);
            }

            var formData = new Dictionary<string, string>();
            var inputs = formNode.SelectNodes(".//input | .//select | .//textarea");
            if (inputs != null)
            {
                foreach (var input in inputs)
                {
                    string name = input.GetAttributeValue("name", "");
                    string value = input.GetAttributeValue("value", input.InnerText?.Trim() ?? "");
                    if (!string.IsNullOrEmpty(name))
                    {
                        formData[name] = value;
                    }
                }
            }

            logContent.AppendLine($"Detected form with {elementType} text: {buttonText}");
            logContent.AppendLine($"Form action URL: {formAction}");

            // Resolve formAction to absolute URL
            if (formAction.StartsWith("//"))
            {
                var linkUri = new Uri(link);
                formAction = $"{linkUri.Scheme}:{formAction}";
            }
            else if (!Uri.TryCreate(formAction, UriKind.Absolute, out Uri formUri))
            {
                formAction = new Uri(new Uri(link), formAction).ToString();
            }
            if (!Uri.TryCreate(formAction, UriKind.Absolute, out _))
            {
                logContent.AppendLine($"Error: Invalid form action URL '{formAction}' after resolution.");
                issueEntry["failure_reason"] = $"Invalid form action URL '{formAction}' after resolution.";
                if (!tldFailures.ContainsKey(tld)) tldFailures[tld] = new Dictionary<string, int>();
                tldFailures[tld]["Invalid form action URL"] = tldFailures[tld].ContainsKey("Invalid form action URL") ? tldFailures[tld]["Invalid form action URL"] + 1 : 1;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("  Form action: Failed");
                Console.ResetColor();
                return (new LinkVisitResult
                {
                    VisitedCount = 1,
                    InitialSuccessCount = 0,
                    ConfirmationSuccessCount = 0,
                    FailedCount = 1,
                    SuccessfulEmailIds = successfulEmailIds
                }, initialBody);
            }

            httpClient.DefaultRequestHeaders.Add("Accept", "application/x-www-form-urlencoded");
            var content = new FormUrlEncodedContent(formData);
            var postResponse = await httpClient.PostAsync(formAction, content);
            var postBody = await Utility.ReadResponseContentAsync(postResponse);

            logContent.AppendLine();
            logContent.AppendLine($"Form Submission URL: {formAction}");
            logContent.AppendLine($"Method: POST");
            logContent.AppendLine("Form Data:");
            foreach (var kvp in formData)
            {
                logContent.AppendLine($"  {kvp.Key}: {kvp.Value}");
            }
            logContent.AppendLine("Request Headers:");
            foreach (var header in httpClient.DefaultRequestHeaders)
            {
                logContent.AppendLine($"  {header.Key}: {string.Join(", ", header.Value)}");
            }
            logContent.AppendLine($"Status: {(int)postResponse.StatusCode} {postResponse.StatusCode}");
            logContent.AppendLine($"Success: {(Utility.IsUnsubscribeSuccessful(postBody) ? "Yes" : "No")}");
            logContent.AppendLine("Response Headers:");
            foreach (var header in postResponse.Headers)
            {
                logContent.AppendLine($"  {header.Key}: {string.Join(", ", header.Value)}");
            }
            logContent.AppendLine("Body:");
            logContent.AppendLine(postBody);

            issueEntry["form_submission_status"] = $"{(int)postResponse.StatusCode} {postResponse.StatusCode}";
            issueEntry["form_submission_headers"] = postResponse.Headers.ToDictionary(h => h.Key, h => string.Join(", ", h.Value));
            issueEntry["form_submission_body"] = postBody;
            issueEntry["form_button_text"] = buttonText;
            issueEntry["form_action_url"] = formAction;
            issueEntry["form_data"] = formData;
            issueEntry["form_element_type"] = elementType;

            bool formSuccess = (postResponse.StatusCode == System.Net.HttpStatusCode.OK || postResponse.StatusCode == System.Net.HttpStatusCode.Accepted) && Utility.IsUnsubscribeSuccessful(postBody);
            Console.ForegroundColor = formSuccess ? ConsoleColor.Green : (postResponse.StatusCode == System.Net.HttpStatusCode.OK ? ConsoleColor.Yellow : ConsoleColor.Red);
            Console.WriteLine($"  Form action ({(int)postResponse.StatusCode}): {(formSuccess ? "Successful" : postResponse.StatusCode == System.Net.HttpStatusCode.OK ? "Indeterminate" : "Unsuccessful")}");
            Console.ResetColor();

            if (formSuccess)
            {
                Utility.SaveVisitedUrl(cacheFile, link);
                if (linkToEmailId.ContainsKey(link))
                {
                    successfulEmailIds.Add(linkToEmailId[link]);
                }
                return (new LinkVisitResult
                {
                    VisitedCount = 1,
                    InitialSuccessCount = 1,
                    ConfirmationSuccessCount = 0,
                    FailedCount = 0,
                    SuccessfulEmailIds = successfulEmailIds
                }, postBody);
            }

            // Handle 500 Internal Server Error explicitly
            if (postResponse.StatusCode == System.Net.HttpStatusCode.InternalServerError)
            {
                LogError(logContent, issueEntry, tld, tldFailures, link, new Exception("Server error during form submission"), "Server error during form submission");
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("  Form action: Failed");
                Console.ResetColor();
                return (new LinkVisitResult
                {
                    VisitedCount = 1,
                    InitialSuccessCount = 0,
                    ConfirmationSuccessCount = 0,
                    FailedCount = 1,
                    SuccessfulEmailIds = successfulEmailIds
                }, postBody);
            }

            if (!formSuccess)
            {
                logContent.AppendLine("Form submission failed to confirm unsubscribe.");
                if (!tldFailures.ContainsKey(tld)) tldFailures[tld] = new Dictionary<string, int>();
                string failureReason = postResponse.StatusCode != System.Net.HttpStatusCode.OK ? $"Form submission failed: {postResponse.StatusCode}" : "No confirmation message";
                tldFailures[tld][failureReason] = tldFailures[tld].ContainsKey(failureReason) ? tldFailures[tld][failureReason] + 1 : 1;
            }

            return (null, postBody);
        }

        private async Task<LinkVisitResult> TryProcessConfirmationAsync(HttpClient httpClient, string link, string body, StringBuilder logContent, Dictionary<string, object> issueEntry, string tld, Dictionary<string, Dictionary<string, int>> tldFailures, Dictionary<string, string> linkToEmailId, string cacheFile)
        {
            var successfulEmailIds = new List<string>();
            var confirmationLinkResult = await FindConfirmationLinkAsync(httpClient, link, body);
            issueEntry["confirmation_link_search_result"] = confirmationLinkResult?.Url ?? "No confirmation link found";

            if (string.IsNullOrEmpty(confirmationLinkResult?.Url) || Utility.LoadVisitedUrls(cacheFile).Contains(confirmationLinkResult.Url))
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("  Confirmation link: No link found on page");
                Console.ResetColor();
                if (!tldFailures.ContainsKey(tld)) tldFailures[tld] = new Dictionary<string, int>();
                tldFailures[tld]["No confirmation link found"] = tldFailures[tld].ContainsKey("No confirmation link found") ? tldFailures[tld]["No confirmation link found"] + 1 : 1;
                return new LinkVisitResult
                {
                    VisitedCount = 1,
                    InitialSuccessCount = 0,
                    ConfirmationSuccessCount = 0,
                    FailedCount = 1,
                    SuccessfulEmailIds = successfulEmailIds
                };
            }

            bool confirmationSuccess = await VisitConfirmationLinkAsync(httpClient, confirmationLinkResult.Url, confirmationLinkResult.IsPost, confirmationLinkResult.FormData, logContent, issueEntry);
            Console.ForegroundColor = confirmationSuccess ? ConsoleColor.Green : ConsoleColor.Yellow;
            Console.WriteLine($"  Confirmation link ({(confirmationLinkResult.IsPost ? "POST" : "GET")}): {(confirmationSuccess ? "Successful" : "Indeterminate")}");
            Console.ResetColor();

            if (confirmationSuccess)
            {
                Utility.SaveVisitedUrl(cacheFile, link);
                if (linkToEmailId.ContainsKey(link))
                {
                    successfulEmailIds.Add(linkToEmailId[link]);
                }
                return new LinkVisitResult
                {
                    VisitedCount = 1,
                    InitialSuccessCount = 0,
                    ConfirmationSuccessCount = 1,
                    FailedCount = 0,
                    SuccessfulEmailIds = successfulEmailIds
                };
            }

            if (!confirmationSuccess)
            {
                if (!tldFailures.ContainsKey(tld)) tldFailures[tld] = new Dictionary<string, int>();
                tldFailures[tld]["No confirmation message"] = tldFailures[tld].ContainsKey("No confirmation message") ? tldFailures[tld]["No confirmation message"] + 1 : 1;
            }

            return new LinkVisitResult
            {
                VisitedCount = 1,
                InitialSuccessCount = 0,
                ConfirmationSuccessCount = 0,
                FailedCount = 1,
                SuccessfulEmailIds = successfulEmailIds
            };
        }

        private void LogError(StringBuilder logContent, Dictionary<string, object> issueEntry, string tld, Dictionary<string, Dictionary<string, int>> tldFailures, string link, Exception ex, string failureReason = "Exception during processing")
        {
            logContent.AppendLine($"Error visiting {link}: {ex.Message}");
            issueEntry["error"] = ex.Message;
            issueEntry["initial_url"] = link;
            issueEntry["failure_reason"] = failureReason;
            if (!tldFailures.ContainsKey(tld)) tldFailures[tld] = new Dictionary<string, int>();
            tldFailures[tld][failureReason] = tldFailures[tld].ContainsKey(failureReason) ? tldFailures[tld][failureReason] + 1 : 1;
        }

        private async Task<ConfirmationLinkResult> FindConfirmationLinkAsync(HttpClient httpClient, string initialUrl, string htmlContent)
        {
            try
            {
                var doc = new HtmlDocument();
                doc.LoadHtml(htmlContent);

                var urlPatterns = new[] { "/unsubscribe", "/unsub", "/optout", "/opt-out", "/remove" };
                var forms = doc.DocumentNode.SelectNodes("//form[@action]");
                if (forms != null)
                {
                    foreach (var form in forms)
                    {
                        string action = form.GetAttributeValue("action", "").ToLower();
                        if (urlPatterns.Any(p => action.Contains(p)))
                        {
                            if (Uri.TryCreate(action, UriKind.RelativeOrAbsolute, out Uri uri))
                            {
                                if (!uri.IsAbsoluteUri)
                                {
                                    Uri baseUri = new Uri(initialUrl);
                                    action = new Uri(baseUri, action).ToString();
                                }
                                var formData = new Dictionary<string, string>();
                                var inputs = form.SelectNodes(".//input");
                                if (inputs != null)
                                {
                                    foreach (var input in inputs)
                                    {
                                        string name = input.GetAttributeValue("name", "");
                                        string value = input.GetAttributeValue("value", "");
                                        if (!string.IsNullOrEmpty(name))
                                        {
                                            formData[name] = value;
                                        }
                                    }
                                }
                                return new ConfirmationLinkResult { Url = action, IsPost = true, FormData = formData };
                            }
                        }
                    }
                }

                var submitButtons = doc.DocumentNode.SelectNodes("//form//input[@type='submit']");
                if (submitButtons != null)
                {
                    foreach (var button in submitButtons)
                    {
                        var form = button.Ancestors("form").FirstOrDefault();
                        if (form != null)
                        {
                            string action = form.GetAttributeValue("action", "").ToLower();
                            if (!string.IsNullOrEmpty(action) && Uri.TryCreate(action, UriKind.RelativeOrAbsolute, out Uri uri))
                            {
                                if (!uri.IsAbsoluteUri)
                                {
                                    Uri baseUri = new Uri(initialUrl);
                                    action = new Uri(baseUri, action).ToString();
                                }
                                var formData = new Dictionary<string, string>();
                                var inputs = form.SelectNodes(".//input");
                                if (inputs != null)
                                {
                                    foreach (var input in inputs)
                                    {
                                        string name = input.GetAttributeValue("name", "");
                                        string value = input.GetAttributeValue("value", "");
                                        if (!string.IsNullOrEmpty(name))
                                        {
                                            formData[name] = value;
                                        }
                                    }
                                }
                                return new ConfirmationLinkResult { Url = action, IsPost = true, FormData = formData };
                            }
                        }
                    }
                }

                var links = doc.DocumentNode.SelectNodes("//a[@href]");
                if (links != null)
                {
                    foreach (var link in links)
                    {
                        string href = link.GetAttributeValue("href", "").ToLower();
                        if (href.StartsWith("javascript:"))
                        {
                            continue;
                        }
                        if (urlPatterns.Any(p => href.Contains(p)))
                        {
                            if (Uri.TryCreate(href, UriKind.RelativeOrAbsolute, out Uri uri))
                            {
                                if (!uri.IsAbsoluteUri)
                                {
                                    Uri baseUri = new Uri(initialUrl);
                                    href = new Uri(baseUri, href).ToString();
                                }
                                return new ConfirmationLinkResult { Url = href, IsPost = false, FormData = null };
                            }
                        }
                    }
                }

                return new ConfirmationLinkResult();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   Warning: Failed to parse confirmation link: {ex.Message}");
                return new ConfirmationLinkResult();
            }
        }

        private async Task<bool> VisitConfirmationLinkAsync(HttpClient httpClient, string confirmationLink, bool isPost, Dictionary<string, string> formData, StringBuilder logContent, Dictionary<string, object> issueEntry)
        {
            HttpResponseMessage confirmResponse;
            if (isPost)
            {
                var content = new FormUrlEncodedContent(formData);
                confirmResponse = await httpClient.PostAsync(confirmationLink, content);
            }
            else
            {
                confirmResponse = await httpClient.GetAsync(confirmationLink);
            }
            string confirmBody = await Utility.ReadResponseContentAsync(confirmResponse);
            bool confirmationSuccess = (confirmResponse.StatusCode == System.Net.HttpStatusCode.OK || confirmResponse.StatusCode == System.Net.HttpStatusCode.Accepted) && Utility.IsUnsubscribeSuccessful(confirmBody);

            logContent.AppendLine();
            logContent.AppendLine($"Confirmation URL: {confirmationLink}");
            logContent.AppendLine($"Method: {(isPost ? "POST" : "GET")}");
            if (isPost && formData != null)
            {
                logContent.AppendLine("Form Data:");
                foreach (var kvp in formData)
                {
                    logContent.AppendLine($"  {kvp.Key}: {kvp.Value}");
                }
            }
            logContent.AppendLine($"Status: {(int)confirmResponse.StatusCode} {confirmResponse.StatusCode}");
            logContent.AppendLine($"Success: {(confirmationSuccess ? "Yes" : "No")}");
            logContent.AppendLine("Headers:");
            foreach (var header in confirmResponse.Headers)
            {
                logContent.AppendLine($"  {header.Key}: {string.Join(", ", header.Value)}");
            }
            logContent.AppendLine("Body:");
            logContent.AppendLine(confirmBody);

            string sanitizedConfirmBody = Utility.SanitizeHtmlContent(confirmBody);
            issueEntry["confirmation_url"] = confirmationLink;
            issueEntry["confirmation_method"] = isPost ? "POST" : "GET";
            if (isPost && formData != null)
            {
                issueEntry["confirmation_form_data"] = formData;
            }
            issueEntry["confirmation_status"] = $"{(int)confirmResponse.StatusCode} {confirmResponse.StatusCode}";
            issueEntry["confirmation_headers"] = confirmResponse.Headers.ToDictionary(h => h.Key, h => string.Join(", ", h.Value));
            issueEntry["confirmation_body"] = sanitizedConfirmBody;

            return confirmationSuccess;
        }
    }
}