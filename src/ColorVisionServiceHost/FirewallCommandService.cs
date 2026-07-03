using System.Diagnostics;
using System.Security.Principal;

namespace ColorVisionServiceHost;

internal static class FirewallCommandService
{
    private const int NetFwProfileDomain = 1;
    private const int NetFwProfilePrivate = 2;
    private const int NetFwProfilePublic = 4;
    private const int NetFwProfileAll = int.MaxValue;

    public static ServiceHostResponse AllowApplication(ServiceHostRequest request)
    {
        string appPath = Path.GetFullPath(GetRequiredDataValue(request, "appPath"));
        if (!File.Exists(appPath))
            return ServiceHostResponse.FromObject(request.RequestId, false, $"Application executable was not found: {appPath}");

        string profile = GetOptionalDataValue(request, "profile", string.Empty);
        FirewallAllowResult result = AddApplicationRule(appPath, profile);
        return ServiceHostResponse.FromObject(request.RequestId, result.Success, result.Message, new
        {
            appPath,
            profile = result.Profile,
            ruleName = result.RuleName,
            identity = WindowsIdentity.GetCurrent().Name,
            isElevated = IsElevated(),
        });
    }

    private static FirewallAllowResult AddApplicationRule(string appPath, string profile)
    {
        string profileArgument = GetProfileArgument(profile);
        string ruleName = BuildFirewallRuleName(appPath, profileArgument);
        try
        {
            ProcessResult deleteResult = RunProcess("netsh.exe", ["advfirewall", "firewall", "delete", "rule", $"name={ruleName}"], 15000);
            ProcessResult addResult = RunProcess(
                "netsh.exe",
                [
                    "advfirewall",
                    "firewall",
                    "add",
                    "rule",
                    $"name={ruleName}",
                    "dir=in",
                    "action=allow",
                    $"program={appPath}",
                    "enable=yes",
                    "protocol=any",
                    $"profile={profileArgument}"
                ],
                15000);

            if (addResult.ExitCode != 0)
                return new FirewallAllowResult(false, ruleName, profileArgument, BuildProcessFailureMessage("防火墙允许规则创建失败", addResult));

            ServiceHostLog.Write($"Firewall allow rule added. Rule={ruleName}, App={appPath}, Profile={profileArgument}, DeleteExit={deleteResult.ExitCode}");
            return new FirewallAllowResult(true, ruleName, profileArgument, $"已创建防火墙入站允许规则：{ruleName}（{FormatProfileDisplay(profileArgument)}）");
        }
        catch (Exception ex)
        {
            ServiceHostLog.Write($"Firewall allow rule add failed for {appPath}: {ex}");
            return new FirewallAllowResult(false, ruleName, profileArgument, $"创建防火墙允许规则失败：{ex.Message}");
        }
    }

    private static string BuildFirewallRuleName(string appPath, string profileArgument)
    {
        string appName = Path.GetFileNameWithoutExtension(appPath);
        return $"ColorVision Application {FormatProfileDisplay(profileArgument)} ({appName})";
    }

    private static string GetProfileArgument(string profile)
    {
        return profile.Trim().ToLowerInvariant() switch
        {
            "domain" => "domain",
            "private" => "private",
            "public" => "public",
            _ => GetActiveProfileArgument()
        };
    }

    private static string GetActiveProfileArgument()
    {
        object? policy = null;
        try
        {
            Type? policyType = Type.GetTypeFromProgID("HNetCfg.FwPolicy2");
            policy = policyType == null ? null : Activator.CreateInstance(policyType);
            if (policy == null)
                return "domain,private,public";

            dynamic firewallPolicy = policy;
            int activeProfiles = Convert.ToInt32(firewallPolicy.CurrentProfileTypes, System.Globalization.CultureInfo.InvariantCulture);
            if (activeProfiles == 0 || activeProfiles == NetFwProfileAll)
                return "domain,private,public";

            var profiles = new List<string>();
            if ((activeProfiles & NetFwProfileDomain) != 0)
                profiles.Add("domain");
            if ((activeProfiles & NetFwProfilePrivate) != 0)
                profiles.Add("private");
            if ((activeProfiles & NetFwProfilePublic) != 0)
                profiles.Add("public");
            return profiles.Count == 0 ? "domain,private,public" : string.Join(",", profiles);
        }
        catch (Exception ex)
        {
            ServiceHostLog.Write($"Failed to resolve active firewall profile, using all profiles. {ex.Message}");
            return "domain,private,public";
        }
        finally
        {
            if (policy != null && System.Runtime.InteropServices.Marshal.IsComObject(policy))
                System.Runtime.InteropServices.Marshal.ReleaseComObject(policy);
        }
    }

    private static string FormatProfileDisplay(string profileArgument)
    {
        return profileArgument
            .Replace("domain", "域", StringComparison.OrdinalIgnoreCase)
            .Replace("private", "专用", StringComparison.OrdinalIgnoreCase)
            .Replace("public", "公用", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetRequiredDataValue(ServiceHostRequest request, string name)
    {
        string? value = request.Data?[name]?.ToString();
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"Missing request data: {name}");

        return value;
    }

    private static string GetOptionalDataValue(ServiceHostRequest request, string name, string defaultValue)
    {
        string? value = request.Data?[name]?.ToString();
        return string.IsNullOrWhiteSpace(value) ? defaultValue : value;
    }

    private static ProcessResult RunProcess(string fileName, IReadOnlyList<string> arguments, int timeoutMilliseconds)
    {
        using Process process = new()
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            },
        };

        foreach (string argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        process.Start();
        Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
        Task<string> errorTask = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(timeoutMilliseconds))
        {
            KillProcessTree(process);
            string timeoutOutput = ReadCompletedOutput(outputTask);
            string timeoutError = AppendProcessError(ReadCompletedOutput(errorTask), "Process timed out.");
            return new ProcessResult(fileName, FormatArgumentsForLog(arguments), -1, timeoutOutput, timeoutError);
        }

        string output = ReadCompletedOutput(outputTask);
        string error = ReadCompletedOutput(errorTask);
        return new ProcessResult(fileName, FormatArgumentsForLog(arguments), process.ExitCode, output, error);
    }

    private static string BuildProcessFailureMessage(string message, ProcessResult result)
    {
        return string.IsNullOrWhiteSpace(result.Error)
            ? $"{message}: {result.ExitCode}"
            : $"{message}: {result.ExitCode} ({result.Error.Trim()})";
    }

    private static void KillProcessTree(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch (Exception ex)
        {
            ServiceHostLog.Write($"Failed to kill timed out process {process.StartInfo.FileName}: {ex.Message}");
        }
    }

    private static string ReadCompletedOutput(Task<string> outputTask)
    {
        try
        {
            return outputTask.Wait(1000) ? outputTask.GetAwaiter().GetResult() : string.Empty;
        }
        catch (Exception ex)
        {
            return $"Failed to read process output: {ex.Message}";
        }
    }

    private static string AppendProcessError(string currentError, string message)
    {
        return string.IsNullOrWhiteSpace(currentError)
            ? message
            : currentError.TrimEnd() + Environment.NewLine + message;
    }

    private static string FormatArgumentsForLog(IEnumerable<string> arguments)
    {
        return string.Join(" ", arguments.Select(argument =>
            argument.Any(char.IsWhiteSpace) || argument.Contains('"', StringComparison.Ordinal)
                ? $"\"{argument.Replace("\"", "\\\"", StringComparison.Ordinal)}\""
                : argument));
    }

    private static bool IsElevated()
    {
        try
        {
            using WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    private sealed record ProcessResult(string FileName, string Arguments, int ExitCode, string Output, string Error);

    private sealed record FirewallAllowResult(bool Success, string RuleName, string Profile, string Message);
}
