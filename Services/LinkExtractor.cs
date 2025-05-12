using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;
using System.Text.RegularExpressions;
using GmailUnsubscribeApp.Models;
using System.Text;

namespace GmailUnsubscribeApp.Services
{
    public class LinkExtractor
    {
        private readonly GmailServiceWrapper _gmailService;

        public LinkExtractor(GmailServiceWrapper gmailService)
        {
            _gmailService = gmailService;
        }

        public async Task<LinkExtractionResult> GetUnsubscribeLinksAsync(string label, long maxResults)
        {
            var result = new LinkExtractionResult
            {
                Links = new List<string>(),
                EmailsScanned = 0,
                LinkToEmailId = new Dictionary<string, string>()
            };
            var (labelId, labelError) = await _gmailService.GetLabelIdAsync(label);

            if (string.IsNullOrEmpty(labelId))
            {
                Console.WriteLine(labelError ?? $"Label '{label}' not found.");
                return result;
            }

            var service = _gmailService.Service;
            var request = service.Users.Messages.List("me");
            request.LabelIds = new[] { labelId }.ToList();
            request.MaxResults = Math.Min(maxResults, 100);

            int totalMessages;
            try
            {
                var labelInfo = await service.Users.Labels.Get("me", labelId).ExecuteAsync();
                totalMessages = labelInfo.MessagesTotal ?? 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error retrieving label info: {ex.Message}");
                return result;
            }
            Console.WriteLine($"Scanning up to {maxResults} emails in label '{label}' (total messages: {totalMessages}).");

            int cursorTop = Console.CursorTop;
            while (request != null && result.EmailsScanned < maxResults)
            {
                try
                {
                    var response = await request.ExecuteAsync();
                    if (response.Messages != null)
                    {
                        foreach (var message in response.Messages)
                        {
                            if (result.EmailsScanned >= maxResults) break;
                            result.EmailsScanned++;
                            try
                            {
                                var email = await service.Users.Messages.Get("me", message.Id).ExecuteAsync();
                                string subject = email.Payload?.Headers?.FirstOrDefault(h => h.Name == "Subject")?.Value ?? "(No Subject)";
                                string unsubscribeLink = ExtractUnsubscribeLink(email);
                                int progressPercent = (int)((double)result.EmailsScanned / Math.Min(maxResults, totalMessages) * 100);
                                string displaySubject = subject.Length > 50 ? subject.Substring(0, 47) + "..." : subject.PadRight(50);
                                string progressMessage = $"Scanning email {result.EmailsScanned.ToString().PadLeft(4)} of {Math.Min(maxResults, totalMessages)} ({progressPercent.ToString().PadLeft(3)}%): {displaySubject}";
                                Console.SetCursorPosition(0, cursorTop);
                                Console.Write(progressMessage);
                                Console.Write(new string(' ', Math.Max(0, Console.WindowWidth - progressMessage.Length)));
                                if (!string.IsNullOrEmpty(unsubscribeLink) && !result.Links.Contains(unsubscribeLink))
                                {
                                    result.Links.Add(unsubscribeLink);
                                    result.LinkToEmailId[unsubscribeLink] = message.Id;
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"\nError processing email ID {message.Id}: {ex.Message}");
                            }
                        }
                    }

                    request.PageToken = response.NextPageToken;
                    if (string.IsNullOrEmpty(response.NextPageToken))
                        break;
                }
                catch (Google.GoogleApiException ex)
                {
                    Console.WriteLine($"\nError retrieving emails: {ex.Message}");
                    Console.WriteLine($"HTTP Status Code: {ex.HttpStatusCode}");
                    if (ex.Error != null)
                    {
                        Console.WriteLine($"Error Code: {ex.Error.Code}, Message: {ex.Error.Message}");
                    }
                    break;
                }
            }

            Console.WriteLine();
            return result;
        }

        public async Task<List<(string Id, string Subject)>> ListLabelContentsAsync(string label)
        {
            var contents = new List<(string Id, string Subject)>();
            var (labelId, labelError) = await _gmailService.GetLabelIdAsync(label);
            if (string.IsNullOrEmpty(labelId))
            {
                Console.WriteLine(labelError ?? $"Label '{label}' not found.");
                return contents;
            }

            Console.WriteLine($"Contents of label '{label}':");
            var service = _gmailService.Service;
            var request = service.Users.Messages.List("me");
            request.LabelIds = new[] { labelId }.ToList();
            request.MaxResults = 100;

            while (request != null)
            {
                try
                {
                    var response = await request.ExecuteAsync();
                    if (response.Messages != null)
                    {
                        foreach (var message in response.Messages)
                        {
                            try
                            {
                                var email = await service.Users.Messages.Get("me", message.Id).ExecuteAsync();
                                string subject = email.Payload?.Headers?.FirstOrDefault(h => h.Name == "Subject")?.Value ?? "(No Subject)";
                                contents.Add((message.Id, subject));
                                Console.WriteLine($"ID: {message.Id}, Subject: {subject}");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Error processing email ID {message.Id}: {ex.Message}");
                            }
                        }
                    }

                    request.PageToken = response.NextPageToken;
                    if (string.IsNullOrEmpty(response.NextPageToken))
                        break;
                }
                catch (Google.GoogleApiException ex)
                {
                    Console.WriteLine($"Error retrieving emails: {ex.Message}");
                    Console.WriteLine($"HTTP Status Code: {ex.HttpStatusCode}");
                    if (ex.Error != null)
                    {
                        Console.WriteLine($"Error Code: {ex.Error.Code}, Message: {ex.Error.Message}");
                    }
                    break;
                }
            }

            return contents;
        }

        public async Task<int> CountLabelItemsAsync(string label)
        {
            var (labelId, labelError) = await _gmailService.GetLabelIdAsync(label);
            if (string.IsNullOrEmpty(labelId))
            {
                Console.WriteLine(labelError ?? $"Label '{label}' not found.");
                return 0;
            }

            int totalCount = 0;
            var service = _gmailService.Service;
            var request = service.Users.Messages.List("me");
            request.LabelIds = new[] { labelId }.ToList();
            request.MaxResults = 100;

            while (request != null)
            {
                try
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
                catch (Google.GoogleApiException ex)
                {
                    Console.WriteLine($"Error retrieving emails: {ex.Message}");
                    Console.WriteLine($"HTTP Status Code: {ex.HttpStatusCode}");
                    if (ex.Error != null)
                    {
                        Console.WriteLine($"Error Code: {ex.Error.Code}, Message: {ex.Error.Message}");
                    }
                    break;
                }
            }

            return totalCount;
        }

        private string ExtractUnsubscribeLink(Message email)
        {
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

        private string GetEmailBody(MessagePart part)
        {
            if (part == null) return string.Empty;

            string body = string.Empty;

            if (!string.IsNullOrEmpty(part.Body?.Data))
            {
                byte[] decodedBytes = Convert.FromBase64String(part.Body.Data.Replace('-', '+').Replace('_', '/'));
                body = Encoding.UTF8.GetString(decodedBytes);
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
    }
}