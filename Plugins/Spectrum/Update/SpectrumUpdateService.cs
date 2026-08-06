#pragma warning disable CA1863 // Localized format strings can change with the active UI culture.
using System.Buffers;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Spectrum.Update;

internal static class SpectrumRuntime
{
    public static bool IsStandalone => string.Equals(
        Assembly.GetEntryAssembly()?.GetName().Name,
        Assembly.GetExecutingAssembly().GetName().Name,
        StringComparison.OrdinalIgnoreCase);

    public static Version CurrentVersion => Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0, 0);
}

internal sealed class SpectrumUpdateManifest
{
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; set; }

    [JsonPropertyName("productId")]
    public string ProductId { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("publishedAtUtc")]
    public string PublishedAtUtc { get; set; } = string.Empty;

    [JsonPropertyName("releaseNotes")]
    public string ReleaseNotes { get; set; } = string.Empty;

    [JsonPropertyName("package")]
    public SpectrumUpdatePackageManifest Package { get; set; } = new();
}

internal sealed class SpectrumUpdatePackageManifest
{
    [JsonPropertyName("fileName")]
    public string FileName { get; set; } = string.Empty;

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("sha256")]
    public string Sha256 { get; set; } = string.Empty;
}

internal sealed class SpectrumLatestResponse
{
    [JsonPropertyName("manifestBase64")]
    public string ManifestBase64 { get; set; } = string.Empty;

    [JsonPropertyName("signatureBase64")]
    public string SignatureBase64 { get; set; } = string.Empty;
}

internal sealed record SpectrumUpdateCheckResult(SpectrumUpdateManifest Manifest, Version Version, bool IsUpdateAvailable);

internal sealed record SpectrumDownloadedUpdate(SpectrumUpdateManifest Manifest, string PackagePath, string WorkDirectory);

internal sealed record SpectrumDownloadProgress(long BytesReceived, long TotalBytes)
{
    public double Percentage => TotalBytes <= 0 ? 0 : Math.Clamp(BytesReceived * 100d / TotalBytes, 0, 100);
}

internal sealed class SpectrumUpdateException(string message, Exception? innerException = null) : Exception(message, innerException);

internal static class SpectrumUpdateService
{
    private const string ProductId = "Spectrum";
    private const int ManifestSchemaVersion = 1;
    private const int MaximumLatestResponseBytes = 1024 * 1024;
    private const int MaximumManifestBytes = 256 * 1024;
    private const long MaximumPackageBytes = 4L * 1024 * 1024 * 1024;
    private const long MaximumExpandedBytes = 12L * 1024 * 1024 * 1024;
    private const int MaximumZipEntries = 100_000;
    private const string PublicKeySpkiBase64 = "MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEA4gY405ZempwK2pWckyGjsSyoQKoE/HYkWzl83sylcObMxPRP4tBugwOxYUjiO05Cw9Bhj00/sTKXLpcUVpVper9s6l7LopF6IB1ubbrcEvKjSqvomyaaP7Wtc7eEI3H5qWKtK+GB9Y0wAQ3VtHp6yuK7x06MGRQrW6cRg+yqRd06NWHjNjCMZq0EmoGLKydTlRO66dJkddKCxnemyfS/w8ikni0xexeVp0nOSHDBYL/tkUz5Es3q75GOgcLbge5K1xE234BHn3lmL8Fewu7WsVHQAvxP5+pENPxFVAMUuIYvQj0r+NXcu3f3oiKrkBbGTHUV/Y/lgdVdv36/4NTLPQIDAQAB";
    private static readonly Uri ApiBaseUri = new("http://xc213618.ddns.me:9998/", UriKind.Absolute);
    private static readonly Uri LatestUri = new(ApiBaseUri, "api/tool/spectrum/latest");
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };
    private static readonly HttpClient HttpClient = CreateHttpClient();

    public static async Task<SpectrumUpdateCheckResult> CheckLatestAsync(CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, LatestUri);
        using HttpResponseMessage response = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new SpectrumUpdateException(UpdateText.Get("UpdateNoPublishedVersion", "服务器暂未发布 Spectrum 更新。"));
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new SpectrumUpdateException(string.Format(
                CultureInfo.CurrentCulture,
                UpdateText.Get("UpdateServerError", "更新服务器返回错误 ({0})。"),
                (int)response.StatusCode));
        }

        byte[] wrapperBytes = await ReadLimitedAsync(response.Content, MaximumLatestResponseBytes, cancellationToken).ConfigureAwait(false);
        SpectrumLatestResponse wrapper;
        try
        {
            wrapper = JsonSerializer.Deserialize<SpectrumLatestResponse>(wrapperBytes, JsonOptions)
                ?? throw new JsonException("Empty update response.");
        }
        catch (JsonException ex)
        {
            throw new SpectrumUpdateException(UpdateText.Get("UpdateInvalidResponse", "更新服务器响应无效。"), ex);
        }

        byte[] manifestBytes;
        byte[] signatureBytes;
        try
        {
            if (string.IsNullOrWhiteSpace(wrapper.ManifestBase64) || string.IsNullOrWhiteSpace(wrapper.SignatureBase64))
            {
                throw new FormatException("Missing signed update data.");
            }

            manifestBytes = Convert.FromBase64String(wrapper.ManifestBase64);
            signatureBytes = Convert.FromBase64String(wrapper.SignatureBase64);
        }
        catch (FormatException ex)
        {
            throw new SpectrumUpdateException(UpdateText.Get("UpdateInvalidSignatureEnvelope", "更新签名数据格式无效。"), ex);
        }

        if (manifestBytes.Length is 0 or > MaximumManifestBytes || signatureBytes.Length == 0)
        {
            throw new SpectrumUpdateException(UpdateText.Get("UpdateInvalidSignatureEnvelope", "更新签名数据格式无效。"));
        }

        VerifyManifestSignature(manifestBytes, signatureBytes);
        SpectrumUpdateManifest manifest = DeserializeAndValidateManifest(manifestBytes);
        Version version = ParseFourPartVersion(manifest.Version);
        return new SpectrumUpdateCheckResult(manifest, version, version > SpectrumRuntime.CurrentVersion);
    }

    public static async Task<SpectrumDownloadedUpdate> DownloadAndValidateAsync(
        SpectrumUpdateManifest manifest,
        IProgress<SpectrumDownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        ValidateManifest(manifest);
        string updateRoot = GetUpdateRoot();
        Directory.CreateDirectory(updateRoot);
        string workDirectory = Path.Combine(updateRoot, $"{manifest.Version}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workDirectory);
        string packagePath = Path.Combine(workDirectory, manifest.Package.FileName);

        try
        {
            Uri downloadUri = new(ApiBaseUri, "api/tool/spectrum/download/" + Uri.EscapeDataString(manifest.Version));
            using HttpRequestMessage request = new(HttpMethod.Get, downloadUri);
            using HttpResponseMessage response = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                throw new SpectrumUpdateException(string.Format(
                    CultureInfo.CurrentCulture,
                    UpdateText.Get("UpdateDownloadServerError", "下载更新包失败 ({0})。"),
                    (int)response.StatusCode));
            }

            long? contentLength = response.Content.Headers.ContentLength;
            if (contentLength.HasValue && contentLength.Value != manifest.Package.Size)
            {
                throw new SpectrumUpdateException(UpdateText.Get("UpdateLengthMismatch", "更新包长度与已签名清单不一致。"));
            }

            await using Stream source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using IncrementalHash sha256 = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            byte[] buffer = ArrayPool<byte>.Shared.Rent(1024 * 128);
            long totalBytes = 0;
            try
            {
                await using FileStream destination = new(packagePath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1024 * 128, FileOptions.Asynchronous | FileOptions.SequentialScan);
                while (true)
                {
                    int read = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false);
                    if (read == 0)
                    {
                        break;
                    }

                    totalBytes += read;
                    if (totalBytes > manifest.Package.Size)
                    {
                        throw new SpectrumUpdateException(UpdateText.Get("UpdateLengthMismatch", "更新包长度与已签名清单不一致。"));
                    }

                    sha256.AppendData(buffer, 0, read);
                    await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                    progress?.Report(new SpectrumDownloadProgress(totalBytes, manifest.Package.Size));
                }

                await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
                if (totalBytes != manifest.Package.Size || destination.Length != manifest.Package.Size)
                {
                    throw new SpectrumUpdateException(UpdateText.Get("UpdateLengthMismatch", "更新包长度与已签名清单不一致。"));
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }

            string actualSha256 = Convert.ToHexString(sha256.GetHashAndReset());
            if (!actualSha256.Equals(manifest.Package.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new SpectrumUpdateException(UpdateText.Get("UpdateHashMismatch", "更新包 SHA-256 校验失败。"));
            }

            progress?.Report(new SpectrumDownloadProgress(totalBytes, manifest.Package.Size));
            await ValidateZipPackageAsync(packagePath, manifest, workDirectory, cancellationToken).ConfigureAwait(false);
            return new SpectrumDownloadedUpdate(manifest, packagePath, workDirectory);
        }
        catch
        {
            TryDeleteDirectory(workDirectory);
            throw;
        }
    }

    public static bool TryLaunchInstaller(SpectrumDownloadedUpdate update, out string? errorMessage)
    {
        errorMessage = null;
        try
        {
            string installDirectory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(AppContext.BaseDirectory));
            if (Directory.GetParent(installDirectory) is null)
            {
                throw new SpectrumUpdateException(UpdateText.Get(
                    "UpdateUnsafeInstallPath",
                    "安装路径包含更新命令无法安全处理的字符。"));
            }

            string workDirectory = Path.GetFullPath(update.WorkDirectory);
            if (IsSameOrDescendantPath(workDirectory, installDirectory)
                || IsSameOrDescendantPath(installDirectory, workDirectory))
            {
                throw new SpectrumUpdateException(UpdateText.Get(
                    "UpdateUnsafeInstallPath",
                    "安装路径包含更新命令无法安全处理的字符。"));
            }

            if (!CanWriteDirectory(installDirectory))
            {
                throw new SpectrumUpdateException(UpdateText.Get(
                    "UpdateInstallDirectoryNotWritable",
                    "当前目录不可写。请将 Spectrum 完整解压到当前用户可写目录后再使用自动更新。"));
            }

            string stagingPath = Path.Combine(workDirectory, "staging");
            string backupPath = Path.Combine(workDirectory, "backup");
            string logPath = Path.Combine(workDirectory, "update.log");
            string scriptPath = CreateUpdateScript(update);
            ProcessStartInfo startInfo = new(Path.Combine(Environment.SystemDirectory, "cmd.exe"))
            {
                Arguments = $"/d /c \"\"{scriptPath}\"\"",
                UseShellExecute = false,
                WorkingDirectory = update.WorkDirectory
            };
            startInfo.Environment["SPECTRUM_INSTALL"] = installDirectory;
            startInfo.Environment["SPECTRUM_PACKAGE"] = update.PackagePath;
            startInfo.Environment["SPECTRUM_WORK"] = workDirectory;
            startInfo.Environment["SPECTRUM_STAGING"] = stagingPath;
            startInfo.Environment["SPECTRUM_BACKUP"] = backupPath;
            startInfo.Environment["SPECTRUM_LOG"] = logPath;
            startInfo.Environment["SPECTRUM_PID"] = Environment.ProcessId.ToString(CultureInfo.InvariantCulture);

            Process.Start(startInfo);
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = string.Format(
                CultureInfo.CurrentCulture,
                UpdateText.Get("UpdateLaunchFailed", "无法启动更新命令：{0}"),
                ex.Message);
            return false;
        }
    }

    public static void DiscardDownloadedUpdate(SpectrumDownloadedUpdate update) => TryDeleteDirectory(update.WorkDirectory);

    private static HttpClient CreateHttpClient()
    {
        HttpClient client = new()
        {
            Timeout = TimeSpan.FromMinutes(30)
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"Spectrum/{SpectrumRuntime.CurrentVersion}");
        return client;
    }

    private static async Task<byte[]> ReadLimitedAsync(HttpContent content, int maximumBytes, CancellationToken cancellationToken)
    {
        if (content.Headers.ContentLength is long contentLength && contentLength > maximumBytes)
        {
            throw new SpectrumUpdateException(UpdateText.Get("UpdateInvalidResponse", "更新服务器响应无效。"));
        }

        await using Stream stream = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using MemoryStream output = new();
        byte[] buffer = ArrayPool<byte>.Shared.Rent(16 * 1024);
        try
        {
            while (true)
            {
                int read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    return output.ToArray();
                }

                if (output.Length + read > maximumBytes)
                {
                    throw new SpectrumUpdateException(UpdateText.Get("UpdateInvalidResponse", "更新服务器响应无效。"));
                }

                output.Write(buffer, 0, read);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static void VerifyManifestSignature(byte[] manifestBytes, byte[] signatureBytes)
    {
        try
        {
            byte[] publicKey = Convert.FromBase64String(PublicKeySpkiBase64);
            using RSA rsa = RSA.Create();
            rsa.ImportSubjectPublicKeyInfo(publicKey, out int bytesRead);
            if (bytesRead != publicKey.Length || !rsa.VerifyData(manifestBytes, signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))
            {
                throw new SpectrumUpdateException(UpdateText.Get("UpdateSignatureInvalid", "更新清单签名验证失败，已拒绝此更新。"));
            }
        }
        catch (SpectrumUpdateException)
        {
            throw;
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            throw new SpectrumUpdateException(UpdateText.Get("UpdateSignatureInvalid", "更新清单签名验证失败，已拒绝此更新。"), ex);
        }
    }

    private static SpectrumUpdateManifest DeserializeAndValidateManifest(byte[] manifestBytes)
    {
        try
        {
            _ = StrictUtf8.GetString(manifestBytes);
            SpectrumUpdateManifest manifest = JsonSerializer.Deserialize<SpectrumUpdateManifest>(manifestBytes, JsonOptions)
                ?? throw new JsonException("Empty update manifest.");
            ValidateManifest(manifest);
            return manifest;
        }
        catch (SpectrumUpdateException)
        {
            throw;
        }
        catch (Exception ex) when (ex is JsonException or DecoderFallbackException)
        {
            throw new SpectrumUpdateException(UpdateText.Get("UpdateManifestInvalid", "更新清单内容无效。"), ex);
        }
    }

    private static void ValidateManifest(SpectrumUpdateManifest manifest)
    {
        if (manifest == null
            || manifest.SchemaVersion != ManifestSchemaVersion
            || !string.Equals(manifest.ProductId, ProductId, StringComparison.Ordinal))
        {
            throw new SpectrumUpdateException(UpdateText.Get("UpdateManifestProductMismatch", "更新清单不属于此 Spectrum 客户端。"));
        }

        if (string.IsNullOrWhiteSpace(manifest.Version)
            || string.IsNullOrWhiteSpace(manifest.PublishedAtUtc)
            || manifest.ReleaseNotes == null
            || manifest.Package == null)
        {
            throw new SpectrumUpdateException(UpdateText.Get("UpdateManifestInvalid", "更新清单内容无效。"));
        }

        _ = ParseFourPartVersion(manifest.Version);
        if (!DateTimeOffset.TryParse(manifest.PublishedAtUtc, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset publishedAt)
            || publishedAt.Offset != TimeSpan.Zero)
        {
            throw new SpectrumUpdateException(UpdateText.Get("UpdateManifestInvalid", "更新清单内容无效。"));
        }

        if (manifest.ReleaseNotes.Length > MaximumManifestBytes
            || manifest.Package.Size <= 0
            || manifest.Package.Size > MaximumPackageBytes
            || manifest.Package.Sha256 == null
            || manifest.Package.FileName == null
            || manifest.Package.Sha256.Length != 64
            || !manifest.Package.Sha256.All(Uri.IsHexDigit)
            || !IsSafePackageFileName(manifest.Package.FileName))
        {
            throw new SpectrumUpdateException(UpdateText.Get("UpdateManifestInvalid", "更新清单内容无效。"));
        }
    }

    private static Version ParseFourPartVersion(string version)
    {
        string[] parts = version.Split('.');
        if (parts.Length != 4
            || parts.Any(part => part.Length == 0 || part.Length > 9 || !part.All(char.IsAsciiDigit))
            || !Version.TryParse(version, out Version? parsed)
            || parsed.Major < 0
            || parsed.Minor < 0
            || parsed.Build < 0
            || parsed.Revision < 0)
        {
            throw new SpectrumUpdateException(UpdateText.Get("UpdateManifestVersionInvalid", "更新版本号必须是四段数字版本。"));
        }

        return parsed;
    }

    private static bool IsSafePackageFileName(string fileName)
    {
        return !string.IsNullOrWhiteSpace(fileName)
            && fileName.Length <= 180
            && fileName.Equals(Path.GetFileName(fileName), StringComparison.Ordinal)
            && fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
            && fileName.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
    }

    private static async Task ValidateZipPackageAsync(
        string packagePath,
        SpectrumUpdateManifest manifest,
        string workDirectory,
        CancellationToken cancellationToken)
    {
        string validationDirectory = Path.Combine(workDirectory, "validation");
        Directory.CreateDirectory(validationDirectory);
        string validationRoot = Path.GetFullPath(validationDirectory) + Path.DirectorySeparatorChar;
        string validationExePath = Path.Combine(validationDirectory, "Spectrum.exe");
        string validationDllPath = Path.Combine(validationDirectory, "Spectrum.dll");
        HashSet<string> entryNames = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> requiredFiles = new(StringComparer.OrdinalIgnoreCase)
        {
            "Spectrum.exe",
            "Spectrum.dll",
            "Spectrum.deps.json",
            "Spectrum.runtimeconfig.json"
        };
        long expandedBytes = 0;

        try
        {
            await using FileStream packageStream = new(packagePath, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 128, FileOptions.Asynchronous | FileOptions.SequentialScan);
            using ZipArchive archive = new(packageStream, ZipArchiveMode.Read, leaveOpen: false);
            if (archive.Entries.Count is 0 or > MaximumZipEntries)
            {
                throw new SpectrumUpdateException(UpdateText.Get("UpdateZipInvalid", "更新 ZIP 结构无效。"));
            }

            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string normalizedName = ValidateZipEntryPath(entry.FullName, validationRoot);
                if (!entryNames.Add(normalizedName.TrimEnd('/')))
                {
                    throw new SpectrumUpdateException(UpdateText.Get("UpdateZipDuplicatePath", "更新 ZIP 包含重复路径。"));
                }

                if (IsLinkOrReparsePoint(entry))
                {
                    throw new SpectrumUpdateException(UpdateText.Get("UpdateZipUnsafePath", "更新 ZIP 包含不安全路径。"));
                }

                if (entry.Name.Length == 0)
                {
                    continue;
                }

                expandedBytes = checked(expandedBytes + entry.Length);
                if (expandedBytes > MaximumExpandedBytes)
                {
                    throw new SpectrumUpdateException(UpdateText.Get("UpdateZipTooLarge", "更新 ZIP 解压后大小异常。"));
                }

                requiredFiles.Remove(normalizedName);
                string? validationFile = normalizedName.Equals("Spectrum.exe", StringComparison.OrdinalIgnoreCase)
                    ? validationExePath
                    : normalizedName.Equals("Spectrum.dll", StringComparison.OrdinalIgnoreCase)
                        ? validationDllPath
                        : null;

                await using Stream entryStream = entry.Open();
                await using FileStream? validationOutput = validationFile == null
                    ? null
                    : new FileStream(validationFile, FileMode.CreateNew, FileAccess.Write, FileShare.None, 64 * 1024, FileOptions.Asynchronous);
                Crc32Accumulator crc32 = new();
                byte[] buffer = ArrayPool<byte>.Shared.Rent(64 * 1024);
                long entryBytes = 0;
                try
                {
                    while (true)
                    {
                        int read = await entryStream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false);
                        if (read == 0)
                        {
                            break;
                        }

                        entryBytes += read;
                        crc32.Append(buffer.AsSpan(0, read));
                        if (validationOutput != null)
                        {
                            await validationOutput.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                        }
                    }
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }

                if (entryBytes != entry.Length || crc32.Value != entry.Crc32)
                {
                    throw new SpectrumUpdateException(UpdateText.Get("UpdateZipCrcFailed", "更新 ZIP CRC 校验失败。"));
                }
            }
        }
        catch (SpectrumUpdateException)
        {
            throw;
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or OverflowException)
        {
            throw new SpectrumUpdateException(UpdateText.Get("UpdateZipInvalid", "更新 ZIP 结构无效。"), ex);
        }

        if (requiredFiles.Count > 0)
        {
            throw new SpectrumUpdateException(string.Format(
                CultureInfo.CurrentCulture,
                UpdateText.Get("UpdateMissingCriticalFiles", "更新包缺少关键文件：{0}"),
                string.Join(", ", requiredFiles.Order())));
        }

        Version manifestVersion = ParseFourPartVersion(manifest.Version);
        ValidateBinaryVersion(validationExePath, manifestVersion, "Spectrum.exe");
        ValidateBinaryVersion(validationDllPath, manifestVersion, "Spectrum.dll");
        TryDeleteDirectory(validationDirectory);
    }

    private static string ValidateZipEntryPath(string entryName, string validationRoot)
    {
        if (string.IsNullOrWhiteSpace(entryName)
            || entryName.Length > 512
            || entryName.Contains('\\')
            || entryName.StartsWith('/')
            || entryName.Contains(':')
            || entryName.Contains('\0')
            || entryName.Contains("//", StringComparison.Ordinal))
        {
            throw new SpectrumUpdateException(UpdateText.Get("UpdateZipUnsafePath", "更新 ZIP 包含不安全路径。"));
        }

        string normalizedName = entryName.TrimEnd('/');
        string[] segments = normalizedName.Split('/');
        if (segments.Length == 0 || segments.Any(IsUnsafeZipSegment))
        {
            throw new SpectrumUpdateException(UpdateText.Get("UpdateZipUnsafePath", "更新 ZIP 包含不安全路径。"));
        }

        string candidate = Path.GetFullPath(Path.Combine(validationRoot, normalizedName.Replace('/', Path.DirectorySeparatorChar)));
        if (!candidate.StartsWith(validationRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new SpectrumUpdateException(UpdateText.Get("UpdateZipUnsafePath", "更新 ZIP 包含不安全路径。"));
        }

        return Path.GetRelativePath(validationRoot, candidate).Replace('\\', '/');
    }

    private static bool IsUnsafeZipSegment(string segment)
    {
        if (segment.Length is 0 or > 180
            || segment is "." or ".."
            || segment.EndsWith(' ')
            || segment.EndsWith('.')
            || segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            return true;
        }

        string deviceName = segment.Split('.')[0];
        return deviceName.Equals("CON", StringComparison.OrdinalIgnoreCase)
            || deviceName.Equals("PRN", StringComparison.OrdinalIgnoreCase)
            || deviceName.Equals("AUX", StringComparison.OrdinalIgnoreCase)
            || deviceName.Equals("NUL", StringComparison.OrdinalIgnoreCase)
            || (deviceName.Length == 4
                && (deviceName.StartsWith("COM", StringComparison.OrdinalIgnoreCase) || deviceName.StartsWith("LPT", StringComparison.OrdinalIgnoreCase))
                && deviceName[3] is >= '1' and <= '9');
    }

    private static bool IsLinkOrReparsePoint(ZipArchiveEntry entry)
    {
        int unixFileType = (entry.ExternalAttributes >> 16) & 0xF000;
        bool isUnixLink = unixFileType == 0xA000;
        bool isWindowsReparsePoint = (entry.ExternalAttributes & (int)FileAttributes.ReparsePoint) != 0;
        return isUnixLink || isWindowsReparsePoint;
    }

    private static void ValidateBinaryVersion(string path, Version expectedVersion, string displayName)
    {
        FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(path);
        if (!Version.TryParse(versionInfo.FileVersion, out Version? actualVersion) || actualVersion != expectedVersion)
        {
            throw new SpectrumUpdateException(string.Format(
                CultureInfo.CurrentCulture,
                UpdateText.Get("UpdateBinaryVersionMismatch", "更新包中的 {0} 版本与清单不一致。"),
                displayName));
        }
    }

    private static string GetUpdateRoot()
    {
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string installDirectory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(AppContext.BaseDirectory));
        string[] candidates =
        {
            string.IsNullOrWhiteSpace(localAppData) ? string.Empty : Path.Combine(localAppData, "Spectrum", "Updates"),
            Path.Combine(Path.GetTempPath(), "Spectrum", "Updates"),
            string.IsNullOrWhiteSpace(userProfile) ? string.Empty : Path.Combine(userProfile, ".spectrum-updates")
        };

        foreach (string candidatePath in candidates.Where(path => !string.IsNullOrWhiteSpace(path)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            string candidate = Path.GetFullPath(candidatePath);
            if (!IsSameOrDescendantPath(candidate, installDirectory)
                && !IsSameOrDescendantPath(installDirectory, candidate))
            {
                return candidate;
            }
        }

        throw new SpectrumUpdateException(UpdateText.Get(
            "UpdateUnsafeInstallPath",
            "安装路径包含更新命令无法安全处理的字符。"));
    }

    private static bool IsSameOrDescendantPath(string candidatePath, string parentPath)
    {
        string candidate = Path.TrimEndingDirectorySeparator(Path.GetFullPath(candidatePath));
        string parent = Path.TrimEndingDirectorySeparator(Path.GetFullPath(parentPath));
        return candidate.Equals(parent, StringComparison.OrdinalIgnoreCase)
            || candidate.StartsWith(parent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static bool CanWriteDirectory(string directory)
    {
        string probePath = Path.Combine(directory, $".spectrum-update-write-{Guid.NewGuid():N}.tmp");
        try
        {
            using (new FileStream(probePath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose))
            {
            }
            return true;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            return false;
        }
        finally
        {
            try
            {
                File.Delete(probePath);
            }
            catch
            {
            }
        }
    }

    private static string CreateUpdateScript(SpectrumDownloadedUpdate update)
    {
        string scriptPath = Path.Combine(update.WorkDirectory, "update.cmd");
        const string script = """
            @echo off
            setlocal EnableExtensions DisableDelayedExpansion
            set "SPECTRUM_POWERSHELL=%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe"
            set "RESTORE_NEEDED=0"

            >"%SPECTRUM_LOG%" echo [%date% %time%] Spectrum update started
            "%SPECTRUM_POWERSHELL%" -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -Command "Wait-Process -Id $env:SPECTRUM_PID -ErrorAction SilentlyContinue" >>"%SPECTRUM_LOG%" 2>&1

            if exist "%SPECTRUM_STAGING%" rmdir /s /q "%SPECTRUM_STAGING%"
            mkdir "%SPECTRUM_STAGING%" >>"%SPECTRUM_LOG%" 2>&1
            if errorlevel 1 goto :failed

            "%SPECTRUM_POWERSHELL%" -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -Command "Expand-Archive -LiteralPath $env:SPECTRUM_PACKAGE -DestinationPath $env:SPECTRUM_STAGING -Force" >>"%SPECTRUM_LOG%" 2>&1
            if errorlevel 1 goto :failed
            if not exist "%SPECTRUM_STAGING%\Spectrum.exe" goto :failed
            if not exist "%SPECTRUM_STAGING%\Spectrum.dll" goto :failed
            if not exist "%SPECTRUM_STAGING%\Spectrum.deps.json" goto :failed
            if not exist "%SPECTRUM_STAGING%\Spectrum.runtimeconfig.json" goto :failed

            if exist "%SPECTRUM_BACKUP%" rmdir /s /q "%SPECTRUM_BACKUP%"
            robocopy "%SPECTRUM_INSTALL%" "%SPECTRUM_BACKUP%" /MIR /COPY:DAT /DCOPY:DAT /R:1 /W:1 /XJ /NP >>"%SPECTRUM_LOG%" 2>&1
            if errorlevel 8 goto :failed
            set "RESTORE_NEEDED=1"

            robocopy "%SPECTRUM_STAGING%" "%SPECTRUM_INSTALL%" /E /COPY:DAT /DCOPY:DAT /R:2 /W:1 /XJ /NP >>"%SPECTRUM_LOG%" 2>&1
            if errorlevel 8 goto :restore
            if not exist "%SPECTRUM_INSTALL%\Spectrum.exe" goto :restore
            set "RESTORE_NEEDED=0"

            >>"%SPECTRUM_LOG%" echo [%date% %time%] Spectrum update completed
            start "" "%SPECTRUM_INSTALL%\Spectrum.exe"
            cd /d "%TEMP%"
            start "" /b "%SPECTRUM_POWERSHELL%" -NoLogo -NoProfile -NonInteractive -WindowStyle Hidden -ExecutionPolicy Bypass -Command "Start-Sleep -Seconds 2; Remove-Item -LiteralPath $env:SPECTRUM_WORK -Recurse -Force -ErrorAction SilentlyContinue" >nul 2>&1
            exit /b 0

            :restore
            >>"%SPECTRUM_LOG%" echo [%date% %time%] Update failed; restoring backup
            if "%RESTORE_NEEDED%"=="1" robocopy "%SPECTRUM_BACKUP%" "%SPECTRUM_INSTALL%" /MIR /COPY:DAT /DCOPY:DAT /R:2 /W:1 /XJ /NP >>"%SPECTRUM_LOG%" 2>&1
            start "" "%SPECTRUM_INSTALL%\Spectrum.exe"
            exit /b 1

            :failed
            >>"%SPECTRUM_LOG%" echo [%date% %time%] Update preparation failed
            start "" "%SPECTRUM_INSTALL%\Spectrum.exe"
            exit /b 1
            """;
        File.WriteAllText(scriptPath, script, Encoding.ASCII);
        return scriptPath;
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            string updateRoot = Path.GetFullPath(GetUpdateRoot()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string target = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (target.StartsWith(updateRoot, StringComparison.OrdinalIgnoreCase))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
        }
    }

    private sealed class Crc32Accumulator
    {
        private static readonly uint[] Table = CreateTable();
        private uint crc = uint.MaxValue;

        public uint Value => ~crc;

        public void Append(ReadOnlySpan<byte> data)
        {
            foreach (byte value in data)
            {
                crc = Table[(crc ^ value) & 0xFF] ^ (crc >> 8);
            }
        }

        private static uint[] CreateTable()
        {
            uint[] table = new uint[256];
            for (uint index = 0; index < table.Length; index++)
            {
                uint value = index;
                for (int bit = 0; bit < 8; bit++)
                {
                    value = (value & 1) != 0 ? 0xEDB88320u ^ (value >> 1) : value >> 1;
                }
                table[index] = value;
            }
            return table;
        }
    }
}

internal static class UpdateText
{
    public static string Get(string name, string fallback) => Properties.Resources.ResourceManager.GetString(name, Properties.Resources.Culture) ?? fallback;
}
