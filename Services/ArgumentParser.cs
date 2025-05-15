using Newtonsoft.Json;
using GmailUnsubscribeApp.Models;

namespace GmailUnsubscribeApp.Services
{
    public class ArgumentParser
    {
        public Arguments Parse(string[] args)
        {
            var config = new Arguments();
            bool settingsFileProcessed = false;

            // First pass: Check for --settingsfile to load JSON file
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].ToLower() == "--settingsfile" && ++i < args.Length)
                {
                    config.SettingsFile = args[i];
                    try
                    {
                        if (File.Exists(config.SettingsFile))
                        {
                            string json = File.ReadAllText(config.SettingsFile);
                            config = JsonConvert.DeserializeObject<Arguments>(json, new JsonSerializerSettings { MissingMemberHandling = MissingMemberHandling.Ignore }) ?? new Arguments();
                            settingsFileProcessed = true;
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

            // Second pass: Parse CLI arguments only if no settings file was processed
            if (!settingsFileProcessed)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    switch (args[i].ToLower())
                    {
                        case "-h":
                        case "--showhelp":
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
                        case "--maxresults":
                            if (++i < args.Length && long.TryParse(args[i], out long max))
                                config.MaxResults = max;
                            else
                                return SetError(config, $"Error: Invalid or missing value for {args[i - 1]}.");
                            break;
                        case "-o":
                        case "--outputfile":
                            if (++i < args.Length)
                                config.OutputFile = args[i];
                            else
                                return SetError(config, $"Error: Missing value for {args[i - 1]}.");
                            break;
                        case "-v":
                        case "--virustotalapikey":
                            if (++i < args.Length)
                                config.VirusTotalApiKey = args[i];
                            else
                                return SetError(config, $"Error: Missing value for {args[i - 1]}.");
                            break;
                        case "-a":
                        case "--hybridapikey":
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
                        case "--listcontents":
                            config.ListContents = true;
                            break;
                        case "--countitems":
                            config.CountItems = true;
                            break;
                        case "-c":
                        case "--credentialspath":
                            if (++i < args.Length)
                                config.CredentialsPath = args[i];
                            else
                                return SetError(config, $"Error: Missing value for {args[i - 1]}.");
                            break;
                        case "--nolimit":
                            config.NoLimit = true;
                            break;
                        case "--showusage":
                            config.ShowUsage = true;
                            break;
                        case "-y":
                        case "--forceyes":
                            config.ForceYes = true;
                            break;
                        case "--settingsfile":
                            if (++i < args.Length)
                                config.SettingsFile = args[i];
                            else
                                return SetError(config, $"Error: Missing value for {args[i - 1]}.");
                            break;
                        case "--dryrunfile":
                            if (++i < args.Length)
                                config.DryRunFile = args[i];
                            else
                                return SetError(config, $"Error: Missing value for {args[i - 1]}.");
                            break;
                        case "--enable-mailto":
                            config.EnableMailto = true;
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
            Console.WriteLine("Can also list email contents, count emails, show API usage, or perform a dry run with a previously generated HTML file.");
            Console.WriteLine();
            Console.WriteLine("Usage: GmailUnsubscribeApp [options]");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  -h, --showHelp          Show this help message");
            Console.WriteLine("  -l, --label <name>      Gmail label to scan, list, or count (default: Promotions)");
            Console.WriteLine("  -m, --maxResults <n>    Maximum number of emails to scan (default: 100)");
            Console.WriteLine("  -o, --outputFile <file> Output HTML file path (default: AppData\\GmailUnsubscribeApp\\unsubscribe_links.html)");
            Console.WriteLine("  -v, --virusTotalApiKey <key> VirusTotal API key (required for VT scanning)");
            Console.WriteLine("  -a, --hybridApiKey <key> Hybrid Analysis API key (required for HA scanning)");
            Console.WriteLine("  -t, --threshold <value> Maliciousness score threshold for visiting links (default: 0)");
            Console.WriteLine("  -c, --credentialsPath <file> Path to Google API credentials file (default: credentials.json)");
            Console.WriteLine("  --listContents          List email subjects and IDs in the specified label");
            Console.WriteLine("  --countItems            Count the number of emails in the specified label");
            Console.WriteLine("  --noLimit               Disable rate limits for selected API (VT: 4/min, 500/day, 15,500/month; HA: 200/min, 2000/hour)");
            Console.WriteLine("  --showUsage             Show detected API usage and exit");
            Console.WriteLine("  -y, --forceYes          Force all confirmation prompts to proceed without user input");
            Console.WriteLine("  --settingsFile <file>   Path to JSON settings file containing command-line options");
            Console.WriteLine("  --dryRunFile <file>     Perform a dry run using links from a previously generated HTML file");
            Console.WriteLine("  --enable-mailto         Enable processing of mailto links to send unsubscribe emails");
            Console.WriteLine();
            Console.WriteLine("Example:");
            Console.WriteLine("  GmailUnsubscribeApp --label INBOX --maxResults 50 --outputFile links.html --virusTotalApiKey your_virustotal_api_key --threshold 5 --credentialsPath ./config/credentials.json --forceYes");
            Console.WriteLine("  GmailUnsubscribeApp --label Promotions --maxResults 50 --outputFile links.html --hybridApiKey your_hybrid_api_key --threshold 10 --credentialsPath ./credentials.json");
            Console.WriteLine("  GmailUnsubscribeApp --label Promotions --listContents --credentialsPath ./credentials.json");
            Console.WriteLine("  GmailUnsubscribeApp --label INBOX --countItems --credentialsPath ./credentials.json");
            Console.WriteLine("  GmailUnsubscribeApp --showUsage --virusTotalApiKey your_virustotal_api_key");
            Console.WriteLine("  GmailUnsubscribeApp --settingsFile settings.json");
            Console.WriteLine("  GmailUnsubscribeApp --dryRunFile unsubscribe_links_20250514_101111.html --threshold 5 --credentialsPath ./credentials.json --forceYes --enable-mailto");
        }
    }
}