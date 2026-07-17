using ColorVision.UI.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.Copilot
{
    public sealed class CopilotPluginSubagentRoleLoadIssue
    {
        public string SourceId { get; init; } = string.Empty;

        public string RoleId { get; init; } = string.Empty;

        public string Message { get; init; } = string.Empty;
    }

    public sealed class CopilotPluginSubagentRoleLoaderSnapshot
    {
        public int LoadedRoleCount { get; init; }

        public IReadOnlyList<CopilotPluginSubagentRoleInfo> DeclaredRoles { get; init; } = Array.Empty<CopilotPluginSubagentRoleInfo>();

        public IReadOnlyList<CopilotPluginSubagentRoleLoadIssue> Issues { get; init; } = Array.Empty<CopilotPluginSubagentRoleLoadIssue>();
    }

    public sealed class CopilotPluginSubagentRoleInfo
    {
        public string Key { get; init; } = string.Empty;

        public string SourceId { get; init; } = string.Empty;

        public string SourceName { get; init; } = string.Empty;

        public string RoleId { get; init; } = string.Empty;

        public string ToolName { get; init; } = string.Empty;

        public string DisplayName { get; init; } = string.Empty;

        public CopilotSubagentContextScope ContextScope { get; init; }

        public CopilotSubagentReadCapabilities ReadCapabilities { get; init; }

        public int MaximumToolCalls { get; init; }

        public int MaximumAgentPasses { get; init; }

        public TimeSpan MaximumDuration { get; init; }

        public int MaximumAnswerCharacters { get; init; }

        public int AdvertisedCharacters { get; init; }

        public bool IsEnabled { get; init; }
    }

    public sealed class CopilotPluginSubagentRoleLoader : IDisposable
    {
        internal const int MaximumRolesPerPlugin = CopilotSubagentRoleManifestValidator.MaximumRolesPerPlugin;
        internal const int MaximumAdvertisedCharactersPerPlugin = CopilotSubagentRoleManifestValidator.MaximumAdvertisedCharactersPerPlugin;

        private readonly CopilotSubagentRoleRegistry _roleRegistry;
        private readonly Dictionary<string, LoadedRegistration> _registrations = new(StringComparer.OrdinalIgnoreCase);
        private readonly object _syncRoot = new();
        private PluginInfo[] _trackedPlugins = Array.Empty<PluginInfo>();
        private CopilotPluginSubagentRoleLoadIssue[] _issues = Array.Empty<CopilotPluginSubagentRoleLoadIssue>();
        private CopilotPluginSubagentRoleInfo[] _declaredRoles = Array.Empty<CopilotPluginSubagentRoleInfo>();
        private HashSet<string> _disabledRoleKeys = new(StringComparer.OrdinalIgnoreCase);
        private bool _disposed;

        public CopilotPluginSubagentRoleLoader(CopilotSubagentRoleRegistry roleRegistry)
        {
            _roleRegistry = roleRegistry ?? throw new ArgumentNullException(nameof(roleRegistry));
        }

        public static CopilotPluginSubagentRoleLoader Shared { get; } = new(CopilotSubagentRoleRegistry.Shared);

        public CopilotPluginSubagentRoleLoaderSnapshot Synchronize(IEnumerable<PluginInfo>? plugins)
        {
            return Synchronize(plugins, Array.Empty<string>());
        }

        public CopilotPluginSubagentRoleLoaderSnapshot Synchronize(IEnumerable<PluginInfo>? plugins, IEnumerable<string>? disabledRoleKeys)
        {
            var pluginSnapshot = (plugins ?? Array.Empty<PluginInfo>()).Where(plugin => plugin != null).Distinct().ToArray();
            SetDisabledRoleKeysCore(disabledRoleKeys);
            TrackPlugins(pluginSnapshot);
            return SynchronizeCore(pluginSnapshot);
        }

        public CopilotPluginSubagentRoleLoaderSnapshot SetDisabledRoleKeys(IEnumerable<string>? disabledRoleKeys)
        {
            PluginInfo[] plugins;
            lock (_syncRoot)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                _disabledRoleKeys = CopilotPluginSubagentRolePreference.NormalizeKeys(disabledRoleKeys)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                plugins = _trackedPlugins.ToArray();
            }
            return SynchronizeCore(plugins);
        }

        private CopilotPluginSubagentRoleLoaderSnapshot SynchronizeCore(IReadOnlyList<PluginInfo> plugins)
        {
            var candidates = new List<RoleCandidate>();
            var declaredRoles = new List<CopilotPluginSubagentRoleInfo>();
            var issues = new List<CopilotPluginSubagentRoleLoadIssue>();
            HashSet<string> disabledRoleKeys;
            lock (_syncRoot)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                disabledRoleKeys = _disabledRoleKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);
            }
            foreach (var plugin in plugins
                .Where(plugin => plugin?.Enabled == true && plugin.Manifest != null)
                .OrderBy(plugin => plugin.Manifest.Id, StringComparer.OrdinalIgnoreCase))
            {
                var roleManifests = plugin.Manifest.CopilotAgents ?? [];
                if (roleManifests.Count == 0)
                    continue;
                if (roleManifests.Count > MaximumRolesPerPlugin)
                {
                    issues.Add(CreateIssue(plugin.Manifest.Id, string.Empty, $"A plugin can declare at most {MaximumRolesPerPlugin} Copilot roles."));
                    continue;
                }
                if (plugin.Assembly == null)
                {
                    issues.Add(CreateIssue(plugin.Manifest.Id, string.Empty, "The plugin assembly did not load, so its Copilot roles were not registered."));
                    continue;
                }

                var pluginCandidates = new List<RoleCandidate>();
                foreach (var roleManifest in roleManifests.Where(role => role != null))
                {
                    try
                    {
                        var registration = CreateRegistration(plugin, roleManifest);
                        var descriptor = CopilotSubagentRoleFactory.Create(registration, isBuiltIn: false);
                        pluginCandidates.Add(new RoleCandidate(registration, descriptor));
                    }
                    catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or FormatException)
                    {
                        issues.Add(CreateIssue(plugin.Manifest.Id, roleManifest.Id, ex.Message));
                    }
                }

                var advertisedCharacters = pluginCandidates.Sum(candidate =>
                    candidate.Descriptor.ToolName.Length
                    + candidate.Descriptor.DisplayName.Length
                    + candidate.Descriptor.Description.Length);
                if (advertisedCharacters > MaximumAdvertisedCharactersPerPlugin)
                {
                    issues.Add(CreateIssue(plugin.Manifest.Id, string.Empty, $"Copilot role names and descriptions exceed the per-plugin limit of {MaximumAdvertisedCharactersPerPlugin:N0} characters."));
                    continue;
                }

                foreach (var candidate in pluginCandidates)
                {
                    var key = CopilotPluginSubagentRolePreference.CreateKey(candidate.Descriptor.SourceId, candidate.Descriptor.Id);
                    var isEnabled = !disabledRoleKeys.Contains(key);
                    declaredRoles.Add(CreateRoleInfo(candidate.Descriptor, key, isEnabled));
                    if (isEnabled)
                        candidates.Add(candidate);
                }
            }

            var desired = new Dictionary<string, RoleCandidate>(StringComparer.OrdinalIgnoreCase);
            foreach (var candidate in candidates.OrderBy(candidate => candidate.Descriptor.Id, StringComparer.OrdinalIgnoreCase))
            {
                if (!desired.TryAdd(candidate.Descriptor.Id, candidate))
                    issues.Add(CreateIssue(candidate.Descriptor.SourceId, candidate.Descriptor.Id, "Another enabled plugin declared the same global subagent role id."));
            }

            lock (_syncRoot)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                foreach (var existing in _registrations.Values.ToArray())
                {
                    if (desired.TryGetValue(existing.RoleId, out var candidate)
                        && string.Equals(candidate.Descriptor.CapabilityFingerprint, existing.Fingerprint, StringComparison.Ordinal))
                    {
                        desired.Remove(existing.RoleId);
                        continue;
                    }

                    existing.Handle.Dispose();
                    _registrations.Remove(existing.RoleId);
                }

                foreach (var candidate in desired.Values.OrderBy(candidate => candidate.Descriptor.Id, StringComparer.OrdinalIgnoreCase))
                {
                    try
                    {
                        var handle = _roleRegistry.RegisterTrustedPluginRole(candidate.Registration);
                        _registrations.Add(candidate.Descriptor.Id, new LoadedRegistration(
                            candidate.Descriptor.Id,
                            candidate.Descriptor.CapabilityFingerprint,
                            handle));
                    }
                    catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
                    {
                        issues.Add(CreateIssue(candidate.Descriptor.SourceId, candidate.Descriptor.Id, ex.Message));
                    }
                }

                _issues = issues
                    .OrderBy(issue => issue.SourceId, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(issue => issue.RoleId, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(issue => issue.Message, StringComparer.Ordinal)
                    .ToArray();
                _declaredRoles = declaredRoles
                    .OrderBy(role => role.SourceId, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(role => role.RoleId, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                return CreateSnapshotLocked();
            }
        }

        public CopilotPluginSubagentRoleLoaderSnapshot GetSnapshot()
        {
            lock (_syncRoot)
                return CreateSnapshotLocked();
        }

        public void Dispose()
        {
            lock (_syncRoot)
            {
                if (_disposed)
                    return;
                _disposed = true;
                foreach (var plugin in _trackedPlugins)
                    plugin.PropertyChanged -= OnTrackedPluginPropertyChanged;
                _trackedPlugins = Array.Empty<PluginInfo>();
                foreach (var registration in _registrations.Values)
                    registration.Handle.Dispose();
                _registrations.Clear();
                _issues = Array.Empty<CopilotPluginSubagentRoleLoadIssue>();
                _declaredRoles = Array.Empty<CopilotPluginSubagentRoleInfo>();
                _disabledRoleKeys.Clear();
            }
        }

        private void TrackPlugins(PluginInfo[] plugins)
        {
            lock (_syncRoot)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                foreach (var plugin in _trackedPlugins)
                    plugin.PropertyChanged -= OnTrackedPluginPropertyChanged;
                _trackedPlugins = plugins;
                foreach (var plugin in _trackedPlugins)
                    plugin.PropertyChanged += OnTrackedPluginPropertyChanged;
            }
        }

        private void OnTrackedPluginPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (!string.Equals(e.PropertyName, nameof(PluginInfo.Enabled), StringComparison.Ordinal))
                return;

            PluginInfo[] plugins;
            lock (_syncRoot)
            {
                if (_disposed)
                    return;
                plugins = _trackedPlugins.ToArray();
            }
            SynchronizeCore(plugins);
        }

        private CopilotPluginSubagentRoleLoaderSnapshot CreateSnapshotLocked()
        {
            return new CopilotPluginSubagentRoleLoaderSnapshot
            {
                LoadedRoleCount = _registrations.Count,
                DeclaredRoles = _declaredRoles.ToArray(),
                Issues = _issues.ToArray(),
            };
        }

        private void SetDisabledRoleKeysCore(IEnumerable<string>? disabledRoleKeys)
        {
            lock (_syncRoot)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                _disabledRoleKeys = CopilotPluginSubagentRolePreference.NormalizeKeys(disabledRoleKeys)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
            }
        }

        private static CopilotPluginSubagentRoleInfo CreateRoleInfo(CopilotSubagentRoleDescriptor descriptor, string key, bool isEnabled)
        {
            return new CopilotPluginSubagentRoleInfo
            {
                Key = key,
                SourceId = descriptor.SourceId,
                SourceName = descriptor.SourceName,
                RoleId = descriptor.Id,
                ToolName = descriptor.ToolName,
                DisplayName = descriptor.DisplayName,
                ContextScope = descriptor.ContextScope,
                ReadCapabilities = descriptor.ReadCapabilities,
                MaximumToolCalls = descriptor.MaximumToolCalls,
                MaximumAgentPasses = descriptor.MaximumAgentPasses,
                MaximumDuration = descriptor.MaximumDuration,
                MaximumAnswerCharacters = descriptor.MaximumAnswerCharacters,
                AdvertisedCharacters = descriptor.ToolName.Length + descriptor.DisplayName.Length + descriptor.Description.Length,
                IsEnabled = isEnabled,
            };
        }

        private static CopilotSubagentRoleRegistration CreateRegistration(PluginInfo plugin, CopilotSubagentRoleManifest role)
        {
            CopilotSubagentRoleManifestValidator.ValidatePluginSource(plugin.Manifest.Id, FirstNonEmpty(plugin.Manifest.Name, plugin.Name, plugin.Manifest.Id), FirstNonEmpty(plugin.Manifest.Version, plugin.AssemblyVersion?.ToString(), "0"));
            CopilotSubagentRoleManifestContract contract = CopilotSubagentRoleManifestValidator.Create(role);

            return new CopilotSubagentRoleRegistration
            {
                SourceId = (plugin.Manifest.Id ?? string.Empty).Trim().ToLowerInvariant(),
                SourceName = FirstNonEmpty(plugin.Manifest.Name, plugin.Name, plugin.Manifest.Id),
                SourceVersion = FirstNonEmpty(plugin.Manifest.Version, plugin.AssemblyVersion?.ToString(), "0"),
                RoleId = contract.Id,
                ToolName = contract.ToolName,
                DisplayName = contract.DisplayName,
                Description = contract.Description,
                RuntimeInstructions = contract.Instructions,
                ContextScope = ParseScope(contract.Scope),
                ReadCapabilities = ParseCapabilities(contract.Capabilities),
                ChildMode = ParseEnum<CopilotAgentMode>(contract.ChildMode, "child mode"),
                ParentModes = contract.ParentModes.Select(value => ParseEnum<CopilotAgentMode>(value, "parent mode")).ToArray(),
                MaximumToolCalls = contract.MaximumToolCalls,
                MaximumAgentPasses = contract.MaximumAgentPasses,
                MaximumDuration = TimeSpan.FromSeconds(contract.MaximumDurationSeconds),
                MaximumAnswerCharacters = contract.MaximumAnswerCharacters,
            };
        }

        private static CopilotSubagentContextScope ParseScope(string? value)
        {
            var normalized = NormalizeToken(value);
            return normalized switch
            {
                "workspace" or "workspacereadonly" => CopilotSubagentContextScope.WorkspaceReadOnly,
                "web" or "publicweb" => CopilotSubagentContextScope.PublicWeb,
                _ => throw new FormatException("Subagent scope must be WorkspaceReadOnly or PublicWeb."),
            };
        }

        private static CopilotSubagentReadCapabilities ParseCapabilities(IReadOnlyList<string>? values)
        {
            var result = CopilotSubagentReadCapabilities.None;
            foreach (var value in values ?? Array.Empty<string>())
            {
                result |= NormalizeToken(value) switch
                {
                    "searchfiles" => CopilotSubagentReadCapabilities.SearchFiles,
                    "greptext" => CopilotSubagentReadCapabilities.GrepText,
                    "readlocalfile" => CopilotSubagentReadCapabilities.ReadLocalFile,
                    "listdirectory" => CopilotSubagentReadCapabilities.ListDirectory,
                    "websearch" => CopilotSubagentReadCapabilities.WebSearch,
                    "fetchurl" => CopilotSubagentReadCapabilities.FetchUrl,
                    _ => throw new FormatException($"Unknown subagent read capability '{value}'."),
                };
            }
            return result;
        }

        private static T ParseEnum<T>(string? value, string label) where T : struct, Enum
        {
            if (Enum.TryParse(value?.Trim(), ignoreCase: true, out T result) && Enum.IsDefined(result))
                return result;
            throw new FormatException($"Unknown subagent {label} '{value}'.");
        }

        private static string NormalizeToken(string? value)
        {
            return new string((value ?? string.Empty)
                .Where(char.IsLetterOrDigit)
                .Select(char.ToLowerInvariant)
                .ToArray());
        }

        private static string FirstNonEmpty(params string?[] values)
        {
            return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;
        }

        private static CopilotPluginSubagentRoleLoadIssue CreateIssue(string? sourceId, string? roleId, string message)
        {
            return new CopilotPluginSubagentRoleLoadIssue
            {
                SourceId = string.IsNullOrWhiteSpace(sourceId) ? "unknown" : sourceId.Trim().ToLowerInvariant(),
                RoleId = roleId?.Trim().ToLowerInvariant() ?? string.Empty,
                Message = message?.Trim() ?? "Unknown plugin subagent registration error.",
            };
        }

        private sealed record RoleCandidate(
            CopilotSubagentRoleRegistration Registration,
            CopilotSubagentRoleDescriptor Descriptor);

        private sealed record LoadedRegistration(string RoleId, string Fingerprint, IDisposable Handle);
    }
}
