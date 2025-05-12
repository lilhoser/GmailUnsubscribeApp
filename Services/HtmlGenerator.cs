namespace GmailUnsubscribeApp.Services
{
    public class HtmlGenerator
    {
        public void GenerateHtmlFile(List<(string Link, double Score)> links, string outputFile, bool includeScores = false)
        {
            string outputDir = Path.GetDirectoryName(outputFile);
            if (!Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }
            using (var writer = new StreamWriter(outputFile))
            {
                writer.WriteLine("<!DOCTYPE html>");
                writer.WriteLine("<html lang=\"en\">");
                writer.WriteLine("<head>");
                writer.WriteLine("<meta charset=\"UTF-8\">");
                writer.WriteLine("<title>Unsubscribe Links</title>");
                writer.WriteLine("<style>");
                writer.WriteLine("body { font-family: Arial, sans-serif; margin: 20px; }");
                writer.WriteLine("table { border-collapse: collapse; width: 100%; }");
                writer.WriteLine("th, td { border: 1px solid #ddd; padding: 8px; text-align: left; }");
                writer.WriteLine("th { background-color: #f2f2f2; }");
                writer.WriteLine("tr:nth-child(even) { background-color: #f9f9f9; }");
                writer.WriteLine("a { color: #007BFF; text-decoration: none; }");
                writer.WriteLine("a:hover { text-decoration: underline; }");
                if (includeScores)
                {
                    writer.WriteLine(".score-low { color: green; }");
                    writer.WriteLine(".score-medium { color: orange; }");
                    writer.WriteLine(".score-high { color: red; }");
                }
                writer.WriteLine("</style>");
                writer.WriteLine("</head>");
                writer.WriteLine("<body>");
                writer.WriteLine("<h1>Unsubscribe Links</h1>");
                writer.WriteLine("<table>");
                if (includeScores)
                {
                    writer.WriteLine("<tr><th>Maliciousness Score</th><th>Unsubscribe Link</th></tr>");
                    foreach (var (link, score) in links)
                    {
                        string scoreClass = score < 5.0 ? "score-low" : score < 20.0 ? "score-medium" : "score-high";
                        writer.WriteLine($"<tr><td class=\"{scoreClass}\">{score:F2}%</td><td><a href=\"{link}\" target=\"_blank\">{link}</a></td></tr>");
                    }
                }
                else
                {
                    writer.WriteLine("<tr><th>Unsubscribe Link</th></tr>");
                    foreach (var (link, _) in links)
                    {
                        writer.WriteLine($"<tr><td><a href=\"{link}\" target=\"_blank\">{link}</a></td></tr>");
                    }
                }
                writer.WriteLine("</table>");
                writer.WriteLine("</body>");
                writer.WriteLine("</html>");
            }
        }
    }
}