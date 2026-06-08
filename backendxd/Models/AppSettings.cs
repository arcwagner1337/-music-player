namespace backendxd.Models
{
    public class AppSettings
    {
        public static string DbConString { get; set; } = string.Empty;
        public static string JwtKey { get; set; } = string.Empty;
        public static string MailSender { get; set; } = string.Empty;
        public static string MailAppKey { get; set; } = string.Empty;
        public static string LastFmApiKey { get; set; } = string.Empty;
        public static string WorkerUrl2 { get; set; } = string.Empty;

    }
}
