using ColorVision.UI.Marketplace;
using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Engine.Diagnostics.HostBenchmark;

internal sealed record HostBenchmarkShareProfile(Guid DeviceId, string ServerUrl, string DeviceName, byte[]? ProtectedApiKey);

internal sealed record HostBenchmarkShareResult(Guid RunId, double OverallScore, string ScoringVersion, bool PublishedComparison = false);

internal sealed record HostBenchmarkPublicReference(string Label, string? CpuModel, string[] GpuModels,
    long? TotalMemoryMiB, double OverallScore, double? LatestScore = null, double? PreviousScore = null);

internal sealed record HostBenchmarkComparisonSnapshot(string WorkloadVersion, string ScoringVersion,
    string ReferenceName, double ReferenceScore, HostBenchmarkPublicReference[] References,
    HostBenchmarkPublicReference[]? ExampleReferences = null);

internal sealed class HostBenchmarkSharing
{
    private const int ProfileSchemaVersion = 2;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly Uri MarketplaceServer = new(MarketplaceConfig.ServiceBaseUrl);
    private static readonly HttpClient HttpsClient = CreateClient(useProxy: true);
    private static readonly HttpClient DirectHttpClient = CreateClient(useProxy: false);
    private readonly string profilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ColorVision", "HostBenchmark", "sharing.json");

    internal static HostBenchmarkShareProfile DefaultProfile
    {
        get
        {
            var configured = MarketplaceConfig.ServiceBaseUrls.Prepend(MarketplaceConfig.UpdateSourceUrl);
            foreach (string address in configured)
            {
                if (TryGetServer(address, out Uri? server) && server != MarketplaceServer)
                    return new HostBenchmarkShareProfile(Guid.Empty, server.AbsoluteUri, string.Empty, null);
            }
            return new HostBenchmarkShareProfile(Guid.Empty, MarketplaceServer.AbsoluteUri, string.Empty, null);
        }
    }

    internal HostBenchmarkShareProfile Load() => WithProfileLock(() =>
    {
        ShareSettings settings = ReadSettings();
        HostBenchmarkShareProfile? saved = FindProfile(settings, settings.ActiveServerUrl);
        if (saved is not null) return saved;
        HostBenchmarkShareProfile configured = DefaultProfile;
        return FindProfile(settings, configured.ServerUrl) ?? configured;
    });

    internal static Uri? GetComparisonPageUri(HostBenchmarkShareProfile profile, Guid? sharedRunId = null)
    {
        if (!TryGetServer(profile.ServerUrl, out Uri? server)) return null;
        var page = new UriBuilder(server) { Path = "/benchmarks" };
        if (sharedRunId is { } id && id != Guid.Empty) page.Fragment = $"run={id:D}";
        return page.Uri;
    }

    internal async Task<HostBenchmarkComparisonSnapshot> LoadComparisonAsync(HostBenchmarkShareProfile profile,
        CancellationToken cancellationToken)
    {
        Uri server = GetServer(profile.ServerUrl);
        string path = "api/benchmarks/comparison";
        if (profile.DeviceId != Guid.Empty) path += $"?excludeDeviceId={profile.DeviceId:D}";

        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(server, path));
        var snapshot = await SendAsync<HostBenchmarkComparisonSnapshot>(request, cancellationToken).ConfigureAwait(false);
        if (snapshot is null || string.IsNullOrWhiteSpace(snapshot.WorkloadVersion) || string.IsNullOrWhiteSpace(snapshot.ScoringVersion)
            || string.IsNullOrWhiteSpace(snapshot.ReferenceName) || !IsValidScore(snapshot.ReferenceScore)
            || snapshot.References is null || snapshot.References.Any(item => !IsValidReference(item))
            || snapshot.ExampleReferences?.Any(item => !IsValidReference(item)) == true)
            throw new HostBenchmarkShareException("服务器返回的公开对照数据无效。");
        return snapshot;
    }

    internal async Task<HostBenchmarkShareResult> SubmitAsync(HostBenchmarkShareProfile profile, HostBenchmarkRun run,
        CancellationToken cancellationToken)
    {
        Uri server = GetServer(profile.ServerUrl);
        cancellationToken.ThrowIfCancellationRequested();
        HostBenchmarkShareProfile identity = WithProfileLock(() => GetOrCreateIdentity(profile, server));
        var payload = new
        {
            SchemaVersion = 1,
            identity.DeviceId,
            DeviceName = string.IsNullOrEmpty(profile.DeviceName) ? "ColorVision 主机" : profile.DeviceName,
            PublishComparison = true,
            Run = new
            {
                run.Id,
                run.WorkloadVersion,
                run.MeasuredAtUtc,
                run.AppStartedAtUtc,
                run.ApproximateSystemStartedAtUtc,
                run.AppVersion,
                run.Runtime,
                run.LogicalProcessors,
                run.AvailableMemoryMiBBefore,
                run.FrameWidth,
                run.FrameHeight,
                run.CpuModel,
                run.GpuModels,
                run.TotalMemoryMiB,
                Metrics = run.Metrics.Select(metric => new { metric.Id, metric.SamplesMilliseconds })
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(server, "api/benchmarks/runs"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Unprotect(identity.ProtectedApiKey, server));
        request.Content = JsonContent.Create(payload, options: JsonOptions);
        var result = await SendAsync<HostBenchmarkShareResult>(request, cancellationToken).ConfigureAwait(false);
        if (result is null || result.RunId != run.Id || !IsValidScore(result.OverallScore) || string.IsNullOrWhiteSpace(result.ScoringVersion))
            throw new HostBenchmarkShareException("服务器返回的成绩信息无效。");
        return result;
    }

    private static async Task<T?> SendAsync<T>(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        HttpClient client = request.RequestUri!.Scheme == Uri.UriSchemeHttps ? HttpsClient : DirectHttpClient;
        try
        {
            using HttpResponseMessage response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) throw new HostBenchmarkShareException(StatusMessage(response.StatusCode, request.Method));
            return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException)
        {
            throw new HostBenchmarkShareException("暂时无法连接性能榜服务，请稍后重试。");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new HostBenchmarkShareException("连接性能榜服务器超时，请稍后重试。");
        }
        catch (JsonException)
        {
            throw new HostBenchmarkShareException("性能榜服务器返回了无效数据。");
        }
    }

    private static bool IsValidScore(double score) => double.IsFinite(score) && score > 0;

    private static bool IsValidReference(HostBenchmarkPublicReference? item) =>
        item is not null && !string.IsNullOrWhiteSpace(item.Label) && item.GpuModels is not null && IsValidScore(item.OverallScore)
        && (item.LatestScore is not { } latest || IsValidScore(latest))
        && (item.PreviousScore is not { } previous || IsValidScore(previous));

    private static Uri GetServer(string serverUrl)
    {
        if (!TryGetServer(serverUrl, out Uri? server))
            throw new HostBenchmarkShareException("当前没有可用的性能榜地址。");
        return server;
    }

    private static bool TryGetServer(string serverUrl, [NotNullWhen(true)] out Uri? server)
    {
        if (!Uri.TryCreate(serverUrl.Trim(), UriKind.Absolute, out server)
            || server.UserInfo.Length > 0 || server.Host.Length == 0 || server.AbsolutePath != "/"
            || server.Query.Length > 0 || server.Fragment.Length > 0)
            return false;
        return server == MarketplaceServer || server.Scheme == Uri.UriSchemeHttps
            || (server.Scheme == Uri.UriSchemeHttp && IsLocalHost(server.IdnHost));
    }

    private static bool IsLocalHost(string host)
    {
        if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)) return true;
        if (!IPAddress.TryParse(host, out IPAddress? address)) return false;
        if (IPAddress.IsLoopback(address)) return true;
        if (address.AddressFamily != AddressFamily.InterNetwork) return false;
        byte[] bytes = address.GetAddressBytes();
        return bytes[0] == 10 || (bytes[0] == 172 && bytes[1] is >= 16 and <= 31) || (bytes[0] == 192 && bytes[1] == 168);
    }

    private static byte[] KeyEntropy(Uri server) => Encoding.UTF8.GetBytes("ColorVision.HostBenchmark.Share.v1\n" + server.AbsoluteUri);

    private static byte[] Protect(string apiKey, Uri server)
    {
        byte[] plaintext = Encoding.UTF8.GetBytes(apiKey);
        try { return ProtectedData.Protect(plaintext, KeyEntropy(server), DataProtectionScope.CurrentUser); }
        catch (CryptographicException) { throw new HostBenchmarkShareException("无法加密提交密钥。"); }
        finally { CryptographicOperations.ZeroMemory(plaintext); }
    }

    private static string Unprotect(byte[]? protectedApiKey, Uri server)
    {
        if (protectedApiKey is not { Length: > 0 }) throw InvalidIdentity();
        try
        {
            byte[] plaintext = ProtectedData.Unprotect(protectedApiKey, KeyEntropy(server), DataProtectionScope.CurrentUser);
            try
            {
                string token = Encoding.UTF8.GetString(plaintext);
                if (token.Length == 0 || token.Any(character => !char.IsAscii(character) || char.IsWhiteSpace(character) || char.IsControl(character)))
                    throw InvalidIdentity();
                return token;
            }
            finally { CryptographicOperations.ZeroMemory(plaintext); }
        }
        catch (CryptographicException) { throw InvalidIdentity(); }
    }

    private HostBenchmarkShareProfile GetOrCreateIdentity(HostBenchmarkShareProfile profile, Uri server)
    {
        ShareSettings settings = ReadSettings();
        HostBenchmarkShareProfile? existing = FindProfile(settings, server.AbsoluteUri);
        if (profile.DeviceId != Guid.Empty && existing?.DeviceId != profile.DeviceId) throw InvalidIdentity();
        if (existing is { DeviceId: var id } && id != Guid.Empty)
        {
            if (settings.ActiveServerUrl != existing.ServerUrl) WriteSettings(settings, existing);
            return existing;
        }

        Span<byte> random = stackalloc byte[32];
        RandomNumberGenerator.Fill(random);
        try
        {
            string token = "cvbench_" + Convert.ToBase64String(random).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            var identity = new HostBenchmarkShareProfile(Guid.NewGuid(), server.AbsoluteUri, profile.DeviceName, Protect(token, server));
            WriteSettings(settings, identity);
            return identity;
        }
        finally { CryptographicOperations.ZeroMemory(random); }
    }

    private ShareSettings ReadSettings()
    {
        try
        {
            using var file = File.OpenRead(profilePath);
            using JsonDocument document = JsonDocument.Parse(file);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) throw InvalidIdentity();
            if (!root.TryGetProperty("schemaVersion", out _))
            {
                var legacy = root.Deserialize<HostBenchmarkShareProfile>(JsonOptions);
                if (legacy is null || legacy.DeviceId == Guid.Empty || !ValidProfile(legacy)) throw InvalidIdentity();
                return new ShareSettings(ProfileSchemaVersion, legacy.ServerUrl, [legacy]);
            }

            var settings = root.Deserialize<ShareSettings>(JsonOptions);
            if (settings is null || settings.SchemaVersion != ProfileSchemaVersion || settings.Profiles is not { Length: > 0 }
                || settings.Profiles.Any(profile => profile is null || !ValidProfile(profile))
                || settings.Profiles.Select(profile => profile.ServerUrl).Distinct(StringComparer.Ordinal).Count() != settings.Profiles.Length
                || FindProfile(settings, settings.ActiveServerUrl) is null)
                throw InvalidIdentity();
            return settings;
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return new ShareSettings(ProfileSchemaVersion, DefaultProfile.ServerUrl, []);
        }
        catch (JsonException) { throw InvalidIdentity(); }
    }

    private static bool ValidProfile(HostBenchmarkShareProfile profile) =>
        profile.DeviceName is not null && !string.IsNullOrWhiteSpace(profile.ServerUrl)
        && TryGetServer(profile.ServerUrl, out Uri? server) && profile.ServerUrl == server.AbsoluteUri
        && ((profile.DeviceId != Guid.Empty) == (profile.ProtectedApiKey is { Length: > 0 }));

    private static HostBenchmarkShareProfile? FindProfile(ShareSettings settings, string serverUrl) =>
        settings.Profiles.FirstOrDefault(item => item.ServerUrl == serverUrl);

    private static HostBenchmarkShareException InvalidIdentity() => new("无法读取原有分享身份。历史成绩仍保存在本机。");

    private void WriteSettings(ShareSettings settings, HostBenchmarkShareProfile profile)
    {
        var updated = new ShareSettings(ProfileSchemaVersion, profile.ServerUrl,
            [.. settings.Profiles.Where(item => item.ServerUrl != profile.ServerUrl), profile]);
        Directory.CreateDirectory(Path.GetDirectoryName(profilePath)!);
        string temporary = profilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write))
                JsonSerializer.Serialize(stream, updated, JsonOptions);
            File.Move(temporary, profilePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private T WithProfileLock<T>(Func<T> operation)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(profilePath.ToUpperInvariant()));
        using var mutex = new Mutex(false, "Local\\ColorVision.HostBenchmark.Share." + Convert.ToHexString(hash));
        try
        {
            if (!mutex.WaitOne(TimeSpan.FromSeconds(4))) throw new HostBenchmarkShareException("性能榜分享设置正在使用，请稍后重试。");
        }
        catch (AbandonedMutexException) { }

        try { return operation(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new HostBenchmarkShareException("无法读取或保存本机的性能榜分享设置。");
        }
        finally { mutex.ReleaseMutex(); }
    }

    private static HttpClient CreateClient(bool useProxy) => new(new HttpClientHandler
    {
        AllowAutoRedirect = false,
        UseCookies = false,
        UseProxy = useProxy
    })
    {
        Timeout = TimeSpan.FromSeconds(20),
        MaxResponseContentBufferSize = 64 * 1024
    };

    private static string StatusMessage(HttpStatusCode status, HttpMethod method) => status switch
    {
        HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "服务器拒绝了分享身份或当前请求。",
        HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity => "服务器不接受该测试结果，请确认客户端和服务版本。",
        HttpStatusCode.Conflict => "服务器发现分享身份或记录冲突。",
        HttpStatusCode.NotFound => method == HttpMethod.Get ? "服务器未提供公开对照接口。" : "服务器未提供性能榜分享接口。",
        HttpStatusCode.TooManyRequests => "提交过于频繁，请稍后重试。",
        _ => $"性能榜服务器返回 HTTP {(int)status}。"
    };

    private sealed record ShareSettings(int SchemaVersion, string ActiveServerUrl, HostBenchmarkShareProfile[] Profiles);
}

internal sealed class HostBenchmarkShareException(string message) : Exception(message);
