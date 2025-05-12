# Gmail Unsubscribe App

## About
The Gmail Unsubscribe App is a .NET console application that automates the process of identifying and unsubscribing from promotional emails in a Gmail account. It scans emails in a specified label, extracts unsubscribe links, scans them for maliciousness using VirusTotal or Hybrid Analysis, visits safe links, and optionally deletes processed emails.

**Note: This program and this readme were both primarily written by Grok AI.**

## Key Features
- Scans Gmail labels for unsubscribe links in emails.
- Evaluates link safety using VirusTotal or Hybrid Analysis APIs.
- Generates an HTML report with scored unsubscribe links.
- Visits safe unsubscribe links (below a user-defined maliciousness threshold).
- Deletes processed emails with user confirmation or automatically with `--y`.
- Supports configuration via command-line arguments or a JSON settings file.
- Logs issues and link visit details in a timestamped folder.

## Why not just use Gmail's unsubscribe feature?

You should.

This tool complements Gmail's built-in unsubscribe feature by addressing a key limitation: Gmail does not allow auto-unsubscribing from emails already sent to the spam folder, regardless of their maliciousness. While Gmail�s unsubscribe button simplifies opting out of inbox emails, it leaves spam folder emails unaddressed, potentially leaving users exposed to risky links. The Gmail Unsubscribe App scans unsubscribe links in any folder (including spam) using VirusTotal or Hybrid Analysis, only visiting those below a user-defined maliciousness threshold, and caches visited URLs to avoid duplicates. This provides a safer, more comprehensive way to manage unwanted emails with detailed logging, surpassing Gmail�s native capabilities.

This is particularly true if you're a victim of a mailbomb attack, where you've been signed up to potentially thousands of legitimate newsletters, blogs, deals sites, etc. Gmail's spam filter will redirect most of these to your spam folder, but you remain subscribed and exposed to thousands of websites around the world for no reason.

**Note: Visiting unsubscribe links from spam or malicious websites is dangerous and should be avoided.**

## Prerequisites
- **.NET SDK**: Version 6.0 or later.
- **Google Cloud Project**:
    - You need to use Google Cloud Setup to give the application access to your inbox. Use the [Google Cloud Console](https://console.cloud.google.com/welcome/new?pli=1&inv=1&invt=Abwwtw) to create anew  Google Cloud project:
       1. Create a new project.
       1. Enable the Gmail API:
          * Navigate to "APIs & Services" > "Library," search for "Gmail API," and enable it.
       1. Create OAuth 2.0 credentials:
          * Go to "Credentials" > "Create Credentials" > "OAuth Client ID."
          * Data type is User Data.
          * Enter values for the OAuth consent form.
          * Skip "Scopes"
          * In "OAuth Client ID", Select "Desktop app" as the application type.
          * Download the JSON credentials file (named `credentials.json`).
       1. Add a test user in "Audience" section:
          * In the "OAuth consent screen," add your Gmail account as a test user.
      1. Add `gmail.modify` scope in "Data Access" section:
          * In the "OAuth consent screen," click "Add or Remove scopes"
          * Navigate to API="Gmail API", Scope="../auth/gmail.modify" and tick the box
          * Click "Update"
          * Back on the "Data Access" page, Click "Save"

- **API Keys**: [VirusTotal](https://www.virustotal.com) or [Hybrid Analysis](https://www.hybrid-analysis.com/) (recommended) API key for link scanning.
- **Dependencies**: Install via `dotnet restore` (includes `Google.Apis.Gmail.v1`, `Newtonsoft.Json`, `HtmlAgilityPack`).

## Getting Started
1. Clone the repository or download the source code.
2. Place `credentials.json` in the project root.
3. Restore dependencies: `dotnet restore`.
4. Run the app: `dotnet run -- <options>`.

## Command-Line Options
| Option | Description | Default |
|--------|-------------|---------|
| `-h`, `--help` | Show help message | - |
| `-l`, `--label <name>` | Gmail label to scan | Promotions |
| `-m`, `--max-results <n>` | Maximum emails to scan | 100 |
| `-o`, `--output <file>` | Output HTML file path | `%APPDATA%\GmailUnsubscribeApp\logs\<timestamp>\unsubscribe_links_<timestamp>.html` |
| `-v`, `--virustotal-key <key>` | VirusTotal API key | - |
| `-a`, `--hybrid-key <key>` | Hybrid Analysis API key | - |
| `-t`, `--threshold <value>` | Maliciousness score threshold for visiting links | 0.0 |
| `-c`, `--credentials <file>` | Path to Google API credentials | `credentials.json` |
| `--list` | List email subjects and IDs in the label | - |
| `--count` | Count emails in the label | - |
| `--no-limit` | Disable API rate limits (VT: 4/min, 500/day, 15,500/month; HA: 200/min, 2000/hour) | - |
| `--showusage` | Show API usage and exit | - |
| `-y`, `--y` | Force all confirmation prompts to proceed | - |
| `--settings <file>` | Path to JSON settings file | - |

## First Run
1. Ensure `credentials.json` is in the project root.
2. Run with minimal arguments: `dotnet run -v <your_virustotal_api_key>`.
3. Authenticate via the browser OAuth prompt, approving the `gmail.modify` scope.
4. The app scans the "Promotions" label, generates an HTML file with scored links, and prompts for visiting safe links and deleting emails.
5. Check the `logs/<timestamp>` folder for output files (`unsubscribe_links_<timestamp>.html`, `visit_<timestamp>_<domain>.log`, `unsubscribe_issues_<timestamp>.json`).

## How It Works
1. **Authentication**: Uses OAuth 2.0 to access Gmail with the `gmail.modify` scope.
2. **Email Scanning**: Fetches up to `max-results` emails from the specified label, extracting unsubscribe links.
3. **Link Scanning**: Evaluates links using VirusTotal or Hybrid Analysis, assigning maliciousness scores.
4. **Link Visiting**: Visits links with scores below the threshold, retrying with POST if needed, and following confirmation links.
5. **Email Deletion**: Deletes emails associated with successfully visited links, with user confirmation or automatically if `--y` is used.
6. **Output**: Generates an HTML report and logs in `logs/<timestamp>`.

## Output Files
- **HTML Report**: `logs/<timestamp>/unsubscribe_links_<timestamp>.html` lists scored unsubscribe links.
- **Visit Logs**: `logs/<timestamp>/visit_<timestamp>_<domain>.log` details each link visit (GET/POST, headers, body).
- **Issues Log**: `logs/<timestamp>/unsubscribe_issues_<timestamp>.json` records errors or unconfirmed unsubscriptions, created even if empty.

## Limitations
- **OAuth Verification**: The `gmail.modify` scope is restricted, requiring the project to be in "Testing" mode or verified for production use.
- **Rate Limits**: VirusTotal (4/min, 500/day, 15,500/month) and Hybrid Analysis (200/min, 2000/hour) impose scanning limits unless `--no-limit` is used.
- **Email Deletion**: Soft deletion (trashing) requires `gmail.modify`; permanent deletion needs the broader `mail.google.com` scope.
- **Link Detection**: May miss unsubscribe links not in `List-Unsubscribe` headers or standard URL patterns.
- **Console Compatibility**: Some terminals (e.g., `cmd.exe`) may not handle ANSI codes well, though mitigated in output.

## Sample Scenarios

### Scenario 1: Basic Run with VirusTotal
**Command**: `dotnet run -v <your_virustotal_api_key> -l INBOX -m 10`
**Output**:
```
Scanning up to 10 emails in label 'INBOX' (total messages: 50).
Scanning email: Newsletter #123
Scanning email: Promo Offer
Found 2 unsubscribe links. Scanning 2 links with VirusTotal.
Estimated time: 0.5 minutes (30 seconds).
Proceed with VirusTotal scanning? (y/n): y
HTML file generated with 2 scored unsubscribe links: C:\Users\<User>\AppData\Roaming\GmailUnsubscribeApp\logs\20250512_100404\unsubscribe_links_20250512_100404.html
Found 1 unvisited unsubscribe link with maliciousness score below or equal to 0%.
Proceed with visiting these links? (y/n): y
Visiting https://example.com/unsubscribe/...: 200
   Unsubscribe successful via initial link.
Remove 1 successfully unsubscribed email from 'INBOX'? (y/n): y
1 email removed.
Issues log initialized at: C:\Users\<User>\AppData\Roaming\GmailUnsubscribeApp\logs\20250512_100404\unsubscribe_issues_20250512_100404.json

=== Run Summary ===
+----------------+---------------+-------------------+-------------------+---------------------+--------------------+--------+
| Emails Scanned | Unsub Links   | Links Already Vis | Links Below Thres | Unsub First Page    | Unsub Conf Page    | Failed |
+----------------+---------------+-------------------+-------------------+---------------------+--------------------+--------+
| 10             | 2             | 1                 | 1                 | 1                   | 0                  | 0      |
+----------------+---------------+-------------------+-------------------+---------------------+--------------------+--------+
- HTML File: C:\Users\<User>\AppData\Roaming\GmailUnsubscribeApp\logs\20250512_100404\unsubscribe_links_20250512_100404.html
- Scores: Low (<5%): 2, Medium (5-20%): 0, High (>20%): 0, Failed: 0
```

### Scenario 2: Using Settings File
**Settings File** (`settings.json`):
```json
{
  "label": "Promotions",
  "maxResults": 5,
  "virusTotalApiKey": "<your_virustotal_api_key>",
  "forceYes": true
}
```
**Command**: `dotnet run --settings settings.json`
**Output**:
```
Scanning up to 5 emails in label 'Promotions' (total messages: 20).
Scanning email: Sale Alert
Found 1 unsubscribe link. Scanning 1 link with VirusTotal.
Estimated time: 0.0 minutes (1 seconds).
HTML file generated with 1 scored unsubscribe link: C:\Users\<User>\AppData\Roaming\GmailUnsubscribeApp\logs\20250512_100404\unsubscribe_links_20250512_100404.html
Found 1 unvisited unsubscribe link with maliciousness score below or equal to 0%.
Visiting https://example.com/unsubscribe/...: 200
   Unsubscribe successful via initial link.
1 email removed.
Issues log initialized at: C:\Users\<User>\AppData\Roaming\GmailUnsubscribeApp\logs\20250512_100404\unsubscribe_issues_20250512_100404.json

=== Run Summary ===
+----------------+---------------+-------------------+-------------------+---------------------+--------------------+--------+
| Emails Scanned | Unsub Links   | Links Already Vis | Links Below Thres | Unsub First Page    | Unsub Conf Page    | Failed |
+----------------+---------------+-------------------+-------------------+---------------------+--------------------+--------+
| 5              | 1             | 0                 | 1                 | 1                   | 0                  | 0      |
+----------------+---------------+-------------------+-------------------+---------------------+--------------------+--------+
- HTML File: C:\Users\<User>\AppData\Roaming\GmailUnsubscribeApp\logs\20250512_100404\unsubscribe_links_20250512_100404.html
- Scores: Low (<5%): 1, Medium (5-20%): 0, High (>20%): 0, Failed: 0
```

### Scenario 3: No Unsubscribe Links Found
**Command**: `dotnet run -v <your_virustotal_api_key> -l SPAM -m 5`
**Output**:
```
Scanning up to 5 emails in label 'SPAM' (total messages: 10).
Scanning email: Suspicious Offer
No unsubscribe links found in 5 emails scanned in label 'SPAM'.
Issues log initialized at: C:\Users\<User>\AppData\Roaming\GmailUnsubscribeApp\logs\20250512_100404\unsubscribe_issues_20250512_100404.json

=== Run Summary ===
+----------------+---------------+-------------------+-------------------+---------------------+--------------------+--------+
| Emails Scanned | Unsub Links   | Links Already Vis | Links Below Thres | Unsub First Page    | Unsub Conf Page    | Failed |
+----------------+---------------+-------------------+-------------------+---------------------+--------------------+--------+
| 5              | 0             | 0                 | 0                 | 0                   | 0                  | 0      |
+----------------+---------------+-------------------+-------------------+---------------------+--------------------+--------+
```

### Scenario 4: OAuth Scope Error
**Command**: `dotnet run -v <your_virustotal_api_key> -y`
**Output** (if scope is incorrect):
```
Scanning up to 100 emails in label 'Promotions' (total messages: 50).
Scanning email: Newsletter
Found 2 unsubscribe links. Scanning 2 links with VirusTotal.
Estimated time: 0.5 minutes (30 seconds).
HTML file generated with 2 scored unsubscribe links: C:\Users\<User>\AppData\Roaming\GmailUnsubscribeApp\logs\20250512_100404\unsubscribe_links_20250512_100404.html
Found 1 unvisited unsubscribe link with maliciousness score below or equal to 0%.
Visiting https://example.com/unsubscribe/...: 200
   Unsubscribe successful via initial link.
Error deleting emails: The service gmail has thrown an exception. HttpStatusCode is Forbidden. Request had insufficient authentication scopes
Issues log saved to: C:\Users\<User>\AppData\Roaming\GmailUnsubscribeApp\logs\20250512_100404\unsubscribe_issues_20250512_100404.json

=== Run Summary ===
+----------------+---------------+-------------------+-------------------+---------------------+--------------------+--------+
| Emails Scanned | Unsub Links   | Links Already Vis | Links Below Thres | Unsub First Page    | Unsub Conf Page    | Failed |
+----------------+---------------+-------------------+-------------------+---------------------+--------------------+--------+
| 100            | 2             | 1                 | 1                 | 1                   | 0                  | 0      |
+----------------+---------------+-------------------+-------------------+---------------------+--------------------+--------+
- HTML File: C:\Users\<User>\AppData\Roaming\GmailUnsubscribeApp\logs\20250512_100404\unsubscribe_links_20250512_100404.html
- Scores: Low (<5%): 2, Medium (5-20%): 0, High (>20%): 0, Failed: 0
```
**Issues Log** (`unsubscribe_issues_20250512_100404.json`):
```json
[
  {
    "error": "Failed to delete emails: The service gmail has thrown an exception. HttpStatusCode is Forbidden. Request had insufficient authentication scopes",
    "emailIds": ["<email_id>"],
    "timestamp": "2025-05-12 10:04:05"
  }
]
```