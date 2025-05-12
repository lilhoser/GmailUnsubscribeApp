namespace GmailUnsubscribeApp.Models
{
    public class ConfirmationLinkResult
    {
        public string Url { get; set; }
        public bool IsPost { get; set; }
        public Dictionary<string, string> FormData { get; set; }
    }
}