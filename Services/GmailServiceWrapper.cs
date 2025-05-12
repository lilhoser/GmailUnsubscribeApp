using Google.Apis.Auth.OAuth2;
using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using GmailUnsubscribeApp.Helpers;

namespace GmailUnsubscribeApp.Services
{
    public class GmailServiceWrapper
    {
        private GmailService _service;

        public GmailService Service => _service ?? throw new InvalidOperationException("GmailService is not initialized. Call AuthenticateAsync first.");

        public GmailServiceWrapper()
        {
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
    }
}