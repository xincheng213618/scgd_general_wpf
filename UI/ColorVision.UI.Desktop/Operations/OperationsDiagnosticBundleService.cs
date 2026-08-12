using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace ColorVision.UI.Desktop.Operations
{
    public sealed class OperationsDiagnosticBundleResult
    {
        public string BundleId { get; init; } = string.Empty;
        public string FilePath { get; init; } = string.Empty;
        public string Sha256 { get; init; } = string.Empty;
        public long SizeBytes { get; init; }
        public DateTimeOffset CreatedAt { get; init; }

        public DateTimeOffset ExpiresAt { get; init; }

        public byte[] Data { get; init; } = [];
    }

    public enum OperationsDiagnosticBundleLookupStatus
    {
        Available,
        InvalidId,
        NotFound,
        Expired,
        TooLarge,
        UnsupportedFormat,
        ReadFailed,
    }

    public sealed class OperationsDiagnosticBundleService
    {
        public const int MaximumDownloadBytes = 2 * 1024 * 1024;
        public static readonly TimeSpan DownloadLifetime = TimeSpan.FromHours(24);

        private readonly string _directory;
        private readonly OperationsWorkStore _workStore;
        private readonly Func<DateTimeOffset> _clock;

        public OperationsDiagnosticBundleService(
            OperationsWorkStore workStore,
            string? directory = null,
            Func<DateTimeOffset>? clock = null)
        {
            _workStore = workStore;
            _directory = directory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ColorVision", "Operations", "diagnostic-bundles");
            _clock = clock ?? (() => DateTimeOffset.UtcNow);
        }

        public OperationsDiagnosticBundleResult Create(
            Func<object> snapshotProvider,
            OperationsLogDigest logDigest,
            OperationsServiceHealthReport serviceHealth)
        {
            ArgumentNullException.ThrowIfNull(snapshotProvider);
            ArgumentNullException.ThrowIfNull(logDigest);
            ArgumentNullException.ThrowIfNull(serviceHealth);
            Directory.CreateDirectory(_directory);
            string bundleId = Guid.NewGuid().ToString("N");
            string path = Path.Combine(_directory, $"colorvision-diagnostics-{bundleId}.zip");
            DateTimeOffset createdAt = _clock();
            JsonSerializerOptions options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };

            using (FileStream file = File.Create(path))
            using (ZipArchive archive = new(file, ZipArchiveMode.Create))
            {
                WriteJson(archive, "manifest.json", new
                {
                    schemaVersion = "2.0",
                    bundleId,
                    createdAt,
                    expiresAt = createdAt.Add(DownloadLifetime),
                    redaction = new
                    {
                        excludes = new[]
                        {
                            "credentials", "environmentVariables", "userDocuments", "rawDatabase", "imageContent",
                            "machineName", "userName", "deviceId", "networkAddress", "endpoint", "processId",
                            "requestId", "correlationId", "rawLogLines",
                        },
                        boundedAuditEntries = 100,
                        boundedRecentLogLines = 500,
                    },
                }, options);
                WriteJson(archive, "host-snapshot.json",
                    OperationsSafeSnapshotFactory.Create(snapshotProvider(), createdAt), options);
                WriteJson(archive, "runtime.json", new
                {
                    application = "ColorVision",
                    applicationVersion = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "unknown",
                    os = Environment.OSVersion.VersionString,
                    runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
                    architecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),
                    processWorkingSetBytes = Environment.WorkingSet,
                }, options);
                WriteJson(archive, "recent-events.json", logDigest, options);
                WriteJson(archive, "service-health.json", serviceHealth, options);
                WriteJson(archive, "operations-audit.json", new
                {
                    entries = _workStore.GetAudit(100).Select(OperationsAuditSummaryFactory.Create).ToArray(),
                }, options);
            }

            File.SetLastWriteTimeUtc(path, createdAt.UtcDateTime);

            byte[] hash;
            using (FileStream file = File.OpenRead(path))
                hash = SHA256.HashData(file);
            FileInfo info = new(path);
            return new OperationsDiagnosticBundleResult
            {
                BundleId = bundleId,
                FilePath = path,
                Sha256 = Convert.ToHexString(hash).ToLowerInvariant(),
                SizeBytes = info.Length,
                CreatedAt = createdAt,
                ExpiresAt = createdAt.Add(DownloadLifetime),
            };
        }

        public OperationsDiagnosticBundleLookupStatus TryRead(string bundleId, out OperationsDiagnosticBundleResult? result)
        {
            result = null;
            if (bundleId.Length != 32 || bundleId.Any(ch => !char.IsAsciiHexDigit(ch)))
                return OperationsDiagnosticBundleLookupStatus.InvalidId;

            string directory = Path.GetFullPath(_directory);
            string path = Path.GetFullPath(Path.Combine(directory, $"colorvision-diagnostics-{bundleId}.zip"));
            if (!string.Equals(Path.GetDirectoryName(path), directory, StringComparison.OrdinalIgnoreCase))
                return OperationsDiagnosticBundleLookupStatus.InvalidId;
            if (!File.Exists(path))
                return OperationsDiagnosticBundleLookupStatus.NotFound;

            try
            {
                FileInfo info = new(path);
                DateTimeOffset createdAt = new(info.LastWriteTimeUtc);
                DateTimeOffset expiresAt = createdAt.Add(DownloadLifetime);
                if (_clock() > expiresAt)
                    return OperationsDiagnosticBundleLookupStatus.Expired;
                if (info.Length is <= 0 or > MaximumDownloadBytes)
                    return OperationsDiagnosticBundleLookupStatus.TooLarge;

                byte[] data = File.ReadAllBytes(path);
                if (!IsSupportedBundle(data))
                    return OperationsDiagnosticBundleLookupStatus.UnsupportedFormat;

                result = new OperationsDiagnosticBundleResult
                {
                    BundleId = bundleId,
                    FilePath = path,
                    Sha256 = Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant(),
                    SizeBytes = data.LongLength,
                    CreatedAt = createdAt,
                    ExpiresAt = expiresAt,
                    Data = data,
                };
                return OperationsDiagnosticBundleLookupStatus.Available;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or JsonException)
            {
                return OperationsDiagnosticBundleLookupStatus.ReadFailed;
            }
        }

        private static bool IsSupportedBundle(byte[] data)
        {
            string[] requiredEntries =
            [
                "host-snapshot.json",
                "manifest.json",
                "operations-audit.json",
                "recent-events.json",
                "runtime.json",
                "service-health.json",
            ];
            using MemoryStream stream = new(data, writable: false);
            using ZipArchive archive = new(stream, ZipArchiveMode.Read);
            string[] names = archive.Entries.Select(entry => entry.FullName).OrderBy(name => name, StringComparer.Ordinal).ToArray();
            if (!names.SequenceEqual(requiredEntries, StringComparer.Ordinal))
                return false;

            ZipArchiveEntry? manifest = archive.GetEntry("manifest.json");
            if (manifest == null || manifest.Length is <= 0 or > 64 * 1024)
                return false;
            using Stream manifestStream = manifest.Open();
            using JsonDocument document = JsonDocument.Parse(manifestStream);
            return document.RootElement.TryGetProperty("schemaVersion", out JsonElement schemaVersion)
                && schemaVersion.ValueKind == JsonValueKind.String
                && schemaVersion.GetString() == "2.0";
        }

        private static void WriteJson(ZipArchive archive, string name, object value, JsonSerializerOptions options)
        {
            ZipArchiveEntry entry = archive.CreateEntry(name, CompressionLevel.SmallestSize);
            using Stream stream = entry.Open();
            JsonSerializer.Serialize(stream, value, options);
        }
    }
}
