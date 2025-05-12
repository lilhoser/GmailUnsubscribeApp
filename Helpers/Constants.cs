namespace GmailUnsubscribeApp.Helpers
{
    public static class Constants
    {
        public static string[] Scopes => new[] { "https://www.googleapis.com/auth/gmail.modify" };
        public static string ApplicationName => "Gmail Unsubscribe App";
        public static string TokenPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GmailUnsubscribeApp", "token.json");
    }
}