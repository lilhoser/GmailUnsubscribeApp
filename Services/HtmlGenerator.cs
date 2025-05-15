using HtmlAgilityPack;

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
                writer.WriteLine("    <meta charset=\"UTF-8\">");
                writer.WriteLine("    <title>Unsubscribe Links</title>");
                writer.WriteLine("    <style>");
                writer.WriteLine("        table { border-collapse: collapse; width: 100%; }");
                writer.WriteLine("        th, td { border: 1px solid black; padding: 8px; text-align: left; }");
                writer.WriteLine("        .score-low { color: green; }");
                writer.WriteLine("        .score-medium { color: orange; }");
                writer.WriteLine("        .score-high { color: red; }");
                writer.WriteLine("    </style>");
                writer.WriteLine("</head>");
                writer.WriteLine("<body>");
                writer.WriteLine("    <h2>Unsubscribe Links</h2>");
                writer.WriteLine("    <table>");
                if (includeScores)
                {
                    writer.WriteLine("        <tr><th>Maliciousness Score</th><th>Unsubscribe Link</th></tr>");
                    foreach (var (link, score) in links)
                    {
                        string scoreClass = score < 5.0 ? "score-low" : score < 20.0 ? "score-medium" : "score-high";
                        writer.WriteLine($"        <tr><td class=\"{scoreClass}\">{score:F2}%</td><td><a href=\"{link}\">{link}</a></td></tr>");
                    }
                }
                else
                {
                    writer.WriteLine("        <tr><th>Unsubscribe Link</th></tr>");
                    foreach (var (link, _) in links)
                    {
                        writer.WriteLine($"        <tr><td><a href=\"{link}\">{link}</a></td></tr>");
                    }
                }
                writer.WriteLine("    </table>");
                writer.WriteLine("</body>");
                writer.WriteLine("</html>");
            }
        }

        public List<(string Link, double Score)> ParseHtmlFile(string inputFile)
        {
            var scoredLinks = new List<(string Link, double Score)>();
            if (!File.Exists(inputFile))
            {
                return scoredLinks;
            }

            var doc = new HtmlDocument();
            doc.Load(inputFile);

            var rows = doc.DocumentNode.SelectNodes("//table/tr");
            if (rows == null || rows.Count <= 1) // Skip header row
            {
                return scoredLinks;
            }

            foreach (var row in rows.Skip(1)) // Skip header
            {
                var cells = row.SelectNodes("td");
                if (cells != null && cells.Count == 2)
                {
                    string scoreText = cells[0].InnerText.Trim().Replace("%", "");
                    string link = cells[1].SelectSingleNode("a")?.GetAttributeValue("href", "") ?? "";

                    if (!string.IsNullOrEmpty(link) && double.TryParse(scoreText, out double score))
                    {
                        scoredLinks.Add((link, score));
                    }
                }
            }

            return scoredLinks;
        }
    }
}