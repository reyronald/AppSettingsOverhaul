namespace AppConfig
{
    public sealed class CommonAppSettings : AppSettingsParent
    {
        private CommonAppSettings() { }

        public static string Title => Get();
        public static int Port => int.Parse(Get());
    }
}