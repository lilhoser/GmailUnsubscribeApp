namespace GmailUnsubscribeApp.Models
{
    public class LinkExtractionResult
    {
        public List<string> Links { get; set; }
        public int EmailsScanned { get; set; }
        public Dictionary<string, string> LinkToEmailId { get; set; }
    }
}