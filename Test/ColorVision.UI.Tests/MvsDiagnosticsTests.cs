using ColorVision.Engine.Services.Devices.Camera.Diagnostics;
using Newtonsoft.Json.Linq;
using System.IO;

namespace ColorVision.UI.Tests;

public sealed class MvsDiagnosticsTests : IDisposable
{
    private readonly string directory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), $"ColorVision_MvsTests_{Guid.NewGuid():N}"));
    private readonly List<string> copies = [];

    public MvsDiagnosticsTests() => Directory.CreateDirectory(directory);

    [Fact]
    public void LogViewerUsesDiscoveredInstallationAndItsOwnWorkingDirectory()
    {
        string customInstallation = Path.Combine(directory, "Custom MVS");
        string x64 = Path.Combine(customInstallation, "Applications", "Win64", "LogViewer.exe");
        string x86 = Path.Combine(directory, "Other", "Applications", "Win32", "LogViewer.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(x64)!);
        Directory.CreateDirectory(Path.GetDirectoryName(x86)!);
        File.WriteAllText(x64, "fixture");
        File.WriteAllText(x86, "fixture");
        string[] installations = [Path.Combine(directory, "Other"), customInstallation];

        Assert.Equal(x64, MvsDiagnosticsPaths.FindLogViewer(installations));
        var startInfo = MvsDiagnosticsPaths.CreateLogViewerStartInfo(x64);
        Assert.Equal(Path.GetDirectoryName(x64), startInfo.WorkingDirectory);
        Assert.True(startInfo.UseShellExecute);
        Assert.Equal("runas", startInfo.Verb);
        File.Delete(x64);
        Assert.Equal(x86, MvsDiagnosticsPaths.FindLogViewer(installations));
        File.Delete(x86);
        Assert.Null(MvsDiagnosticsPaths.FindLogViewer(installations));
    }

    [Fact]
    public void SdkPathConfigurationIsASpecificLogSubdirectory()
    {
        Assert.Equal(Path.Combine(directory, "MvSDKLog"), MvsLogCollector.GetConfiguredSdkLogDirectory(directory + "\r\n"));
        Assert.Null(MvsLogCollector.GetConfiguredSdkLogDirectory("relative-folder"));
        Assert.Equal(@"D:\Custom MVS\uninstall.exe", MvsDiagnosticsPaths.GetExecutablePath(@"""D:\Custom MVS\uninstall.exe"" /silent"));
        Assert.Equal(@"D:\Custom MVS\Applications\Win64\MVS.exe", MvsDiagnosticsPaths.GetExecutablePath(@"D:\Custom MVS\Applications\Win64\MVS.exe,0"));
    }

    [Fact]
    public void CollectionPreservesSharedLogsAndReportsMissingLockedAndOversizeFiles()
    {
        DateTime utcNow = new(2026, 10, 11, 4, 0, 0, DateTimeKind.Utc);
        string recent = WriteFile("SDK_00.log", "camera timeout", utcNow.AddDays(-1));
        WriteFile("SDK_01.log", "old", utcNow.AddDays(-8));
        WriteFile("Log4qt_Client.log.1", "client warning", utcNow.AddDays(-1));
        WriteFile("customer-image.png", "excluded", utcNow.AddDays(-1));
        WriteFile("camera.xml", "excluded", utcNow.AddDays(-1));
        WriteFile("LogServer.ini", "LogLevel=4", utcNow.AddDays(-30));
        string locked = WriteFile("locked.log", "locked", utcNow.AddDays(-1));
        string oversized = WriteFile("oversized.log", "", utcNow.AddDays(-1));
        using (var sparse = new FileStream(oversized, FileMode.Open, FileAccess.Write)) sparse.SetLength(MvsLogCollector.MaxFileBytes + 1);
        File.SetLastWriteTimeUtc(oversized, utcNow.AddDays(-1));

        using var writer = new FileStream(recent, FileMode.Open, FileAccess.Write, FileShare.ReadWrite);
        using var exclusive = new FileStream(locked, FileMode.Open, FileAccess.Read, FileShare.None);
        var files = MvsLogCollector.CollectFiles(
        [
            new("SDK", directory),
            new("Duplicate", directory + Path.DirectorySeparatorChar),
            new("Missing", Path.Combine(directory, "Missing")),
        ], null, 7, utcNow);
        copies.AddRange(files.Select(file => file.FilePath));

        Assert.Equal(4, files.Count);
        Assert.Equal("camera timeout", File.ReadAllText(files.Single(file => file.EntryPath == "MVS/SDK/SDK_00.log").FilePath));
        Assert.Contains(files, file => file.EntryPath == "MVS/SDK/Log4qt_Client.log.1");
        Assert.Contains(files, file => file.EntryPath == "MVS/SDK/LogServer.ini");
        Assert.True(File.Exists(recent));
        JObject report = JObject.Parse(File.ReadAllText(files.Single(file => file.EntryPath == "MVS/collection.json").FilePath));
        var sources = (JArray)report["Sources"]!;
        Assert.Equal(2, sources.Count);
        Assert.Equal("notFound", sources[1]!.Value<string>("Status"));
        var reports = (JArray)sources[0]!["Files"]!;
        Assert.Equal("unavailable", reports.Single(file => file.Value<string>("Name") == "locked.log").Value<string>("Status"));
        Assert.Equal("skippedSizeLimit", reports.Single(file => file.Value<string>("Name") == "oversized.log").Value<string>("Status"));
        Assert.DoesNotContain(reports, file => file.Value<string>("Name") is "SDK_01.log" or "camera.xml" or "customer-image.png");
    }

    private string WriteFile(string name, string content, DateTime modifiedUtc)
    {
        string path = Path.Combine(directory, name);
        File.WriteAllText(path, content);
        File.SetLastWriteTimeUtc(path, modifiedUtc);
        return path;
    }

    public void Dispose()
    {
        foreach (string path in copies) File.Delete(path);
        string tempRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())) + Path.DirectorySeparatorChar;
        if (directory.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase)) Directory.Delete(directory, true);
    }
}
