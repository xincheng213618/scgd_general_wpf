using ColorVision.Solution;
using ColorVision.Solution.Editor;
using ColorVision.UI;
using System.IO;

namespace ColorVision.UI.Tests;

public sealed class EditorDefaultPersistenceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LockedConfigKeepsThePreviousOpenWithDefault(bool folder)
    {
        DirectoryInfo directory = Directory.CreateTempSubdirectory("ColorVision-EditorDefault-");
        string configPath = Path.Combine(directory.FullName, "config.json");
        string path = folder ? directory.FullName : Path.Combine(directory.FullName, "source.cv-default-test");
        if (!folder) File.WriteAllText(path, "test");
        var previousService = ConfigService.Instance;
        var handler = new ConfigHandler { ConfigFilePath = configPath };
        TestEnvironment.SetConfig(handler);
        var openWith = ResourceOpenService.Instance;
        EditorDescriptor? previousDefault = null;
        try
        {
            previousDefault = Assert.IsType<EditorDescriptor>(openWith.GetDefaultOpenWithEditor(path));
            string requested = openWith.GetOpenWithEditors(path, visibleOnly: false).First(item => item.Id != previousDefault.Id).Id;
            EditorManagerConfig config = handler.GetRequiredService<EditorManagerConfig>();
            Assert.True(handler.TrySave(config, out string error), error);
            byte[] before = File.ReadAllBytes(configPath);

            using (File.Open(configPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                Assert.False(openWith.TrySetDefaultOpenWithEditor(path, requested, out error));
                Assert.NotEmpty(error);
                Assert.Equal(previousDefault.Id, openWith.GetDefaultOpenWithEditor(path)?.Id);
                Assert.Null(config.DefaultFolderEditor);
                Assert.Empty(config.DefaultEditors);
            }
            Assert.Equal(before, File.ReadAllBytes(configPath));
        }
        finally
        {
            try
            {
                if (previousDefault != null && openWith.GetDefaultOpenWithEditor(path)?.Id != previousDefault.Id)
                    openWith.TrySetDefaultOpenWithEditor(path, previousDefault.Id, out _);
            }
            finally
            {
                ConfigService.SetInstance(previousService);
                foreach (FileInfo file in directory.EnumerateFiles()) file.Delete();
                directory.Delete();
            }
        }
    }
}
