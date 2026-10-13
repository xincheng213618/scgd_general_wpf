using ColorVision.UI.Desktop.Diagnostics;
using ColorVision.UI.Desktop.Feedback;
using ColorVision.UI.Desktop.Feedback.Collectors;
using ColorVision.UI.Plugins;
using ColorVision.UI.Maintenance;
using ColorVisionServiceHost;
using Newtonsoft.Json.Linq;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;

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
            Assert.IsAssignableFrom<IFeedbackLogCollector>(new CurrentProcessDumpCollector());
            Assert.IsAssignableFrom<IFeedbackLogCollector>(new WindowsEventLogCollector());
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void DumpCollectorsRequireExplicitSelection(bool currentProcess)
        {
            IFeedbackLogCollector collector = currentProcess ? new CurrentProcessDumpCollector() : new CrashDumpFileCollector();
            var item = new CollectorItem(collector);

            Assert.False(collector.IsSelectedByDefault);
            Assert.False(item.IsChecked);

            item.IsChecked = true;

            Assert.True(item.IsChecked);
        }

        [Fact]
        public void CrashDumpMaintenanceOnlyDeletesConfirmedOwnedDumpsAndHonorsClearAll()
        {
            string root = Path.Combine(Path.GetTempPath(), $"ColorVision_DumpMaintenance_{Guid.NewGuid():N}");
            Directory.CreateDirectory(root);
            string oldDump = Path.Combine(root, "ColorVision.exe.123.dmp");
            string recentDump = Path.Combine(root, "ColorVision_20261014_120000.dmp");
            string otherDump = Path.Combine(root, "OtherApp.exe.123.dmp");
            foreach (string path in new[] { oldDump, recentDump, otherDump }) File.WriteAllText(path, "fixture dump");
            File.SetLastWriteTimeUtc(oldDump, DateTime.UtcNow.AddDays(-40));
            try
            {
                var provider = new CrashDumpFileMaintenanceProvider(() => (root, "ColorVision.exe"));
                FileMaintenanceCategory category = Assert.Single(provider.GetCategories());
                Assert.False(string.IsNullOrWhiteSpace(category.Title));
                Assert.False(string.IsNullOrWhiteSpace(category.Description));
                Assert.False(category.DefaultSelected);
                provider.PrepareScan();
                Assert.Equal(root, Assert.Single(category.SourceDirectories));
                FileMaintenanceSnapshot retained = category.Scan(30, null, CancellationToken.None);
                Assert.Equal(oldDump, Assert.Single(retained.Files).FullPath);

                FileMaintenanceSnapshot all = category.Scan(0, retained, CancellationToken.None);
                Assert.Equal(2, all.Count);
                string laterDump = Path.Combine(root, "ColorVision.exe.456.dmp");
                File.WriteAllText(laterDump, "created after scan");
                FileMaintenanceCleanupResult result = category.Cleanup(all, CancellationToken.None);
                Assert.Equal(2, result.DeletedCount);
                Assert.False(File.Exists(oldDump));
                Assert.False(File.Exists(recentDump));
                Assert.True(File.Exists(otherDump));
                Assert.True(File.Exists(laterDump));
            }
            finally
            {
                string fullRoot = Path.GetFullPath(root);
                string tempRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                if (fullRoot.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase) && Path.GetFileName(fullRoot).StartsWith("ColorVision_DumpMaintenance_", StringComparison.Ordinal))
                    Directory.Delete(fullRoot, true);
            }
        }

        [Fact]
        public void CurrentProcessDumpIsCapturedOnDemandAndLargeSnapshotIsCompressedAndCleaned()
        {
            const long dumpLength = 101L * 1024 * 1024;
            string? dumpPath = null;
            string? zipPath = null;
            byte[]? expectedHash = null;
            var collector = new CurrentProcessDumpCollector((path, flags) =>
            {
                Assert.Equal((int)MiniDumpType.MiniDumpWithFullMemory, flags);
                dumpPath = path;
                using (var file = File.Create(path))
                {
                    file.SetLength(dumpLength);
                    file.Write("MDMP"u8);
                    file.Position = dumpLength - 1;
                    file.WriteByte(123);
                }
                using var input = File.OpenRead(path);
                expectedHash = SHA256.HashData(input);
            });

            Assert.Null(dumpPath);
            Assert.False(new CollectorItem(collector).IsChecked);
            try
            {
                var result = FeedbackWindow.CreateLogPackage([collector], new Progress<string>());
                zipPath = result.ZipPath;

                Assert.Equal(1, result.TotalFiles);
                Assert.NotNull(dumpPath);
                Assert.False(File.Exists(dumpPath));
                using var archive = ZipFile.OpenRead(zipPath);
                ZipArchiveEntry entry = Assert.Single(archive.Entries);
                Assert.StartsWith("ProcessDump/", entry.FullName);
                Assert.EndsWith(".dmp", entry.FullName);
                Assert.Equal(dumpLength, entry.Length);
                Assert.True(entry.CompressedLength < entry.Length);
                using var contents = entry.Open();
                Assert.Equal(expectedHash, SHA256.HashData(contents));
            }
            finally
            {
                if (dumpPath != null) File.Delete(dumpPath);
                if (zipPath != null) File.Delete(zipPath);
            }
        }

        [Fact]
        public void CurrentProcessDumpFailureCleansPartialSnapshotAndPreservesOtherDiagnostics()
        {
            string? dumpPath = null;
            string? zipPath = null;
            var collector = new CurrentProcessDumpCollector((path, _) =>
            {
                dumpPath = path;
                File.WriteAllText(path, "partial dump");
                throw new IOException("Synthetic snapshot failure");
            });
            string logPath = Path.Combine(Path.GetTempPath(), $"ColorVision_DumpTest_{Guid.NewGuid():N}.log");
            File.WriteAllText(logPath, "other diagnostics");

            try
            {
                var result = FeedbackWindow.CreateLogPackage([collector, new ExistingFileCollector(logPath)], new Progress<string>());
                zipPath = result.ZipPath;

                Assert.Equal(2, result.TotalFiles);
                Assert.NotNull(dumpPath);
                Assert.False(File.Exists(dumpPath));
                Assert.False(File.Exists(Path.ChangeExtension(dumpPath, ".txt")));
                Assert.False(File.Exists(logPath));
                using var archive = ZipFile.OpenRead(zipPath);
                ZipArchiveEntry error = Assert.Single(archive.Entries, entry => entry.FullName == "ProcessDump/collection-error.txt");
                using var reader = new StreamReader(error.Open());
                Assert.Contains("Synthetic snapshot failure", reader.ReadToEnd());
                Assert.Contains(archive.Entries, entry => entry.FullName == "Logs/other.log");
            }
            finally
            {
                if (dumpPath != null)
                {
                    File.Delete(dumpPath);
                    File.Delete(Path.ChangeExtension(dumpPath, ".txt"));
                }
                if (zipPath != null) File.Delete(zipPath);
                File.Delete(logPath);
            }
        }

        private sealed class ExistingFileCollector(string path) : IFeedbackLogCollector
        {
            public string Name => "Other diagnostics";
            public int Order => 0;
            public IEnumerable<(string EntryPath, string FilePath)> CollectFiles() => [("Logs/other.log", path)];
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
