namespace GmailUnsubscribeApp.Models
{
    public class Arguments
    {
        public bool ShowHelp { get; set; }
        public string Label { get; set; } = "Promotions";
        public long MaxResults { get; set; } = 100;
        public string OutputFile { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GmailUnsubscribeApp", "unsubscribe_links.html");
        public string VirusTotalApiKey { get; set; }
        public string HybridApiKey { get; set; }
        public bool ListContents { get; set; }
        public bool CountItems { get; set; }
        public string CredentialsPath { get; set; } = "credentials.json";
        public bool HasError { get; set; }
        public string ErrorMessage { get; set; }
        public bool NoLimit { get; set; }
        public double Threshold { get; set; }
        public bool ShowUsage { get; set; }
        public bool ForceYes { get; set; }
        public string SettingsFile { get; set; }
    }
}