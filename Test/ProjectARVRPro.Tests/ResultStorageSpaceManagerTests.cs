using System.Diagnostics;
using System.IO;

namespace ProjectARVRPro.Tests;

public sealed class ResultStorageSpaceManagerTests
{
    private const long GB = ResultStorageSpaceManager.BytesPerGB;

    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 1)]
    public async Task DisabledOrEnoughSpaceNeverDeletesExports(bool enabled, int expectedReads)
    {
        using var files = new TestFiles();
        string image = files.Add("SN/image_Whiteresult.png", 0);
        int reads = 0;
        var manager = new ResultStorageSpaceManager(_ => { reads++; return (GB, 10 * GB); });
        using var write = manager.BeginWrite(files.Root, enabled, 1);
        await write.EnsureSpaceAsync();
        Assert.True(File.Exists(image));
        Assert.Equal(expectedReads, reads);
    }

    [Fact]
    public async Task DeletesOldestExportsAcrossDateAndSnFoldersAndStopsAtThreshold()
    {
        using var files = new TestFiles();
        string first = files.Add("2020-01-01/SN1/image_Whiteresult.png", 0);
        string second = files.Add("SN2/image_Whitesource.tif", 1);
        string third = files.Add("TestResults_SN3_20200101_120000_.csv", 2);
        string[] exports = [first, second, third];
        var manager = new ResultStorageSpaceManager(_ => (GB - 2 + exports.Count(path => !File.Exists(path)), 10 * GB));
        using var write = manager.BeginWrite(files.Root, true, 1);
        await write.EnsureSpaceAsync();
        Assert.False(File.Exists(first));
        Assert.False(File.Exists(second));
        Assert.True(File.Exists(third));
        Assert.True(Directory.Exists(files.Root));
    }

    [Fact]
    public async Task OnlyRecognizedExportsAreRemovedAndTodaysAggregateIsRetained()
    {
        using var files = new TestFiles();
        string[] untouched = [files.Add("capture.cvraw"), files.Add("photo.png"), files.Add("result.png"), files.Add("notes.csv"),
            files.Add("manual.xlsx"), files.Add("ProjectARVRPro.db"), files.Add("source.lnk"),
            files.Add(".image_Whiteresult.png.123.arvrexport.tmp"),
            files.Add($"{DateTime.Today:yyyy-M-d}TestResults+ProjectARVRPro.xlsx")];
        string[] exports = [files.Add("SN/image_Whiteresult.jpg"), files.Add("SN/image_Whitesource.bmp"),
            files.Add("TestResults_SN_20200101_120000_.csv"), files.Add("2020-1-1TestResults+ProjectARVRPro.xlsx")];
        List<string> warnings = [];
        var manager = new ResultStorageSpaceManager(_ => (0, 10 * GB), warnings.Add);
        using var write = manager.BeginWrite(files.Root, true, 1);
        await write.EnsureSpaceAsync();
        Assert.All(untouched, path => Assert.True(File.Exists(path)));
        Assert.All(exports, path => Assert.False(File.Exists(path)));
        Assert.Contains(warnings, message => message.Contains("未达到保留1 GB", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AllActiveWritesAreProtectedUntilTheirLeasesEnd()
    {
        using var files = new TestFiles();
        string first = files.Add("SN1/image_Whiteresult.png");
        string second = files.Add("SN2/image_Whiteresult.png");
        string csv = files.Add("TestResults_SN2_20200101_120000_.csv");
        var manager = new ResultStorageSpaceManager(_ => (0, 10 * GB));
        using var firstWrite = manager.BeginWrite(files.Root, true, 1, Path.GetDirectoryName(first)!);
        var secondWrite = manager.BeginWrite(files.Root, true, 1, Path.GetDirectoryName(second)!, csv);
        await firstWrite.EnsureSpaceAsync();
        Assert.True(File.Exists(first));
        Assert.True(File.Exists(second));
        Assert.True(File.Exists(csv));
        secondWrite.Dispose();
        await firstWrite.EnsureSpaceAsync();
        Assert.True(File.Exists(first));
        Assert.False(File.Exists(second));
        Assert.False(File.Exists(csv));
    }

    [Fact]
    public async Task LockedFileIsSkippedAndOtherHistoricalExportsCanStillBeRemoved()
    {
        using var files = new TestFiles();
        string locked = files.Add("SN1/image_Whiteresult.png", 0);
        string removable = files.Add("SN2/image_Whiteresult.png", 1);
        using var fileLock = File.Open(locked, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        List<string> warnings = [];
        var manager = new ResultStorageSpaceManager(_ => (0, 10 * GB), warnings.Add);
        using var write = manager.BeginWrite(files.Root, true, 1);
        await write.EnsureSpaceAsync();
        Assert.True(File.Exists(locked));
        Assert.False(File.Exists(removable));
        Assert.Contains(warnings, message => message.Contains("无法删除", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ConsecutiveWritesReuseCandidatesAndRefreshNewExportsAfterExpiry()
    {
        using var files = new TestFiles();
        var clock = new CleanupClock();
        var manager = new ResultStorageSpaceManager(_ => (0, 10 * GB), clock: clock);
        string first = files.Add("first_Whiteresult.png");
        using var write = manager.BeginWrite(files.Root, true, 1);
        await write.EnsureSpaceAsync();
        Assert.False(File.Exists(first));

        string added = files.Add("added_Whiteresult.png");
        await write.EnsureSpaceAsync();
        Assert.True(File.Exists(added)); // Newly written exports are not rescanned on each consecutive save.

        clock.Advance(TimeSpan.FromSeconds(30));
        await write.EnsureSpaceAsync();
        Assert.False(File.Exists(added));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CachedCandidatesAreRevalidatedBeforeDeletion(bool preserveTimestamp)
    {
        using var files = new TestFiles();
        string first = files.Add("first_Whiteresult.png", 0);
        string changed = files.Add("changed_Whiteresult.png", 1);
        string last = files.Add("last_Whiteresult.png", 2);
        bool firstPass = true;
        var manager = new ResultStorageSpaceManager(_ => (firstPass && !File.Exists(first) ? GB : 0, 10 * GB));
        using var write = manager.BeginWrite(files.Root, true, 1);
        await write.EnsureSpaceAsync();
        Assert.False(File.Exists(first));
        Assert.True(File.Exists(changed));

        DateTime originalTime = File.GetLastWriteTimeUtc(changed);
        if (preserveTimestamp)
        {
            File.AppendAllText(changed, "new result data");
            File.SetLastWriteTimeUtc(changed, originalTime);
        }
        else
        {
            File.WriteAllText(changed, new string('x', File.ReadAllText(changed).Length));
            File.SetLastWriteTimeUtc(changed, originalTime.AddMinutes(10));
        }
        firstPass = false;
        using (var pending = manager.BeginWrite(files.Root, true, 1, last))
        {
            await write.EnsureSpaceAsync();
            Assert.True(File.Exists(changed));
            Assert.True(File.Exists(last));
        }
        await write.EnsureSpaceAsync();
        Assert.True(File.Exists(changed));
        Assert.False(File.Exists(last));
    }

    [Fact]
    public async Task CleanupYieldsToCallerAndSerializesConcurrentWritesWithProtectionRegistered()
    {
        using var files = new TestFiles();
        string protectedFile = files.Add("SN/image_Whiteresult.png");
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int reads = 0;
        var manager = new ResultStorageSpaceManager(_ =>
        {
            if (Interlocked.Increment(ref reads) == 1)
            {
                started.SetResult();
                release.Task.GetAwaiter().GetResult();
            }
            return (0, 10 * GB);
        });
        using var first = manager.BeginWrite(files.Root, true, 1);
        Task firstCleanup = first.EnsureSpaceAsync();
        Task? secondCleanup = null;
        using var second = manager.BeginWrite(files.Root, true, 1, Path.GetDirectoryName(protectedFile)!);
        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.False(firstCleanup.IsCompleted);
            secondCleanup = second.EnsureSpaceAsync();
            Assert.False(secondCleanup.IsCompleted);
            Assert.Equal(1, Volatile.Read(ref reads));
        }
        finally
        {
            release.TrySetResult();
            await firstCleanup;
            if (secondCleanup != null) await secondCleanup;
        }
        Assert.Equal(2, reads);
        Assert.True(File.Exists(protectedFile));
    }

    [Fact]
    public async Task UnreadableDiskSpaceOrImpossibleReserveNeverDeletesFiles()
    {
        using var files = new TestFiles();
        string image = files.Add("SN/image_Whiteresult.png");
        List<string> warnings = [];
        var unreadable = new ResultStorageSpaceManager(_ => throw new IOException("unavailable"), warnings.Add);
        using (var write = unreadable.BeginWrite(files.Root, true, 1)) await write.EnsureSpaceAsync();
        var impossible = new ResultStorageSpaceManager(_ => (0, GB), warnings.Add);
        using (var write = impossible.BeginWrite(files.Root, true, 1)) await write.EnsureSpaceAsync();
        Assert.True(File.Exists(image));
        Assert.Equal(2, warnings.Count);
    }

    [Fact]
    public async Task DoesNotTraverseJunctionsOrCleanThroughARootJunction()
    {
        using var files = new TestFiles();
        string outside = files.Add("outside/SN/image_Whiteresult.png");
        string output = Path.Combine(files.Root, "output");
        Directory.CreateDirectory(output);
        string link = Path.Combine(output, "linked");
        CreateJunction(link, Path.GetDirectoryName(outside)!);
        try
        {
            int reads = 0;
            var manager = new ResultStorageSpaceManager(_ => { reads++; return (0, 10 * GB); });
            using (var write = manager.BeginWrite(output, true, 1)) await write.EnsureSpaceAsync();
            using (var write = manager.BeginWrite(link, true, 1)) await write.EnsureSpaceAsync();
            Assert.True(File.Exists(outside));
            Assert.Equal(1, reads);
        }
        finally { Directory.Delete(link); } // Unlink while its target still exists, before the fixture removes its own tree.
    }

    [Fact]
    public async Task CachedCandidatesDoNotFollowADirectoryReplacedByAJunction()
    {
        using var files = new TestFiles();
        string first = files.Add("output/first_Whiteresult.png", 0);
        string cached = files.Add("output/SN/image_Whiteresult.png", 1);
        string outside = files.Add("outside/image_Whiteresult.png", 1);
        bool firstPass = true;
        var manager = new ResultStorageSpaceManager(_ => (firstPass && !File.Exists(first) ? GB : 0, 10 * GB));
        using var write = manager.BeginWrite(Path.GetDirectoryName(first)!, true, 1);
        await write.EnsureSpaceAsync();
        Assert.False(File.Exists(first));
        Assert.True(File.Exists(cached));

        // Replace just this fixture's now-empty directory; the cached file path must not reach the new target.
        File.Delete(cached);
        string link = Path.GetDirectoryName(cached)!;
        Directory.Delete(link);
        CreateJunction(link, Path.GetDirectoryName(outside)!);
        try
        {
            firstPass = false;
            await write.EnsureSpaceAsync();
            Assert.True(File.Exists(outside));
        }
        finally { Directory.Delete(link); }
    }

    private static void CreateJunction(string link, string target)
    {
        var start = new ProcessStartInfo("pwsh") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-Command");
        start.ArgumentList.Add($"New-Item -ItemType Junction -Path '{link.Replace("'", "''")}' -Target '{target.Replace("'", "''")}' -ErrorAction Stop | Out-Null");
        using var process = System.Diagnostics.Process.Start(start)!;
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, error);
    }

    private sealed class TestFiles : IDisposable
    {
        private static readonly string Parent = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "ColorVision.ArvrStorageTests"));
        internal string Root { get; } = Path.Combine(Parent, Guid.NewGuid().ToString("N"));

        internal TestFiles() => Directory.CreateDirectory(Root);

        internal string Add(string relative, int order = 0)
        {
            string path = Path.Combine(Root, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "synthetic export");
            File.SetLastWriteTimeUtc(path, new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMinutes(order));
            return path;
        }

        public void Dispose()
        {
            string target = Path.GetFullPath(Root);
            if (!target.StartsWith(Parent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Test cleanup escaped its temporary directory.");
            Directory.Delete(target, recursive: true);
        }
    }

    private sealed class CleanupClock : TimeProvider
    {
        private long timestamp;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => timestamp;
        internal void Advance(TimeSpan duration) => timestamp += duration.Ticks;
    }
}
