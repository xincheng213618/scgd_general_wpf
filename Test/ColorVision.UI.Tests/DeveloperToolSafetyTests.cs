using ColorVision.Engine.Services.DeveloperTools;
using System.IO;
using System.Security.Cryptography;

namespace ColorVision.UI.Tests;

public sealed class DeveloperToolSafetyTests
{
    [Fact]
    public void PythonCatalogRejectsPreviewMismatchedFilenameAndUntrustedHost()
    {
        const string html = """
            https://www.python.org/ftp/python/3.14.7/python-3.14.7-amd64.exe
            https://www.python.org/ftp/python/3.14.6/python-3.14.6-amd64.exe
            https://www.python.org/ftp/python/3.13.15/python-3.13.15-amd64.exe
            https://www.python.org/ftp/python/3.15.0/python-3.15.0rc1-amd64.exe
            https://www.python.org/ftp/python/3.16.1/python-3.16.2-amd64.exe
            https://untrusted.example/ftp/python/3.99.1/python-3.99.1-amd64.exe
            """;
        var releases = DeveloperToolCatalogService.ParsePythonReleases(html);
        Assert.Equal([new Version(3, 14, 7), new Version(3, 13, 15)], releases.Select(item => item.Version));
        Assert.All(releases, release => Assert.Equal("www.python.org", release.OfficialUri.Host));
    }

    [Fact]
    public void NodeCatalogRequiresLtsAndWindowsMsiAndSelectsLatestPerMajor()
    {
        const string json = """
            [
              {"version":"v24.18.0","lts":"Krypton","npm":"11.0.0","files":["win-x64-msi"]},
              {"version":"v25.1.0","lts":false,"files":["win-x64-msi"]},
              {"version":"v24.19.0","lts":"Krypton","npm":"11.1.0","files":["win-x64-msi"]},
              {"version":"v26.0.0","lts":"Future","files":["linux-x64"]},
              {"version":"v22.20.0","lts":"Jod","npm":"10.0.0","files":["win-x64-msi"]}
            ]
            """;
        var releases = DeveloperToolCatalogService.ParseNodeReleases(json);
        Assert.Equal([new Version(24, 19, 0), new Version(22, 20, 0)], releases.Select(item => item.Version));
        Assert.Equal("11.1.0", releases[0].NpmVersion);
    }

    [Fact]
    public void OfficialChecksumLookupRequiresTheExactInstallerName()
    {
        string hash = new('a', 64);
        string manifest = $"{hash}  node-v24.19.0-x64.msi\n{new string('b', 64)}  node-v24.19.0-arm64.msi";
        Assert.Equal(hash.ToUpperInvariant(), DeveloperToolCatalogService.ParseNodeSha256(manifest, "node-v24.19.0-x64.msi"));
        Assert.Throws<InvalidDataException>(() => DeveloperToolCatalogService.ParseNodeSha256(manifest, "node-v24.18.0-x64.msi"));
        Assert.Throws<InvalidDataException>(() => DeveloperToolCatalogService.ParsePythonSha256("""
            {"messageSignature":{"messageDigest":{"algorithm":"SHA2_256","digest":"AA=="}}}
            """));
    }

    [Fact]
    public void AnUnsignedDownloadIsRejectedEvenWhenItsHashMatches()
    {
        WithTemporaryDirectory(directory =>
        {
            var release = new DeveloperToolRelease(DeveloperToolKind.Python, new Version(3, 14, 7));
            string file = Path.Combine(directory, release.FileName);
            File.WriteAllText(file, "This file must never execute.");
            string hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file)));
            Assert.Throws<InvalidDataException>(() => DeveloperToolInstallerService.PrepareInstaller(file, release, hash));
            // Rejection must release the handle, allowing a safe retry to replace the rejected download.
            File.WriteAllText(file, "retry");
        });
    }

    [Fact]
    public void TamperedAndRenamedDownloadsAreRejectedBeforeLaunch()
    {
        WithTemporaryDirectory(directory =>
        {
            var release = new DeveloperToolRelease(DeveloperToolKind.Python, new Version(3, 14, 7));
            string file = Path.Combine(directory, release.FileName);
            File.WriteAllText(file, "changed after download");
            Assert.Throws<InvalidDataException>(() => DeveloperToolInstallerService.PrepareInstaller(file, release, new string('0', 64)));
            var differentRelease = new DeveloperToolRelease(DeveloperToolKind.Python, new Version(3, 13, 15));
            string hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file)));
            Assert.Throws<InvalidDataException>(() => DeveloperToolInstallerService.PrepareInstaller(file, differentRelease, hash));
        });
    }

    [Fact]
    public void PathDiscoveryPreservesFirstMatchAndDoesNotExecuteFiles()
    {
        WithTemporaryDirectory(directory =>
        {
            string first = Directory.CreateDirectory(Path.Combine(directory, "first path")).FullName;
            string second = Directory.CreateDirectory(Path.Combine(directory, "second")).FullName;
            File.WriteAllText(Path.Combine(first, "python.exe"), "not an executable");
            File.WriteAllText(Path.Combine(second, "python.exe"), "not an executable");
            Assert.Equal(Path.Combine(first, "python.exe"), DeveloperToolDiscoveryService.ResolvePathCommand($".;relative;\"{first}\";{second}", "python.exe"));
            Assert.Equal("", DeveloperToolDiscoveryService.ResolvePathCommand(".;relative", "python.exe"));
        });
    }

    private static void WithTemporaryDirectory(Action<string> action)
    {
        string directory = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "ColorVisionDeveloperToolTests", Guid.NewGuid().ToString("N"))).FullName;
        try { action(directory); }
        finally { Directory.Delete(directory, recursive: true); }
    }
}
