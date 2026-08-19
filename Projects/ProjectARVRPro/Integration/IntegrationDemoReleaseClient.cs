using ColorVision.Update;
using ColorVision.UI.Marketplace;
using Newtonsoft.Json;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;

namespace ProjectARVRPro.Integration;

internal sealed class IntegrationDemoReleaseInfo
{
    [JsonProperty("schemaVersion")]
    public int SchemaVersion { get; set; }

    [JsonProperty("version")]
    public string Version { get; set; } = string.Empty;

    [JsonProperty("protocolVersion")]
    public string ProtocolVersion { get; set; } = string.Empty;

    [JsonProperty("verifiedProjectARVRProVersion")]
    public string VerifiedProjectARVRProVersion { get; set; } = string.Empty;

    [JsonProperty("fileName")]
    public string FileName { get; set; } = string.Empty;

    [JsonProperty("downloadPath")]
    public string DownloadPath { get; set; } = string.Empty;

    [JsonProperty("sizeBytes")]
    public long SizeBytes { get; set; }

    [JsonProperty("sha256")]
    public string Sha256 { get; set; } = string.Empty;

    [JsonProperty("requiresDotNetFramework")]
    public string RequiresDotNetFramework { get; set; } = string.Empty;

    [JsonProperty("publishedAtUtc")]
    public DateTimeOffset PublishedAtUtc { get; set; }

    [JsonProperty("releaseNotes")]
    public string ReleaseNotes { get; set; } = string.Empty;
}

internal sealed class IntegrationDemoReleaseClient
{
    internal const string ToolDirectory = "Tool/ProjectARVRPro.IntegrationDemo";
    internal const string MetadataFileName = "latest.json";
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(8);
    private readonly HttpClient _httpClient;
    private readonly Uri _serviceRoot;

    public IntegrationDemoReleaseClient()
        : this(UpdateHttpClientProvider.GetClient(), MarketplaceConfig.ServiceBaseUrl)
    {
    }

    internal IntegrationDemoReleaseClient(HttpClient httpClient, string serviceBaseUrl)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        if (!Uri.TryCreate(serviceBaseUrl?.TrimEnd('/') + "/", UriKind.Absolute, out Uri? serviceRoot)
            || (serviceRoot.Scheme != Uri.UriSchemeHttp && serviceRoot.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("下载服务地址无效。", nameof(serviceBaseUrl));
        }

        _serviceRoot = serviceRoot;
    }

    internal string MetadataUrl => new Uri(_serviceRoot, $"download/{ToolDirectory}/{MetadataFileName}").ToString();

    internal async Task<IntegrationDemoReleaseInfo> GetLatestAsync(CancellationToken cancellationToken = default)
    {
        using CancellationTokenSource timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(RequestTimeout);
        using HttpResponseMessage response = await _httpClient.GetAsync(MetadataUrl, timeoutSource.Token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        string json = await response.Content.ReadAsStringAsync(timeoutSource.Token).ConfigureAwait(false);
        IntegrationDemoReleaseInfo? release = JsonConvert.DeserializeObject<IntegrationDemoReleaseInfo>(json);
        if (!TryValidate(release, out string error))
            throw new InvalidDataException(error);

        return release!;
    }

    internal string GetDownloadUrl(IntegrationDemoReleaseInfo release)
    {
        if (!TryValidate(release, out string error))
            throw new InvalidDataException(error);

        return new Uri(_serviceRoot, release.DownloadPath.TrimStart('/')).ToString();
    }

    internal static bool TryValidate(IntegrationDemoReleaseInfo? release, out string error)
    {
        if (release == null)
        {
            error = "Demo 发布元数据为空。";
            return false;
        }

        if (release.SchemaVersion != 1)
        {
            error = $"不支持的 Demo 发布元数据版本：{release.SchemaVersion}。";
            return false;
        }

        if (!System.Version.TryParse(release.Version, out _))
        {
            error = "Demo 版本号无效。";
            return false;
        }

        if (string.IsNullOrWhiteSpace(release.ProtocolVersion))
        {
            error = "Demo 协议版本为空。";
            return false;
        }

        if (string.IsNullOrWhiteSpace(release.FileName)
            || !release.FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(Path.GetFileName(release.FileName), release.FileName, StringComparison.Ordinal))
        {
            error = "Demo 包文件名无效。";
            return false;
        }

        string expectedDownloadPath = $"/download/{ToolDirectory}/{Uri.EscapeDataString(release.FileName)}";
        if (string.IsNullOrWhiteSpace(release.DownloadPath)
            || !string.Equals(release.DownloadPath, expectedDownloadPath, StringComparison.OrdinalIgnoreCase))
        {
            error = "Demo 下载路径不在允许的 Tool 目录中。";
            return false;
        }

        if (release.SizeBytes <= 0)
        {
            error = "Demo 包大小无效。";
            return false;
        }

        string hash = release.Sha256.Trim();
        if (hash.Length != 64 || hash.Any(character => !Uri.IsHexDigit(character)))
        {
            error = "Demo 包 SHA-256 无效。";
            return false;
        }

        error = string.Empty;
        return true;
    }

    internal static bool VerifyPackage(string filePath, IntegrationDemoReleaseInfo release, out string error)
    {
        if (!TryValidate(release, out error))
            return false;
        if (!File.Exists(filePath))
        {
            error = "下载文件不存在。";
            return false;
        }

        FileInfo fileInfo = new(filePath);
        if (fileInfo.Length != release.SizeBytes)
        {
            error = $"文件大小不匹配，应为 {release.SizeBytes:N0} 字节，实际为 {fileInfo.Length:N0} 字节。";
            return false;
        }

        using FileStream stream = File.OpenRead(filePath);
        string actualHash = Convert.ToHexString(SHA256.HashData(stream));
        if (!string.Equals(actualHash, release.Sha256.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            error = "文件 SHA-256 校验失败。";
            return false;
        }

        error = string.Empty;
        return true;
    }
}
