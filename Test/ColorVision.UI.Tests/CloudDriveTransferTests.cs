using ColorVision.Rbac.CloudDrive;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.UI.Tests;

public sealed class CloudDriveTransferTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "ColorVisionCloudDriveTests", Guid.NewGuid().ToString("N"));
    private readonly Uri _server = new("http://localhost:9998/");

    public CloudDriveTransferTests() => Directory.CreateDirectory(_directory);

    [Theory]
    [InlineData(64 * 1024, 64 * 1024)]
    [InlineData(8 * 1024 * 1024, 1024 * 1024)]
    public async Task RestartResumesPersistedAnonymousIdentityAndConfirmedOffset(int suggestedChunkSize, int expectedChunkSize)
    {
        var store = new CloudDriveStore(_directory);
        var state = store.Load(_server.AbsoluteUri);
        var item = CreateItem(2 * expectedChunkSize + 18928);
        state.Items.Add(item);
        using var handler = new UploadServer { SuggestedChunkSize = suggestedChunkSize };
        using var http = new HttpClient(handler);
        var client = new TransferClient(http, _server, state.ClientId);
        using var cancel = new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.UploadAsync(item, () =>
        {
            store.Save(state);
            if (item.Offset == expectedChunkSize) cancel.Cancel();
        }, null, cancel.Token));

        var restored = store.Load(_server.AbsoluteUri);
        Assert.Equal(state.ClientId, restored.ClientId);
        Assert.Equal(expectedChunkSize, restored.Items[0].Offset);
        var resumedClient = new TransferClient(http, _server, restored.ClientId);
        await resumedClient.UploadAsync(restored.Items[0], () => store.Save(restored), null, CancellationToken.None);
        Assert.True(restored.Items[0].CanShare);
        Assert.Equal(1, handler.CreateCount);
        Assert.Equal(new long[] { 0, expectedChunkSize, 2L * expectedChunkSize }, handler.Offsets);
        Assert.Equal(File.ReadAllBytes(item.UploadPath), handler.Data.ToArray());
        Assert.Single(handler.ClientIds.Distinct());
        Assert.Equal(restored.Items[0].ShareUrl, store.Load(_server.AbsoluteUri).Items[0].ShareUrl);
    }

    [Fact]
    public async Task ShowsSentAndConfirmedBytesWithSpeedAndEstimatedTime()
    {
        var store = new CloudDriveStore(_directory);
        var confirmFirstBlock = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var handler = new UploadServer
        {
            BeforePatchResponse = (offset, token) => offset == 0
                ? confirmFirstBlock.Task.WaitAsync(token) : Task.Delay(Timeout.InfiniteTimeSpan, token)
        };
        using var http = new HttpClient(handler);
        var manager = new CloudDriveManager(store, _server, http);
        manager.AddPaths([CreateItem(150000).UploadPath]);
        var item = Assert.Single(manager.Items);
        var row = WpfTestHost.Invoke(() =>
        {
            var window = new CloudDriveWindow(manager);
            var list = Assert.IsType<ListBox>(window.FindName("FilesList"));
            var template = Assert.IsType<Grid>(list.ItemTemplate.LoadContent());
            template.DataContext = item;
            window.Close();
            return template;
        });
        var firstBlockShown = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var speedSampleShown = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondBlockShown = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        item.PropertyChanged += (_, _) =>
        {
            if (item.Progress > 0) firstBlockShown.TrySetResult();
            if (item.Offset == 0 && item.CurrentSpeedText.StartsWith("当前速度 ", StringComparison.Ordinal) && item.CurrentSpeedText != "当前速度 0 B/s")
                speedSampleShown.TrySetResult();
            if (item.Progress > 50) secondBlockShown.TrySetResult();
        };

        Task upload = WpfTestHost.Invoke<Task>(() => manager.UploadAsync());
        try
        {
            await firstBlockShown.Task.WaitAsync(TimeSpan.FromSeconds(10));
            WpfTestHost.Invoke(() =>
            {
                var detail = Assert.IsType<TextBlock>(row.FindName("UploadDetail"));
                Assert.StartsWith("已发送 64 KB / ", detail.Text);
                Assert.EndsWith("服务端已确认 0 B", detail.Text);
                var speed = Assert.IsType<TextBlock>(row.FindName("UploadCurrentSpeed"));
                Assert.Equal(item.CurrentSpeedText, speed.Text);
                Assert.Equal(Visibility.Visible, speed.Visibility);
                var bar = Assert.IsType<ProgressBar>(row.FindName("UploadProgressBar"));
                var percentage = Assert.IsType<TextBlock>(row.FindName("UploadPercentage"));
                Assert.Equal(100d * 65536 / item.Size, bar.Value);
                Assert.Equal($"{bar.Value:0.0}%", percentage.Text);
                Assert.Equal(Visibility.Visible, percentage.Visibility);
            });
            Assert.Equal(0, item.Offset);
            Assert.Equal(0, Assert.Single(store.Load(_server.AbsoluteUri).Items).Offset);

            // The first rate sample must include bytes sent before the server confirms them.
            await speedSampleShown.Task.WaitAsync(TimeSpan.FromSeconds(10));
            confirmFirstBlock.SetResult();
            await secondBlockShown.Task.WaitAsync(TimeSpan.FromSeconds(10));
            WpfTestHost.Invoke(() =>
            {
                var detail = Assert.IsType<TextBlock>(row.FindName("UploadDetail"));
                Assert.StartsWith("已发送 128 KB / ", detail.Text);
                Assert.EndsWith("服务端已确认 64 KB", detail.Text);
                var remaining = Assert.IsType<TextBlock>(row.FindName("UploadRemainingTime"));
                Assert.StartsWith("预计剩余", remaining.Text);
                Assert.Equal(Visibility.Visible, remaining.Visibility);
            });
            Assert.Equal(65536, item.Offset);
        }
        finally
        {
            WpfTestHost.Invoke(manager.Pause);
            await upload;
        }

        Assert.Equal("已暂停，可继续", item.Status);
        Assert.Equal(100d * 65536 / item.Size, item.Progress);
        Assert.Equal(65536, Assert.Single(store.Load(_server.AbsoluteUri).Items).Offset);
    }

    [Fact]
    public async Task LostFinalResponseRecoversReceiptWithoutUploadingAgain()
    {
        var item = CreateItem(1234);
        using var handler = new UploadServer { LoseFinalResponse = true };
        using var http = new HttpClient(handler);
        await new TransferClient(http, _server, Guid.NewGuid().ToString()).UploadAsync(item, () => { }, null, CancellationToken.None);
        Assert.True(item.IsComplete);
        Assert.Equal(item.Size, item.Offset);
        Assert.Single(handler.Offsets);
        Assert.Equal(1, handler.StatusCount);
        Assert.Equal("http://localhost:9998/transfer/share/" + UploadServer.ShareId, item.ShareUrl);
    }

    [Fact]
    public async Task TransientStatusFailureDoesNotResendAnUnconfirmedBlock()
    {
        var item = CreateItem(150000);
        using var handler = new UploadServer
        {
            BeforePatchResponse = (offset, _) => offset == 65536
                ? Task.FromException(new HttpRequestException("response lost after committing block")) : Task.CompletedTask,
            BeforeStatusResponse = (attempt, _) => attempt == 1
                ? Task.FromException(new HttpRequestException("server unavailable", null, HttpStatusCode.ServiceUnavailable)) : Task.CompletedTask
        };
        using var http = new HttpClient(handler);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var client = new TransferClient(http, _server, Guid.NewGuid().ToString());
        await client.UploadAsync(item, () => { }, null, deadline.Token);

        Assert.True(item.CanShare);
        Assert.Equal(item.Size, item.Offset);
        Assert.Equal(1, handler.CreateCount);
        Assert.Equal(2, handler.StatusCount);
        Assert.Equal(new long[] { 0, 65536, 131072 }, handler.Offsets);
        Assert.Equal(File.ReadAllBytes(item.UploadPath), handler.Data.ToArray());
    }

    [Fact]
    public async Task PatchTimeoutKeepsTheConfirmedPrefixAndRetriesWithSmallerBlocks()
    {
        const int originalChunkSize = 1024 * 1024;
        const int resumedChunkSize = originalChunkSize / 2;
        const int trailingBytes = 32768;
        var item = CreateItem(2 * originalChunkSize + trailingBytes);
        var requestedLengths = new List<long>();
        using var handler = new UploadServer
        {
            SuggestedChunkSize = originalChunkSize,
            BeforePatchBody = (length, token) =>
            {
                requestedLengths.Add(length);
                // The second request hangs before reaching the server's upload data stream.
                return requestedLengths.Count == 2 ? Task.Delay(Timeout.InfiniteTimeSpan, token) : Task.CompletedTask;
            }
        };
        using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(2) };
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var client = new TransferClient(http, _server, Guid.NewGuid().ToString());
        await client.UploadAsync(item, () => { }, null, deadline.Token);

        Assert.True(item.CanShare);
        Assert.Equal(1, handler.CreateCount);
        Assert.Equal(1, handler.StatusCount);
        Assert.Equal(new long[] { 0, originalChunkSize, originalChunkSize, originalChunkSize + resumedChunkSize, 2 * originalChunkSize }, handler.Offsets);
        Assert.Equal(new long[] { originalChunkSize, originalChunkSize, resumedChunkSize, resumedChunkSize, trailingBytes }, requestedLengths);
        Assert.Equal(File.ReadAllBytes(item.UploadPath), handler.Data.ToArray());
    }

    [Fact]
    public async Task PausingDuringRecoveryKeepsTheConfirmedCheckpointAndCanResume()
    {
        var store = new CloudDriveStore(_directory);
        var state = store.Load(_server.AbsoluteUri);
        var item = CreateItem(150000);
        state.Items.Add(item);
        var statusRequested = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var handler = new UploadServer
        {
            BeforePatchResponse = (offset, _) => offset == 65536
                ? Task.FromException(new HttpRequestException("response lost after committing block")) : Task.CompletedTask,
            BeforeStatusResponse = (attempt, token) =>
            {
                if (attempt != 1) return Task.CompletedTask;
                statusRequested.TrySetResult();
                return Task.Delay(Timeout.InfiniteTimeSpan, token);
            }
        };
        using var http = new HttpClient(handler);
        using var pause = new CancellationTokenSource();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var client = new TransferClient(http, _server, state.ClientId);
        Task upload = client.UploadAsync(item, () => store.Save(state), null, pause.Token);
        try
        {
            await statusRequested.Task.WaitAsync(deadline.Token);
        }
        finally
        {
            pause.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => upload);
        }

        var restoredState = store.Load(_server.AbsoluteUri);
        Assert.Equal(state.ClientId, restoredState.ClientId);
        var restored = Assert.Single(restoredState.Items);
        Assert.Equal(item.UploadId, restored.UploadId);
        Assert.Equal(65536, restored.Offset);
        await new TransferClient(http, _server, restoredState.ClientId)
            .UploadAsync(restored, () => store.Save(restoredState), null, deadline.Token);

        Assert.True(restored.CanShare);
        Assert.Equal(1, handler.CreateCount);
        Assert.Equal(new long[] { 0, 65536, 131072 }, handler.Offsets);
        Assert.Equal(File.ReadAllBytes(item.UploadPath), handler.Data.ToArray());
    }

    [Fact]
    public async Task ChangedFileIsRejectedBeforeAnyResumeRequest()
    {
        var item = CreateItem(300);
        using var handler = new UploadServer();
        using var http = new HttpClient(handler);
        var client = new TransferClient(http, _server, Guid.NewGuid().ToString());
        using var cancel = new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.UploadAsync(item, () => cancel.Cancel(), null, cancel.Token));
        await File.WriteAllBytesAsync(item.UploadPath, Enumerable.Repeat((byte)99, 300).ToArray());
        var exception = await Assert.ThrowsAsync<IOException>(() => client.UploadAsync(item, () => { }, null, CancellationToken.None));
        Assert.Contains("原文件已变化", exception.Message);
        Assert.Equal(0, handler.CreateCount);
    }

    [Fact]
    public async Task ZeroByteFileCompletesWithoutPatch()
    {
        var item = CreateItem(0);
        using var handler = new UploadServer();
        using var http = new HttpClient(handler);
        await new TransferClient(http, _server, Guid.NewGuid().ToString()).UploadAsync(item, () => { }, null, CancellationToken.None);
        Assert.True(item.IsComplete);
        Assert.Empty(handler.Offsets);
    }

    [Fact]
    public async Task OffsetConflictQueriesServerAndContinuesAtItsPosition()
    {
        var item = CreateItem(150000);
        using var handler = new UploadServer { CommitThenConflict = true };
        using var http = new HttpClient(handler);
        await new TransferClient(http, _server, Guid.NewGuid().ToString()).UploadAsync(item, () => { }, null, CancellationToken.None);
        Assert.True(item.IsComplete);
        Assert.Equal(new long[] { 0, 65536, 131072 }, handler.Offsets);
        Assert.Equal(File.ReadAllBytes(item.UploadPath), handler.Data.ToArray());
    }

    [Fact]
    public async Task FolderPackagePreservesUnicodeNestedFilesAndEmptyDirectories()
    {
        string source = Path.Combine(_directory, "图像数据");
        Directory.CreateDirectory(Path.Combine(source, "子目录"));
        Directory.CreateDirectory(Path.Combine(source, "空目录"));
        await File.WriteAllTextAsync(Path.Combine(source, "子目录", "测量.txt"), "原始图像参数");
        string zip = Path.Combine(_directory, "package.zip");
        await CloudDrivePackage.CreateAsync(source, zip, 1024 * 1024, null, CancellationToken.None);
        using var archive = ZipFile.OpenRead(zip);
        using var reader = new StreamReader(archive.GetEntry("子目录/测量.txt")!.Open());
        Assert.Equal("原始图像参数", await reader.ReadToEndAsync());
        Assert.NotNull(archive.GetEntry("空目录/"));
        Assert.True(File.Exists(Path.Combine(source, "子目录", "测量.txt")));
    }

    [Fact]
    public async Task CanceledOrOversizedPackageDoesNotLeaveCompletedArchive()
    {
        string source = Path.Combine(_directory, "folder");
        Directory.CreateDirectory(source);
        await File.WriteAllBytesAsync(Path.Combine(source, "data.bin"), System.Security.Cryptography.RandomNumberGenerator.GetBytes(10000));
        string zip = Path.Combine(_directory, "package.zip");
        await Assert.ThrowsAsync<IOException>(() => CloudDrivePackage.CreateAsync(source, zip, 100, null, CancellationToken.None));
        Assert.False(File.Exists(zip));
        Assert.False(File.Exists(zip + ".partial"));
        using var cancel = new CancellationTokenSource();
        cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CloudDrivePackage.CreateAsync(source, zip, 100000, null, cancel.Token));
        Assert.False(File.Exists(zip));
        Assert.False(File.Exists(zip + ".partial"));
    }

    [Fact]
    public void CorruptStoreIsNotSilentlyReplacedWithNewIdentity()
    {
        string path = Path.Combine(_directory, "transfers.json");
        File.WriteAllText(path, "{ broken");
        Assert.Throws<JsonException>(() => new CloudDriveStore(_directory).Load(_server.AbsoluteUri));
        Assert.Equal("{ broken", File.ReadAllText(path));
    }

    private CloudDriveItem CreateItem(int size)
    {
        string path = Path.Combine(_directory, "图像.bin");
        File.WriteAllBytes(path, Enumerable.Range(0, size).Select(i => (byte)(i % 251)).ToArray());
        return new CloudDriveItem { SourcePath = path, UploadPath = path, DisplayName = "图像.bin", RemoteName = "图像_abc.bin", Size = size };
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private sealed class UploadServer : HttpMessageHandler
    {
        public const string ShareId = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        private const string UploadId = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        public MemoryStream Data { get; } = new();
        public List<long> Offsets { get; } = [];
        public List<string> ClientIds { get; } = [];
        public bool LoseFinalResponse { get; set; }
        public bool CommitThenConflict { get; set; }
        public int SuggestedChunkSize { get; init; } = 65536;
        public Func<long, CancellationToken, Task>? BeforePatchBody { get; init; }
        public Func<long, CancellationToken, Task>? BeforePatchResponse { get; init; }
        public Func<int, CancellationToken, Task>? BeforeStatusResponse { get; init; }
        public int CreateCount { get; private set; }
        public int StatusCount { get; private set; }
        private string _name = "";
        private long _size;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.Null(request.Headers.Authorization);
            Assert.False(request.Headers.Contains("Cookie"));
            ClientIds.Add(request.Headers.GetValues("X-Transfer-Client").Single());
            if (request.RequestUri!.AbsolutePath == "/api/auth/session")
            {
                return new(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"anonymous_transfer_upload_enabled":true,"anonymous_transfer_max_bytes":2147483648}""", System.Text.Encoding.UTF8, "application/json")
                };
            }
            if (request.Method == HttpMethod.Post)
            {
                CreateCount++;
                using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
                _name = body.RootElement.GetProperty("filename").GetString()!;
                _size = body.RootElement.GetProperty("total_size").GetInt64();
            }
            else if (request.Method == HttpMethod.Patch)
            {
                long offset = long.Parse(request.Headers.GetValues("Upload-Offset").Single(), System.Globalization.CultureInfo.InvariantCulture);
                Assert.Equal(Data.Length, offset);
                Offsets.Add(offset);
                var content = request.Content!;
                if (BeforePatchBody != null) await BeforePatchBody(content.Headers.ContentLength!.Value, cancellationToken);
                await content.CopyToAsync(Data, cancellationToken);
                if (BeforePatchResponse != null) await BeforePatchResponse(offset, cancellationToken);
                if (CommitThenConflict) { CommitThenConflict = false; return new(HttpStatusCode.Conflict) { Content = new StringContent("{}") }; }
                if (LoseFinalResponse && Data.Length == _size) { LoseFinalResponse = false; throw new HttpRequestException("response lost"); }
            }
            else
            {
                StatusCount++;
                if (BeforeStatusResponse != null) await BeforeStatusResponse(StatusCount, cancellationToken);
            }
            bool complete = Data.Length == _size;
            return new(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    upload_id = UploadId, name = _name, total_size = _size, offset = Data.Length, complete,
                    share_url = complete ? "/transfer/share/" + ShareId : "", expires_at = complete ? DateTimeOffset.UtcNow.AddDays(1).ToString("O") : null,
                    chunk_size = SuggestedChunkSize
                }), System.Text.Encoding.UTF8, "application/json")
            };
        }
        protected override void Dispose(bool disposing) { if (disposing) Data.Dispose(); base.Dispose(disposing); }
    }
}
