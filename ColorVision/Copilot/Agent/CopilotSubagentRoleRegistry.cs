using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.Copilot
{
    public sealed class CopilotSubagentRoleRegistryChangedEventArgs : EventArgs
    {
        public long PreviousRevision { get; init; }

        public long Revision { get; init; }

        public int RoleCount { get; init; }
    }

    public sealed class CopilotSubagentRoleRegistry
    {
        private const int MaximumPluginRoles = 16;
        private readonly CopilotCapabilityCatalog? _capabilityCatalog;
        private readonly Dictionary<string, CopilotSubagentRoleDescriptor> _roles = new(StringComparer.OrdinalIgnoreCase);
        private readonly object _syncRoot = new();
        private long _revision = 1;

        public CopilotSubagentRoleRegistry(CopilotCapabilityCatalog? capabilityCatalog = null)
        {
            _capabilityCatalog = capabilityCatalog;
            foreach (var role in CopilotSubagentRoleCatalog.CreateBuiltInRoles())
                _roles.Add(role.Id, role);
        }

        public static CopilotSubagentRoleRegistry Shared { get; } = new(CopilotCapabilityCatalog.Shared);

        public event EventHandler<CopilotSubagentRoleRegistryChangedEventArgs>? Changed;

        public CopilotSubagentRoleCatalog GetSnapshot()
        {
            lock (_syncRoot)
                return new CopilotSubagentRoleCatalog(_roles.Values.ToArray(), _revision);
        }

        public IDisposable RegisterTrustedPluginRole(CopilotSubagentRoleRegistration registration)
        {
            var role = CopilotSubagentRoleFactory.Create(registration, isBuiltIn: false);
            CopilotSubagentRoleRegistryChangedEventArgs change;
            lock (_syncRoot)
            {
                if (_roles.TryGetValue(role.Id, out var existingRole))
                    throw new InvalidOperationException($"Subagent role '{role.Id}' is already registered by '{existingRole.SourceId}'.");
                var existingTool = _roles.Values.FirstOrDefault(candidate => string.Equals(candidate.ToolName, role.ToolName, StringComparison.OrdinalIgnoreCase));
                if (existingTool != null)
                    throw new InvalidOperationException($"Subagent tool '{role.ToolName}' is already registered by role '{existingTool.Id}'.");
                if (_roles.Values.Count(candidate => !string.Equals(candidate.SourceId, "builtin", StringComparison.OrdinalIgnoreCase)) >= MaximumPluginRoles)
                    throw new InvalidOperationException($"The subagent registry reached its {MaximumPluginRoles}-plugin-role limit.");
                var sourceRole = _roles.Values.FirstOrDefault(candidate => string.Equals(candidate.SourceId, role.SourceId, StringComparison.OrdinalIgnoreCase));
                if (sourceRole != null && !string.Equals(sourceRole.SourceName, role.SourceName, StringComparison.Ordinal))
                    throw new InvalidOperationException($"Subagent source '{role.SourceId}' changed its display name within one process.");
                if (sourceRole != null && !string.Equals(sourceRole.SourceVersion, role.SourceVersion, StringComparison.Ordinal))
                    throw new InvalidOperationException($"Subagent source '{role.SourceId}' mixed multiple source versions within one process.");

                var previousRevision = _revision;
                _roles.Add(role.Id, role);
                _revision++;
                change = CreateChange(previousRevision);
            }

            try
            {
                PublishPluginSource(role.SourceId);
            }
            catch
            {
                lock (_syncRoot)
                {
                    if (_roles.TryGetValue(role.Id, out var current)
                        && string.Equals(current.CapabilityFingerprint, role.CapabilityFingerprint, StringComparison.Ordinal))
                    {
                        _roles.Remove(role.Id);
                        _revision++;
                    }
                }
                throw;
            }

            PublishChanged(change);
            return new Registration(this, role.SourceId, role.Id, role.CapabilityFingerprint);
        }

        private void Unregister(string sourceId, string roleId, string fingerprint)
        {
            CopilotSubagentRoleRegistryChangedEventArgs? change = null;
            lock (_syncRoot)
            {
                if (!_roles.TryGetValue(roleId, out var role)
                    || !string.Equals(role.SourceId, sourceId, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(role.CapabilityFingerprint, fingerprint, StringComparison.Ordinal))
                {
                    return;
                }

                var previousRevision = _revision;
                _roles.Remove(roleId);
                _revision++;
                change = CreateChange(previousRevision);
            }

            PublishPluginSource(sourceId);
            PublishChanged(change);
        }

        private void PublishPluginSource(string sourceId)
        {
            if (_capabilityCatalog == null)
                return;

            CopilotSubagentRoleDescriptor[] roles;
            string sourceName;
            lock (_syncRoot)
            {
                roles = _roles.Values
                    .Where(role => string.Equals(role.SourceId, sourceId, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(role => role.Id, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                sourceName = roles.FirstOrDefault()?.SourceName ?? sourceId;
            }
            _capabilityCatalog.PublishSource(
                CopilotCapabilitySourceKind.Plugin,
                "subagent:" + sourceId,
                sourceName,
                roles.Select(role => new CopilotRegisteredSubagentTool(role)));
        }

        private CopilotSubagentRoleRegistryChangedEventArgs CreateChange(long previousRevision)
        {
            return new CopilotSubagentRoleRegistryChangedEventArgs
            {
                PreviousRevision = previousRevision,
                Revision = _revision,
                RoleCount = _roles.Count,
            };
        }

        private void PublishChanged(CopilotSubagentRoleRegistryChangedEventArgs? change)
        {
            if (change == null || Changed is not { } handlers)
                return;
            foreach (var handler in handlers.GetInvocationList().Cast<EventHandler<CopilotSubagentRoleRegistryChangedEventArgs>>())
            {
                try
                {
                    handler(this, change);
                }
                catch
                {
                }
            }
        }

        private sealed class Registration : IDisposable
        {
            private CopilotSubagentRoleRegistry? _owner;
            private readonly string _sourceId;
            private readonly string _roleId;
            private readonly string _fingerprint;

            public Registration(CopilotSubagentRoleRegistry owner, string sourceId, string roleId, string fingerprint)
            {
                _owner = owner;
                _sourceId = sourceId;
                _roleId = roleId;
                _fingerprint = fingerprint;
            }

            public void Dispose()
            {
                var owner = System.Threading.Interlocked.Exchange(ref _owner, null);
                owner?.Unregister(_sourceId, _roleId, _fingerprint);
            }
        }
    }
}
