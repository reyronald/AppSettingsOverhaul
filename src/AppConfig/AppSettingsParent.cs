using System.Configuration;
using System.Runtime.CompilerServices;

namespace AppConfig
{
    public class AppSettingsParent
    {
        // Only meant to be used in testing environments
        private static AppSettingsSection AppSettingsSection { get; set; } = null;

        protected AppSettingsParent()
        {
        }

        protected static string Get([CallerMemberName] string propertyName = null)
        {
            if (AppSettingsSection != null)
            {
                return AppSettingsSection.Settings[propertyName].Value;
            }
            return ConfigurationManager.AppSettings[propertyName];
        }
    }
}
