using Microsoft.Win32;
using System.Diagnostics;
using System.ServiceProcess;
using System.Text.RegularExpressions;

namespace ColorVisionServiceHost;

internal static class Com0ComCommandService
{
    private const string DriverServiceName = "com0com";
    private const string SetupExecutableName = "setupc.exe";
    private const int MaximumComPortNumber = 256;
    private const int FirstPreferredComPortNumber = 4;
    private const int ListCommandTimeoutMilliseconds = 30000;
    private const int MutationCommandTimeoutMilliseconds = 120000;
    private static readonly Lock CommandLock = new();
    private static readonly Regex PortNameRegex = new(
        "^(?:COM)?(?<number>[0-9]{1,3})$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex PairLineRegex = new(
        "^\\s*(?<side>CNC[AB])(?<number>[0-9]+)(?:\\s+(?<settings>.*))?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static ServiceHostResponse GetStatus(ServiceHostRequest request, bool includePairs)
    {
        Com0ComInstallation installation = ResolveInstallation();
        IReadOnlyList<Com0ComPairInfo> pairs = [];
        string output = string.Empty;
        if (includePairs && installation.Installed)
        {
            lock (CommandLock)
            {
                SetupCommandResult listResult = RunSetupCommand(installation, ["list"], ListCommandTimeoutMilliseconds);
                if (!listResult.Success)
                    return CreateCommandFailure(request.RequestId, "com0com list failed", installation, listResult);

                output = listResult.Output;
                pairs = ParsePairs(output);
            }
        }

        return ServiceHostResponse.FromObject(
            request.RequestId,
            true,
            installation.Installed ? "com0com_installed" : "com0com_not_installed",
            BuildStatusData(installation, pairs, output));
    }

    public static ServiceHostResponse CreatePair(ServiceHostRequest request)
    {
        Com0ComInstallation installation = ResolveInstallation();
        if (!installation.Installed)
            return ServiceHostResponse.FromObject(request.RequestId, false, "com0com_not_installed", BuildStatusData(installation, [], string.Empty));

        string portA = NormalizeRequestedPort(request.Data?["portA"]?.ToString());
        string portB = NormalizeRequestedPort(request.Data?["portB"]?.ToString());
        if (!string.Equals(portA, "COM#", StringComparison.OrdinalIgnoreCase)
            && string.Equals(portA, portB, StringComparison.OrdinalIgnoreCase))
        {
            return ServiceHostResponse.FromObject(request.RequestId, false, "com0com_pair_ports_must_be_different");
        }

        lock (CommandLock)
        {
            SetupCommandResult beforeResult = RunSetupCommand(installation, ["list"], ListCommandTimeoutMilliseconds);
            if (!beforeResult.Success)
                return CreateCommandFailure(request.RequestId, "com0com list failed", installation, beforeResult);

            IReadOnlyList<Com0ComPairInfo> before = ParsePairs(beforeResult.Output);
            HashSet<string> existingPorts = GetExistingPortNames(before);
            if (!string.Equals(portA, "COM#", StringComparison.OrdinalIgnoreCase) && existingPorts.Contains(portA))
                return ServiceHostResponse.FromObject(request.RequestId, false, $"serial_port_already_exists: {portA}");
            if (!string.Equals(portB, "COM#", StringComparison.OrdinalIgnoreCase) && existingPorts.Contains(portB))
                return ServiceHostResponse.FromObject(request.RequestId, false, $"serial_port_already_exists: {portB}");

            SetupCommandResult createResult = RunSetupCommand(
                installation,
                ["install", $"PortName={portA}", $"PortName={portB}"],
                MutationCommandTimeoutMilliseconds);
            if (!createResult.Success)
                return CreateCommandFailure(request.RequestId, "com0com pair creation failed", installation, createResult);

            SetupCommandResult afterResult = RunSetupCommand(installation, ["list"], ListCommandTimeoutMilliseconds);
            if (!afterResult.Success)
                return CreateCommandFailure(request.RequestId, "com0com pair created but refresh failed", installation, afterResult);

            IReadOnlyList<Com0ComPairInfo> after = ParsePairs(afterResult.Output);
            HashSet<int> previousPairNumbers = before.Select(pair => pair.PairNumber).ToHashSet();
            Com0ComPairInfo? createdPair = after.FirstOrDefault(pair => !previousPairNumbers.Contains(pair.PairNumber));
            ServiceHostLog.Write($"com0com pair created: requested={portA}<->{portB}; actual={createdPair?.DisplayName ?? "unknown"}");
            return ServiceHostResponse.FromObject(
                request.RequestId,
                true,
                "com0com_pair_created",
                new
                {
                    installation = BuildInstallationData(installation),
                    createdPair,
                    pairs = after,
                    output = CombineOutput(createResult.Output, afterResult.Output),
                });
        }
    }

    public static ServiceHostResponse DeletePair(ServiceHostRequest request)
    {
        Com0ComInstallation installation = ResolveInstallation();
        if (!installation.Installed)
            return ServiceHostResponse.FromObject(request.RequestId, false, "com0com_not_installed", BuildStatusData(installation, [], string.Empty));

        if (!int.TryParse(request.Data?["pairNumber"]?.ToString(), out int pairNumber) || pairNumber < 0 || pairNumber > 999999)
            return ServiceHostResponse.FromObject(request.RequestId, false, "invalid_com0com_pair_number");

        lock (CommandLock)
        {
            SetupCommandResult beforeResult = RunSetupCommand(installation, ["list"], ListCommandTimeoutMilliseconds);
            if (!beforeResult.Success)
                return CreateCommandFailure(request.RequestId, "com0com list failed", installation, beforeResult);

            IReadOnlyList<Com0ComPairInfo> before = ParsePairs(beforeResult.Output);
            Com0ComPairInfo? pair = before.FirstOrDefault(item => item.PairNumber == pairNumber);
            if (pair == null)
                return ServiceHostResponse.FromObject(request.RequestId, false, $"com0com_pair_not_found: {pairNumber}");

            SetupCommandResult deleteResult = RunSetupCommand(
                installation,
                ["remove", pairNumber.ToString()],
                MutationCommandTimeoutMilliseconds);
            if (!deleteResult.Success)
                return CreateCommandFailure(request.RequestId, "com0com pair deletion failed", installation, deleteResult);

            SetupCommandResult afterResult = RunSetupCommand(installation, ["list"], ListCommandTimeoutMilliseconds);
            if (!afterResult.Success)
                return CreateCommandFailure(request.RequestId, "com0com pair deleted but refresh failed", installation, afterResult);

            IReadOnlyList<Com0ComPairInfo> after = ParsePairs(afterResult.Output);
            if (after.Any(item => item.PairNumber == pairNumber))
            {
                return ServiceHostResponse.FromObject(
                    request.RequestId,
                    false,
                    $"com0com_pair_still_exists: {pairNumber}",
                    BuildStatusData(installation, after, CombineOutput(deleteResult.Output, afterResult.Output)));
            }

            ServiceHostLog.Write($"com0com pair deleted: {pair.DisplayName} ({pair.PairNumber})");
            return ServiceHostResponse.FromObject(
                request.RequestId,
                true,
                "com0com_pair_deleted",
                new
                {
                    installation = BuildInstallationData(installation),
                    deletedPair = pair,
                    pairs = after,
                    output = CombineOutput(deleteResult.Output, afterResult.Output),
                });
        }
    }

    internal static string NormalizeRequestedPort(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "COM#";

        string normalized = value.Trim().ToUpperInvariant();
        Match match = PortNameRegex.Match(normalized);
        if (!match.Success
            || !int.TryParse(match.Groups["number"].Value, out int portNumber)
            || portNumber < 1
            || portNumber > MaximumComPortNumber)
        {
            throw new InvalidOperationException($"Invalid COM port name: {value}. Expected 1 through 256 or COM1 through COM256, or leave it empty for automatic assignment.");
        }

        return $"COM{portNumber}";
    }

    internal static IReadOnlyList<int> FindAvailablePortNumbers(IEnumerable<string> existingPortNames)
    {
        HashSet<int> existingPortNumbers = existingPortNames
            .Select(TryGetComPortNumber)
            .Where(portNumber => portNumber.HasValue)
            .Select(portNumber => portNumber!.Value)
            .ToHashSet();

        return Enumerable.Range(1, MaximumComPortNumber)
            .Where(portNumber => !existingPortNumbers.Contains(portNumber))
            .ToArray();
    }

    internal static Com0ComPortPairSuggestion? FindSuggestedPair(IReadOnlyList<int> availablePortNumbers)
    {
        HashSet<int> available = availablePortNumbers.ToHashSet();
        for (int portNumber = FirstPreferredComPortNumber; portNumber < MaximumComPortNumber; portNumber++)
        {
            if (available.Contains(portNumber) && available.Contains(portNumber + 1))
                return new Com0ComPortPairSuggestion(portNumber, portNumber + 1);
        }

        for (int portNumber = 1; portNumber < FirstPreferredComPortNumber; portNumber++)
        {
            if (available.Contains(portNumber) && available.Contains(portNumber + 1))
                return new Com0ComPortPairSuggestion(portNumber, portNumber + 1);
        }

        return availablePortNumbers.Count >= 2
            ? new Com0ComPortPairSuggestion(availablePortNumbers[0], availablePortNumbers[1])
            : null;
    }

    private static int? TryGetComPortNumber(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        Match match = PortNameRegex.Match(value.Trim());
        return match.Success
            && int.TryParse(match.Groups["number"].Value, out int portNumber)
            && portNumber is >= 1 and <= MaximumComPortNumber
                ? portNumber
                : null;
    }

    internal static IReadOnlyList<Com0ComPairInfo> ParsePairs(string output)
    {
        Dictionary<int, Com0ComPairBuilder> builders = [];
        foreach (string line in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            Match match = PairLineRegex.Match(line);
            if (!match.Success || !int.TryParse(match.Groups["number"].Value, out int pairNumber))
                continue;

            if (!builders.TryGetValue(pairNumber, out Com0ComPairBuilder? builder))
            {
                builder = new Com0ComPairBuilder(pairNumber);
                builders.Add(pairNumber, builder);
            }

            string internalName = match.Groups["side"].Value.ToUpperInvariant() + pairNumber;
            string displayName = ResolveDisplayPortName(internalName, match.Groups["settings"].Value);
            if (internalName.StartsWith("CNCA", StringComparison.Ordinal))
                builder.SetPortA(internalName, displayName);
            else
                builder.SetPortB(internalName, displayName);
        }

        return builders.Values
            .OrderBy(builder => builder.PairNumber)
            .Select(builder => builder.Build())
            .ToArray();
    }

    private static string ResolveDisplayPortName(string internalName, string settings)
    {
        Dictionary<string, string> values = settings
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => part.Split('=', 2, StringSplitOptions.TrimEntries))
            .Where(parts => parts.Length == 2)
            .GroupBy(parts => parts[0], StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last()[1], StringComparer.OrdinalIgnoreCase);

        if (values.TryGetValue("RealPortName", out string? realPortName) && !string.IsNullOrWhiteSpace(realPortName))
            return realPortName.ToUpperInvariant();
        if (values.TryGetValue("PortName", out string? portName)
            && !string.IsNullOrWhiteSpace(portName)
            && !string.Equals(portName, "-", StringComparison.Ordinal)
            && !string.Equals(portName, "COM#", StringComparison.OrdinalIgnoreCase))
        {
            return portName.ToUpperInvariant();
        }

        return internalName;
    }

    private static HashSet<string> GetExistingPortNames(IReadOnlyList<Com0ComPairInfo> pairs)
    {
        HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);
        foreach (Com0ComPairInfo pair in pairs)
        {
            names.Add(pair.PortA);
            names.Add(pair.PortB);
            names.Add(pair.InternalPortA);
            names.Add(pair.InternalPortB);
        }

        using (RegistryKey? serialMap = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DEVICEMAP\SERIALCOMM"))
        {
            if (serialMap != null)
            {
                foreach (string valueName in serialMap.GetValueNames())
                {
                    if (serialMap.GetValue(valueName) is string portName && !string.IsNullOrWhiteSpace(portName))
                        names.Add(portName.Trim());
                }
            }
        }

        using RegistryKey? comNameArbiter = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\COM Name Arbiter");
        if (comNameArbiter?.GetValue("ComDB") is byte[] comDatabase)
        {
            for (int portNumber = 1; portNumber <= MaximumComPortNumber; portNumber++)
            {
                int zeroBasedPortNumber = portNumber - 1;
                int byteIndex = zeroBasedPortNumber / 8;
                int bitIndex = zeroBasedPortNumber % 8;
                if (byteIndex < comDatabase.Length && (comDatabase[byteIndex] & (1 << bitIndex)) != 0)
                    names.Add($"COM{portNumber}");
            }
        }

        return names;
    }

    private static Com0ComInstallation ResolveInstallation()
    {
        string? setupPath = ResolveSetupExecutablePath();
        string driverState = GetDriverState();
        bool installed = setupPath != null && !string.Equals(driverState, "NotInstalled", StringComparison.OrdinalIgnoreCase);
        string version = setupPath == null
            ? string.Empty
            : ResolveInstalledVersion(setupPath);
        return new Com0ComInstallation(installed, setupPath ?? string.Empty, version, driverState);
    }

    private static string ResolveInstalledVersion(string setupPath)
    {
        string fileVersion = FileVersionInfo.GetVersionInfo(setupPath).FileVersion ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(fileVersion))
            return fileVersion;

        foreach (RegistryView view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            using RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
            using RegistryKey? uninstallKey = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\com0com");
            if (uninstallKey?.GetValue("DisplayVersion") is string displayVersion && !string.IsNullOrWhiteSpace(displayVersion))
                return displayVersion.Trim();
        }

        return string.Empty;
    }

    private static string? ResolveSetupExecutablePath()
    {
        List<string> candidates = [];
        foreach (RegistryView view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            using RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
            using RegistryKey? uninstallKey = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\com0com");
            if (uninstallKey?.GetValue("InstallLocation") is string installLocation && !string.IsNullOrWhiteSpace(installLocation))
                candidates.Add(Path.Combine(installLocation.Trim().Trim('"'), SetupExecutableName));
        }

        string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        if (!string.IsNullOrWhiteSpace(programFilesX86))
            candidates.Add(Path.Combine(programFilesX86, "com0com", SetupExecutableName));
        if (!string.IsNullOrWhiteSpace(programFiles))
            candidates.Add(Path.Combine(programFiles, "com0com", SetupExecutableName));

        foreach (string candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(candidate);
            }
            catch
            {
                continue;
            }

            if (File.Exists(fullPath) && IsUnderProgramFiles(fullPath, programFiles, programFilesX86))
                return fullPath;
        }

        return null;
    }

    private static bool IsUnderProgramFiles(string path, params string[] roots)
    {
        foreach (string root in roots.Where(value => !string.IsNullOrWhiteSpace(value)))
        {
            string relativePath = Path.GetRelativePath(Path.GetFullPath(root), path);
            if (!Path.IsPathRooted(relativePath)
                && !relativePath.Equals("..", StringComparison.Ordinal)
                && !relativePath.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string GetDriverState()
    {
        try
        {
            using ServiceController controller = new(DriverServiceName);
            return controller.Status.ToString();
        }
        catch (InvalidOperationException)
        {
            return "NotInstalled";
        }
        catch (Exception ex)
        {
            return $"Unknown: {ex.Message}";
        }
    }

    private static SetupCommandResult RunSetupCommand(
        Com0ComInstallation installation,
        IReadOnlyList<string> commandArguments,
        int timeoutMilliseconds)
    {
        if (!installation.Installed || string.IsNullOrWhiteSpace(installation.SetupExecutablePath))
            return new SetupCommandResult(-1, string.Empty, "com0com is not installed.");

        string outputDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "ColorVision",
            "ServiceHost",
            "Com0Com");
        Directory.CreateDirectory(outputDirectory);
        string outputPath = Path.Combine(outputDirectory, $"setupc-{Guid.NewGuid():N}.log");

        try
        {
            using Process process = new()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = installation.SetupExecutablePath,
                    WorkingDirectory = Path.GetDirectoryName(installation.SetupExecutablePath) ?? string.Empty,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                },
            };
            process.StartInfo.ArgumentList.Add("--output");
            process.StartInfo.ArgumentList.Add(outputPath);
            foreach (string argument in commandArguments)
                process.StartInfo.ArgumentList.Add(argument);

            process.Start();
            Task<string> standardOutputTask = process.StandardOutput.ReadToEndAsync();
            Task<string> standardErrorTask = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(timeoutMilliseconds))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                }

                return new SetupCommandResult(-1, ReadOutputFile(outputPath), "com0com setup command timed out.");
            }

            string output = CombineOutput(ReadOutputFile(outputPath), ReadCompletedOutput(standardOutputTask));
            string error = ReadCompletedOutput(standardErrorTask);
            return new SetupCommandResult(process.ExitCode, output, error);
        }
        catch (Exception ex)
        {
            return new SetupCommandResult(-1, ReadOutputFile(outputPath), ex.Message);
        }
        finally
        {
            try
            {
                File.Delete(outputPath);
            }
            catch
            {
            }
        }
    }

    private static string ReadCompletedOutput(Task<string> task)
    {
        try
        {
            return task.Wait(TimeSpan.FromSeconds(2)) ? task.Result.Trim() : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string ReadOutputFile(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path).Trim() : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static ServiceHostResponse CreateCommandFailure(
        string requestId,
        string message,
        Com0ComInstallation installation,
        SetupCommandResult result)
    {
        string detail = string.IsNullOrWhiteSpace(result.Error) ? result.Output : result.Error;
        string responseMessage = string.IsNullOrWhiteSpace(detail)
            ? $"{message}: exit code {result.ExitCode}"
            : $"{message}: {detail}";
        return ServiceHostResponse.FromObject(
            requestId,
            false,
            responseMessage,
            new
            {
                installation = BuildInstallationData(installation),
                result.ExitCode,
                result.Output,
                result.Error,
            });
    }

    private static object BuildStatusData(
        Com0ComInstallation installation,
        IReadOnlyList<Com0ComPairInfo> pairs,
        string output)
    {
        IReadOnlyList<int> availablePortNumbers = installation.Installed
            ? FindAvailablePortNumbers(GetExistingPortNames(pairs))
            : [];
        Com0ComPortPairSuggestion? suggestedPair = FindSuggestedPair(availablePortNumbers);
        return new
        {
            installation.Installed,
            installation.SetupExecutablePath,
            installation.Version,
            installation.DriverState,
            pairs,
            availablePortNumbers,
            suggestedPair,
            output,
        };
    }

    private static object BuildInstallationData(Com0ComInstallation installation)
    {
        return new
        {
            installation.Installed,
            installation.SetupExecutablePath,
            installation.Version,
            installation.DriverState,
        };
    }

    private static string CombineOutput(string first, string second)
    {
        if (string.IsNullOrWhiteSpace(first))
            return second;
        if (string.IsNullOrWhiteSpace(second))
            return first;
        return first.Trim() + Environment.NewLine + second.Trim();
    }

    private sealed class Com0ComPairBuilder(int pairNumber)
    {
        public int PairNumber { get; } = pairNumber;
        private string InternalPortA { get; set; } = $"CNCA{pairNumber}";
        private string InternalPortB { get; set; } = $"CNCB{pairNumber}";
        private string PortA { get; set; } = $"CNCA{pairNumber}";
        private string PortB { get; set; } = $"CNCB{pairNumber}";

        public void SetPortA(string internalName, string displayName)
        {
            InternalPortA = internalName;
            PortA = displayName;
        }

        public void SetPortB(string internalName, string displayName)
        {
            InternalPortB = internalName;
            PortB = displayName;
        }

        public Com0ComPairInfo Build() => new(PairNumber, PortA, PortB, InternalPortA, InternalPortB);
    }

    private sealed record Com0ComInstallation(bool Installed, string SetupExecutablePath, string Version, string DriverState);

    private sealed record SetupCommandResult(int ExitCode, string Output, string Error)
    {
        public bool Success => ExitCode == 0;
    }
}

internal sealed record Com0ComPairInfo(
    int PairNumber,
    string PortA,
    string PortB,
    string InternalPortA,
    string InternalPortB)
{
    public string DisplayName => $"{PortA} ↔ {PortB}";
}

internal sealed record Com0ComPortPairSuggestion(int PortA, int PortB);
