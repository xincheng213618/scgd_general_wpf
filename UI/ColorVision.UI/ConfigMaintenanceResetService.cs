using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ColorVision.UI;

public enum ConfigMaintenanceResetStatus
{
    None,
    Scheduled,
    Applied,
    Cancelled,
    BackupCreated,
    Failed,
    Deferred
}

public sealed class ConfigMaintenanceResetPlan
{
    internal ConfigMaintenanceResetPlan(string[] sectionNames) => SectionNames = Array.AsReadOnly(sectionNames);

    public IReadOnlyList<string> SectionNames { get; }
}

public sealed class ConfigMaintenanceResetResult
{
    internal ConfigMaintenanceResetResult(ConfigMaintenanceResetStatus status, IEnumerable<string>? sectionNames = null,
        string? backupPath = null, string errorMessage = "", bool configurationChanged = false)
    {
        Status = status;
        SectionNames = Array.AsReadOnly(sectionNames?.ToArray() ?? []);
        BackupPath = backupPath;
        ErrorMessage = errorMessage;
        ConfigurationChanged = configurationChanged;
    }

    public ConfigMaintenanceResetStatus Status { get; }
    public bool Succeeded => Status != ConfigMaintenanceResetStatus.Failed;
    public IReadOnlyList<string> SectionNames { get; }
    public string? BackupPath { get; }
    public string ErrorMessage { get; }
    public bool ConfigurationChanged { get; }
}

/// <summary>
/// Schedules section-only reset for the next startup. Never replaces live configuration instances.
/// Reset backups are exact persisted bytes and are independent of the rolling BackupConfigs backups.
/// </summary>
public sealed class ConfigMaintenanceResetService
{
    private readonly HashSet<string> _allowedSections;

    public ConfigMaintenanceResetService(string configFilePath, IEnumerable<string> allowedSections)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configFilePath);
        ConfigFilePath = Path.GetFullPath(configFilePath);
        _allowedSections = new HashSet<string>(ValidateSectionNames(allowedSections), StringComparer.Ordinal);
        PendingFilePath = ConfigFilePath + ".maintenance-reset.json";
        BackupDirectoryPath = Path.Combine(Path.GetDirectoryName(ConfigFilePath)!, "MaintenanceBackups");
    }

    public string ConfigFilePath { get; }
    public string PendingFilePath { get; }
    public string BackupDirectoryPath { get; }

    public ConfigMaintenanceResetPlan Prepare(IEnumerable<string> sectionNames)
    {
        string[] sections = ValidateSelection(sectionNames);
        using var fileLock = ConfigHandler.AcquireSaveFileLock(ConfigFilePath);
        ReadConfiguration();
        return new ConfigMaintenanceResetPlan(sections);
    }

    public ConfigMaintenanceResetResult Schedule(ConfigMaintenanceResetPlan plan)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(plan);
            string[] sections = ValidateSelection(plan.SectionNames);
            using var fileLock = ConfigHandler.AcquireSaveFileLock(ConfigFilePath);
            ReadConfiguration();
            var existing = ReadPending();
            if (existing != null && existing.State != "Applied")
                throw new InvalidOperationException("A reset is already scheduled. Cancel that plan before scheduling another one.");

            var pending = new PendingReset { Id = Guid.NewGuid().ToString("N"), SectionNames = sections };
            WritePending(pending);
            return Result(ConfigMaintenanceResetStatus.Scheduled, pending);
        }
        catch (Exception ex)
        {
            return Failure(ex);
        }
    }

    public ConfigMaintenanceResetResult GetPending()
    {
        try
        {
            using var fileLock = ConfigHandler.AcquireSaveFileLock(ConfigFilePath);
            var pending = ReadPending();
            return pending == null ? new(ConfigMaintenanceResetStatus.None)
                : Result(pending.State == "Applied" ? ConfigMaintenanceResetStatus.Applied : ConfigMaintenanceResetStatus.Scheduled, pending);
        }
        catch (Exception ex)
        {
            return Failure(ex);
        }
    }

    public ConfigMaintenanceResetResult CancelPending()
    {
        try
        {
            using var fileLock = ConfigHandler.AcquireSaveFileLock(ConfigFilePath);
            if (!File.Exists(PendingFilePath))
                return new(ConfigMaintenanceResetStatus.None);

            // Cancellation also removes a corrupt intent file; it never modifies configuration or backups.
            File.Delete(PendingFilePath);
            return new(ConfigMaintenanceResetStatus.Cancelled);
        }
        catch (Exception ex)
        {
            return Failure(ex);
        }
    }

    public ConfigMaintenanceResetResult CreateBackup()
    {
        try
        {
            using var fileLock = ConfigHandler.AcquireSaveFileLock(ConfigFilePath);
            byte[] original = ReadConfiguration();
            string path = GetBackupPath("manual-" + Guid.NewGuid().ToString("N"));
            WriteVerifiedBackup(path, original, allowExisting: false);
            return new(ConfigMaintenanceResetStatus.BackupCreated, backupPath: path);
        }
        catch (Exception ex)
        {
            return Failure(ex);
        }
    }

    // Only ConfigHandler.Load calls this in production, before loading JSON or creating configuration instances.
    internal ConfigMaintenanceResetResult ApplyPending(Func<bool>? startupAdmission = null)
    {
        PendingReset? pending = null;
        bool configurationChanged = false;
        try
        {
            using var fileLock = ConfigHandler.AcquireSaveFileLock(ConfigFilePath);
            pending = ReadPending();
            if (pending == null)
                return new(ConfigMaintenanceResetStatus.None);
            if (pending.State == "Applied")
                return Result(ConfigMaintenanceResetStatus.Applied, pending);

            // The application's single-instance handoff can happen after configuration loading.
            // An earlier instance must finish its last save before reset reads/backups/replacement.
            // Check under the same file mutex as SaveConfigs and preserve the intent on deferral.
            if (startupAdmission?.Invoke() == false)
                return new(ConfigMaintenanceResetStatus.Deferred, pending.SectionNames,
                    pending.State == "Prepared" ? GetBackupPath(pending.Id) : null,
                    "Another application instance may still save configuration. Close all instances and start the application again to apply the scheduled reset.");

            byte[] original = ReadConfiguration();
            string currentHash = Hash(original);
            if (pending.State == "Scheduled")
            {
                JObject reset = RemoveSections(original, pending.SectionNames);
                WriteVerifiedBackup(GetBackupPath(pending.Id), original, allowExisting: true);
                pending.BeforeSha256 = currentHash;
                pending.AfterSha256 = Hash(Serialize(reset));
                pending.State = "Prepared";
                // This durable journal makes a restart after the atomic config replace idempotent.
                WritePending(pending);
            }

            byte[] backup = File.ReadAllBytes(GetBackupPath(pending.Id));
            ParseObject(backup);
            if (!string.Equals(Hash(backup), pending.BeforeSha256, StringComparison.Ordinal))
                throw new InvalidDataException("The reset backup did not pass SHA-256 verification; configuration was not reset.");

            if (!string.Equals(currentHash, pending.AfterSha256, StringComparison.Ordinal))
            {
                if (!string.Equals(currentHash, pending.BeforeSha256, StringComparison.Ordinal))
                    throw new InvalidDataException("Configuration changed after this reset was prepared. Cancel the plan and review a new one.");
                JObject reset = RemoveSections(original, pending.SectionNames);
                if (!string.Equals(Hash(Serialize(reset)), pending.AfterSha256, StringComparison.Ordinal))
                    throw new InvalidDataException("The prepared reset content did not pass verification.");
                ConfigHandler.WriteConfigFile(ConfigFilePath, reset);
                configurationChanged = true;
            }

            pending.State = "Applied";
            WritePending(pending);
            return Result(ConfigMaintenanceResetStatus.Applied, pending, configurationChanged);
        }
        catch (Exception ex)
        {
            return Failure(ex, pending, configurationChanged);
        }
    }

    internal static string[] ValidateSectionNames(IEnumerable<string> sectionNames)
    {
        ArgumentNullException.ThrowIfNull(sectionNames);
        string[] sections = sectionNames.Distinct(StringComparer.Ordinal).ToArray();
        foreach (string section in sections)
        {
            if (string.IsNullOrEmpty(section) || section.Length > 128
                || !(char.IsAsciiLetter(section[0]) || section[0] == '_')
                || section.Any(c => !(char.IsAsciiLetterOrDigit(c) || c == '_')))
                throw new ArgumentException("Reset entries must be explicit configuration section names, not paths or patterns.", nameof(sectionNames));
        }
        return sections;
    }

    private string[] ValidateSelection(IEnumerable<string> sectionNames)
    {
        string[] sections = ValidateSectionNames(sectionNames);
        if (sections.Length == 0)
            throw new ArgumentException("Select at least one configuration section to reset.", nameof(sectionNames));
        if (sections.Any(section => !_allowedSections.Contains(section)))
            throw new ArgumentException("The reset includes a section that is not in the host's approved non-critical settings list.", nameof(sectionNames));
        return sections;
    }

    private byte[] ReadConfiguration()
    {
        byte[] bytes = File.ReadAllBytes(ConfigFilePath);
        ParseObject(bytes);
        return bytes;
    }

    private static JObject ParseObject(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes, writable: false);
        using var text = new StreamReader(stream, new UTF8Encoding(false, true), detectEncodingFromByteOrderMarks: true);
        using var reader = new JsonTextReader(text) { DateParseHandling = DateParseHandling.None };
        var parsed = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
        if (reader.Read())
            throw new InvalidDataException("Additional content was found after the configuration JSON object.");
        return parsed;
    }

    private static JObject RemoveSections(byte[] bytes, IEnumerable<string> sectionNames)
    {
        // Keep unselected JSON values verbatim, including unknown plugins' high-precision numbers.
        // A JObject round-trip alone can reinterpret dates or round floating-point tokens.
        using var stream = new MemoryStream(bytes, writable: false);
        using var reader = new StreamReader(stream, new UTF8Encoding(false, true), detectEncodingFromByteOrderMarks: true);
        using var document = System.Text.Json.JsonDocument.Parse(reader.ReadToEnd(), new System.Text.Json.JsonDocumentOptions
        {
            AllowTrailingCommas = true,
            CommentHandling = System.Text.Json.JsonCommentHandling.Skip
        });
        var selected = new HashSet<string>(sectionNames, StringComparer.Ordinal);
        var json = new JObject();
        foreach (var property in document.RootElement.EnumerateObject())
        {
            if (!selected.Contains(property.Name))
                json.Add(property.Name, new JRaw(property.Value.GetRawText()));
        }
        return json;
    }

    private static byte[] Serialize(JObject json) => Encoding.UTF8.GetBytes(json.ToString(Formatting.None));
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private string GetBackupPath(string id) => Path.Combine(BackupDirectoryPath, $"{Path.GetFileNameWithoutExtension(ConfigFilePath)}.reset-{id}.json");

    private void WriteVerifiedBackup(string path, byte[] original, bool allowExisting)
    {
        Directory.CreateDirectory(BackupDirectoryPath);
        if ((File.GetAttributes(BackupDirectoryPath) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("The maintenance backup directory must not be a link or junction.");
        if (File.Exists(path))
        {
            if (!allowExisting || (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("A maintenance backup already exists at the selected path; it was not overwritten.");
        }
        else
        {
            using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough);
            stream.Write(original);
            stream.Flush(flushToDisk: true);
        }

        byte[] saved = File.ReadAllBytes(path);
        ParseObject(saved);
        if (!saved.AsSpan().SequenceEqual(original))
            throw new IOException("The complete configuration backup could not be verified; it was not overwritten.");
    }

    private PendingReset? ReadPending()
    {
        if (!File.Exists(PendingFilePath))
            return null;
        JObject json = ParseObject(File.ReadAllBytes(PendingFilePath));
        if (json["Version"]?.Type != JTokenType.Integer || json["Id"]?.Type != JTokenType.String
            || json["State"]?.Type != JTokenType.String || json["SectionNames"]?.Type != JTokenType.Array)
            throw new InvalidDataException("The pending reset is missing required fields.");
        var pending = json.ToObject<PendingReset>(JsonSerializer.Create(new JsonSerializerSettings
        {
            MissingMemberHandling = MissingMemberHandling.Error
        })) ?? throw new InvalidDataException("The pending reset is empty.");
        if (pending.Version != 1 || !Guid.TryParseExact(pending.Id, "N", out _)
            || pending.State is not ("Scheduled" or "Prepared" or "Applied"))
            throw new InvalidDataException("The pending reset has an unsupported version, identifier or state.");
        pending.SectionNames = ValidateSelection(pending.SectionNames);
        if (pending.State != "Scheduled" && (!IsHash(pending.BeforeSha256) || !IsHash(pending.AfterSha256)))
            throw new InvalidDataException("The prepared reset has invalid verification hashes.");
        return pending;
    }

    private static bool IsHash(string? hash) => hash is { Length: 64 } && hash.All(Uri.IsHexDigit);
    private void WritePending(PendingReset pending) => ConfigHandler.WriteConfigFile(PendingFilePath, JObject.FromObject(pending));

    private ConfigMaintenanceResetResult Result(ConfigMaintenanceResetStatus status, PendingReset pending, bool configurationChanged = false)
        => new(status, pending.SectionNames, pending.State == "Scheduled" ? null : GetBackupPath(pending.Id), configurationChanged: configurationChanged);

    private ConfigMaintenanceResetResult Failure(Exception exception, PendingReset? pending = null, bool configurationChanged = false)
        => new(ConfigMaintenanceResetStatus.Failed, pending?.SectionNames, pending?.State is "Prepared" or "Applied" ? GetBackupPath(pending.Id) : null,
            exception.GetBaseException().Message, configurationChanged);

    private sealed class PendingReset
    {
        public int Version { get; set; } = 1;
        public string Id { get; set; } = "";
        public string State { get; set; } = "Scheduled";
        public string[] SectionNames { get; set; } = [];
        public string? BeforeSha256 { get; set; }
        public string? AfterSha256 { get; set; }
    }
}
