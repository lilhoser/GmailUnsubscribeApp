using Google.Apis.Auth.OAuth2;
using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using GmailUnsubscribeApp.Helpers;
using System.Text;

namespace GmailUnsubscribeApp.Services
{
    public class GmailServiceWrapper
    {
        private GmailService _service;
        private readonly bool _isDryRun;

        public GmailService Service => _service ?? throw new InvalidOperationException("GmailService is not initialized. Call AuthenticateAsync first.");
        public bool IsDryRun => _isDryRun;

        public GmailServiceWrapper(bool isDryRun)
        {
            _isDryRun = isDryRun;
        }

        public async Task AuthenticateAsync(string credentialsPath)
        {
            UserCredential credential;
            using (var stream = new FileStream(credentialsPath, FileMode.Open, FileAccess.Read))
            {
                string tokenFolder = Path.GetDirectoryName(Constants.TokenPath);
                if (!Directory.Exists(tokenFolder))
                {
                    Directory.CreateDirectory(tokenFolder);
                }
                credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                    GoogleClientSecrets.FromStream(stream).Secrets,
                    Constants.Scopes,
                    "user",
                    CancellationToken.None,
                    new FileDataStore(tokenFolder, true));
            }

            _service = new GmailService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = Constants.ApplicationName
            });
        }

        public async Task<(string LabelId, string Error)> GetLabelIdAsync(string labelName)
        {
            try
            {
                var labels = await Service.Users.Labels.List("me").ExecuteAsync();
                var label = labels.Labels.FirstOrDefault(l => l.Name.Equals(labelName, StringComparison.OrdinalIgnoreCase));
                return (label?.Id, null);
            }
            catch (Exception ex)
            {
                return (null, $"Failed to get label ID for '{labelName}': {ex.Message}");
            }
        }

        public async Task<List<(string EmailId, string Error)>> DeleteEmailsAsync(List<string> emailIds)
        {
            if (_isDryRun)
            {
                return emailIds.Select(id => (id, "Dry run mode: email deletion skipped")).ToList();
            }

            var results = new List<(string EmailId, string Error)>();
            foreach (var emailId in emailIds)
            {
                try
                {
                    await Service.Users.Messages.Delete("me", emailId).ExecuteAsync();
                    results.Add((emailId, null));
                }
                catch (Exception ex)
                {
                    results.Add((emailId, $"Failed to delete email ID {emailId}: {ex.Message}"));
                }
            }
            return results;
        }

        public async Task SendEmailAsync(string toAddress, string subject, string body)
        {
            if (_isDryRun)
            {
                throw new InvalidOperationException("Email sending not allowed in dry run mode.");
            }

            if (string.IsNullOrEmpty(toAddress) || !toAddress.Contains("@"))
            {
                throw new ArgumentException("Invalid email address.");
            }

            try
            {
                // Construct MIME message
                var mimeMessage = $"From: me\r\n" +
                                  $"To: {toAddress}\r\n" +
                                  $"Subject: {subject}\r\n" +
                                  "MIME-Version: 1.0\r\n" +
                                  "Content-Type: text/plain; charset=utf-8\r\n" +
                                  $"\r\n{body}";

                // Encode to base64url
                var encodedMessage = Convert.ToBase64String(Encoding.UTF8.GetBytes(mimeMessage))
                    .Replace('+', '-')
                    .Replace('/', '_')
                    .Replace("=", "");

                var message = new Message
                {
                    Raw = encodedMessage
                };

                await Service.Users.Messages.Send(message, "me").ExecuteAsync();
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to send email to {toAddress}: {ex.Message}", ex);
            }
        }
    }
}