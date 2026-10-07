using ColorVision.UI.Desktop.Download;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace ColorVision.UI.Tests;

public sealed class DownloadInfrastructureTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), nameof(DownloadInfrastructureTests), Guid.NewGuid().ToString("N"));
    public DownloadInfrastructureTests() => Directory.CreateDirectory(_root);
    private string Write(string name, string data) { string path = Path.Combine(_root, name); File.WriteAllText(path, data); return path; }
    private static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CopyFailureOrCancellationPreservesPreexistingDestination(bool cancel)
    {
        string source = Write("source.bin", "DATA");
        string destination = Write("destination.bin", "FOREIGN");
        using var cancellation = new CancellationTokenSource();
        Task copy = LocalFileCopyService.CopyAsync(source, destination, 4, _ => { if (cancel) cancellation.Cancel(); }, cancellation.Token);
        if (cancel) await Assert.ThrowsAnyAsync<OperationCanceledException>(() => copy);
        else await Assert.ThrowsAsync<IOException>(() => copy);
        Assert.Equal("FOREIGN", File.ReadAllText(destination));
        Assert.Empty(Directory.EnumerateFiles(_root, ".cvdownload-*.tmp"));
    }

    [Theory]
    [InlineData(3, false)]
    [InlineData(4, true)]
    public async Task SizeOrHashMismatchCannotPromoteCopy(long expectedBytes, bool badHash)
    {
        string source = Write("source.bin", "DATA");
        string destination = Path.Combine(_root, "destination.bin");
        await Assert.ThrowsAsync<InvalidDataException>(() => LocalFileCopyService.CopyAsync(source, destination, expectedBytes, _ => { },
            CancellationToken.None, badHash ? Hash("WRONG") : null));
        Assert.False(File.Exists(destination));
        Assert.Empty(Directory.EnumerateFiles(_root, ".cvdownload-*.tmp"));
    }

    [Theory]
    [InlineData("..%5Coutside.bin", "outside.bin")]
    [InlineData("C%3A%5CWindows%5Coutside.bin", "outside.bin")]
    [InlineData("CON", "_CON")]
    public void MagnetNamesStayInsideSelectedDirectory(string encoded, string expected)
    {
        string name = DownloadPathResolver.GetFileNameFromUrl("magnet:?xt=urn:btih:123&dn=" + encoded);
        string path = DownloadPathResolver.GetUniqueFilePath(_root, name);
        Assert.Equal(expected, Path.GetFileName(path));
        Assert.Equal(Path.GetFullPath(_root), Path.GetDirectoryName(path));
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("BBBB", false)]
    [InlineData("AAAA", true)]
    public async Task LocalReuseRequiresTrustedContentIdentity(string? digestContents, bool reusable)
    {
        string source = Write("local.bin", "AAAA");
        using var reuse = new DownloadReuseService(new Handler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent("BBBB"u8.ToArray()) };
            response.Headers.TryAddWithoutValidation("ETag", "\"same-length\"");
            if (digestContents != null) response.Headers.TryAddWithoutValidation("Content-Digest", "sha-256=:" + Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(digestContents))) + ":");
            return response;
        }));
        var result = await reuse.TryValidateLocalFileAgainstRemoteAsync("http://example.invalid/file", source, null, CancellationToken.None);
        Assert.Equal(reusable, result != null);
    }

    [Fact]
    public async Task CallerSuppliedHashAllowsReuseWithoutNetworkValidation()
    {
        string source = Write("local.bin", "AAAA");
        using var reuse = new DownloadReuseService(new Handler(_ => throw new InvalidOperationException("Network must not be needed.")));
        var result = await reuse.TryValidateLocalFileAgainstRemoteAsync("http://example.invalid/file", source, null, CancellationToken.None, Hash("AAAA"));
        Assert.Equal(Hash("AAAA"), result?.ContentSha256);
    }

    [Fact]
    public async Task CopyConcurrencyHonorsCapacityChangesAndCancellation()
    {
        var gate = new DownloadCopyGate();
        int capacity = 1;
        using var first = await gate.EnterAsync(() => capacity, CancellationToken.None);
        Task<IDisposable> second = gate.EnterAsync(() => capacity, CancellationToken.None);
        Assert.False(second.IsCompleted);
        capacity = 2;
        gate.NotifyCapacityChanged();
        using var secondLease = await second.WaitAsync(TimeSpan.FromSeconds(15));
        using var cancellation = new CancellationTokenSource();
        Task<IDisposable> blocked = gate.EnterAsync(() => capacity, cancellation.Token);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => blocked);
    }

    [Fact]
    public void LegacyAuthorizationMigrationPreservesRecordsAndEncryptsForCurrentWindowsUser()
    {
        string dbPath = Path.Combine(_root, "Downloads.db");
        var store = new DownloadTaskStore(dbPath);
        store.Initialize();
        string auth = "synthetic-user:synthetic-password";
        var entry = new DownloadEntry { Url = "http://example.invalid/old.bin", FileName = "old.bin", SavePath = Path.Combine(_root, "old.bin"),
            Status = (int)DownloadStatus.Paused, Authorization = Convert.ToBase64String(Encoding.UTF8.GetBytes(auth)) };
        entry.Id = store.Insert(entry);
        using (var legacyDb = DownloadTaskStore.CreateDbClient(dbPath))
            foreach (string column in new[] { "ExpectedSha256", "DownloadDirectory", "WorkingPath", "ContentSha256" })
                legacyDb.Ado.ExecuteCommand($"ALTER TABLE DownloadEntry DROP COLUMN {column}");
        store.Initialize();
        var migrated = Assert.Single(store.GetAllEntries());
        Assert.Equal(entry.Id, migrated.Id);
        Assert.Equal(entry.SavePath, migrated.SavePath);
        Assert.Equal((int)DownloadStatus.Paused, migrated.Status);
        Assert.Null(migrated.WorkingPath);
        Assert.Null(migrated.ContentSha256);
        Assert.StartsWith("dpapi:", migrated.Authorization!);
        Assert.Equal(auth, DownloadAuthorization.Decode(migrated.Authorization));
        Assert.Equal(auth, DownloadAuthorization.Decode(entry.Authorization));
        Assert.Null(DownloadAuthorization.Decode("dpapi:invalid"));
    }

    [Fact]
    public async Task RpcErrorsAreExceptionsAndBatchAuthenticatesEachChild()
    {
        JObject? request = null;
        using var rpc = new Aria2RpcClient(() => 6800, "synthetic-secret", new Handler(message =>
        {
            request = JObject.Parse(message.Content!.ReadAsStringAsync().GetAwaiter().GetResult());
            var response = request["method"]!.ToString() == "system.multicall"
                ? new JObject { ["result"] = new JArray(new JArray(new JObject { ["status"] = "active" }), new JObject { ["code"] = 1, ["message"] = "GID is not found." }) }
                : new JObject { ["error"] = new JObject { ["code"] = 1, ["message"] = "GID is not found." } };
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(response.ToString()) };
        }));
        await Assert.ThrowsAsync<Aria2RpcException>(() => rpc.CallAsync("aria2.unpause", "old-gid"));
        var statuses = await rpc.GetStatusesAsync(new[] { "first", "second" }, new[] { "status" }, CancellationToken.None);
        Assert.NotNull(statuses[0].Status);
        Assert.Null(statuses[1].Status);
        Assert.Contains("not found", statuses[1].Error!);
        Assert.Equal("system.multicall", request!["method"]!.ToString());
        var calls = (JArray)request["params"]![0]!;
        Assert.All(calls, call => Assert.Equal("token:synthetic-secret", call["params"]![0]!.ToString()));
    }

    [Fact]
    public void OccupiedRpcPortSelectsAlternativeWithoutTouchingExistingListener()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        using var daemon = new Aria2Daemon();
        int alternate = daemon.PreparePort(port);
        Assert.NotEqual(port, alternate);
        Assert.InRange(alternate, 1024, 65535);
        using var probe = new TcpClient();
        probe.Connect(IPAddress.Loopback, port);
        Assert.True(probe.Connected);
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { }
    }
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> action) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(action(request));
    }
}
