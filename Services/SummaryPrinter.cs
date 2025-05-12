namespace GmailUnsubscribeApp.Services
{
    public class SummaryPrinter
    {
        public void PrintSummary((List<(string Link, double Score)> Links, int EmailsScanned, string OutputFile, int VisitedLinks, int InitialSuccessCount, int ConfirmationSuccessCount, int FailedCount, int AlreadyVisitedCount) result)
        {
            string appDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GmailUnsubscribeApp");
            string relativeOutputFile = Path.GetRelativePath(appDataDir, result.OutputFile);

            Console.WriteLine("\n=== Run Summary ===");
            Console.WriteLine("+----------------+---------------+-------------------+-------------------+---------------------+--------------------+--------+");
            Console.WriteLine("| Emails Scanned | Unsub Links   | Links Already Vis | Links Below Thres | Unsub First Page    | Unsub Conf Page    | Failed |");
            Console.WriteLine("+----------------+---------------+-------------------+-------------------+---------------------+--------------------+--------+");
            Console.WriteLine($"| {result.EmailsScanned,-14} | {result.Links.Count,-13} | {result.AlreadyVisitedCount,-17} | {result.VisitedLinks,-17} | {result.InitialSuccessCount,-19} | {result.ConfirmationSuccessCount,-18} | {result.FailedCount,-6} |");
            Console.WriteLine("+----------------+---------------+-------------------+-------------------+---------------------+--------------------+--------+");
            Console.WriteLine($"- HTML File: {Path.GetFullPath(result.OutputFile)}");
            if (result.Links.Any(l => l.Score >= 0))
            {
                int low = result.Links.Count(l => l.Score >= 0 && l.Score < 5.0);
                int med = result.Links.Count(l => l.Score >= 5.0 && l.Score < 20.0);
                int high = result.Links.Count(l => l.Score >= 20.0);
                int failed = result.Links.Count(l => l.Score < 0);
                Console.WriteLine($"- Scores: Low (<5%): {low}, Medium (5-20%): {med}, High (>20%): {high}, Failed: {failed}");
            }
        }
    }
}