using ColorVision.Recovery;
using System.IO;

namespace ColorVision.UI.Tests
{
    public sealed class StartupRecoveryPluginScannerTests : IDisposable
    {
        private readonly string _tempDirectory = Path.Combine(
            Path.GetTempPath(),
            $"ColorVisionStartupRecoveryTests-{Guid.NewGuid():N}");

        public StartupRecoveryPluginScannerTests()
        {
            Directory.CreateDirectory(_tempDirectory);
        }

        [Fact]
        public void ScanMarksTheRecordedPluginWithoutLoadingItsAssembly()
        {
            string pluginDirectory = Path.Combine(_tempDirectory, "CameraDirectory");
            Directory.CreateDirectory(pluginDirectory);
            File.WriteAllText(
                Path.Combine(pluginDirectory, "manifest.json"),
                """{"id":"camera.plugin","name":"Camera Plugin","version":"2.0","dllpath":"Camera.dll"}""");
            File.WriteAllBytes(Path.Combine(pluginDirectory, "Camera.dll"), [0, 1, 2, 3]);
            StartupFailureInfo failure = new(
                "1.4.12.31",
                "LoadingPlugin",
                "camera.plugin",
                DateTimeOffset.UtcNow,
                42);

            StartupRecoveryPluginItem item = Assert.Single(
                StartupRecoveryPluginScanner.Scan(_tempDirectory, failure));

            Assert.Equal("camera.plugin", item.PluginKey);
            Assert.Equal("Camera Plugin", item.DisplayName);
            Assert.Equal("2.0", item.VersionText);
            Assert.True(item.IsSuspected);
            Assert.False(item.IsLegacy);
        }

        [Fact]
        public void ScanIncludesLegacyAndInvalidManifestDirectories()
        {
            Directory.CreateDirectory(Path.Combine(_tempDirectory, "LegacyPlugin"));
            string invalidDirectory = Path.Combine(_tempDirectory, "BrokenManifest");
            Directory.CreateDirectory(invalidDirectory);
            File.WriteAllText(Path.Combine(invalidDirectory, "manifest.json"), "{ invalid json");

            IReadOnlyList<StartupRecoveryPluginItem> items =
                StartupRecoveryPluginScanner.Scan(_tempDirectory);

            StartupRecoveryPluginItem legacy = Assert.Single(items, item => item.DirectoryName == "LegacyPlugin");
            StartupRecoveryPluginItem invalid = Assert.Single(items, item => item.DirectoryName == "BrokenManifest");
            Assert.True(legacy.IsLegacy);
            Assert.False(legacy.HasInvalidManifest);
            Assert.True(invalid.IsLegacy);
            Assert.True(invalid.HasInvalidManifest);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDirectory))
                Directory.Delete(_tempDirectory, recursive: true);
        }
    }
}
