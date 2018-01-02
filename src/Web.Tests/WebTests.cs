using AppConfig;
using NUnit.Framework;
using System;
using System.Configuration;
using System.IO;
using System.Reflection;

namespace Web.Tests
{
    [TestFixture]
    public class WebTests
    {
        [SetUp]
        public void MountConfigurationFiles()
        {
            string exeConfigFilename = Path.Combine(TestContext.CurrentContext.TestDirectory, "Web.config");

            var configFileMap = new ExeConfigurationFileMap
            {
                ExeConfigFilename = exeConfigFilename
            };

            Configuration originalConfig = ConfigurationManager.OpenMappedExeConfiguration(configFileMap, ConfigurationUserLevel.None);
            originalConfig.AppSettings.File = "Common.App.config";
            originalConfig.Save(ConfigurationSaveMode.Modified);

            Configuration testConfig = ConfigurationManager.OpenMappedExeConfiguration(configFileMap, ConfigurationUserLevel.None);
            PropertyInfo appSettingsSection = typeof(AppSettingsParent).GetProperty("AppSettingsSection", BindingFlags.Static | BindingFlags.NonPublic);
            if (appSettingsSection == null)
            {
                throw new InvalidOperationException($"Couldn't find \"AppSettingsSection\" property in {nameof(AppSettingsParent)}.");
            }
            appSettingsSection.SetValue(null, testConfig.AppSettings, null);
        }

        [Test]
        public void AppSettings_Is_Properly_Configured()
        {
            BindingFlags bindingAttrs = BindingFlags.Static | BindingFlags.Public;
            PropertyInfo[] webAppSettings = typeof(AppSettings).GetProperties(bindingAttrs);

            foreach (PropertyInfo webAppSetting in webAppSettings)
            {
                webAppSetting.GetValue(null, null);
            }

            PropertyInfo[] commonAppSettings = typeof(CommonAppSettings).GetProperties(bindingAttrs);
            foreach (PropertyInfo commonAppSetting in commonAppSettings)
            {
                commonAppSetting.GetValue(null, null);
            }

            // No exceptions thrown? Pass
            Assert.Pass();
        }
    }
}
