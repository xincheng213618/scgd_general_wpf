using ColorVision.UI.Desktop.Diagnostics;
using ColorVision.UI.Desktop.Feedback.Collectors;
using ColorVision.UI.Plugins;
using ColorVisionServiceHost;
using Newtonsoft.Json.Linq;
using System.ComponentModel;
using System.Reflection;

namespace ColorVision.UI.Tests
{
    public sealed class CrashDumpDiagnosticsTests
    {
        [Fact]
        public void SettingsProviderExposesDesktopCrashDumpPage()
        {
            ConfigSettingMetadata setting = Assert.Single(new CrashDumpSettingsProvider().GetConfigSettings());

            Assert.Equal("崩溃转储", setting.Name);
            Assert.Equal(ConfigSettingType.Class, setting.Type);
            Assert.Same(CrashDumpConfiguration.Current, setting.Source);
            Assert.Equal(typeof(CrashDumpSettingsControl), setting.ViewType);
        }

        [Fact]
        public void ConfigurationUsesGenericReflectionMetadata()
        {
            Dictionary<string, ConfigSettingAttribute> settings = typeof(CrashDumpConfiguration)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(property => (Property: property, Attribute: property.GetCustomAttribute<ConfigSettingAttribute>()))
                .Where(item => item.Attribute != null)
                .ToDictionary(item => item.Property.Name, item => item.Attribute!);

            Assert.Equal(
                [nameof(CrashDumpConfiguration.DumpType), nameof(CrashDumpConfiguration.DumpCount), nameof(CrashDumpConfiguration.DumpFolder), nameof(CrashDumpConfiguration.CustomDumpFlagsText)],
                settings.OrderBy(item => item.Value.Order).Select(item => item.Key));
            Assert.Equal(ConfigSettingLayout.Wide, settings[nameof(CrashDumpConfiguration.DumpFolder)].Layout);

            PropertyInfo customFlags = typeof(CrashDumpConfiguration).GetProperty(nameof(CrashDumpConfiguration.CustomDumpFlagsText))!;
            PropertyVisibilityAttribute visibility = Assert.IsType<PropertyVisibilityAttribute>(customFlags.GetCustomAttribute<PropertyVisibilityAttribute>());
            Assert.Equal(nameof(CrashDumpConfiguration.DumpType), visibility.PropertyName);
            Assert.Equal(CrashDumpType.Custom, visibility.ExpectedValue);
        }

        [Fact]
        public void ConfigurationTargetsExecutableSpecificWerKey()
        {
            var configuration = new CrashDumpConfiguration("ColorVision");

            Assert.Equal("ColorVision.exe", configuration.ProcessExecutableName);
            Assert.EndsWith(@"LocalDumps\ColorVision.exe", configuration.RegistryKeyPath, StringComparison.OrdinalIgnoreCase);
        }

        [Theory]
        [InlineData("0x00000002", MiniDumpType.MiniDumpWithFullMemory)]
        [InlineData("4096", MiniDumpType.MiniDumpWithThreadInfo)]
        public void CustomDumpFlagsAcceptHexAndDecimal(string text, MiniDumpType expected)
        {
            Assert.Equal(expected, CrashDumpConfiguration.ParseCustomDumpFlags(text));
        }

        [Fact]
        public void PrivilegedServiceParsesTypedLocalMachineRegistryMutation()
        {
            var request = new ColorVisionServiceHost.ServiceHostRequest
            {
                Data = JObject.FromObject(new
                {
                    keyPath = @"HKLM\SOFTWARE\Microsoft\Windows\Windows Error Reporting\LocalDumps\ColorVision.exe",
                    registryView = "Registry32",
                    values = new object[]
                    {
                        new { name = "DumpFolder", kind = "ExpandString", value = @"C:\Users\tester\AppData\Local\CrashDumps" },
                        new { name = "DumpCount", kind = "DWord", value = 10 },
                        new { name = "DumpType", kind = "DWord", value = 2 },
                    },
                    deleteValueNames = new JArray("CustomDumpFlags"),
                })
            };

            LocalMachineRegistryMutation mutation = RegistryCommandService.ParseMutation(request);

            Assert.Equal(@"SOFTWARE\Microsoft\Windows\Windows Error Reporting\LocalDumps\ColorVision.exe", mutation.KeyPath);
            Assert.Equal(Microsoft.Win32.RegistryView.Registry32, mutation.RegistryView);
            Assert.Collection(
                mutation.Values,
                value =>
                {
                    Assert.Equal("DumpFolder", value.Name);
                    Assert.Equal(Microsoft.Win32.RegistryValueKind.ExpandString, value.Kind);
                    Assert.Equal(@"C:\Users\tester\AppData\Local\CrashDumps", value.Value);
                },
                value =>
                {
                    Assert.Equal("DumpCount", value.Name);
                    Assert.Equal(Microsoft.Win32.RegistryValueKind.DWord, value.Kind);
                    Assert.Equal(10, value.Value);
                },
                value =>
                {
                    Assert.Equal("DumpType", value.Name);
                    Assert.Equal(Microsoft.Win32.RegistryValueKind.DWord, value.Kind);
                    Assert.Equal(2, value.Value);
                });
            Assert.Equal(["CustomDumpFlags"], mutation.DeleteValueNames);
        }

        [Theory]
        [InlineData(@"HKLM\SOFTWARE\ColorVision", @"SOFTWARE\ColorVision")]
        [InlineData(@"HKEY_LOCAL_MACHINE\SYSTEM\ColorVision", @"SYSTEM\ColorVision")]
        [InlineData(@"SOFTWARE/ColorVision", @"SOFTWARE\ColorVision")]
        public void PrivilegedServiceAcceptsAnyLocalMachineSubkey(string keyPath, string expected)
        {
            Assert.Equal(expected, RegistryCommandService.NormalizeKeyPath(keyPath));
        }

        [Theory]
        [InlineData(@"HKCU\Software\ColorVision")]
        [InlineData(@"HKEY_USERS\.Default\Software\ColorVision")]
        public void PrivilegedServiceRejectsOtherExplicitHives(string keyPath)
        {
            Assert.Throws<InvalidOperationException>(() => RegistryCommandService.NormalizeKeyPath(keyPath));
        }

        [Theory]
        [InlineData("HKLM")]
        [InlineData("HKEY_LOCAL_MACHINE")]
        public void PrivilegedServiceRejectsTheHiveRoot(string keyPath)
        {
            Assert.Throws<InvalidOperationException>(() => RegistryCommandService.NormalizeKeyPath(keyPath));
        }

        [Fact]
        public void PrivilegedServicePreservesTypedRegistryValueData()
        {
            var values = new JArray
            {
                JObject.FromObject(new { name = "DWord", kind = "DWord", value = "0xFFFFFFFF" }),
                JObject.FromObject(new { name = "QWord", kind = "QWord", value = "0xFFFFFFFFFFFFFFFF" }),
                new JObject
                {
                    ["name"] = "Multi",
                    ["kind"] = "MultiString",
                    ["value"] = new JArray { "one", "two" },
                },
                JObject.FromObject(new { name = "Binary", kind = "Binary", value = Convert.ToBase64String([0, 127, 255]) }),
                JObject.FromObject(new { name = "None", kind = "None", value = Convert.ToBase64String([1, 2, 3]) }),
            };
            var request = new ColorVisionServiceHost.ServiceHostRequest
            {
                Data = new JObject
                {
                    ["keyPath"] = @"SOFTWARE\ColorVision\RegistryWriterTest",
                    ["values"] = values,
                }
            };

            LocalMachineRegistryMutation mutation = RegistryCommandService.ParseMutation(request);

            Assert.Equal(-1, mutation.Values[0].Value);
            Assert.Equal(-1L, mutation.Values[1].Value);
            Assert.Equal(["one", "two"], Assert.IsType<string[]>(mutation.Values[2].Value));
            Assert.Equal([0, 127, 255], Assert.IsType<byte[]>(mutation.Values[3].Value));
            Assert.Equal([1, 2, 3], Assert.IsType<byte[]>(mutation.Values[4].Value));
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
