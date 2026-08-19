using ProjectARVRPro.Integration;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace ProjectARVRPro.Tests;

public class IntegrationDemoReleaseClientTests
{
    [Fact]
    public async Task GetLatestAsync_ReadsValidHttpMetadata()
    {
        byte[] package = Encoding.UTF8.GetBytes("demo-package");
        string json = $$"""
        {
          "schemaVersion": 1,
          "version": "1.0.0",
          "protocolVersion": "1.0",
          "verifiedProjectARVRProVersion": "1.1.8.10",
          "fileName": "ProjectARVRPro.IntegrationDemo-1.0.0.zip",
          "downloadPath": "/download/Tool/ProjectARVRPro.IntegrationDemo/ProjectARVRPro.IntegrationDemo-1.0.0.zip",
          "sizeBytes": {{package.Length}},
          "sha256": "{{Convert.ToHexString(SHA256.HashData(package))}}",
          "requiresDotNetFramework": "4.8",
          "publishedAtUtc": "2026-08-19T02:00:00Z",
          "releaseNotes": "Initial release"
        }
        """;
        using HttpClient httpClient = new(new StubHttpMessageHandler(json));
        var client = new IntegrationDemoReleaseClient(httpClient, "http://downloads.example:9998");

        IntegrationDemoReleaseInfo release = await client.GetLatestAsync();

        Assert.Equal("1.0.0", release.Version);
        Assert.Equal("http://downloads.example:9998/download/Tool/ProjectARVRPro.IntegrationDemo/latest.json", client.MetadataUrl);
        Assert.Equal(
            "http://downloads.example:9998/download/Tool/ProjectARVRPro.IntegrationDemo/ProjectARVRPro.IntegrationDemo-1.0.0.zip",
            client.GetDownloadUrl(release));
    }

    [Theory]
    [InlineData("http://attacker.example/demo.zip")]
    [InlineData("/download/Tool/../demo.zip")]
    [InlineData("/download/Tool/ProjectARVRPro.IntegrationDemo/other.zip")]
    [InlineData("/download/Tool/ProjectARVRPro.IntegrationDemo/archive/ProjectARVRPro.IntegrationDemo-1.0.0.zip")]
    public void TryValidate_RejectsDownloadOutsideDeclaredPackage(string downloadPath)
    {
        IntegrationDemoReleaseInfo release = CreateRelease(downloadPath: downloadPath);

        bool valid = IntegrationDemoReleaseClient.TryValidate(release, out string error);

        Assert.False(valid);
        Assert.NotEmpty(error);
    }

    [Fact]
    public void VerifyPackage_AcceptsMatchingSizeAndSha256()
    {
        string filePath = Path.Combine(Path.GetTempPath(), $"ProjectARVRPro.IntegrationDemo-{Guid.NewGuid():N}.zip");
        byte[] content = Encoding.UTF8.GetBytes("verified-demo-package");
        File.WriteAllBytes(filePath, content);
        try
        {
            IntegrationDemoReleaseInfo release = CreateRelease(
                sizeBytes: content.Length,
                sha256: Convert.ToHexString(SHA256.HashData(content)));

            bool valid = IntegrationDemoReleaseClient.VerifyPackage(filePath, release, out string error);

            Assert.True(valid, error);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void VerifyPackage_RejectsHashMismatch()
    {
        string filePath = Path.Combine(Path.GetTempPath(), $"ProjectARVRPro.IntegrationDemo-{Guid.NewGuid():N}.zip");
        byte[] content = Encoding.UTF8.GetBytes("tampered-demo-package");
        File.WriteAllBytes(filePath, content);
        try
        {
            IntegrationDemoReleaseInfo release = CreateRelease(sizeBytes: content.Length, sha256: new string('0', 64));

            bool valid = IntegrationDemoReleaseClient.VerifyPackage(filePath, release, out string error);

            Assert.False(valid);
            Assert.Contains("SHA-256", error);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    private static IntegrationDemoReleaseInfo CreateRelease(
        string downloadPath = "/download/Tool/ProjectARVRPro.IntegrationDemo/ProjectARVRPro.IntegrationDemo-1.0.0.zip",
        long sizeBytes = 10,
        string? sha256 = null)
    {
        return new IntegrationDemoReleaseInfo
        {
            SchemaVersion = 1,
            Version = "1.0.0",
            ProtocolVersion = "1.0",
            VerifiedProjectARVRProVersion = "1.1.8.10",
            FileName = "ProjectARVRPro.IntegrationDemo-1.0.0.zip",
            DownloadPath = downloadPath,
            SizeBytes = sizeBytes,
            Sha256 = sha256 ?? new string('A', 64),
            RequiresDotNetFramework = "4.8",
            PublishedAtUtc = DateTimeOffset.UtcNow
        };
    }

    private sealed class StubHttpMessageHandler(string responseJson) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                RequestMessage = request,
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            });
        }
    }
}
