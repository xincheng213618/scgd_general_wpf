using ColorVision.UI.LogImp;
using System.IO;

namespace ColorVision.UI.Tests;

public sealed class FeedbackDiagnosticCleanupTests
{
    [Fact]
    public void ApplicationCleanupKeepsNewestActiveLogAndFindsLegacyLogs()
    {
        using TemporaryDirectory root = new();
        DateTime cutoffUtc = new(2026, 9, 11, 12, 0, 0, DateTimeKind.Utc);
        string currentLogDirectory = Directory.CreateDirectory(Path.Combine(root.Path, "Current", "log")).FullName;
        string applicationDirectory = Directory.GetParent(currentLogDirectory)!.FullName;
        string appDataDirectory = Directory.CreateDirectory(Path.Combine(root.Path, "AppData")).FullName;
        string legacyLogDirectory = Directory.CreateDirectory(Path.Combine(appDataDirectory, "ColorVision", "Log")).FullName;

        string oldCurrentLog = CreateFile(currentLogDirectory, "20260910.txt", 10, cutoffUtc.AddDays(-1));
        string activeCurrentLog = CreateFile(currentLogDirectory, "20260911.txt", 20, cutoffUtc.AddMinutes(-1));
        string legacyLog = CreateFile(legacyLogDirectory, "legacy.txt", 30, cutoffUtc.AddDays(-2));

        IReadOnlyList<FileInfo> files = AppLogCollector.GetHistoricalApplicationLogFiles(
            currentLogDirectory,
            appDataDirectory,
            applicationDirectory,
            cutoffUtc);

        Assert.Contains(files, file => file.FullName == oldCurrentLog);
        Assert.Contains(files, file => file.FullName == legacyLog);
        Assert.DoesNotContain(files, file => file.FullName == activeCurrentLog);
    }

    [Fact]
    public void CleanupDeletesUniqueFilesAndReportsEnumerationFailures()
    {
        using TemporaryDirectory root = new();
        string firstFile = CreateFile(root.Path, "first.log", 11, DateTime.UtcNow.AddDays(-1));
        string secondFile = CreateFile(root.Path, "second.dmp", 13, DateTime.UtcNow.AddDays(-1));
        IFeedbackDiagnosticCleanupSource[] sources =
        [
            new FixedCleanupSource(firstFile, firstFile, secondFile),
            new ThrowingCleanupSource(),
        ];

        DiagnosticFileCleanupResult result = DiagnosticFileCleanupService.Cleanup(sources, DateTime.UtcNow);

        Assert.Equal(2, result.DeletedFileCount);
        Assert.Equal(1, result.FailedFileCount);
        Assert.Equal(24, result.ReleasedBytes);
        Assert.False(File.Exists(firstFile));
        Assert.False(File.Exists(secondFile));
    }

    private static string CreateFile(string directory, string fileName, int length, DateTime lastWriteTimeUtc)
    {
        string filePath = Path.Combine(directory, fileName);
        File.WriteAllBytes(filePath, new byte[length]);
        File.SetLastWriteTimeUtc(filePath, lastWriteTimeUtc);
        return filePath;
    }

    private sealed class FixedCleanupSource(params string[] files) : IFeedbackDiagnosticCleanupSource
    {
        public IEnumerable<string> GetHistoricalDiagnosticFiles(DateTime preserveFromUtc) => files;
    }

    private sealed class ThrowingCleanupSource : IFeedbackDiagnosticCleanupSource
    {
        public IEnumerable<string> GetHistoricalDiagnosticFiles(DateTime preserveFromUtc) =>
            throw new IOException("Enumeration failed for test coverage.");
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = Directory.CreateTempSubdirectory("ColorVision-DiagnosticCleanupTests-").FullName;
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }
}
