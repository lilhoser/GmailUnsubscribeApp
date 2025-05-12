namespace GmailUnsubscribeApp.Models
{
    public class QuotaCheckResult
    {
        public bool IsLimitReached { get; set; }
        public int RequestsMade { get; set; }
        public DateTime MinuteStart { get; set; }
    }
}