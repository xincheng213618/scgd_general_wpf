using ColorVision.Common.MVVM;
using ColorVision.UI;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Security.Cryptography;

namespace ColorVision.Copilot
{
    public sealed class CopilotConfigFutureVersionException : InvalidOperationException
    {
        public int SchemaVersion { get; }

        public int SupportedSchemaVersion { get; }

        public CopilotConfigFutureVersionException(
            int schemaVersion,
            int supportedSchemaVersion)
            : base($"Copilot configuration schema {schemaVersion} was created by a newer application version; this version supports schema {supportedSchemaVersion} and will not overwrite it.")
        {
            SchemaVersion = schemaVersion;
            SupportedSchemaVersion = supportedSchemaVersion;
        }
    }

    public class CopilotConfig : ViewModelBase, IConfigSecure
    {
        public const string ConfigAESKey = "ColorVision";
        public const string ConfigAESVector = "CopilotConfig";
        public const int CurrentSchemaVersion = 8;
        public const string DefaultBackendSyncUrl = "";
        internal const string LegacyInsecureBackendSyncUrl = "http://xc213618.ddns.me:9998";

        public static CopilotConfig Instance => ConfigHandler.GetInstance().GetRequiredService<CopilotConfig>();

        public ObservableCollection<CopilotProfileConfig> Profiles { get; set; } = new();

        public ObservableCollection<CopilotMcpClientServerConfig> ExternalMcpServers { get; set; } = new();

        public CopilotAgentDefaultsConfig AgentDefaults { get; set; } = new();

        [Browsable(false)]
        public string BackendSyncUrl
        {
            get => _backendSyncUrl;
            set => SetProperty(ref _backendSyncUrl, value?.Trim() ?? string.Empty);
        }
        private string _backendSyncUrl = DefaultBackendSyncUrl;

        [Browsable(false)]
        public bool AllowInsecureBackendSync
        {
            get => _allowInsecureBackendSync;
            set => SetProperty(ref _allowInsecureBackendSync, value);
        }
        private bool _allowInsecureBackendSync;

        public bool ShouldSerializeAllowInsecureBackendSync() => false;

        [Browsable(false)]
        public string WebPagePref64Prefixes
        {
            get => _webPagePref64Prefixes;
            set => SetProperty(ref _webPagePref64Prefixes, value?.Trim() ?? string.Empty);
        }
        private string _webPagePref64Prefixes = string.Empty;

        public bool ShouldSerializeWebPagePref64Prefixes() =>
            !string.IsNullOrWhiteSpace(WebPagePref64Prefixes);

        [Browsable(false)]
        public int SchemaVersion { get; set; }

        [JsonIgnore]
        [Browsable(false)]
        public bool IsPersistenceBlocked => SchemaVersion > CurrentSchemaVersion;

        public const int DefaultMcpPort = 38473;

        [Browsable(false)]
        public bool McpEnabled
        {
            get => _mcpEnabled;
            set => SetProperty(ref _mcpEnabled, value);
        }
        private bool _mcpEnabled;

        [Browsable(false)]
        public int McpPort
        {
            get => _mcpPort;
            set => SetProperty(ref _mcpPort, value);
        }
        private int _mcpPort = DefaultMcpPort;

        [Browsable(false)]
        public string McpBearerToken
        {
            get => _mcpBearerToken;
            set => SetProperty(ref _mcpBearerToken, value ?? string.Empty);
        }
        private string _mcpBearerToken = string.Empty;

        [JsonIgnore]
        [Browsable(false)]
        public string McpEndpoint => $"http://127.0.0.1:{McpPort}/mcp";

        [JsonIgnore]
        public bool IsConfigured => Profiles.Any(profile => profile.IsConfigured);

        [Browsable(false)]
        public bool AutoShowPanelOnFirstLaunch
        {
            get => _autoShowPanelOnFirstLaunch;
            set => SetProperty(ref _autoShowPanelOnFirstLaunch, value);
        }
        private bool _autoShowPanelOnFirstLaunch = true;

        public bool EnsureInitialized()
        {
            if (IsPersistenceBlocked)
                return false;

            var changed = false;

            Profiles ??= new ObservableCollection<CopilotProfileConfig>();
            ExternalMcpServers ??= new ObservableCollection<CopilotMcpClientServerConfig>();
            for (var index = Profiles.Count - 1; index >= 0; index--)
            {
                if (Profiles[index] != null)
                    continue;

                Profiles.RemoveAt(index);
                changed = true;
            }
            if (AgentDefaults == null)
            {
                AgentDefaults = new CopilotAgentDefaultsConfig();
                changed = true;
            }
            changed |= AgentDefaults.EnsureValid();

            if (McpPort <= 0 || McpPort > 65535)
            {
                McpPort = DefaultMcpPort;
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(McpBearerToken))
            {
                McpBearerToken = GenerateMcpBearerToken();
                changed = true;
            }

            changed |= CopilotTemporaryProfileSource.Sync(Profiles);

            for (var index = Profiles.Count - 1; index >= 0; index--)
            {
                var profile = Profiles[index];
                if (IsUntrustedBackendProfile(profile))
                {
                    Profiles.RemoveAt(index);
                    changed = true;
                    continue;
                }
                if (profile.IsBackendSynced && profile.AllowInsecureHttp)
                {
                    profile.AllowInsecureHttp = false;
                    changed = true;
                }
            }

            if (IsLegacyInsecureBackendSyncUrl(BackendSyncUrl))
            {
                BackendSyncUrl = DefaultBackendSyncUrl;
                changed = true;
            }

            if (Profiles.Count == 0)
            {
                Profiles.Add(CopilotProfileConfig.CreateDefault());
                changed = true;
            }

            if (AllowInsecureBackendSync)
            {
                AllowInsecureBackendSync = false;
                changed = true;
            }

            if (CopilotWebPagePref64Configuration.TryParse(
                    WebPagePref64Prefixes,
                    out var pref64Prefixes,
                    out _))
            {
                var normalizedPref64Prefixes = CopilotWebPagePref64Configuration.Format(pref64Prefixes);
                if (!string.Equals(WebPagePref64Prefixes, normalizedPref64Prefixes, StringComparison.Ordinal))
                {
                    WebPagePref64Prefixes = normalizedPref64Prefixes;
                    changed = true;
                }
            }

            if (SchemaVersion < CurrentSchemaVersion)
            {
                SchemaVersion = CurrentSchemaVersion;
                changed = true;
            }

            foreach (var profile in Profiles)
                changed |= profile.EnsureValid();

            for (var index = ExternalMcpServers.Count - 1; index >= 0; index--)
            {
                var server = ExternalMcpServers[index];
                if (server == null)
                {
                    ExternalMcpServers.RemoveAt(index);
                    changed = true;
                    continue;
                }

                server.ToolRules ??= new ObservableCollection<CopilotMcpClientToolRule>();
                for (var ruleIndex = server.ToolRules.Count - 1; ruleIndex >= 0; ruleIndex--)
                {
                    if (server.ToolRules[ruleIndex] != null)
                        continue;

                    server.ToolRules.RemoveAt(ruleIndex);
                    changed = true;
                }
            }

            OnPropertyChanged(nameof(IsConfigured));
            return changed;
        }

        private static bool IsUntrustedBackendProfile(CopilotProfileConfig profile)
        {
            var hasSyncSource = !string.IsNullOrWhiteSpace(profile.SyncSource);
            var hasSyncProfileId = !string.IsNullOrWhiteSpace(profile.SyncProfileId);
            if (!hasSyncSource && !hasSyncProfileId)
                return false;
            if (!hasSyncSource || !hasSyncProfileId)
                return true;

            return !CopilotBackendSyncClient.IsTrustedSyncSource(profile.SyncSource)
                || !CopilotProviderEndpoint.Validate(
                    profile.BaseUrl,
                    profile.ProviderType,
                    allowInsecureHttp: false).IsValid;
        }

        private static bool IsLegacyInsecureBackendSyncUrl(string? value)
        {
            return Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var candidate)
                && Uri.TryCreate(LegacyInsecureBackendSyncUrl, UriKind.Absolute, out var legacy)
                && string.Equals(candidate.Scheme, legacy.Scheme, StringComparison.OrdinalIgnoreCase)
                && string.Equals(
                    candidate.IdnHost.TrimEnd('.'),
                    legacy.IdnHost.TrimEnd('.'),
                    StringComparison.OrdinalIgnoreCase)
                && candidate.Port == legacy.Port;
        }

        public CopilotProfileConfig? FindProfile(string? profileId)
        {
            if (string.IsNullOrWhiteSpace(profileId))
                return null;

            return Profiles.FirstOrDefault(profile => string.Equals(profile.Id, profileId, System.StringComparison.Ordinal));
        }

        public CopilotProfileConfig? GetPreferredDefaultProfile()
        {
            return Profiles.FirstOrDefault(profile => profile.IsConfigured)
                ?? Profiles.FirstOrDefault();
        }

        internal CopilotConfig CreatePersistenceSnapshot(
            IEnumerable<CopilotProfileConfig>? profiles = null)
        {
            var profileSnapshot = (profiles ?? Profiles ?? Enumerable.Empty<CopilotProfileConfig>())
                .Where(profile => profile != null)
                .Select(profile => profile.Clone());
            var externalMcpServerSnapshot = (ExternalMcpServers
                    ?? new ObservableCollection<CopilotMcpClientServerConfig>())
                .Where(server => server != null)
                .Select(server => server.Clone());

            return new CopilotConfig
            {
                Profiles = new ObservableCollection<CopilotProfileConfig>(profileSnapshot),
                ExternalMcpServers = new ObservableCollection<CopilotMcpClientServerConfig>(externalMcpServerSnapshot),
                AgentDefaults = AgentDefaults?.Clone() ?? new CopilotAgentDefaultsConfig(),
                BackendSyncUrl = BackendSyncUrl,
                AllowInsecureBackendSync = AllowInsecureBackendSync,
                WebPagePref64Prefixes = WebPagePref64Prefixes,
                SchemaVersion = SchemaVersion,
                McpEnabled = McpEnabled,
                McpPort = McpPort,
                McpBearerToken = McpBearerToken,
                AutoShowPanelOnFirstLaunch = AutoShowPanelOnFirstLaunch,
            };
        }

        internal void ApplyPersistenceSnapshot(CopilotConfig snapshot)
        {
            CommitPersistenceSnapshot(snapshot);
            NotifyPersistenceSnapshotApplied();
        }

        internal void CommitPersistenceSnapshot(CopilotConfig snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);

            var profiles = CreateProfileCollection(snapshot.Profiles);
            var externalMcpServers = new ObservableCollection<CopilotMcpClientServerConfig>((snapshot.ExternalMcpServers
                    ?? new ObservableCollection<CopilotMcpClientServerConfig>())
                .Where(server => server != null)
                .Select(server => server.Clone())
                .ToArray());
            var agentDefaults = snapshot.AgentDefaults?.Clone() ?? new CopilotAgentDefaultsConfig();

            Profiles = profiles;
            ExternalMcpServers = externalMcpServers;
            AgentDefaults = agentDefaults;
            _backendSyncUrl = snapshot.BackendSyncUrl;
            _allowInsecureBackendSync = snapshot.AllowInsecureBackendSync;
            _webPagePref64Prefixes = snapshot.WebPagePref64Prefixes;
            SchemaVersion = snapshot.SchemaVersion;
            _mcpEnabled = snapshot.McpEnabled;
            _mcpPort = snapshot.McpPort;
            _mcpBearerToken = snapshot.McpBearerToken;
            _autoShowPanelOnFirstLaunch = snapshot.AutoShowPanelOnFirstLaunch;
        }

        internal void NotifyPersistenceSnapshotApplied()
        {
            OnPropertyChanged(nameof(Profiles));
            OnPropertyChanged(nameof(ExternalMcpServers));
            OnPropertyChanged(nameof(AgentDefaults));
            OnPropertyChanged(nameof(BackendSyncUrl));
            OnPropertyChanged(nameof(AllowInsecureBackendSync));
            OnPropertyChanged(nameof(WebPagePref64Prefixes));
            OnPropertyChanged(nameof(SchemaVersion));
            OnPropertyChanged(nameof(IsPersistenceBlocked));
            OnPropertyChanged(nameof(McpEnabled));
            OnPropertyChanged(nameof(McpPort));
            OnPropertyChanged(nameof(McpBearerToken));
            OnPropertyChanged(nameof(McpEndpoint));
            OnPropertyChanged(nameof(AutoShowPanelOnFirstLaunch));
            OnPropertyChanged(nameof(IsConfigured));
        }

        internal void ReplaceProfiles(IEnumerable<CopilotProfileConfig> profiles)
        {
            CommitProfiles(profiles);
            NotifyProfilesReplaced();
        }

        internal void CommitProfiles(IEnumerable<CopilotProfileConfig> profiles)
        {
            ArgumentNullException.ThrowIfNull(profiles);
            var profileCollection = CreateProfileCollection(profiles);
            Profiles = profileCollection;
        }

        internal void NotifyProfilesReplaced()
        {
            OnPropertyChanged(nameof(Profiles));
            OnPropertyChanged(nameof(IsConfigured));
        }

        private static ObservableCollection<CopilotProfileConfig> CreateProfileCollection(
            IEnumerable<CopilotProfileConfig> profiles)
        {
            return new ObservableCollection<CopilotProfileConfig>(profiles
                .Where(profile => profile != null)
                .Select(profile => profile.Clone())
                .ToArray());
        }

        public static string GenerateMcpBearerToken()
        {
            Span<byte> bytes = stackalloc byte[32];
            RandomNumberGenerator.Fill(bytes);
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }

        public void Encryption()
        {
            ThrowIfPersistenceBlocked();
            Profiles ??= new ObservableCollection<CopilotProfileConfig>();
            var profiles = Profiles
                .Where(profile => profile != null)
                .ToArray();
            var protectedProfileKeys = profiles
                .Select(profile => CopilotCredentialProtector.Protect(profile.ApiKey))
                .ToArray();
            var protectedMcpBearerToken = CopilotCredentialProtector.Protect(McpBearerToken);

            for (var index = 0; index < profiles.Length; index++)
                profiles[index].ApiKey = protectedProfileKeys[index];
            McpBearerToken = protectedMcpBearerToken;
        }

        private void ThrowIfPersistenceBlocked()
        {
            if (IsPersistenceBlocked)
            {
                throw new CopilotConfigFutureVersionException(
                    SchemaVersion,
                    CurrentSchemaVersion);
            }
        }

        public void Decrypt()
        {
            Profiles ??= new ObservableCollection<CopilotProfileConfig>();
            foreach (var profile in Profiles.Where(profile => profile != null))
            {
                if (CopilotCredentialProtector.TryUnprotect(profile.ApiKey, out var apiKey, out _))
                {
                    profile.ApiKey = apiKey;
                    profile.CredentialNeedsReentry = false;
                }
                else
                {
                    profile.ApiKey = string.Empty;
                    profile.CredentialNeedsReentry = true;
                }
            }

            McpBearerToken = CopilotCredentialProtector.TryUnprotect(McpBearerToken, out var bearerToken, out _)
                ? bearerToken
                : string.Empty;
        }
    }
}
