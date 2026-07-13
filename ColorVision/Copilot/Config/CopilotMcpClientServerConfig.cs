using ColorVision.Common.MVVM;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ColorVision.Copilot
{
    public enum CopilotMcpClientAccessPolicy
    {
        RequireApproval,
        ReadOnly,
    }

    public sealed class CopilotMcpClientServerConfig : ViewModelBase
    {
        public const int DefaultConnectionTimeoutSeconds = 10;
        public const int DefaultToolTimeoutSeconds = 60;

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, (value ?? string.Empty).Trim());
        }
        private string _name = string.Empty;

        public string Endpoint
        {
            get => _endpoint;
            set => SetProperty(ref _endpoint, (value ?? string.Empty).Trim());
        }
        private string _endpoint = string.Empty;

        public string BearerTokenEnvironmentVariable
        {
            get => _bearerTokenEnvironmentVariable;
            set => SetProperty(ref _bearerTokenEnvironmentVariable, (value ?? string.Empty).Trim());
        }
        private string _bearerTokenEnvironmentVariable = string.Empty;

        public bool Enabled
        {
            get => _enabled;
            set => SetProperty(ref _enabled, value);
        }
        private bool _enabled = true;

        public CopilotMcpClientAccessPolicy AccessPolicy
        {
            get => _accessPolicy;
            set => SetProperty(ref _accessPolicy, value);
        }
        private CopilotMcpClientAccessPolicy _accessPolicy = CopilotMcpClientAccessPolicy.RequireApproval;

        public int ConnectionTimeoutSeconds
        {
            get => _connectionTimeoutSeconds;
            set => SetProperty(ref _connectionTimeoutSeconds, Math.Clamp(value, 1, 60));
        }
        private int _connectionTimeoutSeconds = DefaultConnectionTimeoutSeconds;

        public int ToolTimeoutSeconds
        {
            get => _toolTimeoutSeconds;
            set => SetProperty(ref _toolTimeoutSeconds, Math.Clamp(value, 1, 600));
        }
        private int _toolTimeoutSeconds = DefaultToolTimeoutSeconds;

        public CopilotMcpClientServerConfig Clone()
        {
            return new CopilotMcpClientServerConfig
            {
                Name = Name,
                Endpoint = Endpoint,
                BearerTokenEnvironmentVariable = BearerTokenEnvironmentVariable,
                Enabled = Enabled,
                AccessPolicy = AccessPolicy,
                ConnectionTimeoutSeconds = ConnectionTimeoutSeconds,
                ToolTimeoutSeconds = ToolTimeoutSeconds,
            };
        }
    }

    public static class CopilotMcpClientConfigurationText
    {
        private const int MaximumServers = 8;
        private static readonly Regex NameRegex = new("^[A-Za-z0-9][A-Za-z0-9._-]{0,39}$", RegexOptions.Compiled);
        private static readonly Regex EnvironmentVariableRegex = new("^[A-Za-z_][A-Za-z0-9_]{0,127}$", RegexOptions.Compiled);

        public static bool TryParse(string? text, out IReadOnlyList<CopilotMcpClientServerConfig> servers, out string error)
        {
            var parsed = new List<CopilotMcpClientServerConfig>();
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var endpoints = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var lines = (text ?? string.Empty).Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
            for (var index = 0; index < lines.Length; index++)
            {
                var line = lines[index].Trim();
                if (line.Length == 0 || line.StartsWith('#'))
                    continue;
                if (parsed.Count >= MaximumServers)
                    return Fail($"At most {MaximumServers} external MCP servers can be configured.", out servers, out error);

                var parts = line.Split('|').Select(part => part.Trim()).ToArray();
                if (parts.Length is < 2 or > 4)
                    return Fail($"Line {index + 1} must use: name | endpoint | token-environment-variable | approval/read-only.", out servers, out error);

                var name = parts[0];
                if (!NameRegex.IsMatch(name))
                    return Fail($"Line {index + 1} has an invalid server name. Use 1-40 letters, digits, '.', '_' or '-'.", out servers, out error);
                if (!names.Add(name))
                    return Fail($"Line {index + 1} duplicates MCP server name '{name}'.", out servers, out error);

                if (!Uri.TryCreate(parts[1], UriKind.Absolute, out var endpoint)
                    || endpoint.Scheme != Uri.UriSchemeHttp && endpoint.Scheme != Uri.UriSchemeHttps
                    || !string.IsNullOrEmpty(endpoint.UserInfo))
                {
                    return Fail($"Line {index + 1} must contain an absolute HTTP or HTTPS endpoint without embedded credentials.", out servers, out error);
                }
                if (endpoint.Scheme == Uri.UriSchemeHttp && !endpoint.IsLoopback)
                    return Fail($"Line {index + 1} uses plain HTTP for a non-loopback endpoint. Use HTTPS for remote MCP servers.", out servers, out error);
                if (!endpoints.Add(endpoint.AbsoluteUri))
                    return Fail($"Line {index + 1} duplicates MCP endpoint '{endpoint.AbsoluteUri}'.", out servers, out error);

                var tokenEnvironmentVariable = parts.Length >= 3 ? parts[2] : string.Empty;
                if (tokenEnvironmentVariable.Length > 0 && !EnvironmentVariableRegex.IsMatch(tokenEnvironmentVariable))
                    return Fail($"Line {index + 1} has an invalid token environment-variable name.", out servers, out error);

                var policyText = parts.Length >= 4 ? parts[3] : "approval";
                var accessPolicy = policyText.ToLowerInvariant() switch
                {
                    "approval" or "require-approval" => CopilotMcpClientAccessPolicy.RequireApproval,
                    "read-only" or "readonly" => CopilotMcpClientAccessPolicy.ReadOnly,
                    _ => (CopilotMcpClientAccessPolicy?)null,
                };
                if (!accessPolicy.HasValue)
                    return Fail($"Line {index + 1} access policy must be 'approval' or 'read-only'.", out servers, out error);

                parsed.Add(new CopilotMcpClientServerConfig
                {
                    Name = name,
                    Endpoint = endpoint.AbsoluteUri,
                    BearerTokenEnvironmentVariable = tokenEnvironmentVariable,
                    AccessPolicy = accessPolicy.Value,
                });
            }

            servers = parsed;
            error = string.Empty;
            return true;
        }

        public static string Format(IEnumerable<CopilotMcpClientServerConfig>? servers)
        {
            var builder = new StringBuilder();
            foreach (var server in servers?.Where(server => server != null && server.Enabled) ?? Enumerable.Empty<CopilotMcpClientServerConfig>())
            {
                builder.Append(server.Name).Append(" | ")
                    .Append(server.Endpoint).Append(" | ")
                    .Append(server.BearerTokenEnvironmentVariable).Append(" | ")
                    .AppendLine(server.AccessPolicy == CopilotMcpClientAccessPolicy.ReadOnly ? "read-only" : "approval");
            }
            return builder.ToString().TrimEnd();
        }

        private static bool Fail(string message, out IReadOnlyList<CopilotMcpClientServerConfig> servers, out string error)
        {
            servers = Array.Empty<CopilotMcpClientServerConfig>();
            error = message;
            return false;
        }
    }
}
