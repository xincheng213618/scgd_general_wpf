using ColorVision.Rbac.CloudDrive;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Text.Json;

namespace ColorVision.UI.Tests;

public sealed class CloudDriveTransferTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "ColorVisionCloudDriveTests", Guid.NewGuid().ToString("N"));
    private readonly Uri _server = new("http://localhost:9998/");

    public CloudDriveTransferTests() => Directory.CreateDirectory(_directory);

    [Fact]
    public async Task RestartResumesPersistedAnonymousIdentityAndConfirmedOffset()
    {
        var store = new CloudDriveStore(_directory);
        var state = store.Load(_server.AbsoluteUri);
        var item = CreateItem(150000);
        state.Items.Add(item);
        using var handler = new UploadServer();
        using var http = new HttpClient(handler);
        var client = new TransferClient(http, _server, state.ClientId);
        using var cancel = new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.UploadAsync(item, () =>
        {
            store.Save(state);
            if (item.Offset == 65536) cancel.Cancel();
        }, null, cancel.Token));

        var restored = store.Load(_server.AbsoluteUri);
        Assert.Equal(state.ClientId, restored.ClientId);
        Assert.Equal(65536, restored.Items[0].Offset);
        var resumedClient = new TransferClient(http, _server, restored.ClientId);
        await resumedClient.UploadAsync(restored.Items[0], () => store.Save(restored), null, CancellationToken.None);
        Assert.True(restored.Items[0].CanShare);
        Assert.Equal(1, handler.CreateCount);
        Assert.Equal(new long[] { 0, 65536, 131072 }, handler.Offsets);
        Assert.Equal(File.ReadAllBytes(item.UploadPath), handler.Data.ToArray());
        Assert.Single(handler.ClientIds.Distinct());
        Assert.Equal(restored.Items[0].ShareUrl, store.Load(_server.AbsoluteUri).Items[0].ShareUrl);
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
                await request.Content!.CopyToAsync(Data, cancellationToken);
                if (CommitThenConflict) { CommitThenConflict = false; return new(HttpStatusCode.Conflict) { Content = new StringContent("{}") }; }
                if (LoseFinalResponse && Data.Length == _size) { LoseFinalResponse = false; throw new HttpRequestException("response lost"); }
            }
            else StatusCount++;
            bool complete = Data.Length == _size;
            return new(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    upload_id = UploadId, name = _name, total_size = _size, offset = Data.Length, complete,
                    share_url = complete ? "/transfer/share/" + ShareId : "", expires_at = complete ? DateTimeOffset.UtcNow.AddDays(1).ToString("O") : null,
                    chunk_size = 65536
                }), System.Text.Encoding.UTF8, "application/json")
            };
        }
        protected override void Dispose(bool disposing) { if (disposing) Data.Dispose(); base.Dispose(disposing); }
    }
}
