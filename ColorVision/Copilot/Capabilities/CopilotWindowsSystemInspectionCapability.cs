using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public sealed record CopilotWindowsSystemInfoSnapshot(
        string ProductName,
        string DisplayVersion,
        string EditionId,
        string InstallationType,
        string OsVersion,
        int BuildNumber,
        int UpdateBuildRevision,
        string OsArchitecture,
        string ProcessArchitecture,
        string FrameworkDescription);

    public interface ICopilotWindowsSystemInfoProvider
    {
        CopilotWindowsSystemInfoSnapshot Read();
    }

    public sealed class CopilotWindowsSystemInfoProvider : ICopilotWindowsSystemInfoProvider
    {
        private const string CurrentVersionRegistryPath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";

        public CopilotWindowsSystemInfoSnapshot Read()
        {
            var registryValues = ReadCurrentVersionRegistry();
            var osVersion = Environment.OSVersion.Version;
            var buildNumber = GetInt32(registryValues, "CurrentBuildNumber", osVersion.Build);
            var updateBuildRevision = GetInt32(registryValues, "UBR", Math.Max(0, osVersion.Revision));
            var productName = NormalizeProductName(GetString(registryValues, "ProductName", RuntimeInformation.OSDescription), buildNumber);

            return new CopilotWindowsSystemInfoSnapshot(
                productName,
                GetString(registryValues, "DisplayVersion", GetString(registryValues, "ReleaseId", string.Empty)),
                GetString(registryValues, "EditionID", string.Empty),
                GetString(registryValues, "InstallationType", string.Empty),
                osVersion.ToString(),
                buildNumber,
                updateBuildRevision,
                RuntimeInformation.OSArchitecture.ToString(),
                RuntimeInformation.ProcessArchitecture.ToString(),
                RuntimeInformation.FrameworkDescription);
        }

        private static Dictionary<string, object?> ReadCurrentVersionRegistry()
        {
            var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var view in new[] { RegistryView.Registry64, RegistryView.Default })
            {
                try
                {
                    using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
                    using var key = baseKey.OpenSubKey(CurrentVersionRegistryPath, writable: false);
                    if (key == null)
                        continue;

                    foreach (var name in new[] { "ProductName", "DisplayVersion", "ReleaseId", "EditionID", "InstallationType", "CurrentBuildNumber", "UBR" })
                        values[name] = key.GetValue(name);
                    break;
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or System.IO.IOException)
                {
                    // RuntimeInformation and Environment.OSVersion remain available as bounded fallbacks.
                }
            }
            return values;
        }

        private static string GetString(Dictionary<string, object?> values, string name, string fallback)
        {
            if (!values.TryGetValue(name, out var value) || value == null)
                return Normalize(fallback);
            return Normalize(Convert.ToString(value, CultureInfo.InvariantCulture) ?? fallback);
        }

        private static int GetInt32(Dictionary<string, object?> values, string name, int fallback)
        {
            if (!values.TryGetValue(name, out var value) || value == null)
                return Math.Max(0, fallback);
            if (value is int intValue)
                return Math.Max(0, intValue);
            if (value is long longValue && longValue is >= 0 and <= int.MaxValue)
                return (int)longValue;
            return int.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                ? Math.Max(0, parsed)
                : Math.Max(0, fallback);
        }

        private static string NormalizeProductName(string productName, int buildNumber)
        {
            if (buildNumber >= 22000 && productName.Contains("Windows 10", StringComparison.OrdinalIgnoreCase))
                return productName.Replace("Windows 10", "Windows 11", StringComparison.OrdinalIgnoreCase);
            return productName;
        }

        private static string Normalize(string value)
        {
            var normalized = (value ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Trim();
            return normalized.Length <= 256 ? normalized : normalized[..256];
        }
    }

    public sealed class CopilotWindowsSystemInspectionService
    {
        private readonly ICopilotWindowsSystemInfoProvider _provider;

        public CopilotWindowsSystemInspectionService()
            : this(new CopilotWindowsSystemInfoProvider())
        {
        }

        public CopilotWindowsSystemInspectionService(ICopilotWindowsSystemInfoProvider provider)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        public Task<CopilotToolResult> ExecuteAsync(CopilotAgentRequest request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            cancellationToken.ThrowIfCancellationRequested();
            if (!OperatingSystem.IsWindows())
            {
                return Task.FromResult(new CopilotToolResult
                {
                    ToolName = "InspectWindowsSystem",
                    Success = false,
                    FailureKind = CopilotToolFailureKind.NotFound,
                    Summary = "Windows system information is unavailable.",
                    ErrorMessage = "This fixed diagnostic is available only on Windows.",
                });
            }

            CopilotWindowsSystemInfoSnapshot snapshot;
            try
            {
                snapshot = _provider.Read();
            }
            catch
            {
                return Task.FromResult(new CopilotToolResult
                {
                    ToolName = "InspectWindowsSystem",
                    Success = false,
                    FailureKind = CopilotToolFailureKind.Internal,
                    Summary = "Windows system information could not be inspected.",
                    ErrorMessage = "The fixed Windows system information provider failed.",
                });
            }

            var result = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["product_name"] = snapshot.ProductName,
                ["display_version"] = snapshot.DisplayVersion,
                ["edition_id"] = snapshot.EditionId,
                ["installation_type"] = snapshot.InstallationType,
                ["os_version"] = snapshot.OsVersion,
                ["build_number"] = snapshot.BuildNumber,
                ["update_build_revision"] = snapshot.UpdateBuildRevision,
                ["os_architecture"] = snapshot.OsArchitecture,
                ["process_architecture"] = snapshot.ProcessArchitecture,
                ["framework_description"] = snapshot.FrameworkDescription,
            };
            var displayVersion = string.IsNullOrWhiteSpace(snapshot.DisplayVersion) ? string.Empty : $" {snapshot.DisplayVersion}";
            var summary = $"{snapshot.ProductName}{displayVersion}, build {snapshot.BuildNumber}.{snapshot.UpdateBuildRevision}, {snapshot.OsArchitecture}.";

            return Task.FromResult(new CopilotToolResult
            {
                ToolName = "InspectWindowsSystem",
                Success = true,
                Summary = summary,
                Content = $"[Windows System Inspection]\nproduct_name: {snapshot.ProductName}\ndisplay_version: {snapshot.DisplayVersion}\nedition_id: {snapshot.EditionId}\ninstallation_type: {snapshot.InstallationType}\nos_version: {snapshot.OsVersion}\nbuild: {snapshot.BuildNumber}.{snapshot.UpdateBuildRevision}\nos_architecture: {snapshot.OsArchitecture}\nprocess_architecture: {snapshot.ProcessArchitecture}\nframework: {snapshot.FrameworkDescription}\nresult_json: {JsonSerializer.Serialize(result)}",
            });
        }
    }
}
