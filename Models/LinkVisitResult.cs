namespace GmailUnsubscribeApp.Models
{
    public class LinkVisitResult
    {
        public int VisitedCount { get; set; }
        public int InitialSuccessCount { get; set; }
        public int ConfirmationSuccessCount { get; set; }
        public int FailedCount { get; set; }
        public List<string> SuccessfulEmailIds { get; set; } = new List<string>();
        public int AlreadyVisitedCount { get; set; }
    }
}