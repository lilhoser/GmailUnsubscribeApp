using GmailUnsubscribeApp.Helpers;

namespace GmailUnsubscribeApp.Services
{
    public class QuotaDisplay
    {
        public async Task DisplayServiceAndQuotasAsync(string virusTotalApiKey, string hybridApiKey, bool noLimit)
        {
            if (!string.IsNullOrEmpty(virusTotalApiKey))
            {
                Console.WriteLine("Service: VirusTotal");
                string vtRequestFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GmailUnsubscribeApp", "vt_requests.txt");
                int dailyLimit = 500;
                int monthlyLimit = 15500;
                var (requestsToday, requestsThisMonth) = Utility.ReadRequests(vtRequestFile);
                Console.WriteLine($"Detected Quotas:");
                Console.WriteLine($"  Daily Limit: {dailyLimit} requests");
                Console.WriteLine($"  Monthly Limit: {monthlyLimit} requests");
                Console.WriteLine($"Consumed:");
                Console.WriteLine($"  Daily: {requestsToday} requests");
                Console.WriteLine($"  Monthly: {requestsThisMonth} requests");
            }
            else if (!string.IsNullOrEmpty(hybridApiKey))
            {
                Console.WriteLine("Service: Hybrid Analysis");
                string haRequestFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GmailUnsubscribeApp", "ha_requests.txt");
                var (minuteLimit, hourLimit) = (200, 2000);
                var (minuteUsed, hourUsed) = Utility.ReadRequests(haRequestFile);
                Console.WriteLine($"Detected Quotas:");
                Console.WriteLine($"  Minute Limit: {minuteLimit} requests");
                Console.WriteLine($"  Hour Limit: {hourLimit} requests");
                Console.WriteLine($"Consumed:");
                Console.WriteLine($"  Minute: {minuteUsed} requests");
                Console.WriteLine($"  Hour: {hourUsed} requests");
            }
            else
            {
                Console.WriteLine("Service: None (no API key provided)");
            }
        }
    }
}