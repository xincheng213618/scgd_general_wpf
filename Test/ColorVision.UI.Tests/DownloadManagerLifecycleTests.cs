using ColorVision.UI.Desktop.Download;
using Newtonsoft.Json.Linq;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;

namespace ColorVision.UI.Tests;

// These tests protect file ownership, task lifecycle and legacy resume contracts shared by every downloader caller.
public sealed class DownloadManagerLifecycleTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), nameof(DownloadManagerLifecycleTests), Guid.NewGuid().ToString("N"));
    private readonly string _dbPath;
    private readonly List<Aria2cDownloadManager> _managers = new();
    public DownloadManagerLifecycleTests()
    {
        Directory.CreateDirectory(_root);
        _dbPath = Path.Combine(_root, "Downloads.db");
        WpfTestHost.Invoke(() => { });
    }
    private static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    private Aria2cDownloadManager Manager(FakeRpc rpc, FakeDaemon? daemon = null, DownloadManagerConfig? config = null)
    {
        var manager = new Aria2cDownloadManager(_dbPath, config ?? new DownloadManagerConfig { DefaultDownloadPath = _root },
            daemon ?? new FakeDaemon(), rpc, enablePolling: false, registerLifetime: false, directMode: () => true);
        _managers.Add(manager);
        return manager;
    }
    private DownloadEntry Entry(int id) => Assert.Single(new DownloadTaskStore(_dbPath).GetEntries(new[] { id }));
    private static Task Idle(Aria2cDownloadManager manager, DownloadTask task) => manager.WaitForTaskIdleAsync(task).WaitAsync(TimeSpan.FromSeconds(15));

    [Fact]
    public async Task CancelPendingAddRemovesAcceptedGidAndPreservesPartialFile()
    {
        var rpc = new FakeRpc { BlockAdd = true };
        var manager = Manager(rpc);
        int callbacks = 0;
        var task = manager.AddDownload("http://example.invalid/a.bin", _root, onCompleted: _ => callbacks++);
        await rpc.AddStarted.Task.WaitAsync(TimeSpan.FromSeconds(15));
        File.WriteAllText(task.WorkingPath!, "partial");
        File.WriteAllText(task.WorkingPath + ".aria2", "resume");
        manager.CancelDownload(task);
        rpc.AddRelease.TrySetResult();
        await Idle(manager, task);
        Assert.Equal(DownloadStatus.Paused, task.Status);
        Assert.Equal((int)DownloadStatus.Paused, Entry(task.Id).Status);
        Assert.Contains(rpc.Calls, call => call.Method == "aria2.remove");
        Assert.Equal("partial", File.ReadAllText(task.WorkingPath!));
        Assert.Equal("resume", File.ReadAllText(task.WorkingPath + ".aria2"));
        Assert.Equal(0, callbacks);
    }

    [Fact]
    public async Task DeleteDuringPendingAddWaitsForOwnedDownloadToStop()
    {
        var rpc = new FakeRpc { BlockAdd = true };
        var manager = Manager(rpc);
        var task = manager.AddDownload("http://example.invalid/deleted.bin", _root);
        await rpc.AddStarted.Task.WaitAsync(TimeSpan.FromSeconds(15));
        Task deletion = manager.DeleteRecordsAsync(new[] { task.Id });
        rpc.AddRelease.TrySetResult();
        await deletion.WaitAsync(TimeSpan.FromSeconds(15));
        Assert.Empty(new DownloadTaskStore(_dbPath).GetAllEntries());
        Assert.True(task.Runtime.IsDeleted);
        Assert.Contains(rpc.Calls, call => call.Method == "aria2.remove");
        await manager.PollAsync();
        Assert.Equal(0, rpc.BatchCalls);
    }

    [Fact]
    public async Task ClearDuringPendingAddPreservesFilesAndNewlyAddedTask()
    {
        var rpc = new FakeRpc { BlockAdd = true };
        var manager = Manager(rpc);
        var old = manager.AddDownload("http://example.invalid/old.bin", _root);
        await rpc.AddStarted.Task.WaitAsync(TimeSpan.FromSeconds(15));
        File.WriteAllText(old.WorkingPath!, "partial");
        Task clearing = manager.ClearAllRecordsAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        while (!old.Runtime.IsDeleted) await Task.Delay(10, timeout.Token);
        var added = manager.AddDownload("http://example.invalid/new.bin", _root);
        rpc.AddRelease.TrySetResult();
        await clearing.WaitAsync(timeout.Token);
        await Idle(manager, added);
        Assert.Equal(added.Id, Assert.Single(new DownloadTaskStore(_dbPath).GetAllEntries()).Id);
        Assert.Equal("partial", File.ReadAllText(old.WorkingPath!));
        Assert.Contains(rpc.Calls, call => call.Method == "aria2.remove");
        Assert.False(added.Runtime.IsDeleted);
        Assert.True(added.Runtime.IsRunning(added.Runtime.Version));
    }

    [Fact]
    public async Task CancelThenResumeBeforeAddResponseLeavesOnlyLatestDownloadRunning()
    {
        var rpc = new FakeRpc { BlockAdd = true };
        var manager = Manager(rpc);
        var task = manager.AddDownload("http://example.invalid/race.bin", _root);
        await rpc.AddStarted.Task.WaitAsync(TimeSpan.FromSeconds(15));
        string oldGid = task.Gid!;
        manager.CancelDownload(task);
        manager.ResumeDownload(task);
        rpc.AddRelease.TrySetResult();
        await Idle(manager, task);
        Assert.NotEqual(oldGid, task.Gid);
        Assert.Equal("removed", rpc.Statuses[oldGid]["status"]?.ToString());
        Assert.Equal("waiting", rpc.Statuses[task.Gid!]["status"]?.ToString());
        Assert.Equal((int)DownloadStatus.Waiting, Entry(task.Id).Status);
    }

    [Fact]
    public async Task PausedTaskKeepsItsGidAcrossPageReloadAndDoesNotLoseResumeFiles()
    {
        var rpc = new FakeRpc();
        var manager = Manager(rpc);
        var task = manager.AddDownload("http://example.invalid/resume.bin", _root);
        await Idle(manager, task);
        string gid = task.Gid!;
        File.WriteAllText(task.WorkingPath!, "partial");
        File.WriteAllText(task.WorkingPath + ".aria2", "resume");
        manager.PauseDownload(task);
        await Idle(manager, task);
        await manager.LoadRecordsAsync();
        var loaded = WpfTestHost.Invoke(() => Assert.Single(manager.Tasks));
        Assert.Same(task, loaded);
        Assert.Equal(gid, loaded.Gid);
        manager.ResumeDownload(loaded);
        await Idle(manager, loaded);
        Assert.Single(rpc.Calls.Where(call => call.Method == "aria2.addUri"));
        Assert.Contains(rpc.Calls, call => call.Method == "aria2.unpause");
        Assert.Equal("partial", File.ReadAllText(task.WorkingPath!));
        Assert.True(File.Exists(task.WorkingPath + ".aria2"));
    }

    [Fact]
    public async Task RemovedGidResumeCreatesFreshRpcTaskWithoutDeletingPartialFile()
    {
        var rpc = new FakeRpc { RejectUnpause = true };
        var manager = Manager(rpc);
        var task = manager.AddDownload("http://example.invalid/removed.bin", _root);
        await Idle(manager, task);
        File.WriteAllText(task.WorkingPath!, "partial");
        File.WriteAllText(task.WorkingPath + ".aria2", "resume");
        manager.PauseDownload(task);
        await Idle(manager, task);
        manager.ResumeDownload(task);
        await Idle(manager, task);
        Assert.Equal(2, rpc.Calls.Count(call => call.Method == "aria2.addUri"));
        Assert.Equal("partial", File.ReadAllText(task.WorkingPath!));
        Assert.True(File.Exists(task.WorkingPath + ".aria2"));
    }

    [Fact]
    public async Task ErrorStatusPreservesResumeDataAndCompletesFailureCallback()
    {
        var rpc = new FakeRpc();
        var manager = Manager(rpc);
        int callbacks = 0;
        var task = manager.AddDownload("http://example.invalid/error.bin", _root, onCompleted: _ => callbacks++);
        await Idle(manager, task);
        File.WriteAllText(task.WorkingPath!, "partial");
        File.WriteAllText(task.WorkingPath + ".aria2", "resume");
        rpc.Statuses[task.Gid!] = new JObject { ["status"] = "error", ["errorCode"] = "15", ["errorMessage"] = "Could not open existing file." };
        await manager.PollAsync();
        Assert.Equal(DownloadStatus.Failed, task.Status);
        Assert.Equal(1, callbacks);
        Assert.True(File.Exists(task.WorkingPath!));
        Assert.True(File.Exists(task.WorkingPath + ".aria2"));
    }

    [Fact]
    public async Task MissingGidFailsRatherThanStayingInDownloadingState()
    {
        var rpc = new FakeRpc();
        var manager = Manager(rpc);
        var task = manager.AddDownload("http://example.invalid/missing.bin", _root);
        await Idle(manager, task);
        rpc.Statuses.TryRemove(task.Gid!, out _);
        await manager.PollAsync();
        Assert.Equal(DownloadStatus.Failed, task.Status);
        Assert.Contains("not found", task.ErrorMessage!);
    }

    [Fact]
    public async Task ConcurrentTasksReserveDifferentFinalAndWorkingPaths()
    {
        var rpc = new FakeRpc();
        var manager = Manager(rpc);
        DownloadTask[] tasks = await Task.WhenAll(Enumerable.Range(0, 8).Select(index => Task.Run(() => manager.AddDownload($"http://example.invalid/{index}/same.bin", _root))));
        await Task.WhenAll(tasks.Select(task => Idle(manager, task)));
        Assert.Equal(tasks.Length, tasks.Select(task => task.SavePath).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(tasks.Length, tasks.Select(task => task.WorkingPath).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public async Task TrustedLocalReusePreservesSuccessfulFileWhenConsumerThrows()
    {
        const string contents = "valid cached package";
        File.WriteAllText(Path.Combine(_root, "package.bin"), contents);
        var rpc = new FakeRpc();
        var manager = Manager(rpc);
        int callbacks = 0;
        var task = manager.AddVerifiedDownload("http://example.invalid/package.bin", _root,
            onCompleted: _ => { callbacks++; throw new InvalidOperationException("Consumer failure"); }, expectedSha256: Hash(contents));
        await Idle(manager, task);
        Assert.Equal(DownloadStatus.Completed, task.Status);
        Assert.Equal(contents, File.ReadAllText(task.SavePath));
        Assert.Equal(1, callbacks);
        Assert.DoesNotContain(rpc.Calls, call => call.Method == "aria2.addUri");
        Assert.Equal((int)DownloadStatus.Completed, Entry(task.Id).Status);
    }

    [Fact]
    public async Task NetworkCompletionPromotesValidatedWorkingFileAndPersistsHash()
    {
        const string contents = "downloaded package";
        var rpc = new FakeRpc();
        var manager = Manager(rpc);
        int callbacks = 0;
        var task = manager.AddVerifiedDownload("http://example.invalid/package.bin", _root, onCompleted: _ => callbacks++, expectedSha256: Hash(contents));
        await Idle(manager, task);
        rpc.Complete(task, contents);
        await manager.PollAsync();
        Assert.Equal(DownloadStatus.Completed, task.Status);
        Assert.Equal(contents, File.ReadAllText(task.SavePath));
        Assert.False(File.Exists(task.WorkingPath!));
        Assert.Equal(Hash(contents), Entry(task.Id).ContentSha256);
        Assert.Equal(1, callbacks);
    }

    [Fact]
    public async Task OutputCollisionPreservesForeignFileAndLeavesValidatedWorkingFileRecoverable()
    {
        var rpc = new FakeRpc();
        var manager = Manager(rpc);
        var task = manager.AddDownload("http://example.invalid/collision.bin", _root);
        await Idle(manager, task);
        File.WriteAllText(task.SavePath, "foreign contents");
        rpc.Complete(task, "downloaded contents");
        await manager.PollAsync();
        Assert.Equal(DownloadStatus.Failed, task.Status);
        Assert.Equal("foreign contents", File.ReadAllText(task.SavePath));
        Assert.Equal("downloaded contents", File.ReadAllText(task.WorkingPath!));
    }

    [Fact]
    public async Task HashMismatchCannotPromoteOrReportCompletion()
    {
        var rpc = new FakeRpc();
        var manager = Manager(rpc);
        var task = manager.AddVerifiedDownload("http://example.invalid/bad.bin", _root, expectedSha256: Hash("expected"));
        await Idle(manager, task);
        rpc.Complete(task, "different");
        await manager.PollAsync();
        Assert.Equal(DownloadStatus.Failed, task.Status);
        Assert.False(File.Exists(task.SavePath));
        Assert.True(File.Exists(task.WorkingPath!));
    }

    [Fact]
    public async Task RestartLeavesUserPausedTaskPausedAndRestoresLegacyIncompleteTask()
    {
        var store = new DownloadTaskStore(_dbPath);
        store.Initialize();
        var paused = new DownloadEntry { Url = "http://example.invalid/paused.bin", FileName = "paused.bin", SavePath = Path.Combine(_root, "paused.bin"), Status = (int)DownloadStatus.Paused };
        var incomplete = new DownloadEntry { Url = "http://example.invalid/legacy.bin", FileName = "legacy.bin", SavePath = Path.Combine(_root, "legacy.bin"), Status = (int)DownloadStatus.Downloading };
        paused.Id = store.Insert(paused); incomplete.Id = store.Insert(incomplete);
        File.WriteAllText(incomplete.SavePath, "legacy partial");
        File.WriteAllText(incomplete.SavePath + ".aria2", "legacy resume");
        var rpc = new FakeRpc();
        var manager = Manager(rpc);
        manager.AutoRestartIncompleteDownloads();
        await manager.LoadRecordsAsync();
        var task = WpfTestHost.Invoke(() => manager.Tasks.Single(item => item.Id == incomplete.Id));
        await Idle(manager, task);
        Assert.Equal((int)DownloadStatus.Paused, Entry(paused.Id).Status);
        Assert.Single(rpc.Calls.Where(call => call.Method == "aria2.addUri"));
        Assert.Equal("legacy partial", File.ReadAllText(incomplete.SavePath));
        Assert.True(File.Exists(incomplete.SavePath + ".aria2"));
        Assert.True(DownloadTaskStore.IsPathProtectedFromCleanup(_dbPath, incomplete.SavePath));
    }

    [Fact]
    public async Task BatchedPollingLimitsRequestCountForQueuedTasks()
    {
        var rpc = new FakeRpc();
        var manager = Manager(rpc);
        var tasks = Enumerable.Range(0, 65).Select(index => manager.AddDownload($"http://example.invalid/item-{index}.bin", _root)).ToArray();
        await Task.WhenAll(tasks.Select(task => Idle(manager, task)));
        await manager.PollAsync();
        Assert.Equal(2, rpc.BatchCalls);
        Assert.All(tasks, task => Assert.Equal(DownloadStatus.Waiting, task.Status));
    }

    [Fact]
    public async Task MetadataCompletionFollowsChildBeforeCompletingTorrent()
    {
        var rpc = new FakeRpc();
        var manager = Manager(rpc);
        var task = manager.AddDownload("magnet:?xt=urn:btih:123&dn=..%5Cpayload", _root);
        await Idle(manager, task);
        string parent = task.Gid!;
        rpc.Statuses[parent] = new JObject { ["status"] = "complete", ["followedBy"] = new JArray("child-gid") };
        await manager.PollAsync();
        Assert.NotEqual(DownloadStatus.Completed, task.Status);
        Assert.Equal("child-gid", task.Gid);
        Directory.CreateDirectory(task.DownloadDirectory!);
        string path = Path.Combine(task.DownloadDirectory!, "movie.bin");
        File.WriteAllText(path, "BT data");
        rpc.Statuses["child-gid"] = FakeRpc.CompletedStatus(path, 7);
        rpc.Statuses["child-gid"]["bittorrent"] = new JObject();
        await manager.PollAsync();
        Assert.Equal(DownloadStatus.Completed, task.Status);
        Assert.Equal(path, task.SavePath);
        Assert.StartsWith(Path.GetFullPath(_root) + Path.DirectorySeparatorChar, path, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        foreach (var manager in _managers) manager.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { }
    }

    internal sealed class FakeDaemon : IAria2Daemon
    {
        public bool IsRunning { get; private set; } = true;
        public bool IsRunningForNetworkMode(bool direct) => IsRunning;
        public int PreparePort(int port) => port;
        public void Start(int port, string secret, DownloadManagerConfig config, bool direct) => IsRunning = true;
        public void Stop(Action shutdown) => IsRunning = false;
        public void Dispose() { }
    }
    internal sealed class FakeRpc : IAria2RpcClient
    {
        public readonly ConcurrentQueue<(string Method, string? Gid)> Calls = new();
        public readonly ConcurrentDictionary<string, JObject> Statuses = new();
        public bool BlockAdd; public bool RejectUnpause;
        public int BatchCalls;
        public TaskCompletionSource AddStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource AddRelease { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<JObject> CallAsync(string method, params object[] args) => CallAsync(method, CancellationToken.None, args);
        public async Task<JObject> CallAsync(string method, CancellationToken token, params object[] args)
        {
            Calls.Enqueue((method, args.FirstOrDefault() as string));
            if (method == "aria2.addUri")
            {
                var options = (Dictionary<string, string>)args[1];
                string gid = options["gid"];
                AddStarted.TrySetResult();
                if (BlockAdd) await AddRelease.Task.WaitAsync(TimeSpan.FromSeconds(15), token);
                Statuses[gid] = new JObject { ["status"] = "waiting", ["totalLength"] = "0", ["completedLength"] = "0", ["downloadSpeed"] = "0" };
                return new JObject { ["result"] = gid };
            }
            if (method == "aria2.unpause" && RejectUnpause) throw new Aria2RpcException("GID is not found.");
            if (args.FirstOrDefault() is string target && Statuses.TryGetValue(target, out var status))
            {
                if (method == "aria2.remove") status["status"] = "removed";
                if (method == "aria2.pause") status["status"] = "paused";
                if (method == "aria2.unpause") status["status"] = "waiting";
            }
            return new JObject { ["result"] = "OK" };
        }
        public Task<Aria2StatusResult[]> GetStatusesAsync(string[] gids, string[] keys, CancellationToken token)
        {
            Interlocked.Increment(ref BatchCalls);
            return Task.FromResult(gids.Select(gid => Statuses.TryGetValue(gid, out var status)
                ? new Aria2StatusResult(gid, (JObject)status.DeepClone(), null)
                : new Aria2StatusResult(gid, null, "GID is not found.")).ToArray());
        }
        public void Complete(DownloadTask task, string contents)
        {
            string path = task.WorkingPath ?? task.SavePath;
            File.WriteAllText(path, contents);
            Statuses[task.Gid!] = CompletedStatus(path, Encoding.UTF8.GetByteCount(contents));
        }
        public static JObject CompletedStatus(string path, long length) => new()
        {
            ["status"] = "complete", ["totalLength"] = length.ToString(), ["completedLength"] = length.ToString(), ["downloadSpeed"] = "0",
            ["files"] = new JArray(new JObject { ["path"] = path, ["selected"] = "true" })
        };
        public void Dispose() { }
    }
}
