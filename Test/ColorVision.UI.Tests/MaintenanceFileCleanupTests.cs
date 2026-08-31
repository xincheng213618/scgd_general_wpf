using ColorVision.UI.Maintenance;
using System.IO;

namespace ColorVision.UI.Tests;

public sealed class MaintenanceFileCleanupTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("ColorVisionMaintenanceTests-").FullName;

    [Fact]
    public void ScanAndCleanupOnlyConfirmedOldAllowlistedFiles()
    {
        string old = CreateFile("old.log");
        string recent = CreateFile("recent.log", ageDays: 1);
        string image = CreateFile("original.png");
        string nested = CreateFile("nested/old.log");
        MaintenanceFileCleanupScanResult scan = MaintenanceFileCleanup.Scan([Rule()]);
        string later = CreateFile("created-after-scan.log");

        Assert.Equal(old, Assert.Single(scan.Files).FullPath);
        Assert.Equal(new FileInfo(old).Length, scan.TotalBytes);
        MaintenanceFileCleanupResult result = MaintenanceFileCleanup.Cleanup(scan);

        Assert.Equal(1, result.DeletedFileCount);
        Assert.Equal(scan.TotalBytes, result.DeletedBytes);
        Assert.Equal(0, result.FailedFileCount);
        Assert.False(File.Exists(old));
        Assert.All(new[] { recent, image, nested, later }, file => Assert.True(File.Exists(file)));
        Assert.True(Directory.Exists(_root));
    }

    [Fact]
    public void RecursiveRulesAreExplicitAndRepeatedRulesDoNotDuplicateFiles()
    {
        string nested = CreateFile("nested/old.log");
        MaintenanceFileCleanupRule rule = Rule() with { Recursive = true };
        MaintenanceFileCleanupScanResult scan = MaintenanceFileCleanup.Scan([rule, rule]);

        Assert.Equal(nested, Assert.Single(scan.Files).FullPath);
        Assert.Equal(1, MaintenanceFileCleanup.Cleanup(scan).DeletedFileCount);
        Assert.True(Directory.Exists(Path.GetDirectoryName(nested)));
    }

    [Theory]
    [InlineData("../*.log")]
    [InlineData("sub\\*.log")]
    [InlineData("C:*.log")]
    public void ScanRejectsPatternsThatEscapeTheRoot(string pattern)
    {
        string original = CreateFile("keep.log");
        MaintenanceFileCleanupScanResult scan = MaintenanceFileCleanup.Scan([Rule() with { SearchPattern = pattern }]);

        Assert.Empty(scan.Files);
        Assert.Equal(MaintenanceFileCleanupIssueKind.Failed, Assert.Single(scan.Issues).Kind);
        Assert.Equal(0, MaintenanceFileCleanup.Cleanup(scan).DeletedFileCount);
        Assert.True(File.Exists(original));
    }

    [Fact]
    public void ScanRejectsRelativeDriveRootsAndInvalidRetention()
    {
        Assert.Empty(MaintenanceFileCleanup.Scan([Rule() with { RootPath = "relative" }]).Files);
        Assert.Single(MaintenanceFileCleanup.Scan([Rule() with { RootPath = Path.GetPathRoot(_root)! }]).Issues);
        Assert.Single(MaintenanceFileCleanup.Scan([Rule() with { RetentionDays = -1 }]).Issues);
        Assert.Single(MaintenanceFileCleanup.Scan([Rule() with { RetentionDays = int.MaxValue }]).Issues);
    }

    [Fact]
    public void CleanupSkipsFileChangedSinceConfirmation()
    {
        string file = CreateFile("changed.log");
        MaintenanceFileCleanupScanResult scan = MaintenanceFileCleanup.Scan([Rule()]);
        File.AppendAllText(file, "changed");
        File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddDays(-90));

        MaintenanceFileCleanupResult result = MaintenanceFileCleanup.Cleanup(scan);

        Assert.Equal(1, result.SkippedFileCount);
        Assert.Equal(0, result.DeletedFileCount);
        Assert.True(File.Exists(file));
    }

    [Fact]
    public void CleanupSkipsChangedTimestampEvenWhenLengthIsUnchanged()
    {
        string file = CreateFile("changed.log");
        MaintenanceFileCleanupScanResult scan = MaintenanceFileCleanup.Scan([Rule()]);
        File.SetLastWriteTimeUtc(file, DateTime.UtcNow);

        Assert.Equal(1, MaintenanceFileCleanup.Cleanup(scan).SkippedFileCount);
        Assert.True(File.Exists(file));
    }

    [Fact]
    public void CleanupSkipsAnOpenFile()
    {
        string file = CreateFile("locked.log");
        MaintenanceFileCleanupScanResult scan = MaintenanceFileCleanup.Scan([Rule()]);
        using var held = new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        MaintenanceFileCleanupResult result = MaintenanceFileCleanup.Cleanup(scan);

        Assert.Equal(1, result.SkippedFileCount);
        Assert.Equal(0, result.FailedFileCount);
        Assert.True(File.Exists(file));
    }

    [Fact]
    public void ReadOnlyFileIsReportedAsFailureAndKept()
    {
        string file = CreateFile("readonly.log");
        MaintenanceFileCleanupScanResult scan = MaintenanceFileCleanup.Scan([Rule()]);
        File.SetAttributes(file, File.GetAttributes(file) | FileAttributes.ReadOnly);
        try
        {
            MaintenanceFileCleanupResult result = MaintenanceFileCleanup.Cleanup(scan);
            Assert.Equal(1, result.FailedFileCount);
            Assert.Equal(0, result.DeletedFileCount);
            Assert.True(File.Exists(file));
        }
        finally
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }
    }

    [Fact]
    public void ProtectionIsCheckedDuringScanningAndAgainBeforeDeletion()
    {
        string file = CreateFile("protected.log");
        bool protectedNow = true;
        MaintenanceFileCleanupRule rule = Rule() with { IsProtected = _ => protectedNow };
        Assert.Empty(MaintenanceFileCleanup.Scan([rule]).Files);
        protectedNow = false;
        MaintenanceFileCleanupScanResult scan = MaintenanceFileCleanup.Scan([rule]);
        Assert.Single(scan.Files);
        protectedNow = true;

        Assert.Equal(1, MaintenanceFileCleanup.Cleanup(scan).SkippedFileCount);
        Assert.True(File.Exists(file));
    }

    [Fact]
    public void ProtectionFailureFailsClosed()
    {
        string file = CreateFile("protected.log");
        bool fail = false;
        MaintenanceFileCleanupRule rule = Rule() with { IsProtected = _ => fail ? throw new InvalidOperationException("Protection state unavailable") : false };
        MaintenanceFileCleanupScanResult scan = MaintenanceFileCleanup.Scan([rule]);
        fail = true;

        Assert.Equal(1, MaintenanceFileCleanup.Cleanup(scan).FailedFileCount);
        Assert.True(File.Exists(file));
    }

    [Fact]
    public void CancellationReturnsPartialResultsWithoutDeletingRemainingFiles()
    {
        CreateFile("first.log");
        CreateFile("second.log");
        using var cancellation = new CancellationTokenSource();
        bool executing = false;
        int visits = 0;
        MaintenanceFileCleanupRule rule = Rule() with
        {
            IsProtected = _ =>
            {
                if (executing && ++visits == 2)
                    cancellation.Cancel();
                return false;
            }
        };
        MaintenanceFileCleanupScanResult scan = MaintenanceFileCleanup.Scan([rule]);
        Assert.Equal(2, scan.Files.Count);
        executing = true;

        MaintenanceFileCleanupResult result = MaintenanceFileCleanup.Cleanup(scan, cancellation.Token);

        Assert.True(result.IsCancelled);
        Assert.Equal(1, result.DeletedFileCount);
        Assert.Equal(1, scan.Files.Count(file => File.Exists(file.FullPath)));
        Assert.Equal(0, result.FailedFileCount);
    }

    [Fact]
    public void CancellationBeforeScanOrCleanupDoesNotDeleteAnything()
    {
        string file = CreateFile("keep.log");
        MaintenanceFileCleanupScanResult scan = MaintenanceFileCleanup.Scan([Rule()]);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.True(MaintenanceFileCleanup.Scan([Rule()], cancellation.Token).IsCancelled);
        MaintenanceFileCleanupResult result = MaintenanceFileCleanup.Cleanup(scan, cancellation.Token);
        Assert.True(result.IsCancelled);
        Assert.Equal(0, result.DeletedFileCount);
        Assert.True(File.Exists(file));
    }

    [Fact]
    public void ScanCollectionsAreReadOnlyAndMissingRootsAreHarmless()
    {
        CreateFile("old.log");
        MaintenanceFileCleanupScanResult scan = MaintenanceFileCleanup.Scan([Rule()]);
        Assert.Throws<NotSupportedException>(() => ((IList<MaintenanceFileCleanupFile>)scan.Files).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<MaintenanceFileCleanupIssue>)scan.Issues).Clear());
        MaintenanceFileCleanupScanResult missing = MaintenanceFileCleanup.Scan([Rule() with { RootPath = Path.Combine(_root, "missing") }]);
        Assert.Empty(missing.Files);
        Assert.Empty(missing.Issues);
    }

    [Fact]
    public void SingleLevelPrefixRuleDoesNotDeleteOtherTemporaryFiles()
    {
        string owned = CreateFile("ColorVision_UpdateLog_old.log");
        string other = CreateFile("other.log");
        string nested = CreateFile("nested/ColorVision_UpdateLog_old.log");
        MaintenanceFileCleanupScanResult scan = MaintenanceFileCleanup.Scan([Rule() with { SearchPattern = "ColorVision_UpdateLog_*.log" }]);

        Assert.Equal(owned, Assert.Single(scan.Files).FullPath);
        Assert.Equal(1, MaintenanceFileCleanup.Cleanup(scan).DeletedFileCount);
        Assert.True(File.Exists(other));
        Assert.True(File.Exists(nested));
    }

    [Fact]
    public void ScanDoesNotFollowDirectoryOrFileLinksWhenSymbolicLinksAreAvailable()
    {
        string target = CreateFile("outside/keep.log");
        string allowed = Path.Combine(_root, "allowed");
        Directory.CreateDirectory(allowed);
        try
        {
            Directory.CreateSymbolicLink(Path.Combine(allowed, "linked-directory"), Path.GetDirectoryName(target)!);
            File.CreateSymbolicLink(Path.Combine(allowed, "linked-file.log"), target);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        {
            // Windows may deny symlink creation without Developer Mode or the privilege.
            return;
        }

        MaintenanceFileCleanupScanResult scan = MaintenanceFileCleanup.Scan([Rule() with { RootPath = allowed, Recursive = true }]);

        Assert.Empty(scan.Files);
        Assert.Equal(2, scan.Issues.Count);
        Assert.Equal(0, MaintenanceFileCleanup.Cleanup(scan).DeletedFileCount);
        Assert.True(File.Exists(target));
    }

    [Fact]
    public void CleanupRejectsRootReplacedByDirectoryLinkWhenSymbolicLinksAreAvailable()
    {
        string target = CreateFile("outside/keep.log");
        string old = CreateFile("allowed/keep.log");
        string allowed = Path.GetDirectoryName(old)!;
        MaintenanceFileCleanupScanResult scan = MaintenanceFileCleanup.Scan([Rule() with { RootPath = allowed }]);
        Directory.Move(allowed, Path.Combine(_root, "original-allowed"));
        try
        {
            Directory.CreateSymbolicLink(allowed, Path.GetDirectoryName(target)!);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        {
            return;
        }

        Assert.Equal(1, MaintenanceFileCleanup.Cleanup(scan).SkippedFileCount);
        Assert.True(File.Exists(target));
    }

    private MaintenanceFileCleanupRule Rule() => new("logs", _root, "*.log", RetentionDays: 30);

    private string CreateFile(string relativePath, int ageDays = 90)
    {
        string file = Path.Combine(_root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, "test data");
        File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddDays(-ageDays));
        return file;
    }

    public void Dispose()
    {
        string fullRoot = Path.GetFullPath(_root);
        string tempRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())) + Path.DirectorySeparatorChar;
        if (!fullRoot.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(fullRoot).StartsWith("ColorVisionMaintenanceTests-", StringComparison.Ordinal))
            throw new InvalidOperationException("Refusing to remove an unexpected test directory.");
        if (Directory.Exists(fullRoot))
            Directory.Delete(fullRoot, recursive: true);
    }
}
