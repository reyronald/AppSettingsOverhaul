using AppConfig;
using System.Diagnostics;

namespace Web
{
    public class Global : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            string title = CommonAppSettings.Title;
            int port = CommonAppSettings.Port;
            string welcomeMessage = AppSettings.WelcomeMessage;

            Debug.WriteLine(title);
            Debug.WriteLine(port);
            Debug.WriteLine(welcomeMessage);
        }
    }
}