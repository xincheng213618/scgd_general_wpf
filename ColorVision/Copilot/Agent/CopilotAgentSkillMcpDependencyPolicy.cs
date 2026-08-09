using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.Copilot
{
    internal enum CopilotAgentSkillMcpDependencyStatus
    {
        NotMcp,
        Installed,
        ConfiguredDisabled,
        Installable,
        MissingInstallMetadata,
        UnsupportedTransport,
        InvalidConfiguration,
    }

    internal static class CopilotAgentSkillMcpDependencyPolicy
    {
        internal const string StreamableHttpTransport = "streamable_http";

        public static CopilotAgentSkillMcpDependencyStatus Evaluate(
            CopilotAgentSkillDependency dependency,
            IEnumerable<CopilotMcpClientServerConfig>? configuredServers = null)
        {
            ArgumentNullException.ThrowIfNull(dependency);
            if (!string.Equals(dependency.Type, "mcp", StringComparison.OrdinalIgnoreCase))
                return CopilotAgentSkillMcpDependencyStatus.NotMcp;

            var transport = ResolveTransport(dependency);
            if (!string.Equals(transport, StreamableHttpTransport, StringComparison.OrdinalIgnoreCase))
                return CopilotAgentSkillMcpDependencyStatus.UnsupportedTransport;
            if (string.IsNullOrWhiteSpace(dependency.Url))
                return CopilotAgentSkillMcpDependencyStatus.MissingInstallMetadata;
            if (!TryCreateServerConfig(dependency, out var candidate, out _))
                return CopilotAgentSkillMcpDependencyStatus.InvalidConfiguration;

            var matchingServers = (configuredServers ?? Array.Empty<CopilotMcpClientServerConfig>())
                .Where(server => server != null && EndpointsEqual(server.Endpoint, candidate.Endpoint))
                .ToArray();
            if (matchingServers.Any(server => server.Enabled))
                return CopilotAgentSkillMcpDependencyStatus.Installed;
            return matchingServers.Length > 0
                ? CopilotAgentSkillMcpDependencyStatus.ConfiguredDisabled
                : CopilotAgentSkillMcpDependencyStatus.Installable;
        }

        public static bool TryCreateServerConfig(
            CopilotAgentSkillDependency dependency,
            out CopilotMcpClientServerConfig server,
            out string error)
        {
            ArgumentNullException.ThrowIfNull(dependency);
            server = new CopilotMcpClientServerConfig();
            error = string.Empty;
            if (!string.Equals(dependency.Type, "mcp", StringComparison.OrdinalIgnoreCase))
            {
                error = "Skill dependency is not an MCP tool dependency.";
                return false;
            }

            var transport = ResolveTransport(dependency);
            if (!string.Equals(transport, StreamableHttpTransport, StringComparison.OrdinalIgnoreCase))
            {
                error = $"ColorVision external MCP currently supports only {StreamableHttpTransport}; dependency transport is {transport}.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(dependency.Url))
            {
                error = "The Skill MCP dependency does not declare a URL.";
                return false;
            }

            var line = $"{dependency.Value} | {dependency.Url} | | approval";
            if (!CopilotMcpClientConfigurationText.TryParse(line, out var parsed, out error)
                || parsed.Count != 1)
            {
                return false;
            }

            server = parsed[0];
            return true;
        }

        public static string ResolveTransport(CopilotAgentSkillDependency dependency)
        {
            ArgumentNullException.ThrowIfNull(dependency);
            return string.IsNullOrWhiteSpace(dependency.Transport)
                ? StreamableHttpTransport
                : dependency.Transport.Trim();
        }

        internal static bool EndpointsEqual(string? left, string? right)
        {
            return Uri.TryCreate(left, UriKind.Absolute, out var leftUri)
                && Uri.TryCreate(right, UriKind.Absolute, out var rightUri)
                && Uri.Compare(
                    leftUri,
                    rightUri,
                    UriComponents.HttpRequestUrl,
                    UriFormat.SafeUnescaped,
                    StringComparison.OrdinalIgnoreCase) == 0;
        }
    }
}
