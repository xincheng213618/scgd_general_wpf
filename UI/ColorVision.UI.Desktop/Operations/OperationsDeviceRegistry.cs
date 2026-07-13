using System.Security.Cryptography;
using System.IO;
using System.Text.Json;

namespace ColorVision.UI.Desktop.Operations
{
    public sealed class OperationsPairedDevice
    {
        public string DeviceId { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public string PublicKeySpki { get; set; } = string.Empty;

        public string[] Scopes { get; set; } = [];

        public DateTimeOffset ApprovedAt { get; set; }

        public DateTimeOffset? LastSeenAt { get; set; }

        public DateTimeOffset? RevokedAt { get; set; }

        public bool IsActive => RevokedAt == null;
    }

    public sealed class OperationsDeviceRegistry
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
        };

        private readonly object _syncRoot = new();
        private readonly string _storePath;
        private List<OperationsPairedDevice> _devices;

        public OperationsDeviceRegistry(string? storePath = null)
        {
            _storePath = storePath ?? GetDefaultStorePath();
            _devices = Load();
        }

        public IReadOnlyList<OperationsPairedDevice> GetAll()
        {
            lock (_syncRoot)
            {
                return _devices.Select(Clone).ToList();
            }
        }

        public OperationsPairedDevice? FindActive(string deviceId)
        {
            lock (_syncRoot)
            {
                OperationsPairedDevice? device = _devices.FirstOrDefault(item => item.IsActive
                    && string.Equals(item.DeviceId, deviceId, StringComparison.Ordinal));
                return device == null ? null : Clone(device);
            }
        }

        public void Approve(string deviceId, string displayName, string publicKeySpki, IEnumerable<string> scopes)
        {
            ValidatePublicKey(publicKeySpki);
            lock (_syncRoot)
            {
                OperationsPairedDevice? existing = _devices.FirstOrDefault(item => string.Equals(item.DeviceId, deviceId, StringComparison.Ordinal));
                if (existing == null)
                {
                    existing = new OperationsPairedDevice { DeviceId = deviceId };
                    _devices.Add(existing);
                }

                existing.DisplayName = displayName;
                existing.PublicKeySpki = publicKeySpki;
                existing.Scopes = scopes.Distinct(StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal).ToArray();
                existing.ApprovedAt = DateTimeOffset.UtcNow;
                existing.LastSeenAt = null;
                existing.RevokedAt = null;
                SaveNoLock();
            }
        }

        public bool Revoke(string deviceId)
        {
            lock (_syncRoot)
            {
                OperationsPairedDevice? existing = _devices.FirstOrDefault(item => item.IsActive
                    && string.Equals(item.DeviceId, deviceId, StringComparison.Ordinal));
                if (existing == null)
                    return false;

                existing.RevokedAt = DateTimeOffset.UtcNow;
                SaveNoLock();
                return true;
            }
        }

        public void MarkSeen(string deviceId)
        {
            lock (_syncRoot)
            {
                OperationsPairedDevice? existing = _devices.FirstOrDefault(item => item.IsActive
                    && string.Equals(item.DeviceId, deviceId, StringComparison.Ordinal));
                if (existing == null)
                    return;

                DateTimeOffset now = DateTimeOffset.UtcNow;
                if (existing.LastSeenAt.HasValue && now - existing.LastSeenAt.Value < TimeSpan.FromMinutes(1))
                    return;
                existing.LastSeenAt = now;
                SaveNoLock();
            }
        }

        public static void ValidatePublicKey(string publicKeySpki)
        {
            byte[] bytes = Convert.FromBase64String(publicKeySpki);
            using ECDsa key = ECDsa.Create();
            key.ImportSubjectPublicKeyInfo(bytes, out int read);
            if (read != bytes.Length || key.KeySize != 256)
                throw new CryptographicException("Only P-256 public keys are accepted.");
        }

        private List<OperationsPairedDevice> Load()
        {
            try
            {
                if (!File.Exists(_storePath))
                    return [];

                return JsonSerializer.Deserialize<List<OperationsPairedDevice>>(File.ReadAllText(_storePath), JsonOptions) ?? [];
            }
            catch
            {
                return [];
            }
        }

        private void SaveNoLock()
        {
            string? directory = Path.GetDirectoryName(_storePath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            string tempPath = _storePath + ".tmp";
            File.WriteAllText(tempPath, JsonSerializer.Serialize(_devices, JsonOptions));
            File.Move(tempPath, _storePath, true);
        }

        private static string GetDefaultStorePath()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "ColorVision", "Operations", "paired-devices.json");
        }

        private static OperationsPairedDevice Clone(OperationsPairedDevice value) => new()
        {
            DeviceId = value.DeviceId,
            DisplayName = value.DisplayName,
            PublicKeySpki = value.PublicKeySpki,
            Scopes = [.. value.Scopes],
            ApprovedAt = value.ApprovedAt,
            LastSeenAt = value.LastSeenAt,
            RevokedAt = value.RevokedAt,
        };
    }
}
