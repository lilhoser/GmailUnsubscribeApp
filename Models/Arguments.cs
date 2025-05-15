namespace GmailUnsubscribeApp.Models
{
    public class Arguments
    {
        public bool ShowHelp { get; set; }
        public string Label { get; set; }
        public long MaxResults { get; set; } = 100;
        public string OutputFile { get; set; }
        public string VirusTotalApiKey { get; set; }
        public string HybridApiKey { get; set; }
        public double Threshold { get; set; } = 0;
        public string CredentialsPath { get; set; }
        public bool ListContents { get; set; }
        public bool CountItems { get; set; }
        public bool NoLimit { get; set; }
        public bool ShowUsage { get; set; }
        public bool ForceYes { get; set; }
        public string SettingsFile { get; set; }
        public string DryRunFile { get; set; }
        public bool EnableMailto { get; set; }
        public bool HasError { get; set; }
        public string ErrorMessage { get; set; }
    }
}