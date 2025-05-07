# Overview

The **Gmail Unsubscribe App** is a C# console application that enhances email management by interacting with the Gmail API to scan, analyze, and safely unsubscribe from emails in a specified label (e.g., Inbox, Spam, Promotions). It extracts unsubscribe links, evaluates their safety using VirusTotal or Hybrid Analysis, and optionally visits safe links to unsubscribe, caching visited URLs to avoid duplicates. The app generates HTML reports with link details and logs visit responses in timestamped subdirectories, providing a secure alternative to Gmail’s unsubscribe feature, especially for spam emails. With customizable maliciousness thresholds and detailed console summaries, it offers users control over their email cleanup process.

***Note:*** This program and this readme were both primarily written by Grok AI.

# Features
- **Email Scanning**: Scans emails in a specified Gmail label for unsubscribe links, extracted from `List-Unsubscribe` headers or email bodies using regex.
- **Safety Analysis**: Evaluates link safety using VirusTotal (4/min, 500/day, 15,500/month limits) or Hybrid Analysis (200/min, 2000/hour limits), with dynamic quota fetching for Hybrid Analysis.
- **Safe Unsubscribing**: Visits unsubscribe links with maliciousness scores below a user-defined threshold (default: 0%), filtering out invalid URLs (e.g., `mailto:`) and caching visited URLs to prevent duplicates.
- **HTML Reports**: Generates two HTML files per run: an initial file listing unsubscribe links and a scored file with maliciousness scores and links, stored in `%AppData%\GmailUnsubscribeApp` with unique timestamped filenames.
- **Verbose Logging**: Logs HTTP responses (status, headers, body) for visited links in `%AppData%\GmailUnsubscribeApp\logs\<timestamp>\`, with filenames including the host and timestamp.
- **Email Listing and Counting**: Lists email IDs and subjects (`--list`) or counts emails (`--count`) in a specified label.
- **Console Feedback**: Displays progress messages (red for links found, gray otherwise), quota usage, visited link status, and a summary including emails scanned, links found, visited, and score breakdown.
- **Command-Line Flexibility**: Supports options for label, max emails, output path, API keys, threshold, and more, with a help menu (`-h`) for guidance.


# Why not just use Gmail's unsubscribe feature?

You should.

This tool complements Gmail's built-in unsubscribe feature by addressing a key limitation: Gmail does not allow auto-unsubscribing from emails already sent to the spam folder, regardless of their maliciousness. While Gmail’s unsubscribe button simplifies opting out of inbox emails, it leaves spam folder emails unaddressed, potentially leaving users exposed to risky links. The Gmail Unsubscribe App scans unsubscribe links in any folder (including spam) using VirusTotal or Hybrid Analysis, only visiting those below a user-defined maliciousness threshold, and caches visited URLs to avoid duplicates. This provides a safer, more comprehensive way to manage unwanted emails with detailed logging, surpassing Gmail’s native capabilities.

This is particularly true if you're a victim of a mailbomb attack, where you've been signed up to potentially thousands of legitimate newsletters, blogs, deals sites, etc. Gmail's spam filter will redirect most of these to your spam folder, but you remain subscribed and exposed to thousands of websites around the world for no reason.

***Note:*** Visiting unsubscribe links from spam or malicious websites is dangerous and should be avoided.
# Prerequisites
## Google Cloud Setup
You need to use Google Cloud Setup to give the application access to your inbox. Use the [Google Cloud Console](https://console.cloud.google.com/welcome/new?pli=1&inv=1&invt=Abwwtw) to create anew  Google Cloud project:

1. Create a new project.
1. Enable the Gmail API:
   * Navigate to "APIs & Services" > "Library," search for "Gmail API," and enable it.
1. Create OAuth 2.0 credentials:
   * Go to "Credentials" > "Create Credentials" > "OAuth Client ID."
   * Data type is User Data.
   * Enter values for the OAuth consent form.
   * Skip "Scopes"
   * In "OAuth Client ID", Select "Desktop app" as the application type.
   * Download the JSON credentials file (named credentials.json).
1. Add a test user in "Audience" section:
   * In the "OAuth consent screen," add your Gmail account as a test user.
## VirusTotal API Key (optional)
_Note_: Required for scanning unsubscribe links (not needed for `--list` or `--count`).

1. Sign up for a free account at VirusTotal.
1. Obtain your API key from your account settings.
1. Pass the API key via the --virustotal-key command-line argument when scanning.

## Hybrid Analysis API Key (optional)
_Note_: Required for scanning unsubscribe links with Hybrid Analysis (not needed for `--list` or `--count`).

- Sign up for a free account at [Hybrid Analysis](https://www.hybrid-analysis.com/).
- Obtain your API key from your account settings under the "API Keys" section.
- Pass the API key via the `-a`/`--hybrid-key` command-line argument when scanning.

## Project Setup
Place the credentials.json file in the project directory.
# Usage

The app is run from the command line with configurable options. ## Command-Line Options
| Option | Description | Default |
|--------|-------------|---------|
| `-h`, `--help` | Show the help menu and exit. | N/A |
| `-l`, `--label <name>` | Gmail label to scan, list, or count (e.g., INBOX, Promotions). | Promotions |
| `-m`, `--max-results <n>` | Maximum number of emails to scan (for unsubscribe link extraction). | 100 |
| `-o`, `--output <file>` | Output HTML file path (for unsubscribe link scanning). | %AppData%\\GmailUnsubscribeApp\\unsubscribe_links_<timestamp>_<random>.html |
| `-v`, `--virustotal-key <key>` | VirusTotal API key (required for VT scanning). | N/A |
| `-a`, `--hybrid-key <key>` | Hybrid Analysis API key (required for HA scanning). | N/A |
| `-t`, `--threshold <value>` | Maliciousness score threshold for visiting links (non-negative percentage). | 0 |
| `-c`, `--credentials <file>` | Path to Google API credentials file. | credentials.json |
| `--list` | List email IDs and subjects in the specified label. | N/A |
| `--count` | Count the total number of emails in the specified label. | N/A |
| `--no-limit` | Disable rate limits for selected API (VT: 4/min, 500/day, 15,500/month; HA: 200/min, 2000/hour). | N/A |
| `--showusage` | Show detected API usage and exit. | N/A |## Example Commands
* Scan the "Promotions" label for 50 emails and output to links.html:
    ```
    GmailUnsubscribeApp -l Promotions -m 50 -o links.html -v your_virustotal_api_key
    ```
* List email contents in the "INBOX" label:
    ```
    GmailUnsubscribeApp -l INBOX --list
    ```* Count emails in the "Promotions" label:
    ```    GmailUnsubscribeApp -l Promotions --count
    ```## First Run
* The first time you run the app, it will open a browser window to authenticate with your Gmail account.
* Grant the requested permissions (read-only access to Gmail).
* OAuth tokens are saved to `token.json` for subsequent runs.

## Automatically Visiting Unsubscribe Links

***Note:*** Visiting unsubscribe links from spam or malicious websites is dangerous and should be avoided.

After scanning unsubscribe links with VirusTotal or Hybrid Analysis, the app can automatically visit links that meet a user-specified maliciousness score threshold. The logic for visiting these links is as follows:

1. **Filtering Links**:
   - The app selects links with a maliciousness score below or equal to the threshold specified via the `-t`/`--threshold` option (default: 0%).
   - Only well-formed URLs with `http` or `https` schemes are considered (e.g., `mailto:` or invalid URLs are excluded).
   - Links previously visited (tracked in `%AppData%\GmailUnsubscribeApp\visited_urls.txt`) are skipped to avoid redundant visits.

2. **User Prompt**:
   - If eligible unvisited links are found, the app displays the count (e.g., "Found 3 unvisited unsubscribe links with maliciousness score below or equal to 0%").
   - The user is prompted to proceed (`Proceed with visiting these links? (y/n):`).
   - If the user selects 'n' or no eligible links exist, visiting is skipped.

3. **Visiting Links**:
   - Each link is visited using an HTTP GET request with a `User-Agent` header set to `GmailUnsubscribeApp`.
   - The URL (abbreviated to 100 characters with `...` if longer) is printed to the console, followed by the HTTP status code (e.g., `Visiting https://example.com/unsubscribe: 200`).
   - If a visit fails (e.g., due to a network error), the error is printed (e.g., `Error: Request timed out`).

4. **Logging Responses**:
   - Each visit’s response (or error) is logged to a file in `%AppData%\GmailUnsubscribeApp\logs`, named `visit_<timestamp>_<host>.log` (e.g., `visit_20250507_123456_click_email_billboard_com.log`).
   - The log includes:
     - Timestamp
     - Full URL
     - HTTP status (e.g., `200 OK`)
     - Response headers
     - Response body (or error message for failed requests)
   - The host part is derived from the URL (e.g., `click.email.billboard.com` ? `click_email_billboard_com`), truncated to 20 characters.

5. **Caching Visited URLs**:
   - Successfully visited URLs (or those that fail with an error) are added to `visited_urls.txt` and an in-memory cache to prevent revisiting in the same session.
   - The cache uses case-insensitive matching to handle URL variations.
   - Cache read/write errors are logged as warnings without interrupting the process.

6. **Summary**:
   - The number of links visited during the run (successful or failed attempts) is included in the console summary (e.g., `Unsubscribe links visited: 3`).

This feature allows users to safely visit unsubscribe links deemed non-malicious while avoiding duplicates and maintaining detailed logs for auditing.


## Output
- **For Unsubscribe Link Scanning**:
  - **Initial HTML File**: A file (e.g., `%AppData%\GmailUnsubscribeApp\unsubscribe_links_<timestamp>_<random>.html` or user-specified) is generated with a single-column table of unsubscribe links if any are found.
  - **Scored HTML File**: If VirusTotal scanning is performed (user selects "y" at prompt), a second file (e.g., `%AppData%\GmailUnsubscribeApp\unsubscribe_links_<timestamp>_<random>_scored.html`) is generated with a two-column table: VirusTotal maliciousness scores in the first column (color-coded: green for <5%, orange for 5-20%, red for >20%) and unsubscribe links in the second.
  - **Console Output**:
    - Progress messages show each email's subject during scanning, in red if an unsubscribe link is found, gray otherwise.
    - If no links are found, a message indicates the number of emails scanned.
    - If links are found, the path to the initial HTML file is shown.
    - If VirusTotal scanning occurs, a summary includes:
      - Number of emails scanned.
      - Number of unsubscribe links found.
      - VirusTotal score breakdown (low, medium, high risk).
      - Paths to both HTML files.
- **For Listing Contents (`--list`)**:
  - Console output listing each email’s ID and subject (or “(No Subject)” if missing).
- **For Counting Items (`--count`)**:
  - Console output showing the total number of emails in the specified label.

# Tips and Caveats

## Tips
* Label Names: Use standard Gmail labels (e.g., INBOX, Promotions, SPAM) or custom labels created in Gmail. Case-insensitive.
* Adjusting Max Results: Increase `--max-results` for thorough scans, but be aware of Gmail API and VirusTotal API quotas. Not applicable for `--list` or `--count`.
* Testing: Use a small `--max-results` value (e.g., 10) during initial testing of unsubscribe link scanning to verify setup and avoid excessive API calls.
## Caveats
* Gmail API Quotas: The Gmail API has rate limits (e.g., 250 quota units/second, 1,000,000 units/day). Scanning, listing, or counting many emails may consume significant quota. Monitor usage in the Google Cloud Console.
* VirusTotal API Limits: The free VirusTotal API has strict limits (500 requests/day, 4 requests/minute). Scanning many links may exceed these limits, causing delays or errors. Consider a paid plan for heavy use.
* Hybrid-Analysis API Limits: The quote is provided dynamically, however based on manual inspection, it appears that the free version of the public API limits requests to 2,000 per day and 200 per minute.
* Unsubscribe Link Detection: The app uses a basic regex `(https?://[^\s""]+unsubscribe[^\s""]*)` to find unsubscribe links in email bodies. Some links may be missed if they use non-standard wording or complex HTML structures. Consider using an HTML parser (e.g., `HtmlAgilityPack`) for better accuracy.
* False Positives/Negatives: VirusTotal scores are based on community and scanner data, which may not always be accurate. Use scores as a guide, not a definitive judgment.
* Authentication Errors: Ensure `credentials.json` is correctly configured and your Gmail account is added as a test user in the Google Cloud Console. Clear the `token.json` file if authentication issues persist.
* File Permissions: The app requires write access to the output directory for the HTML file and token.json. Ensure permissions are set correctly.
* Network Reliability: The app depends on internet access for Gmail and VirusTotal API calls. Network issues may cause failures, which are logged to the console.
* Listing Performance: The `--list` option fetches email metadata, which may be slow for labels with many emails due to API calls for each email’s subject.

# Sample Output

After getting mailbombed, my spam folder still accumulates several hundred messages per day. Many of these are "legitimate" websites that offer automated newsletters, which the attacker signed me up for. Here's a sample run:

```
GmailUnsubscribeApp.exe -l spam -c cred.json -a ha_api_key.key

Service: Hybrid Analysis
Detected Quotas:
  Minute Limit: 200 requests
  Hour Limit: 2000 requests
Consumed:
  Minute: 0 requests
  Hour: 13 requests

Scanning email: Dwayne Johnson, Joe Ballarini Get 'Ripped' for 20th Century Studios (Exclusive)
Scanning email: How did you do? What insights did you receive? ??
Scanning email: Looking for Health Answers? We've Got You ??
Scanning email: Atençao: o CACD 2025 já é uma realidade
Scanning email: Hollywood Studio Chiefs Weigh In On Trump Movie Tariff Proposal and Realities Of U.S. Production: "If The Incentives Are Stronger...We'll Shoot Here"
Scanning email: Stay Connected with Rentyl Resorts
Scanning email: Winter of zomer? Bij Hans altijd stijlvolle brillen met zonnige kortingen!??
Scanning email: DOJ, FTC Launch Public Inquiry Into Live Music Business Following Trump Executive Order
Scanning email: Grab these best sellers with 15% off
Scanning email: 'PAW Patrol: The Dino Movie' Adds Snoop Dogg, Paris Hilton, Terry Crews, Jameela Jamil and More
Scanning email: Már csak 3 helyünk maradt a vasárnapi "Sminkeld magad!" workshopunkra!??
Scanning email: [Day 2] Five Powerful Binary Options Trading Facts
Scanning email: Netflix's Ted Sarandos Says He Loves Movies. Do You Believe Him?
Scanning email: Cannibal Corpse Ticket Presale Offer.
Scanning email: ¿Y si te garantizo resultados?
Scanning email: ??For the mother who gives everything
Scanning email: OMG, up to 67% OFF
Scanning email: Kanye West stomps out of Piers Morgan interview live over the dumbest thing
Scanning email: [Email #3 of 3] it takes a village
Scanning email: ?? Unbelievable! Do You Realize What You Just.
Scanning email: Kathy Bates, Mara Brock Akil, Natasha Lyonne, and More to Recieve Awards at IndieWire Honors
Scanning email: Aptera - Less than 500 Priority Delivery Slots Left
Scanning email: Fête de mères : Édition spéciale ??
Scanning email: Let's Plan Your Summer TV-Watching Adventures
Scanning email: 'Ferris Bueller's Matthew Broderick and Alan Ruck Reunite In 'The Best Is Yet To Come'
Scanning email: Wage Growth Tracker Was 4.3 Percent in April - May 7, 2025
Scanning email: Rescheduled: Be Bold With Color LIVE Event ??
Scanning email: Utilisez Viniou sur votre ordinateur ! (2/5)
Scanning email: NBCUniversal To Handle Sales For Comcast's Cable Spinoff Versant Under Two-Year Agreement
Scanning email: OnePlus Nord 5: o poderoso gama média da marca que vais poder comprar
Scanning email: Michelle Rodriguez and Richard Gere Lead Survival Thriller 'Left Seat' For 'Highest 2 Lowest' Producer Jason Michael Berman and Mandalay; Anton and WME Launch For Cannes Market
Scanning email: ?? LOOK BOOK: ??????? ?????? ?? ????????? ???????
Scanning email: LAST CHANCE! Strategies to Smooth Out Tariff Uncertainties
Scanning email: [Eilt] so geht Um.satz heute!
Scanning email: Breaking Baz: Jennifer Garner Joins Producing Team Turning `13 Going On 30' Into A Stage  Musical
Scanning email: Can Starz Scale? Now Split From Lionsgate, CEO Sets Sights On 80 Million Subscriber Goal
Scanning email: Você será o próximo.
Scanning email: NBCUniversal Strikes Deal to Sell Versant Ads for Two Cycles
Scanning email: AirNav Radar - Make your Cockpit feel like home, Captain!
Scanning email: ????? ?????? ?????? ???????? ???? ?? ???? "???! ??????!".
Scanning email: NEW Cancer Recovery Fitness Class
Scanning email: OPEN CUP MATCHDAY! ???? EP Locomotive FC vs Austin FC
Scanning email: Even voorstellen...
Scanning email: Outside Lands - Single Day Tickets On Sale Now
Scanning email: New Zealand PM Jacinda Ardern Docu 'Prime Minister' Acquired By Magnolia, HBO Docu Films and CNN Films After Prize-Winning Sundance Bow
Scanning email: The Next Steps For Hollywood Following Donald Trump's Tariff Bombshell: An MPA Confab, And POTUS Plans For A White House Meeting
Scanning email: Here's What Quietly Prepared People Do First-Before the Noise
Scanning email: CONGRATS GRADS!!
Scanning email: TikTok Viral Hit 'Hell N Back' Faces Lawsuit Over Sampling Dispute
Scanning email: ABC Orders 'RJ Decker' Drama Pilot From 'Elementary' Creator Based On Carl Hiaasen's Novel
Scanning email: Helllooo?
Scanning email: Zondag is het Moederdag... en opnieuw BBQ-weer! ??
Scanning email: New Pre Order!
Scanning email: Bravo Orders `The Real Housewives of Rhode Island,' New `Ladies of London' and `Shahs' Reboot
Scanning email: Paul Walter Hauser, Lili Reinhart, Tim Roth, Jake Lacy, Jai Courtney and Kerry Bishé Set For Comedic Thriller 'The Very Best People'
Scanning email: Your last and final chance
Scanning email: Confirm your VERA Files subscription
Scanning email: `The Valley' Lands Spinoff, `Ladies Of London' Returns and `Wife Swap' Heads Into `The Real Housewives' Universe As Bravo Unveils Slate
Scanning email: Grow Grow Trim: Last call
Scanning email: ?????? ???? 18+. ??? ????? ???? ? ??????????
Scanning email: Our Final Sunday Pick Up
Scanning email: We've Got The Edge On Your Savings!
Scanning email: [?????] ??????? ?? ????????????? ??? ????? 10 ?????! ???????????????!
Scanning email: Explore outdoor activities in Rosslyn!
Scanning email: {VIP} Your Personal Tour Inside Pain Free Living Lab!
Scanning email: CFO Says Disney Happy To Work With President "On Things That Would Make Sense" For Industry
Scanning email: What Happens to Hollywood When the U.S. Is No Longer the Good Guy?
Scanning email: Comment ça marche ?
Scanning email: John, Reply back: Are you getting my alerts?
Scanning email: Stáhnete si miliony verejnych souboru s FastShare
Scanning email: Ofícios mostram que Exército pediu apoio logístico ao acampamento golpista
Scanning email: 20 TV Shows to Watch This Summer
Scanning email: Discover the #1 Sign of Excessive Drinking!
Scanning email: Discover the #1 Sign of Excessive Drinking!
Scanning email: ADVNC Summer Camps & Clinics
Scanning email: David Dastmalchian, Georgina Campbell Teaming for Horror 'The Shepherd,' Anton Launching Sales in Cannes (EXCLUSIVE)
Scanning email: What is KulKote?
Scanning email: Your Maintenance Revolution: Unveiling Ranyan!
Scanning email: Life-saving skills at 10% off? Yes please!
Scanning email: Should you skip breakfast to save your brain from Alzheimer's?
Scanning email: [FOUND GOLD] Your BEST Health Support Resource Inside.
Scanning email: [A venir] Anne Neukamp, Mirror, 17 mai - 21 juin, 2025
Scanning email: TestUser, Westgate's Gift To You!
Scanning email: ?????? ???? ??????? ????????????, ? ?????? - ???? ??????????? ????? ??????!
Scanning email: Conferma Iscrizione Pampaninirossi
Scanning email: Get Started with Treasury Software
Scanning email: La Marinière, un classique qui ne prend pas l'eau
Scanning email: ? Rimani sempre aggiornato
Scanning email: Made some sales funnel training for ya
Scanning email: Leslie Grace to Play Diver Swallowed by Giant Sperm Whale in Survival Thriller 'Propel,' Altitude Launching Sales in Cannes (EXCLUSIVE)
Scanning email: Comment gagner de l'argent sur YouTube ?
Scanning email: To juz ostatnia szansa - BEACTIVE TOUR BY REEBOK WARSZAWA
Scanning email: Julianne Moore Questions If Her Daughter Sydney Sweeney Is a Murderer in 'Echo Valley' Trailer
Scanning email: Start Making More Money From Referrals
Scanning email: Confirm your Dan Vega subscription
Scanning email: Watch CRT TV Content On-Demand
Scanning email: Ari Aster to Receive the Coveted Filmmaker on the Edge Award at 2025 Provincetown International Film Festival: Get the Full Lineup
Scanning email: Mail delivery failed: returning message to sender
Scanning email: Methods Of 100 Coaches Exclusive Free Access Offer
Scanning email: Peloton Led a Fitness Music Licensing Boom. Then the Pandemic Ended
Initial HTML file generated with 95 unsubscribe links: C:\Users\-----------\AppData\Roaming\GmailUnsubscribeApp\unsubscribe_links_20250507_151513_ec63b34a.html
Found 95 unsubscribe links. Scanning 95 links with Hybrid Analysis.
Estimated time: 1.0 minutes (60 seconds).
Proceed with Hybrid Analysis scanning? (y/n): y
Found 52 unvisited unsubscribe links with maliciousness score below or equal to 0%.
Proceed with visiting these links? (y/n): y
Visiting https://click.email.hollywoodreporter.com/subscription_center.aspx?jwt=eyJhbGciOiJIUzI1NiIsInR5cC...: 200
Visiting https://facebook.us16.list-manage.com/unsubscribe?u=cb003efee1a80a21d1e49301b&id=768ef09638&t=h&e...: 200
Visiting https://mktsapientia.activehosted.com/box.php?nl=4&c=213&m=327&s=556ca7791c356db4de0a475432d76f0f...: 200
Visiting https://click.contact.rentyl.com/subscription_center.aspx?jwt=eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ...: 200
Visiting https://click.email.hansanders.be/subscription_center.aspx?jwt=eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVC...: 200
Visiting https://click.email.hollywoodreporter.com/subscription_center.aspx?jwt=eyJhbGciOiJIUzI1NiIsInR5cC...: 200
Visiting https://manage.kmail-lists.com/subscriptions/unsubscribe?a=RU2ww8&c=01JTGX8WA1NS54BFJXDF8PYZ4V&k=...: 200
Visiting https://click.email.variety.com/subscription_center.aspx?jwt=eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...: 200
Visiting https://ajwxyy.clicks.mlsend.com/tf/c/eyJ2Ijoie1wiYVwiOjcyMzc2NCxcImxcIjoxNTM3Njc4NDcwOTY0ODUwNDU...: 200
Visiting https://click.email.variety.com/subscription_center.aspx?jwt=eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...: 200
Visiting https://links.engage.ticketmaster.com/luoo/v1/aW9zZmhtZ1VVYUFjU2Q4Y0djRlNCRGg2TFdaREhBVDZtTXRDYng...: 200
Visiting https://mail.lambertakademie.de/unsubscribe/c89e80d3-3f18-4fd1-b5d8-096585dc2615/559cef6d-f26b-4e...: 200
Visiting https://click.email.hollywoodreporter.com/subscription_center.aspx?jwt=eyJhbGciOiJIUzI1NiIsInR5cC...: 200
Visiting https://ninjadoexcel.activehosted.com/box.php?nl=166&c=5255&m=7367&s=e8443eb95e5943afafb49e74e958...: 200
Visiting https://click.email.variety.com/subscription_center.aspx?jwt=eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...: 200
Visiting https://ArtofLifeCancer.us10.list-manage.com/unsubscribe?u=4c7718be090087c010f94f1b6&id=9cd20b3db...: 200
Visiting https://eplocomotivefc.us17.list-manage.com/unsubscribe?u=5b72bdfe45f693325e37b9a97&id=b7299731cd...: 200
Visiting https://haccgcg.r.af.d.sendibt2.com/tr/un/li/7S2xjzj98qNteZvudOnueOHpPs_DQLTuoyq9rmUeVh-sTDWm_Lkc...: 200
Visiting http://links.mail8.spopessentials8.com/luoo/v1/YlcrOXA4bWsvU3U3OHM4K2QvQmkxQlBsOVdxN2dZVWFrNHVjcn...: 200
Visiting https://trk.esps3.com/production/unsubscribe/afe895ffed92c1854277fa5b4575af95/210dd14cf9a44d69b6d...: 403
Visiting https://dm.celerant.com/unsubscribe-oc.cfm?&eid=454809528fac0a015c3f7f31ec3222fb&c=31421&jid=d2c7...: 200
Visiting https://click.email.billboard.com/subscription_center.aspx?jwt=eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVC...: 200
Visiting https://links.richardlrcoaching.com/a/2353/one_click_unsubscribe_via_post/920/861323/4d95eacab2e1...: 200
Visiting https://ninadesigns.us16.list-manage.com/unsubscribe?u=038167d18ec86a2b18ec33738&id=3d227876d0&t=...: 200
Visiting https://click.email.variety.com/subscription_center.aspx?jwt=eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...: 200
Visiting https://br965.infusionsoft.com/app/optOut/noConfirm/3808165/e6babf54852d9204: 200
Visiting https://seacrestsourdough.us12.list-manage.com/unsubscribe?u=a06812dfdadbafd758d1c82b0&id=26a326b...: 200
Visiting https://unsub.cmail19.com/t/completeheader/d/shyqyk/dkkkldtidd/c/: 200
Visiting https://rosslynva.us21.list-manage.com/unsubscribe?u=088a444fced6ad3db278c2172&id=f9ce02ea57&t=h&...: 200
Visiting https://painfreeforlife.activehosted.com/box.php?nl=7&c=1760&m=2428&s=c8fcdcea0791c88337d5b6be49a...: 200
Visiting https://click.email.hollywoodreporter.com/subscription_center.aspx?jwt=eyJhbGciOiJIUzI1NiIsInR5cC...: 200
Visiting https://corsairesdefrance.activehosted.com/box.php?nl=1&c=61&m=18&s=6500174fc7355623ec19baf7a3b75...: 200
Visiting https://email.institutoliberta.com.br/box.php?nl=192&c=21807&m=22382&s=e0d47507931e27c3f4c316c4bd...: 200
Visiting https://click.email.indiewire.com/subscription_center.aspx?jwt=eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVC...: 200
Visiting https://er477.infusionsoft.com/app/optOut/noConfirm/13105734/c58e7dfc19d04cac: 200
Visiting https://click.email.variety.com/subscription_center.aspx?jwt=eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...: 200
Visiting https://mail.simalfa-kulkote.com/unsubscribe/TNW0gQoSqDxIzWK4STl7Jxk842jqsa892ZJqyy892zQUwhM/Ug9P...: 200
Visiting https://owlzt-zglp.maillist-manage.com/ua/optout?od=3z996295a70eff8b477667a129e31e1571aad2babe84b...: 200
Visiting https://awfa18488.activehosted.com/box.php?nl=0&c=162&m=399&s=e0d47507931e27c3f4c316c4bd00f53e&fu...: 200
Visiting https://marolink.drkareem.com/a/2576/one_click_unsubscribe_via_post/6589/600020/f6d6ab0f4f3f9d132...: 200
Visiting https://click.bio-email.bioptimizers.com/subscription_center.aspx?jwt=eyJhbGciOiJIUzI1NiIsInR5cCI...: 200
Visiting https://sylver.infusionsoft.com/app/optOut/noConfirm/75041964/26078795316b572e: 200
Visiting https://treasurysoftware.activehosted.com/box.php?nl=3&c=19&m=22&s=eeedfaf522af6501c21cbd3898bba1...: 200
Visiting https://lucamastella.activehosted.com/box.php?nl=40&c=2375&m=11840&s=4ef7703d55a75bf2b72f6daa0b65...: 200
Visiting https://cem.activehosted.com/box.php?nl=1&c=722&m=778&s=a4f31b5d01596749530ca23e1dda2a51&funcml=u...: 200
Visiting https://click.email.variety.com/subscription_center.aspx?jwt=eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...: 200
Visiting https://beactive.activehosted.com/box.php?nl=24&c=1441&m=5181&s=f1314f569e6c6c56d6004fb1ea74dd6d&...: 200
Visiting https://click.email.indiewire.com/subscription_center.aspx?jwt=eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVC...: 200
Visiting https://manage.kmail-lists.com/subscriptions/unsubscribe?a=XEpWww&c=01JTGMC5HBVTYY7TDHNR7NQCY1&k=...: 200
Visiting https://click.email.indiewire.com/subscription_center.aspx?jwt=eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVC...: 200
Visiting https://methodsofleaders.activehosted.com/box.php?nl=41&c=233&m=1514&s=1e0993302b7f4b1aa47ece4f7d...: 200
Visiting https://click.email.billboard.com/subscription_center.aspx?jwt=eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVC...: 200

=== Run Summary ===
Emails scanned: 100
Unsubscribe links found: 95
Unsubscribe links visited: 52

Initial HTML file generated: C:\Users\---------\AppData\Roaming\GmailUnsubscribeApp\unsubscribe_links_20250507_151513_ec63b34a.html

Score Breakdown:
Low risk (< 5%): 76
Medium risk (5-20%): 1
High risk (> 20%): 18
Failed scans: 0

Scored HTML file generated: C:\Users\---------\AppData\Roaming\GmailUnsubscribeApp\unsubscribe_links_20250507_151513_ec63b34a_scored.html
```