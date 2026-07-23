using System.Diagnostics;
using System.Text;
using Microsoft.Win32;
using Newtonsoft.Json;

namespace ColorVisionServiceHost;

internal sealed class ApplicationUpdateScanProtectionService : IDisposable
{
    private const int DefaultLifetimeSeconds = 180;
    private const int MinimumLifetimeSeconds = 30;
    private const int MaximumLifetimeSeconds = 300;
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromSeconds(15);
    private readonly object _syncRoot = new();
    private readonly IDefenderExclusionManager _defenderExclusions;
    private readonly string _stateDirectory;
    private readonly Func<DateTimeOffset> _utcNow;
    private Timer? _cleanupTimer;

    public static ApplicationUpdateScanProtectionService Default { get; } = new(
        new PowerShellDefenderExclusionManager(),
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "ColorVision",
            "ServiceHost",
            "UpdateScanProtection"));

    internal ApplicationUpdateScanProtectionService(
        IDefenderExclusionManager defenderExclusions,
        string stateDirectory,
        Func<DateTimeOffset>? utcNow = null)
    {
        _defenderExclusions = defenderExclusions;
        _stateDirectory = Path.GetFullPath(stateDirectory);
        _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
    }

    public void Start()
    {
        lock (_syncRoot)
        {
            if (_cleanupTimer != null)
                return;

            Directory.CreateDirectory(_stateDirectory);
            CleanupExpiredStatesCore();
            _cleanupTimer = new Timer(
                _ => CleanupExpiredStates(),
                null,
                CleanupInterval,
                CleanupInterval);
        }
    }

    public ServiceHostResponse Begin(
        ServiceHostRequest request,
        ServiceHostRequestContext context)
    {
        ApplicationUpdateScanProtectionPaths paths = ResolvePaths(
            context,
            GetRequiredDataValue(request, "updateRoot"));
        int lifetimeSeconds = Math.Clamp(
            GetOptionalDataInt(request, "lifetimeSeconds", DefaultLifetimeSeconds),
            MinimumLifetimeSeconds,
            MaximumLifetimeSeconds);

        lock (_syncRoot)
        {
            Directory.CreateDirectory(_stateDirectory);
            if (!CleanupApplicationStatesCore(paths.ApplicationDirectory))
            {
                return ServiceHostResponse.FromObject(
                    request.RequestId,
                    false,
                    "Unable to clear a previous application update scan protection session.");
            }

            string protectionId = Guid.NewGuid().ToString("N");
            string recoveryJournalPath = GetRecoveryJournalPath(protectionId);
            DefenderExclusionChangeResult result = _defenderExclusions.AddPaths(
                [paths.UpdateRoot, paths.ApplicationDirectory],
                recoveryJournalPath);
            if (!result.Success)
            {
                CleanupRecoveryJournalCore(recoveryJournalPath);
                ServiceHostLog.Write($"Application update scan protection was not enabled: {result.Message}");
                return ServiceHostResponse.FromObject(request.RequestId, false, result.Message, new
                {
                    paths.UpdateRoot,
                    paths.ApplicationDirectory,
                });
            }

            if (result.ChangedPaths.Count == 0)
            {
                TryDeleteFile(recoveryJournalPath);
                ServiceHostLog.Write($"Application update paths were already excluded. Application={paths.ApplicationDirectory}; UpdateRoot={paths.UpdateRoot}");
                return ServiceHostResponse.FromObject(request.RequestId, true, "application update paths were already excluded", new
                {
                    protectionId = string.Empty,
                    expiresAtUtc = _utcNow().AddSeconds(lifetimeSeconds),
                    addedPaths = Array.Empty<string>(),
                    alreadyExcludedPaths = result.UnchangedPaths,
                });
            }

            DateTimeOffset createdAtUtc = _utcNow();
            ApplicationUpdateScanProtectionState state = new()
            {
                ProtectionId = protectionId,
                ApplicationDirectory = paths.ApplicationDirectory,
                UpdateRoot = paths.UpdateRoot,
                AddedPaths = result.ChangedPaths.ToArray(),
                CreatedAtUtc = createdAtUtc,
                ExpiresAtUtc = createdAtUtc.AddSeconds(lifetimeSeconds),
                CallerSid = context.UserSid,
            };

            try
            {
                WriteState(state);
                TryDeleteFile(recoveryJournalPath);
            }
            catch
            {
                DefenderExclusionChangeResult removal = _defenderExclusions.RemovePaths(result.ChangedPaths);
                if (removal.Success)
                    TryDeleteFile(recoveryJournalPath);
                throw;
            }

            ServiceHostLog.Write($"Application update scan protection enabled. Id={state.ProtectionId}; Application={state.ApplicationDirectory}; UpdateRoot={state.UpdateRoot}; Expires={state.ExpiresAtUtc:O}");
            return ServiceHostResponse.FromObject(request.RequestId, true, "application update scan protection enabled", new
            {
                protectionId = state.ProtectionId,
                state.ExpiresAtUtc,
                addedPaths = state.AddedPaths,
                alreadyExcludedPaths = result.UnchangedPaths,
            });
        }
    }

    public ServiceHostResponse Complete(
        ServiceHostRequest request,
        ServiceHostRequestContext context)
    {
        string protectionId = GetRequiredDataValue(request, "protectionId").Trim();
        if (!Guid.TryParseExact(protectionId, "N", out _))
            return ServiceHostResponse.FromObject(request.RequestId, false, "invalid application update scan protection id");

        string applicationDirectory = ResolveApplicationDirectory(context);
        lock (_syncRoot)
        {
            string statePath = GetStatePath(protectionId);
            if (!File.Exists(statePath))
            {
                return ServiceHostResponse.FromObject(
                    request.RequestId,
                    true,
                    "application update scan protection was already cleared");
            }

            ApplicationUpdateScanProtectionState state = ReadState(statePath);
            if (!IsSamePath(state.ApplicationDirectory, applicationDirectory))
            {
                return ServiceHostResponse.FromObject(
                    request.RequestId,
                    false,
                    "application update scan protection belongs to a different application directory");
            }

            if (!RemoveStateCore(statePath, state))
            {
                return ServiceHostResponse.FromObject(
                    request.RequestId,
                    false,
                    "failed to remove application update scan protection; automatic cleanup will retry");
            }

            return ServiceHostResponse.FromObject(request.RequestId, true, "application update scan protection cleared", new
            {
                protectionId,
                removedPaths = state.AddedPaths,
            });
        }
    }

    public void Dispose()
    {
        lock (_syncRoot)
        {
            _cleanupTimer?.Dispose();
            _cleanupTimer = null;
        }
    }

    internal void CleanupExpiredStatesNow()
    {
        lock (_syncRoot)
        {
            CleanupExpiredStatesCore();
        }
    }

    internal static ApplicationUpdateScanProtectionPaths ResolvePaths(
        ServiceHostRequestContext context,
        string updateRoot)
    {
        string applicationDirectory = ResolveApplicationDirectory(context);
        string normalizedUpdateRoot = NormalizeDirectoryPath(updateRoot);
        string updateDirectoryName = Path.GetFileName(normalizedUpdateRoot);
        bool isUnderAllowedTemporaryRoot = ResolveAllowedTemporaryRoots(context)
            .Any(temporaryRoot => IsPathInside(normalizedUpdateRoot, temporaryRoot));

        if (!Directory.Exists(normalizedUpdateRoot)
            || !isUnderAllowedTemporaryRoot
            || !updateDirectoryName.StartsWith("ColorVisionUpdate-", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The update scan protection root must be an existing ColorVisionUpdate directory under the system temporary directory.");
        }

        return new ApplicationUpdateScanProtectionPaths(
            normalizedUpdateRoot,
            applicationDirectory);
    }

    internal static IReadOnlyList<string> ResolveAllowedTemporaryRoots(ServiceHostRequestContext context)
    {
        HashSet<string> roots = new(StringComparer.OrdinalIgnoreCase)
        {
            NormalizeDirectoryPath(Path.GetTempPath()),
        };

        try
        {
            using RegistryKey? profileKey = Registry.LocalMachine.OpenSubKey(
                $@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList\{context.UserSid}");
            string? profilePath = profileKey?.GetValue("ProfileImagePath")?.ToString();
            if (!string.IsNullOrWhiteSpace(profilePath))
            {
                profilePath = Environment.ExpandEnvironmentVariables(profilePath);
                roots.Add(NormalizeDirectoryPath(Path.Combine(profilePath, "AppData", "Local", "Temp")));
            }
        }
        catch (Exception ex)
        {
            ServiceHostLog.Write($"Unable to resolve the caller temporary directory from profile data: {ex.Message}");
        }

        return roots.ToArray();
    }

    private void CleanupExpiredStates()
    {
        try
        {
            lock (_syncRoot)
            {
                CleanupExpiredStatesCore();
            }
        }
        catch (Exception ex)
        {
            ServiceHostLog.Write($"Application update scan protection cleanup failed: {ex}");
        }
    }

    private void CleanupExpiredStatesCore()
    {
        if (!Directory.Exists(_stateDirectory))
            return;

        CleanupRecoveryJournalsCore();
        DateTimeOffset utcNow = _utcNow();
        foreach (string statePath in Directory.EnumerateFiles(_stateDirectory, "*.json", SearchOption.TopDirectoryOnly))
        {
            try
            {
                ApplicationUpdateScanProtectionState state = ReadState(statePath);
                if (state.ExpiresAtUtc <= utcNow)
                    RemoveStateCore(statePath, state);
            }
            catch (Exception ex)
            {
                ServiceHostLog.Write($"Failed to inspect application update scan protection state '{statePath}': {ex.Message}");
            }
        }
    }

    private void CleanupRecoveryJournalsCore()
    {
        foreach (string recoveryJournalPath in Directory.EnumerateFiles(
            _stateDirectory,
            "*.pending",
            SearchOption.TopDirectoryOnly))
        {
            try
            {
                CleanupRecoveryJournalCore(recoveryJournalPath);
            }
            catch (Exception ex)
            {
                ServiceHostLog.Write($"Failed to clear application update scan protection recovery journal '{recoveryJournalPath}': {ex.Message}");
            }
        }
    }

    private bool CleanupRecoveryJournalCore(string recoveryJournalPath)
    {
        if (!File.Exists(recoveryJournalPath))
            return true;

        string[] paths = File.ReadAllLines(recoveryJournalPath)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => Encoding.UTF8.GetString(Convert.FromBase64String(line)))
            .ToArray();
        DefenderExclusionChangeResult removal = _defenderExclusions.RemovePaths(paths);
        if (!removal.Success)
            return false;

        File.Delete(recoveryJournalPath);
        ServiceHostLog.Write($"Application update scan protection recovery journal cleared: {Path.GetFileName(recoveryJournalPath)}");
        return true;
    }

    private bool CleanupApplicationStatesCore(string applicationDirectory)
    {
        if (!Directory.Exists(_stateDirectory))
            return true;

        bool success = true;
        foreach (string statePath in Directory.EnumerateFiles(_stateDirectory, "*.json", SearchOption.TopDirectoryOnly))
        {
            try
            {
                ApplicationUpdateScanProtectionState state = ReadState(statePath);
                if (IsSamePath(state.ApplicationDirectory, applicationDirectory))
                    success &= RemoveStateCore(statePath, state);
            }
            catch (Exception ex)
            {
                ServiceHostLog.Write($"Failed to clear previous application update scan protection state '{statePath}': {ex.Message}");
                success = false;
            }
        }
        return success;
    }

    private bool RemoveStateCore(
        string statePath,
        ApplicationUpdateScanProtectionState state)
    {
        DefenderExclusionChangeResult result = _defenderExclusions.RemovePaths(state.AddedPaths);
        if (!result.Success)
        {
            ServiceHostLog.Write($"Application update scan protection removal failed. Id={state.ProtectionId}; Error={result.Message}");
            return false;
        }

        File.Delete(statePath);
        ServiceHostLog.Write($"Application update scan protection cleared. Id={state.ProtectionId}; Application={state.ApplicationDirectory}");
        return true;
    }

    private void WriteState(ApplicationUpdateScanProtectionState state)
    {
        string statePath = GetStatePath(state.ProtectionId);
        string temporaryPath = statePath + $".{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(
                temporaryPath,
                JsonConvert.SerializeObject(state, Formatting.Indented),
                new UTF8Encoding(false));
            File.Move(temporaryPath, statePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    private ApplicationUpdateScanProtectionState ReadState(string statePath)
    {
        string fullStatePath = Path.GetFullPath(statePath);
        if (!IsPathInside(fullStatePath, _stateDirectory))
            throw new InvalidOperationException("Invalid application update scan protection state path.");

        ApplicationUpdateScanProtectionState state = JsonConvert.DeserializeObject<ApplicationUpdateScanProtectionState>(
            File.ReadAllText(fullStatePath))
            ?? throw new InvalidDataException("Invalid application update scan protection state.");
        if (!Guid.TryParseExact(state.ProtectionId, "N", out _)
            || state.AddedPaths.Length == 0
            || state.AddedPaths.Any(path => string.IsNullOrWhiteSpace(path)))
        {
            throw new InvalidDataException("Incomplete application update scan protection state.");
        }
        return state;
    }

    private string GetStatePath(string protectionId)
    {
        return Path.Combine(_stateDirectory, protectionId + ".json");
    }

    private string GetRecoveryJournalPath(string protectionId)
    {
        return Path.Combine(_stateDirectory, protectionId + ".pending");
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
        }
    }

    private static string ResolveApplicationDirectory(ServiceHostRequestContext context)
    {
        string processPath = Path.GetFullPath(context.ProcessPath);
        if (!File.Exists(processPath)
            || !string.Equals(Path.GetFileName(processPath), "ColorVision.exe", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The update scan protection caller must be ColorVision.exe.");
        }

        string applicationDirectory = NormalizeDirectoryPath(
            Path.GetDirectoryName(processPath)
            ?? throw new InvalidOperationException("Unable to resolve the ColorVision application directory."));
        string volumeRoot = NormalizeDirectoryPath(Path.GetPathRoot(applicationDirectory) ?? applicationDirectory);
        if (IsSamePath(applicationDirectory, volumeRoot))
            throw new InvalidOperationException("The application directory cannot be a volume root.");
        return applicationDirectory;
    }

    private static string NormalizeDirectoryPath(string path)
    {
        return Path.GetFullPath(path)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static bool IsPathInside(string path, string parentDirectory)
    {
        string normalizedPath = Path.GetFullPath(path);
        string normalizedParent = NormalizeDirectoryPath(parentDirectory) + Path.DirectorySeparatorChar;
        return normalizedPath.StartsWith(normalizedParent, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSamePath(string first, string second)
    {
        return string.Equals(
            NormalizeDirectoryPath(first),
            NormalizeDirectoryPath(second),
            StringComparison.OrdinalIgnoreCase);
    }

    private static string GetRequiredDataValue(ServiceHostRequest request, string name)
    {
        string value = request.Data?[name]?.ToString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"Missing request data: {name}");
        return value;
    }

    private static int GetOptionalDataInt(ServiceHostRequest request, string name, int defaultValue)
    {
        return int.TryParse(request.Data?[name]?.ToString(), out int value) ? value : defaultValue;
    }

}

internal sealed class PowerShellDefenderExclusionManager : IDefenderExclusionManager
{
    private const int CommandTimeoutMilliseconds = 20000;
    private const string PathsEnvironmentVariable = "COLORVISION_DEFENDER_EXCLUSION_PATHS";
    private const string RecoveryJournalEnvironmentVariable = "COLORVISION_DEFENDER_RECOVERY_JOURNAL";

    private const string AddScript = """
$ErrorActionPreference = 'Stop'
$toAdd = @()
try {
    $json = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($env:COLORVISION_DEFENDER_EXCLUSION_PATHS))
    $paths = @()
    foreach ($path in ($json | ConvertFrom-Json)) { $paths += [string]$path }
    $existing = @((Get-MpPreference -ErrorAction Stop).ExclusionPath | Where-Object { $_ -and $_ -notlike 'N/A:*' })
    $toAdd = @($paths | Where-Object { $existing -notcontains $_ })
    $journalLines = @($toAdd | ForEach-Object { [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes([string]$_)) })
    [IO.File]::WriteAllLines($env:COLORVISION_DEFENDER_RECOVERY_JOURNAL, $journalLines, [Text.UTF8Encoding]::new($false))
    if ($toAdd.Count -gt 0) {
        Add-MpPreference -ExclusionPath $toAdd -ErrorAction Stop
    }
    $after = @((Get-MpPreference -ErrorAction Stop).ExclusionPath | Where-Object { $_ -and $_ -notlike 'N/A:*' })
    $missing = @($paths | Where-Object { $after -notcontains $_ })
    if ($missing.Count -gt 0) {
        throw "Defender did not retain all requested exclusions: $($missing -join '; ')"
    }
    foreach ($path in $toAdd) {
        [Console]::WriteLine('CV_CHANGED:' + [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes([string]$path)))
    }
    foreach ($path in @($paths | Where-Object { $toAdd -notcontains $_ })) {
        [Console]::WriteLine('CV_UNCHANGED:' + [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes([string]$path)))
    }
    [Console]::WriteLine('CV_SUCCESS')
}
catch {
    $failureMessage = $_.Exception.Message
    if ($toAdd.Count -gt 0) {
        try {
            Remove-MpPreference -ExclusionPath $toAdd -ErrorAction Stop
            $afterRollback = @((Get-MpPreference -ErrorAction Stop).ExclusionPath | Where-Object { $_ -and $_ -notlike 'N/A:*' })
            $rollbackRemaining = @($toAdd | Where-Object { $afterRollback -contains $_ })
            if ($rollbackRemaining.Count -eq 0) {
                Remove-Item -LiteralPath $env:COLORVISION_DEFENDER_RECOVERY_JOURNAL -Force -ErrorAction SilentlyContinue
            }
        }
        catch {
            # Keep the recovery journal so the service timer can retry the rollback.
        }
    }
    else {
        Remove-Item -LiteralPath $env:COLORVISION_DEFENDER_RECOVERY_JOURNAL -Force -ErrorAction SilentlyContinue
    }
    [Console]::Error.WriteLine($failureMessage)
    exit 1
}
""";

    private const string RemoveScript = """
$ErrorActionPreference = 'Stop'
try {
    $json = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($env:COLORVISION_DEFENDER_EXCLUSION_PATHS))
    $paths = @()
    foreach ($path in ($json | ConvertFrom-Json)) { $paths += [string]$path }
    $existing = @((Get-MpPreference -ErrorAction Stop).ExclusionPath | Where-Object { $_ -and $_ -notlike 'N/A:*' })
    $toRemove = @($paths | Where-Object { $existing -contains $_ })
    if ($toRemove.Count -gt 0) {
        Remove-MpPreference -ExclusionPath $toRemove -ErrorAction Stop
    }
    $after = @((Get-MpPreference -ErrorAction Stop).ExclusionPath | Where-Object { $_ -and $_ -notlike 'N/A:*' })
    $remaining = @($paths | Where-Object { $after -contains $_ })
    if ($remaining.Count -gt 0) {
        throw "Defender retained exclusions that should have been removed: $($remaining -join '; ')"
    }
    foreach ($path in $toRemove) {
        [Console]::WriteLine('CV_CHANGED:' + [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes([string]$path)))
    }
    foreach ($path in @($paths | Where-Object { $toRemove -notcontains $_ })) {
        [Console]::WriteLine('CV_UNCHANGED:' + [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes([string]$path)))
    }
    [Console]::WriteLine('CV_SUCCESS')
}
catch {
    [Console]::Error.WriteLine($_.Exception.Message)
    exit 1
}
""";

    public DefenderExclusionChangeResult AddPaths(IReadOnlyCollection<string> paths, string recoveryJournalPath)
    {
        return Run(AddScript, paths, recoveryJournalPath);
    }

    public DefenderExclusionChangeResult RemovePaths(IReadOnlyCollection<string> paths)
    {
        return paths.Count == 0
            ? DefenderExclusionChangeResult.Succeeded([], [])
            : Run(RemoveScript, paths, recoveryJournalPath: null);
    }

    private static DefenderExclusionChangeResult Run(
        string script,
        IReadOnlyCollection<string> paths,
        string? recoveryJournalPath)
    {
        string[] normalizedPaths = paths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (normalizedPaths.Length == 0)
            return DefenderExclusionChangeResult.Succeeded([], []);

        string powershellPath = Path.Combine(
            Environment.SystemDirectory,
            "WindowsPowerShell",
            "v1.0",
            "powershell.exe");
        string encodedScript = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
        string encodedPaths = Convert.ToBase64String(
            Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(normalizedPaths)));

        using Process process = new()
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = powershellPath,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            },
        };
        process.StartInfo.ArgumentList.Add("-NoLogo");
        process.StartInfo.ArgumentList.Add("-NoProfile");
        process.StartInfo.ArgumentList.Add("-NonInteractive");
        process.StartInfo.ArgumentList.Add("-ExecutionPolicy");
        process.StartInfo.ArgumentList.Add("Bypass");
        process.StartInfo.ArgumentList.Add("-EncodedCommand");
        process.StartInfo.ArgumentList.Add(encodedScript);
        process.StartInfo.Environment[PathsEnvironmentVariable] = encodedPaths;
        if (!string.IsNullOrWhiteSpace(recoveryJournalPath))
            process.StartInfo.Environment[RecoveryJournalEnvironmentVariable] = Path.GetFullPath(recoveryJournalPath);

        try
        {
            process.Start();
            Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
            Task<string> errorTask = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(CommandTimeoutMilliseconds))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                }
                return DefenderExclusionChangeResult.Failed("Microsoft Defender exclusion command timed out.");
            }

            string output = outputTask.GetAwaiter().GetResult().Trim();
            string error = errorTask.GetAwaiter().GetResult().Trim();
            if (process.ExitCode != 0)
            {
                return DefenderExclusionChangeResult.Failed(
                    string.IsNullOrWhiteSpace(error)
                        ? $"Microsoft Defender exclusion command failed with exit code {process.ExitCode}."
                        : error);
            }

            string[] outputLines = output.Split(
                [Environment.NewLine],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (!outputLines.Contains("CV_SUCCESS", StringComparer.Ordinal))
                return DefenderExclusionChangeResult.Failed("Microsoft Defender exclusion command returned an invalid response.");

            return DefenderExclusionChangeResult.Succeeded(
                DecodePaths(outputLines, "CV_CHANGED:"),
                DecodePaths(outputLines, "CV_UNCHANGED:"));
        }
        catch (Exception ex)
        {
            return DefenderExclusionChangeResult.Failed($"Microsoft Defender exclusion command could not run: {ex.Message}");
        }
    }

    private static string[] DecodePaths(IEnumerable<string> outputLines, string prefix)
    {
        return outputLines
            .Where(line => line.StartsWith(prefix, StringComparison.Ordinal))
            .Select(line => Encoding.UTF8.GetString(Convert.FromBase64String(line[prefix.Length..])))
            .ToArray();
    }
}

internal interface IDefenderExclusionManager
{
    DefenderExclusionChangeResult AddPaths(IReadOnlyCollection<string> paths, string recoveryJournalPath);

    DefenderExclusionChangeResult RemovePaths(IReadOnlyCollection<string> paths);
}

internal sealed record DefenderExclusionChangeResult(
    bool Success,
    IReadOnlyList<string> ChangedPaths,
    IReadOnlyList<string> UnchangedPaths,
    string Message)
{
    public static DefenderExclusionChangeResult Succeeded(
        IReadOnlyList<string> changedPaths,
        IReadOnlyList<string> unchangedPaths)
    {
        return new DefenderExclusionChangeResult(
            true,
            changedPaths,
            unchangedPaths,
            "Microsoft Defender exclusions updated.");
    }

    public static DefenderExclusionChangeResult Failed(string message)
    {
        return new DefenderExclusionChangeResult(false, [], [], message);
    }
}

internal sealed record ApplicationUpdateScanProtectionPaths(
    string UpdateRoot,
    string ApplicationDirectory);

internal sealed class ApplicationUpdateScanProtectionState
{
    public string ProtectionId { get; set; } = string.Empty;

    public string ApplicationDirectory { get; set; } = string.Empty;

    public string UpdateRoot { get; set; } = string.Empty;

    public string[] AddedPaths { get; set; } = [];

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset ExpiresAtUtc { get; set; }

    public string CallerSid { get; set; } = string.Empty;
}
