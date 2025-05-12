using Newtonsoft.Json;
using GmailUnsubscribeApp.Models;

namespace GmailUnsubscribeApp.Services
{
    public class ArgumentParser
    {
        public Arguments Parse(string[] args)
        {
            var config = new Arguments();

            // First pass: Check for --settings to load JSON file
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].ToLower() == "--settings" && ++i < args.Length)
                {
                    config.SettingsFile = args[i];
                    try
                    {
                        if (File.Exists(config.SettingsFile))
                        {
                            string json = File.ReadAllText(config.SettingsFile);
                            config = JsonConvert.DeserializeObject<Arguments>(json) ?? new Arguments();
                        }
                        else
                        {
                            return SetError(config, $"Error: Settings file '{config.SettingsFile}' not found.");
                        }
                    }
                    catch (Exception ex)
                    {
                        return SetError(config, $"Error: Failed to parse settings file '{config.SettingsFile}': {ex.Message}");
                    }
                    break;
                }
            }

            // Second pass: Override with CLI arguments
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i].ToLower())
                {
                    case "-h":
                    case "--help":
                        config.ShowHelp = true;
                        break;
                    case "-l":
                    case "--label":
                        if (++i < args.Length)
                            config.Label = args[i];
                        else
                            return SetError(config, $"Error: Missing value for {args[i - 1]}.");
                        break;
                    case "-m":
                    case "--max-results":
                        if (++i < args.Length && long.TryParse(args[i], out long max))
                            config.MaxResults = max;
                        else
                            return SetError(config, $"Error: Invalid or missing value for {args[i - 1]}.");
                        break;
                    case "-o":
                    case "--output":
                        if (++i < args.Length)
                            config.OutputFile = args[i];
                        else
                            return SetError(config, $"Error: Missing value for {args[i - 1]}.");
                        break;
                    case "-v":
                    case "--virustotal-key":
                        if (++i < args.Length)
                            config.VirusTotalApiKey = args[i];
                        else
                            return SetError(config, $"Error: Missing value for {args[i - 1]}.");
                        break;
                    case "-a":
                    case "--hybrid-key":
                        if (++i < args.Length)
                            config.HybridApiKey = args[i];
                        else
                            return SetError(config, $"Error: Missing value for {args[i - 1]}.");
                        break;
                    case "-t":
                    case "--threshold":
                        if (++i < args.Length && double.TryParse(args[i], out double thresh) && thresh >= 0)
                            config.Threshold = thresh;
                        else
                            return SetError(config, $"Error: Invalid or missing value for {args[i - 1]}. Must be a non-negative number.");
                        break;
                    case "--list":
                        config.ListContents = true;
                        break;
                    case "--count":
                        config.CountItems = true;
                        break;
                    case "-c":
                    case "--credentials":
                        if (++i < args.Length)
                            config.CredentialsPath = args[i];
                        else
                            return SetError(config, $"Error: Missing value for {args[i - 1]}.");
                        break;
                    case "--no-limit":
                        config.NoLimit = true;
                        break;
                    case "--showusage":
                        config.ShowUsage = true;
                        break;
                    case "-y":
                    case "--y":
                        config.ForceYes = true;
                        break;
                    case "--settings":
                        if (++i < args.Length)
                            config.SettingsFile = args[i]; // Already handled in first pass
                        else
                            return SetError(config, $"Error: Missing value for {args[i - 1]}.");
                        break;
                    default:
                        return SetError(config, $"Error: Unrecognized argument '{args[i]}'.");
                }
            }

            if (args.Length == 0)
            {
                config.HasError = true;
                config.ErrorMessage = "Error: No arguments provided.";
            }

            return config;
        }

        private Arguments SetError(Arguments config, string message)
        {
            config.HasError = true;
            config.ErrorMessage = message;
            return config;
        }

        public void ShowHelp()
        {
            Console.WriteLine("Gmail Unsubscribe App");
            Console.WriteLine("Scans emails in a Gmail label for unsubscribe links and generates an HTML file with links and VirusTotal or Hybrid Analysis scores.");
            Console.WriteLine("Can also list email contents, count emails, or show API usage.");
            Console.WriteLine();
            Console.WriteLine("Usage: GmailUnsubscribeApp [options]");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  -h, --help              Show this help message");
            Console.WriteLine("  -l, --label <name>      Gmail label to scan, list, or count (default: Promotions)");
            Console.WriteLine("  -m, --max-results <n>   Maximum number of emails to scan (default: 100)");
            Console.WriteLine("  -o, --output <file>     Output HTML file path (default: AppData\\GmailUnsubscribeApp\\unsubscribe_links.html)");
            Console.WriteLine("  -v, --virustotal-key <key> VirusTotal API key (required for VT scanning)");
            Console.WriteLine("  -a, --hybrid-key <key>  Hybrid Analysis API key (required for HA scanning)");
            Console.WriteLine("  -t, --threshold <value> Maliciousness score threshold for visiting links (default: 0)");
            Console.WriteLine("  -c, --credentials <file> Path to Google API credentials file (default: credentials.json)");
            Console.WriteLine("  --list                  List email subjects and IDs in the specified label");
            Console.WriteLine("  --count                 Count the number of emails in the specified label");
            Console.WriteLine("  --no-limit              Disable rate limits for selected API (VT: 4/min, 500/day, 15,500/month; HA: 200/min, 2000/hour)");
            Console.WriteLine("  --showusage             Show detected API usage and exit");
            Console.WriteLine("  -y, --y                 Force all confirmation prompts to proceed without user input");
            Console.WriteLine("  --settings <file>       Path to JSON settings file containing command-line options");
            Console.WriteLine();
            Console.WriteLine("Example:");
            Console.WriteLine("  GmailUnsubscribeApp -l INBOX -m 50 -o links.html -v your_virustotal_api_key -t 5 -c ./config/credentials.json -y");
            Console.WriteLine("  GmailUnsubscribeApp -l Promotions -m 50 -o links.html -a your_hybrid_api_key -t 10 -c ./credentials.json");
            Console.WriteLine("  GmailUnsubscribeApp -l Promotions --list -c ./credentials.json");
            Console.WriteLine("  GmailUnsubscribeApp -l INBOX --count -c ./credentials.json");
            Console.WriteLine("  GmailUnsubscribeApp --showusage -v your_virustotal_api_key");
            Console.WriteLine("  GmailUnsubscribeApp --settings settings.json");
        }
    }
}