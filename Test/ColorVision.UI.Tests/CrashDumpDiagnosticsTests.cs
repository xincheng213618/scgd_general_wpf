using ColorVision.UI.Desktop.Diagnostics;
using ColorVision.UI.Desktop.Feedback.Collectors;
using ColorVision.UI.Plugins;

namespace ColorVision.UI.Tests
{
    public sealed class CrashDumpDiagnosticsTests
    {
        [Fact]
        public void SettingsProviderExposesDesktopCrashDumpPage()
        {
            ConfigSettingMetadata setting = Assert.Single(new CrashDumpSettingsProvider().GetConfigSettings());

            Assert.Equal("崩溃转储", setting.Name);
            Assert.Equal(ConfigSettingType.TabItem, setting.Type);
            Assert.Equal(typeof(CrashDumpSettingsControl), setting.ViewType);
        }

        [Fact]
        public void ConfigurationTargetsExecutableSpecificWerKey()
        {
            var configuration = new CrashDumpConfiguration("ColorVision");

            Assert.Equal("ColorVision.exe", configuration.ProcessExecutableName);
            Assert.EndsWith(@"LocalDumps\ColorVision.exe", configuration.RegistryKeyPath, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void FeedbackCollectorsAreBuiltIntoDesktopModule()
        {
            Assert.IsAssignableFrom<IFeedbackLogCollector>(new CrashDumpFileCollector());
            Assert.IsAssignableFrom<IFeedbackLogCollector>(new WindowsEventLogCollector());
        }

        [Theory]
        [InlineData("EventVWR")]
        [InlineData("eventvwr")]
        public void RetiredEventViewerPluginIsNotLoaded(string pluginId)
        {
            Assert.True(PluginLoader.IsRetiredPlugin(pluginId));
        }

        [Fact]
        public void ActivePluginsAreNotMarkedAsRetired()
        {
            Assert.False(PluginLoader.IsRetiredPlugin("Spectrum"));
        }
    }
}
