using ColorVision.UI.Desktop.Download;
using ColorVision.UI.Desktop.Marketplace;
using ColorVision.UI.Marketplace;
using ColorVision.Update;
using Newtonsoft.Json;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ColorVision.UI.Tests;

// Protects the folder layout shared by Scripts/export_offline_updates.py and the client, and its SHA-256 gate.
[Collection(nameof(MarketplaceConfig))]
public sealed class OfflineUpdateFolderTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), nameof(OfflineUpdateFolderTests), Guid.NewGuid().ToString("N"));
    private readonly string _folder;
    private readonly Dictionary<string, string> _files = new();

    public OfflineUpdateFolderTests()
    {
        _folder = Path.Combine(_root, "离线 更新");
        WpfTestHost.Invoke(() => { });
    }

    public void Dispose()
    {
        MarketplaceConfig.SetServiceBaseUrls(new[] { MarketplaceConfig.DefaultServiceBaseUrl });
        UpdateSourceSelector.Invalidate();
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { }
    }

    private void Write(string relative, string content, bool listed = true)
    {
        string path = Path.Combine(_folder, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, Encoding.UTF8.GetBytes(content));
        if (listed)
            _files[relative] = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();
    }

    private void WriteIndex()
    {
        var index = new { format = 1, appLatestVersion = "9.9.9.9", plugins = new Dictionary<string, string> { ["DemoPlugin"] = "2.0.0.0" }, files = _files };
        File.WriteAllText(Path.Combine(_folder, OfflineUpdateFolder.IndexFileName), JsonConvert.SerializeObject(index));
    }

    [Fact]
    public async Task FolderSourceAnswersUpdateChecksAndCopiesVerifiedPackages()
    {
        Write("app/latest-version.json", """{"version":"9.9.9.9"}""");
        Write("app/updates/ColorVision-Update-[9.9.9.9].cvx", "incremental-package");
        Write("plugins/DemoPlugin/update.json", """{"PluginId":"DemoPlugin","LatestVersion":"2.0.0.0","Versions":[{"Version":"2.0.0.0"}]}""");
        Write("plugins/DemoPlugin/DemoPlugin-2.0.0.0.cvxp", "plugin-package");
        WriteIndex();

        // An unreachable HTTP source first: probing falls through to the folder.
        MarketplaceConfig.SetServiceBaseUrls(new[] { "http://127.0.0.1:1", _folder });
        UpdateSourceSelector.Invalidate();
        string source = await UpdateSourceSelector.GetUpdateSourceUrlAsync();
        Assert.StartsWith("file:", source);
        Assert.Equal("http://127.0.0.1:1", MarketplaceConfig.ServiceBaseUrl);
        Assert.True(WindowsNetworkState.CanReachUpdateService());

        LatestVersionCheckResult latest = await AutoUpdater.GetLatestVersionCheckResultAsync(AutoUpdater.UpdateUrl, forceRefresh: true);
        Assert.Equal(UpdateServerCheckStatus.Success, latest.Status);
        Assert.Equal(new Version(9, 9, 9, 9), latest.Version);

        MarketplaceClient client = new();
        Dictionary<string, string?> versions = await client.BatchVersionCheckAsync(new[] { "DemoPlugin", "Missing" });
        Assert.Equal("2.0.0.0", versions["DemoPlugin"]);
        Assert.Null(versions["Missing"]);
        MarketplacePluginDetail? detail = await client.GetPluginUpdateMetadataAsync("DemoPlugin", forceRefresh: true);
        Assert.Equal("2.0.0.0", Assert.Single(detail!.Versions).Version);

        var manager = new Aria2cDownloadManager(Path.Combine(_root, "Downloads.db"), new DownloadManagerConfig { DefaultDownloadPath = _root },
            new DownloadManagerLifecycleTests.FakeDaemon(), new DownloadManagerLifecycleTests.FakeRpc(), enablePolling: false, registerLifetime: false, directMode: () => true);
        try
        {
            string cache = Path.Combine(_root, "cache");
            DownloadTask good = manager.AddDownload(AutoUpdater.GetIncrementalPackageDownloadUrl(new Version(9, 9, 9, 9)), cache, fileName: "app.cvx");
            await manager.WaitForTaskIdleAsync(good).WaitAsync(TimeSpan.FromSeconds(15));
            Assert.Equal(DownloadStatus.Completed, good.Status);
            Assert.Equal("incremental-package", File.ReadAllText(good.SavePath));

            DownloadTask plugin = manager.AddVerifiedDownload(client.GetDownloadUrl("DemoPlugin", "2.0.0.0"), cache, fileName: "DemoPlugin-2.0.0.0.cvxp");
            await manager.WaitForTaskIdleAsync(plugin).WaitAsync(TimeSpan.FromSeconds(15));
            Assert.Equal(DownloadStatus.Completed, plugin.Status);

            // A file changed after export no longer matches the index and is never placed in the cache.
            File.WriteAllText(Path.Combine(_folder, "plugins", "DemoPlugin", "DemoPlugin-2.0.0.0.cvxp"), "tampered");
            DownloadTask tampered = manager.AddVerifiedDownload(client.GetDownloadUrl("DemoPlugin", "2.0.0.0"), cache, fileName: "tampered.cvxp");
            await manager.WaitForTaskIdleAsync(tampered).WaitAsync(TimeSpan.FromSeconds(15));
            Assert.Equal(DownloadStatus.Failed, tampered.Status);
            Assert.False(File.Exists(tampered.SavePath));
        }
        finally
        {
            manager.Dispose();
        }
    }

    [Fact]
    public void FolderWithoutIndexIsSkippedAndUnlistedFilesAreNotServed()
    {
        Write("app/latest-version.json", """{"version":"9.9.9.9"}""", listed: false);
        MarketplaceConfig.SetServiceBaseUrls(new[] { _folder });
        string url = MarketplaceConfig.BuildUpdateApiUrl("api/app/updates/9.9.9.9/download");

        Assert.True(OfflineUpdateFolder.TryResolveDownload(url, out string? path, out _, out string? error));
        Assert.Null(path);
        Assert.Contains(OfflineUpdateFolder.IndexFileName, error);

        WriteIndex();
        Assert.True(OfflineUpdateFolder.TryResolveDownload(url, out path, out _, out _));
        Assert.Null(path);
        Assert.False(OfflineUpdateFolder.TryResolveDownload("http://example.test/api/app/updates/9.9.9.9/download", out _, out _, out _));
    }
}
