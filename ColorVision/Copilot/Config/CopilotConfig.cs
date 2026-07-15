using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.UI;
using Newtonsoft.Json;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Security.Cryptography;

namespace ColorVision.Copilot
{
    public class CopilotConfig : ViewModelBase, IConfigSecure
    {
        public const string ConfigAESKey = "ColorVision";
        public const string ConfigAESVector = "CopilotConfig";
        public const int CurrentSchemaVersion = 2;

        public static CopilotConfig Instance => ConfigHandler.GetInstance().GetRequiredService<CopilotConfig>();

        public ObservableCollection<CopilotProfileConfig> Profiles { get; set; } = new();

        public ObservableCollection<CopilotMcpClientServerConfig> ExternalMcpServers { get; set; } = new();

        public ObservableCollection<string> DisabledPluginSubagentRoles { get; set; } = new();

        public CopilotAgentDefaultsConfig AgentDefaults { get; set; } = new();

        [Browsable(false)]
        public int SchemaVersion { get; set; }

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
            var changed = false;

            Profiles ??= new ObservableCollection<CopilotProfileConfig>();
            ExternalMcpServers ??= new ObservableCollection<CopilotMcpClientServerConfig>();
            DisabledPluginSubagentRoles ??= new ObservableCollection<string>();
            if (AgentDefaults == null)
            {
                AgentDefaults = new CopilotAgentDefaultsConfig();
                changed = true;
            }
            changed |= AgentDefaults.EnsureValid();

            var normalizedDisabledRoles = CopilotPluginSubagentRolePreference.NormalizeKeys(DisabledPluginSubagentRoles);
            if (!DisabledPluginSubagentRoles.SequenceEqual(normalizedDisabledRoles, StringComparer.OrdinalIgnoreCase))
            {
                DisabledPluginSubagentRoles.Clear();
                foreach (var roleKey in normalizedDisabledRoles)
                    DisabledPluginSubagentRoles.Add(roleKey);
                changed = true;
            }

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

            changed |= CopilotTemporaryProfileSource.Sync(Profiles, DateTimeOffset.UtcNow);

            if (Profiles.Count == 0)
            {
                Profiles.Add(CopilotProfileConfig.CreateDefault());
                changed = true;
            }

            if (SchemaVersion < CurrentSchemaVersion)
            {
                SchemaVersion = CurrentSchemaVersion;
                changed = true;
            }

            foreach (var profile in Profiles)
            {
                changed |= profile.EnsureValid();
            }

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
            }

            OnPropertyChanged(nameof(IsConfigured));
            return changed;
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

        public static string GenerateMcpBearerToken()
        {
            Span<byte> bytes = stackalloc byte[32];
            RandomNumberGenerator.Fill(bytes);
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }

        public void Encryption()
        {
            foreach (var profile in Profiles)
            {
                if (!string.IsNullOrWhiteSpace(profile.ApiKey))
                    profile.ApiKey = Cryptography.AESEncrypt(profile.ApiKey, ConfigAESKey, ConfigAESVector);
            }

            if (!string.IsNullOrWhiteSpace(McpBearerToken))
                McpBearerToken = Cryptography.AESEncrypt(McpBearerToken, ConfigAESKey, ConfigAESVector);
        }

        public void Decrypt()
        {
            foreach (var profile in Profiles)
            {
                if (!string.IsNullOrWhiteSpace(profile.ApiKey))
                    profile.ApiKey = Cryptography.AESDecrypt(profile.ApiKey, ConfigAESKey, ConfigAESVector);
            }

            if (!string.IsNullOrWhiteSpace(McpBearerToken))
                McpBearerToken = Cryptography.AESDecrypt(McpBearerToken, ConfigAESKey, ConfigAESVector);
        }
    }
}
