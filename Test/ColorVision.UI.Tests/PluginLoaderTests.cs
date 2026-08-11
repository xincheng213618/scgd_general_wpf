using ColorVision.UI.Plugins;

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
    }
}
