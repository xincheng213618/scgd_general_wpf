using ColorVision.UI.Desktop.Download;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace ColorVision.UI.Tests;

// Exercise the bundled executable and its real HTTP/RPC resume contract without external services.
public sealed class DownloadAria2IntegrationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RealAria2_DownloadAndResume_PromotesOnlyVerifiedFile(bool pauseAndResume)
    {
        string root = Path.Combine(Path.GetTempPath(), nameof(DownloadAria2IntegrationTests), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        byte[] bytes = new byte[4 * 1024 * 1024];
        RandomNumberGenerator.Fill(bytes);
        string hash = Convert.ToHexString(SHA256.HashData(bytes));
        await using var server = new RangeServer(bytes, pauseAndResume);
        WpfTestHost.Invoke(() => { });
        var config = new DownloadManagerConfig { DefaultDownloadPath = root, RpcPort = FreePort() };
        using var manager = new Aria2cDownloadManager(Path.Combine(root, "Downloads.db"), config,
            enablePolling: false, registerLifetime: false, directMode: () => true);
        try
        {
            int callbacks = 0;
            var task = manager.AddVerifiedDownload(server.Url, root, onCompleted: _ => Interlocked.Increment(ref callbacks), expectedSha256: hash);
            await manager.WaitForTaskIdleAsync(task).WaitAsync(TimeSpan.FromSeconds(15));
            if (pauseAndResume)
            {
                await server.FirstChunkSent.Task.WaitAsync(TimeSpan.FromSeconds(15));
                await UntilAsync(manager, () => task.DownloadedBytes >= 2 * 1024 * 1024, () => $"{task.Status}: {task.ErrorMessage}; received {task.DownloadedBytes} bytes");
                Assert.False(File.Exists(task.SavePath));
                string? gid = task.Gid;
                manager.PauseDownload(task);
                await manager.WaitForTaskIdleAsync(task).WaitAsync(TimeSpan.FromSeconds(10));
                Assert.Equal(DownloadStatus.Paused, task.Status);
                Assert.True(File.Exists(task.WorkingPath));
                Assert.True(File.Exists(task.WorkingPath + ".aria2"));
                await manager.LoadRecordsAsync();
                Assert.Same(task, WpfTestHost.Invoke(() => manager.Tasks.Single()));
                Assert.Equal(gid, task.Gid);
                server.ReleaseFirstRequest.TrySetResult();
                manager.ResumeDownload(task);
                await manager.WaitForTaskIdleAsync(task).WaitAsync(TimeSpan.FromSeconds(10));
            }
            await UntilAsync(manager, () => task.Status is DownloadStatus.Completed or DownloadStatus.Failed, () => $"{task.Status}: {task.ErrorMessage}; received {task.DownloadedBytes} bytes");
            await manager.WaitForTaskIdleAsync(task).WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(task.Status == DownloadStatus.Completed, task.ErrorMessage);
            Assert.Equal(1, callbacks);
            Assert.Equal(hash, Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(task.SavePath))));
            Assert.False(File.Exists(task.WorkingPath));
            Assert.False(File.Exists(task.WorkingPath + ".aria2"));
            if (pauseAndResume) Assert.True(server.RangeRequestCount > 0, "aria2 must request the remaining bytes when resuming.");
            using var db = DownloadTaskStore.CreateDbClient(Path.Combine(root, "Downloads.db"));
            var entry = db.Queryable<DownloadEntry>().Single();
            Assert.Equal((int)DownloadStatus.Completed, entry.Status);
            Assert.Equal(hash, entry.ContentSha256);
        }
        finally
        {
            manager.Dispose();
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            Directory.Delete(root, recursive: true);
        }
    }

    private static int FreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static async Task UntilAsync(Aria2cDownloadManager manager, Func<bool> predicate, Func<string> diagnostic)
    {
        var timer = Stopwatch.StartNew();
        while (!predicate())
        {
            Assert.True(timer.Elapsed < TimeSpan.FromSeconds(30), "Timed out waiting for the real aria2 download. " + diagnostic());
            await manager.PollAsync(waitForUpdates: false);
            await Task.Delay(50);
        }
    }

    private sealed class RangeServer : IAsyncDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _stop = new();
        private readonly ConcurrentBag<Task> _workers = new();
        private readonly byte[] _bytes;
        private readonly bool _gateFirst;
        private readonly Task _accept;
        private int _getRequests;
        private int _rangeRequests;
        public TaskCompletionSource FirstChunkSent { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseFirstRequest { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int RangeRequestCount => Volatile.Read(ref _rangeRequests);
        public string Url { get; }
        public RangeServer(byte[] bytes, bool gateFirst)
        {
            _bytes = bytes;
            _gateFirst = gateFirst;
            _listener.Start();
            Url = $"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}/payload.bin";
            _accept = AcceptAsync();
        }
        private async Task AcceptAsync()
        {
            try
            {
                while (!_stop.IsCancellationRequested)
                {
                    var client = await _listener.AcceptTcpClientAsync(_stop.Token);
                    _workers.Add(ServeAsync(client));
                }
            }
            catch (OperationCanceledException) { }
        }
        private async Task ServeAsync(TcpClient client)
        {
            using (client)
            {
                try
                {
                    var stream = client.GetStream();
                    using var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, leaveOpen: true);
                    string request = await reader.ReadLineAsync(_stop.Token) ?? string.Empty;
                    string? line;
                    int offset = 0;
                    bool range = false;
                    while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync(_stop.Token)))
                    {
                        if (line.StartsWith("Range: bytes=", StringComparison.OrdinalIgnoreCase))
                        {
                            offset = int.Parse(line[13..].Split('-')[0]);
                            range = true;
                            Interlocked.Increment(ref _rangeRequests);
                        }
                    }
                    string headers = $"HTTP/1.1 {(range ? "206 Partial Content" : "200 OK")}\r\nContent-Length: {_bytes.Length - offset}\r\nAccept-Ranges: bytes\r\nConnection: close\r\n";
                    if (range) headers += $"Content-Range: bytes {offset}-{_bytes.Length - 1}/{_bytes.Length}\r\n";
                    await stream.WriteAsync(Encoding.ASCII.GetBytes(headers + "\r\n"), _stop.Token);
                    if (request.StartsWith("HEAD ", StringComparison.Ordinal)) return;
                    int requestIndex = Interlocked.Increment(ref _getRequests);
                    if (_gateFirst && requestIndex == 1)
                    {
                        int chunk = Math.Min(2 * 1024 * 1024, _bytes.Length - offset);
                        await stream.WriteAsync(_bytes.AsMemory(offset, chunk), _stop.Token);
                        await stream.FlushAsync(_stop.Token);
                        offset += chunk;
                        FirstChunkSent.TrySetResult();
                        await ReleaseFirstRequest.Task.WaitAsync(_stop.Token);
                    }
                    await stream.WriteAsync(_bytes.AsMemory(offset), _stop.Token);
                }
                catch (IOException) { }
                catch (OperationCanceledException) { }
                catch (SocketException) { }
            }
        }
        public async ValueTask DisposeAsync()
        {
            _stop.Cancel();
            _listener.Stop();
            ReleaseFirstRequest.TrySetResult();
            await _accept;
            await Task.WhenAll(_workers);
            _stop.Dispose();
        }
    }
}
