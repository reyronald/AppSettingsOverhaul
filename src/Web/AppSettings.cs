using AppConfig;

namespace Web
{
    public sealed class AppSettings : AppSettingsParent
    {
        public static string WelcomeMessage => Get();
    }
}