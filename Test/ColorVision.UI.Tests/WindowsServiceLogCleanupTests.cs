using System.IO;
using WindowsServicePlugin.ServiceManager;

namespace ColorVision.UI.Tests;

public sealed class WindowsServiceLogCleanupTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ColorVisionTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void ResolveLogDirectoryUsesExecutableDirectory()
    {
        string serviceDirectory = Path.Combine(_root, "InstalledService");
        ServiceEntry entry = new() { ExePath = Path.Combine(serviceDirectory, "service.exe") };

        string? actual = ServiceLogCleanup.ResolveLogDirectory(entry, Path.Combine(_root, "Ignored"));

        Assert.Equal(Path.Combine(serviceDirectory, "log"), actual);
    }

    [Fact]
    public void ResolveLogDirectoryRejectsFolderOutsideConfiguredRoot()
    {
        ServiceEntry entry = new() { FolderName = Path.Combine("..", "Outside") };

        string? actual = ServiceLogCleanup.ResolveLogDirectory(entry, Path.Combine(_root, "Services"));

        Assert.Null(actual);
    }

    [Fact]
    public void ClearDeletesNestedLogFilesAndPreservesDirectories()
    {
        string logDirectory = Path.Combine(_root, "Service", "log");
        string infoDirectory = Path.Combine(logDirectory, "LogInfo");
        Directory.CreateDirectory(infoDirectory);
        File.WriteAllText(Path.Combine(logDirectory, "service.log"), "1234");
        string readOnlyLog = Path.Combine(infoDirectory, "info.log");
        File.WriteAllText(readOnlyLog, "123456");
        File.SetAttributes(readOnlyLog, FileAttributes.ReadOnly);

        ServiceLogCleanupResult result = ServiceLogCleanup.Clear(logDirectory);

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.DeletedFileCount);
        Assert.Equal(10, result.DeletedBytes);
        Assert.Empty(Directory.EnumerateFiles(logDirectory, "*", SearchOption.AllDirectories));
        Assert.True(Directory.Exists(infoDirectory));
    }

    [Fact]
    public void ClearRejectsDirectoryThatIsNotNamedLog()
    {
        string serviceDirectory = Path.Combine(_root, "Service");
        Directory.CreateDirectory(serviceDirectory);
        string protectedFile = Path.Combine(serviceDirectory, "service.exe");
        File.WriteAllText(protectedFile, "keep");

        ServiceLogCleanupResult result = ServiceLogCleanup.Clear(serviceDirectory);

        Assert.False(result.Succeeded);
        Assert.Equal(0, result.DeletedFileCount);
        Assert.True(File.Exists(protectedFile));
    }

    public void Dispose()
    {
        if (!Directory.Exists(_root))
            return;

        string testParent = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "ColorVisionTests"))
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string resolvedRoot = Path.GetFullPath(_root)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!resolvedRoot.StartsWith(testParent, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Refusing to remove unexpected test directory: {resolvedRoot}");

        foreach (string file in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories))
            File.SetAttributes(file, FileAttributes.Normal);
        Directory.Delete(_root, recursive: true);
    }
}
