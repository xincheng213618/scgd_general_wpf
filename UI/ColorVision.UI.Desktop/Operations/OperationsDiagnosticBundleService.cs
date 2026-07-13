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
    }

    public sealed class OperationsDiagnosticBundleService
    {
        private readonly string _directory;
        private readonly OperationsWorkStore _workStore;

        public OperationsDiagnosticBundleService(OperationsWorkStore workStore, string? directory = null)
        {
            _workStore = workStore;
            _directory = directory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ColorVision", "Operations", "diagnostic-bundles");
        }

        public OperationsDiagnosticBundleResult Create(Func<object> snapshotProvider)
        {
            Directory.CreateDirectory(_directory);
            string bundleId = Guid.NewGuid().ToString("N");
            string path = Path.Combine(_directory, $"colorvision-diagnostics-{bundleId}.zip");
            DateTimeOffset createdAt = DateTimeOffset.UtcNow;
            JsonSerializerOptions options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };

            using (FileStream file = File.Create(path))
            using (ZipArchive archive = new(file, ZipArchiveMode.Create))
            {
                WriteJson(archive, "manifest.json", new
                {
                    schemaVersion = "1.0",
                    bundleId,
                    createdAt,
                    redaction = new
                    {
                        excludes = new[] { "credentials", "environmentVariables", "userDocuments", "rawDatabase", "imageContent" },
                        boundedAuditEntries = 100,
                    },
                }, options);
                WriteJson(archive, "host-snapshot.json", snapshotProvider(), options);
                WriteJson(archive, "runtime.json", new
                {
                    machine = Environment.MachineName,
                    os = Environment.OSVersion.VersionString,
                    runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
                    architecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),
                    processId = Environment.ProcessId,
                    processWorkingSetBytes = Environment.WorkingSet,
                }, options);
                WriteJson(archive, "operations-audit.json", new { entries = _workStore.GetAudit(100) }, options);
            }

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
            };
        }

        private static void WriteJson(ZipArchive archive, string name, object value, JsonSerializerOptions options)
        {
            ZipArchiveEntry entry = archive.CreateEntry(name, CompressionLevel.SmallestSize);
            using Stream stream = entry.Open();
            JsonSerializer.Serialize(stream, value, options);
        }
    }
}
