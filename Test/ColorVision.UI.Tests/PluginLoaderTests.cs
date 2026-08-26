using ColorVision.UI.Plugins;
using System.IO;

namespace ColorVision.UI.Tests
{
    public sealed class PluginLoaderTests
    {
        [Fact]
        public void SkipOnceMatchingIsCaseInsensitive()
        {
            Assert.True(PluginLoader.ShouldSkipPlugin(["sPeCtRuM"], "SPECTRUM", "DifferentDirectory"));
        }

        [Fact]
        public void ManifestPluginCanBeSkippedByManifestId()
        {
            Assert.True(PluginLoader.ShouldSkipPlugin(["camera.plugin"], "camera.plugin", "CameraDirectory"));
        }

        [Fact]
        public void ManifestPluginCanBeSkippedByDirectoryName()
        {
            Assert.True(PluginLoader.ShouldSkipPlugin(["CameraDirectory"], "camera.plugin", "CameraDirectory"));
        }

        [Fact]
        public void LegacyPluginCanBeSkippedByDirectoryName()
        {
            Assert.True(PluginLoader.ShouldSkipPlugin(["LegacyCamera"], null, "LegacyCamera"));
        }

        [Fact]
        public void EmptySkipOnceCollectionDoesNotSkipPlugin()
        {
            Assert.False(PluginLoader.ShouldSkipPlugin([], "camera.plugin", "CameraDirectory"));
        }

        [Fact]
        public void PluginAssemblyAvailabilityDetectsMissingDllWithoutThrowing()
        {
            string pluginDirectory = Path.Combine(Path.GetTempPath(), $"ColorVisionPluginLoaderTests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(pluginDirectory);
            try
            {
                string pluginDll = Path.Combine(pluginDirectory, "Camera.Plugin.dll");
                Assert.False(PluginLoader.IsPluginAssemblyAvailable(pluginDll));

                File.WriteAllBytes(pluginDll, [0, 1, 2, 3]);
                Assert.True(PluginLoader.IsPluginAssemblyAvailable(pluginDll));
            }
            finally
            {
                Directory.Delete(pluginDirectory, recursive: true);
            }
        }
    }
}
