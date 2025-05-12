using HtmlAgilityPack;
using Newtonsoft.Json;
using System.Net.Http;
using System.Web;
using GmailUnsubscribeApp.Helpers;
using GmailUnsubscribeApp.Models;
using System.Text;

namespace GmailUnsubscribeApp.Services
{
    public class LinkVisitor
    {
        private readonly GmailServiceWrapper _gmailService;

        public LinkVisitor(GmailServiceWrapper gmailService)
        {
            _gmailService = gmailService;
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

            using (var httpClient = new HttpClient())
            {
                httpClient.DefaultRequestHeaders.Add("User-Agent", "GmailUnsubscribeApp");

                foreach (var link in linksToVisit)
                {
                    var singleResult = await VisitSingleLinkAsync(httpClient, link, linkToEmailId, cacheFile, logDir, timestamp, issues);
                    result.VisitedCount += singleResult.VisitedCount;
                    result.InitialSuccessCount += singleResult.InitialSuccessCount;
                    result.ConfirmationSuccessCount += singleResult.ConfirmationSuccessCount;
                    result.FailedCount += singleResult.FailedCount;
                    result.SuccessfulEmailIds.AddRange(singleResult.SuccessfulEmailIds);
                }
            }

            return result;
        }

        private async Task<LinkVisitResult> VisitSingleLinkAsync(HttpClient httpClient, string link, Dictionary<string, string> linkToEmailId, string cacheFile, string logDir, string timestamp, List<object> issues)
        {
            string abbreviatedLink = link.Length > 76 ? link.Substring(0, 35) + "..." + link.Substring(link.Length - 38) : link;
            Console.Write($"Visiting {abbreviatedLink}: ");
            string safeUriPart = new Uri(link).Host.Replace(".", "_").Substring(0, Math.Min(20, new Uri(link).Host.Length));
            string logFile = Path.Combine(logDir, $"visit_{timestamp}_{safeUriPart}.log");
            var logContent = new StringBuilder();
            var issueEntry = new Dictionary<string, object>();
            bool initialSuccess = false;
            bool confirmationSuccess = false;
            var successfulEmailIds = new List<string>();

            try
            {
                var (initialResponse, initialBody) = await VisitInitialLinkAsync(httpClient, link, logContent, issueEntry);
                initialSuccess = Utility.IsUnsubscribeSuccessful(initialBody);
                Console.WriteLine($"{(int)initialResponse.StatusCode}");

                if (!initialSuccess)
                {
                    await RetryWithPostAsync(httpClient, link, initialBody, logContent, issueEntry);
                    initialSuccess = Utility.IsUnsubscribeSuccessful(initialBody);
                }

                ConfirmationLinkResult confirmationLinkResult = null;
                if (!initialSuccess)
                {
                    confirmationLinkResult = await FindConfirmationLinkAsync(httpClient, link, initialBody);
                }

                if (!string.IsNullOrEmpty(confirmationLinkResult?.Url) && !Utility.LoadVisitedUrls(cacheFile).Contains(confirmationLinkResult.Url))
                {
                    confirmationSuccess = await VisitConfirmationLinkAsync(httpClient, confirmationLinkResult.Url, confirmationLinkResult.IsPost, confirmationLinkResult.FormData, logContent, issueEntry);
                }

                if (initialSuccess || confirmationSuccess)
                {
                    Console.WriteLine(initialSuccess ? "   Unsubscribe successful via initial link." : "   Unsubscribe successful via confirmation link.");
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
                else
                {
                    if (string.IsNullOrEmpty(confirmationLinkResult?.Url))
                    {
                        issues.Add(issueEntry);
                    }
                    Console.WriteLine("   Unsubscribe not confirmed.");
                    return new LinkVisitResult
                    {
                        VisitedCount = 1,
                        InitialSuccessCount = 0,
                        ConfirmationSuccessCount = 0,
                        FailedCount = 1,
                        SuccessfulEmailIds = successfulEmailIds
                    };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   Error: {ex.Message}");
                Console.WriteLine("   Unsubscribe not confirmed.");
                logContent.AppendLine($"Error visiting {link}: {ex.Message}");
                issueEntry["error"] = ex.Message;
                issueEntry["initial_url"] = link;
                issues.Add(issueEntry);
                File.WriteAllText(logFile, logContent.ToString());
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
            }
        }

        private async Task<(HttpResponseMessage Response, string Body)> VisitInitialLinkAsync(HttpClient httpClient, string link, StringBuilder logContent, Dictionary<string, object> issueEntry)
        {
            var initialResponse = await httpClient.GetAsync(link);
            string initialBody = await Utility.ReadResponseContentAsync(initialResponse);
            string sanitizedInitialBody = Utility.SanitizeHtmlContent(initialBody);

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
            issueEntry["initial_body"] = sanitizedInitialBody;

            return (initialResponse, initialBody);
        }

        private async Task RetryWithPostAsync(HttpClient httpClient, string link, string initialBody, StringBuilder logContent, Dictionary<string, object> issueEntry)
        {
            var uri = new Uri(link);
            var queryParams = HttpUtility.ParseQueryString(uri.Query);
            var retryFormData = queryParams.AllKeys.ToDictionary(k => k, k => queryParams[k]);
            var content = new FormUrlEncodedContent(retryFormData);
            var postResponse = await httpClient.PostAsync(link, content);
            initialBody = await Utility.ReadResponseContentAsync(postResponse);
            string sanitizedInitialBody = Utility.SanitizeHtmlContent(initialBody);

            Console.WriteLine($"   Retrying with POST: {(int)postResponse.StatusCode}");

            logContent.AppendLine();
            logContent.AppendLine($"Retry URL: {link}");
            logContent.AppendLine($"Method: POST");
            logContent.AppendLine("Form Data:");
            foreach (var kvp in retryFormData)
            {
                logContent.AppendLine($"  {kvp.Key}: {kvp.Value}");
            }
            logContent.AppendLine($"Status: {(int)postResponse.StatusCode} {postResponse.StatusCode}");
            logContent.AppendLine($"Success: {(Utility.IsUnsubscribeSuccessful(initialBody) ? "Yes" : "No")}");
            logContent.AppendLine("Headers:");
            foreach (var header in postResponse.Headers)
            {
                logContent.AppendLine($"  {header.Key}: {string.Join(", ", header.Value)}");
            }
            logContent.AppendLine("Body:");
            logContent.AppendLine(initialBody);

            issueEntry["retry_status"] = $"{(int)postResponse.StatusCode} {postResponse.StatusCode}";
            issueEntry["retry_headers"] = postResponse.Headers.ToDictionary(h => h.Key, h => string.Join(", ", h.Value));
            issueEntry["retry_body"] = sanitizedInitialBody;
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

                Console.WriteLine("   No confirmation link found");
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
            Console.Write("   Confirming: ");
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
            bool confirmationSuccess = Utility.IsUnsubscribeSuccessful(confirmBody);
            Console.WriteLine($"{(int)confirmResponse.StatusCode}");

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