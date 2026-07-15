using ColorVision.UI.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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

        public IReadOnlyList<CopilotPluginSubagentRoleLoadIssue> Issues { get; init; } = Array.Empty<CopilotPluginSubagentRoleLoadIssue>();
    }

    public sealed class CopilotPluginSubagentRoleLoader : IDisposable
    {
        private static readonly CopilotAgentMode[] DefaultParentModes =
        [
            CopilotAgentMode.Auto,
            CopilotAgentMode.Explain,
            CopilotAgentMode.Web,
            CopilotAgentMode.Code,
            CopilotAgentMode.Diagnose,
        ];

        private readonly CopilotSubagentRoleRegistry _roleRegistry;
        private readonly Dictionary<string, LoadedRegistration> _registrations = new(StringComparer.OrdinalIgnoreCase);
        private readonly object _syncRoot = new();
        private PluginInfo[] _trackedPlugins = Array.Empty<PluginInfo>();
        private CopilotPluginSubagentRoleLoadIssue[] _issues = Array.Empty<CopilotPluginSubagentRoleLoadIssue>();
        private bool _disposed;

        public CopilotPluginSubagentRoleLoader(CopilotSubagentRoleRegistry roleRegistry)
        {
            _roleRegistry = roleRegistry ?? throw new ArgumentNullException(nameof(roleRegistry));
        }

        public static CopilotPluginSubagentRoleLoader Shared { get; } = new(CopilotSubagentRoleRegistry.Shared);

        public CopilotPluginSubagentRoleLoaderSnapshot Synchronize(IEnumerable<PluginInfo>? plugins)
        {
            var pluginSnapshot = (plugins ?? Array.Empty<PluginInfo>()).Where(plugin => plugin != null).Distinct().ToArray();
            TrackPlugins(pluginSnapshot);
            return SynchronizeCore(pluginSnapshot);
        }

        private CopilotPluginSubagentRoleLoaderSnapshot SynchronizeCore(IReadOnlyList<PluginInfo> plugins)
        {
            var candidates = new List<RoleCandidate>();
            var issues = new List<CopilotPluginSubagentRoleLoadIssue>();
            foreach (var plugin in plugins
                .Where(plugin => plugin?.Enabled == true && plugin.Manifest != null)
                .OrderBy(plugin => plugin.Manifest.Id, StringComparer.OrdinalIgnoreCase))
            {
                var roleManifests = plugin.Manifest.CopilotAgents ?? [];
                if (roleManifests.Count == 0)
                    continue;
                if (plugin.Assembly == null)
                {
                    issues.Add(CreateIssue(plugin.Manifest.Id, string.Empty, "The plugin assembly did not load, so its Copilot roles were not registered."));
                    continue;
                }

                foreach (var roleManifest in roleManifests.Where(role => role != null))
                {
                    try
                    {
                        var registration = CreateRegistration(plugin, roleManifest);
                        var descriptor = CopilotSubagentRoleFactory.Create(registration, isBuiltIn: false);
                        candidates.Add(new RoleCandidate(registration, descriptor));
                    }
                    catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or FormatException)
                    {
                        issues.Add(CreateIssue(plugin.Manifest.Id, roleManifest.Id, ex.Message));
                    }
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
                Issues = _issues.ToArray(),
            };
        }

        private static CopilotSubagentRoleRegistration CreateRegistration(PluginInfo plugin, CopilotSubagentRoleManifest role)
        {
            var scope = ParseScope(role.Scope);
            var childMode = string.IsNullOrWhiteSpace(role.ChildMode)
                ? scope == CopilotSubagentContextScope.PublicWeb ? CopilotAgentMode.Web : CopilotAgentMode.Code
                : ParseEnum<CopilotAgentMode>(role.ChildMode, "child mode");
            var parentModes = role.ParentModes == null || role.ParentModes.Count == 0
                ? DefaultParentModes
                : role.ParentModes.Select(value => ParseEnum<CopilotAgentMode>(value, "parent mode")).ToArray();
            var roleId = role.Id?.Trim() ?? string.Empty;

            return new CopilotSubagentRoleRegistration
            {
                SourceId = (plugin.Manifest.Id ?? string.Empty).Trim().ToLowerInvariant(),
                SourceName = FirstNonEmpty(plugin.Manifest.Name, plugin.Name, plugin.Manifest.Id),
                SourceVersion = FirstNonEmpty(plugin.Manifest.Version, plugin.AssemblyVersion?.ToString(), "0"),
                RoleId = roleId,
                ToolName = FirstNonEmpty(role.ToolName, CreateDefaultToolName(roleId)),
                DisplayName = FirstNonEmpty(role.Name, roleId),
                Description = role.Description?.Trim() ?? string.Empty,
                RuntimeInstructions = role.Instructions?.Trim() ?? string.Empty,
                ContextScope = scope,
                ReadCapabilities = ParseCapabilities(role.Capabilities),
                ChildMode = childMode,
                ParentModes = parentModes,
                MaximumToolCalls = DefaultIfZero(role.MaximumToolCalls, 6),
                MaximumAgentPasses = DefaultIfZero(role.MaximumAgentPasses, 2),
                MaximumDuration = TimeSpan.FromSeconds(DefaultIfZero(role.MaximumDurationSeconds, 90)),
                MaximumAnswerCharacters = DefaultIfZero(role.MaximumAnswerCharacters, 12_000),
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

        private static string CreateDefaultToolName(string roleId)
        {
            var builder = new StringBuilder("Delegate");
            foreach (var segment in (roleId ?? string.Empty).Split(['-', '_', '.'], StringSplitOptions.RemoveEmptyEntries))
            {
                builder.Append(char.ToUpperInvariant(segment[0]));
                if (segment.Length > 1)
                    builder.Append(segment.AsSpan(1));
            }
            return builder.ToString();
        }

        private static string NormalizeToken(string? value)
        {
            return new string((value ?? string.Empty)
                .Where(char.IsLetterOrDigit)
                .Select(char.ToLowerInvariant)
                .ToArray());
        }

        private static int DefaultIfZero(int value, int defaultValue) => value == 0 ? defaultValue : value;

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
