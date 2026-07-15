using ColorVision.UI.Plugins;
using Newtonsoft.Json;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace ColorVision.UI.Desktop.Marketplace
{
    internal sealed class MarketplaceCopilotRolePreview
    {
        public string Id { get; init; } = string.Empty;

        public string ToolName { get; init; } = string.Empty;

        public string DisplayName { get; init; } = string.Empty;

        public string Scope { get; init; } = string.Empty;

        public IReadOnlyList<string> Capabilities { get; init; } = Array.Empty<string>();

        public int MaximumToolCalls { get; init; }

        public int MaximumAgentPasses { get; init; }

        public int MaximumDurationSeconds { get; init; }

        public int MaximumAnswerCharacters { get; init; }

        public int AdvertisedCharacters { get; init; }
    }

    internal sealed class MarketplaceCopilotPackagePreflight
    {
        public bool IsValid { get; init; }

        public string PluginId { get; init; } = string.Empty;

        public string PluginName { get; init; } = string.Empty;

        public string Version { get; init; } = string.Empty;

        public IReadOnlyList<MarketplaceCopilotRolePreview> Roles { get; init; } = Array.Empty<MarketplaceCopilotRolePreview>();

        public string ErrorMessage { get; init; } = string.Empty;

        public bool RequiresPermissionReview => IsValid && Roles.Count > 0;

        public int AdvertisedCharacters => Roles.Sum(role => role.AdvertisedCharacters);
    }

    internal static class MarketplaceCopilotPackagePreflightReader
    {
        private const long MaximumManifestBytes = 1_048_576;

        public static MarketplaceCopilotPackagePreflight Read(string packagePath, MarketplacePackageRequest? request = null)
        {
            try
            {
                using ZipArchive archive = ZipFile.OpenRead(packagePath);
                List<ZipArchiveEntry> allManifestEntries = archive.Entries
                    .Where(entry => string.Equals(Path.GetFileName(entry.FullName), "manifest.json", StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (allManifestEntries.Any(entry => !IsSafeArchivePath(entry.FullName)))
                    return Invalid("The package contains an unsafe manifest path.");

                List<ZipArchiveEntry> manifestEntries = allManifestEntries
                    .Where(entry => GetPathSegments(entry.FullName).Length <= 2)
                    .ToList();
                if (manifestEntries.Count == 0)
                    return ValidWithoutRoles(request);
                if (manifestEntries.Count > 1)
                    return Invalid("The package contains more than one top-level manifest.json.");

                ZipArchiveEntry manifestEntry = manifestEntries[0];
                if (manifestEntry.Length > MaximumManifestBytes)
                    return Invalid($"manifest.json exceeds the {MaximumManifestBytes:N0}-byte inspection limit.");

                PluginManifest manifest = ReadManifest(manifestEntry);
                if (request != null
                    && !string.IsNullOrWhiteSpace(manifest.Id)
                    && !string.Equals(manifest.Id.Trim(), request.PluginId.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return Invalid($"Manifest id '{manifest.Id.Trim()}' does not match requested package '{request.PluginId.Trim()}'.");
                }

                List<CopilotSubagentRoleManifest> roleManifests = manifest.CopilotAgents ?? [];
                if (roleManifests.Count == 0)
                {
                    return new MarketplaceCopilotPackagePreflight
                    {
                        IsValid = true,
                        PluginId = FirstNonEmpty(manifest.Id, request?.PluginId),
                        PluginName = FirstNonEmpty(manifest.Name, manifest.Id, request?.PluginId),
                        Version = FirstNonEmpty(manifest.Version, request?.Version),
                    };
                }

                if (roleManifests.Count > CopilotSubagentRoleManifestValidator.MaximumRolesPerPlugin)
                    return Invalid($"A plugin can declare at most {CopilotSubagentRoleManifestValidator.MaximumRolesPerPlugin} Copilot roles.");

                string pluginId = FirstNonEmpty(manifest.Id, request?.PluginId);
                string pluginName = FirstNonEmpty(manifest.Name, manifest.Id, request?.PluginId);
                string version = FirstNonEmpty(manifest.Version, request?.Version, "0");
                CopilotSubagentRoleManifestValidator.ValidatePluginSource(pluginId, pluginName, version);

                var roles = new List<MarketplaceCopilotRolePreview>(roleManifests.Count);
                var roleIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var toolNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (CopilotSubagentRoleManifest roleManifest in roleManifests)
                {
                    CopilotSubagentRoleManifestContract contract = CopilotSubagentRoleManifestValidator.Create(roleManifest);
                    if (!roleIds.Add(contract.Id))
                        return Invalid($"Copilot role id '{contract.Id}' is declared more than once.");
                    if (!toolNames.Add(contract.ToolName))
                        return Invalid($"Copilot tool name '{contract.ToolName}' is declared more than once.");

                    roles.Add(new MarketplaceCopilotRolePreview
                    {
                        Id = contract.Id,
                        ToolName = contract.ToolName,
                        DisplayName = contract.DisplayName,
                        Scope = contract.Scope,
                        Capabilities = contract.Capabilities,
                        MaximumToolCalls = contract.MaximumToolCalls,
                        MaximumAgentPasses = contract.MaximumAgentPasses,
                        MaximumDurationSeconds = contract.MaximumDurationSeconds,
                        MaximumAnswerCharacters = contract.MaximumAnswerCharacters,
                        AdvertisedCharacters = contract.AdvertisedCharacters,
                    });
                }

                int advertisedCharacters = roles.Sum(role => role.AdvertisedCharacters);
                if (advertisedCharacters > CopilotSubagentRoleManifestValidator.MaximumAdvertisedCharactersPerPlugin)
                {
                    return Invalid($"Copilot role names and descriptions exceed the per-plugin limit of {CopilotSubagentRoleManifestValidator.MaximumAdvertisedCharactersPerPlugin:N0} characters.");
                }

                return new MarketplaceCopilotPackagePreflight
                {
                    IsValid = true,
                    PluginId = pluginId,
                    PluginName = pluginName,
                    Version = version,
                    Roles = roles,
                };
            }
            catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException or JsonException or ArgumentException or FormatException)
            {
                return Invalid(ex.Message);
            }
        }

        private static PluginManifest ReadManifest(ZipArchiveEntry entry)
        {
            using Stream stream = entry.Open();
            using var reader = new StreamReader(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true), detectEncodingFromByteOrderMarks: true);
            var jsonBuilder = new StringBuilder((int)Math.Min(entry.Length, MaximumManifestBytes));
            char[] buffer = new char[8_192];
            int charactersRead;
            while ((charactersRead = reader.Read(buffer, 0, buffer.Length)) > 0)
            {
                if (jsonBuilder.Length + charactersRead > MaximumManifestBytes)
                    throw new InvalidDataException($"manifest.json exceeds the {MaximumManifestBytes:N0}-character inspection limit.");
                jsonBuilder.Append(buffer, 0, charactersRead);
            }

            string json = jsonBuilder.ToString();
            return JsonConvert.DeserializeObject<PluginManifest>(json, new JsonSerializerSettings { MaxDepth = 64 })
                ?? throw new JsonException("manifest.json must contain a JSON object.");
        }

        private static bool IsSafeArchivePath(string path)
        {
            string[] segments = GetPathSegments(path);
            return segments.Length > 0
                && !Path.IsPathRooted(path)
                && !path.Contains(':')
                && segments.All(segment => segment is not "." and not "..");
        }

        private static string[] GetPathSegments(string path)
        {
            return path.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        }

        private static MarketplaceCopilotPackagePreflight ValidWithoutRoles(MarketplacePackageRequest? request)
        {
            return new MarketplaceCopilotPackagePreflight
            {
                IsValid = true,
                PluginId = request?.PluginId ?? string.Empty,
                PluginName = request?.PluginId ?? string.Empty,
                Version = request?.Version ?? string.Empty,
            };
        }

        private static MarketplaceCopilotPackagePreflight Invalid(string message)
        {
            return new MarketplaceCopilotPackagePreflight
            {
                ErrorMessage = string.IsNullOrWhiteSpace(message) ? "Unknown package inspection error." : message.Trim(),
            };
        }

        private static string FirstNonEmpty(params string?[] values)
        {
            return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;
        }
    }
}
