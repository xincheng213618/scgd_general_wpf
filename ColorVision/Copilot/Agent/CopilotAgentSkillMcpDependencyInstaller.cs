using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.Copilot
{
    internal sealed record CopilotAgentSkillMcpDependencyInstallPlan(
        IReadOnlyList<CopilotMcpClientServerConfig> Servers,
        IReadOnlyList<string> Issues)
    {
        public bool HasServers => Servers.Count > 0;
    }

    internal static class CopilotAgentSkillMcpDependencyInstaller
    {
        public static IReadOnlyList<CopilotAgentSkillCatalogItem> ResolveExplicitSkills(
            string? prompt,
            CopilotAgentSkillReference? exactReference,
            IEnumerable<CopilotAgentSkillCatalogItem>? availableSkills)
        {
            var catalog = (availableSkills ?? Array.Empty<CopilotAgentSkillCatalogItem>()).ToArray();
            var resolved = new Dictionary<string, CopilotAgentSkillCatalogItem>(StringComparer.OrdinalIgnoreCase);
            if (exactReference?.IsExplicitlyInvokedBy(prompt) == true)
            {
                var exact = catalog.FirstOrDefault(item => exactReference.Matches(
                    item.Name,
                    item.SkillFilePath));
                if (exact != null)
                    resolved[exact.SkillFilePath] = exact;
            }

            foreach (var sameName in catalog.GroupBy(item => item.Name, StringComparer.OrdinalIgnoreCase))
            {
                var candidates = sameName.ToArray();
                if (candidates.Length != 1)
                    continue;
                var item = candidates[0];
                var reference = CopilotAgentSkillReference.FromCatalogItem(item);
                if (reference?.IsExplicitlyInvokedBy(prompt) == true)
                    resolved[item.SkillFilePath] = item;
            }

            return resolved.Values
                .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.SkillFilePath, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public static CopilotAgentSkillMcpDependencyInstallPlan CreatePlan(
            IEnumerable<CopilotAgentSkillDependency>? dependencies,
            IEnumerable<CopilotMcpClientServerConfig>? configuredServers)
        {
            var existing = (configuredServers ?? Array.Empty<CopilotMcpClientServerConfig>())
                .Where(server => server != null)
                .ToArray();
            var candidates = new List<CopilotMcpClientServerConfig>();
            var issues = new List<string>();
            foreach (var dependency in dependencies ?? Array.Empty<CopilotAgentSkillDependency>())
            {
                if (!string.Equals(dependency.Type, "mcp", StringComparison.OrdinalIgnoreCase))
                    continue;

                var status = CopilotAgentSkillMcpDependencyPolicy.Evaluate(dependency, existing);
                if (status == CopilotAgentSkillMcpDependencyStatus.Installed)
                    continue;
                if (status == CopilotAgentSkillMcpDependencyStatus.ConfiguredDisabled)
                {
                    issues.Add($"{dependency.Value} 已存在但被用户禁用，未自动重新启用。");
                    continue;
                }
                var error = string.Empty;
                if (status != CopilotAgentSkillMcpDependencyStatus.Installable
                    || !CopilotAgentSkillMcpDependencyPolicy.TryCreateServerConfig(
                        dependency,
                        out var candidate,
                        out error))
                {
                    issues.Add($"{dependency.Value} 无法安全配置：{FormatStatus(status, error)}");
                    continue;
                }

                var existingName = existing.FirstOrDefault(server => string.Equals(
                    server.Name,
                    candidate.Name,
                    StringComparison.OrdinalIgnoreCase));
                if (existingName != null)
                {
                    issues.Add($"{candidate.Name} 与现有 MCP server 名称冲突，未覆盖现有配置。");
                    continue;
                }
                if (candidates.Any(server => CopilotAgentSkillMcpDependencyPolicy.EndpointsEqual(
                    server.Endpoint,
                    candidate.Endpoint)))
                {
                    continue;
                }
                if (candidates.Any(server => string.Equals(
                    server.Name,
                    candidate.Name,
                    StringComparison.OrdinalIgnoreCase)))
                {
                    issues.Add($"{candidate.Name} 在 Skill 依赖中对应多个不同端点，未自动配置。");
                    continue;
                }
                if (existing.Length + candidates.Count >= CopilotMcpClientConfigurationText.MaximumServers)
                {
                    issues.Add($"外部 MCP 已达到 {CopilotMcpClientConfigurationText.MaximumServers} 个上限，未配置 {candidate.Name}。");
                    continue;
                }

                candidates.Add(candidate);
            }

            return new CopilotAgentSkillMcpDependencyInstallPlan(
                candidates.Select(server => server.Clone()).ToArray(),
                issues.ToArray());
        }

        public static bool TryInstall(
            CopilotAgentSkillMcpDependencyInstallPlan plan,
            IList<CopilotMcpClientServerConfig> configuredServers,
            out IReadOnlyList<CopilotMcpClientServerConfig> addedServers,
            out string error)
        {
            ArgumentNullException.ThrowIfNull(plan);
            ArgumentNullException.ThrowIfNull(configuredServers);
            var existing = configuredServers.Where(server => server != null).ToArray();
            var additions = new List<CopilotMcpClientServerConfig>();
            foreach (var server in plan.Servers)
            {
                var dependency = new CopilotAgentSkillDependency(
                    "mcp",
                    server.Name,
                    string.Empty,
                    CopilotAgentSkillMcpDependencyPolicy.StreamableHttpTransport,
                    string.Empty,
                    server.Endpoint);
                if (!CopilotAgentSkillMcpDependencyPolicy.TryCreateServerConfig(
                    dependency,
                    out var safeServer,
                    out var validationError))
                {
                    addedServers = Array.Empty<CopilotMcpClientServerConfig>();
                    error = "MCP 安装计划不再符合安全配置规则；没有写入任何新配置。" + Environment.NewLine
                        + validationError;
                    return false;
                }
                if (existing.Any(candidate => CopilotAgentSkillMcpDependencyPolicy.EndpointsEqual(
                    candidate.Endpoint,
                    safeServer.Endpoint)))
                {
                    addedServers = Array.Empty<CopilotMcpClientServerConfig>();
                    error = $"MCP 端点 {safeServer.Endpoint} 已被其他配置占用；没有写入任何新配置。";
                    return false;
                }
                if (existing.Any(candidate => string.Equals(
                    candidate.Name,
                    safeServer.Name,
                    StringComparison.OrdinalIgnoreCase)))
                {
                    addedServers = Array.Empty<CopilotMcpClientServerConfig>();
                    error = $"MCP server 名称 {safeServer.Name} 已存在；没有覆盖现有配置。";
                    return false;
                }
                if (additions.Any(candidate => string.Equals(
                        candidate.Name,
                        safeServer.Name,
                        StringComparison.OrdinalIgnoreCase)
                    || CopilotAgentSkillMcpDependencyPolicy.EndpointsEqual(
                        candidate.Endpoint,
                        safeServer.Endpoint)))
                {
                    addedServers = Array.Empty<CopilotMcpClientServerConfig>();
                    error = "安装计划包含重复的 MCP server 名称或端点；没有写入任何新配置。";
                    return false;
                }
                additions.Add(safeServer);
            }

            if (existing.Length + additions.Count > CopilotMcpClientConfigurationText.MaximumServers)
            {
                addedServers = Array.Empty<CopilotMcpClientServerConfig>();
                error = $"外部 MCP 最多配置 {CopilotMcpClientConfigurationText.MaximumServers} 个；没有写入任何新配置。";
                return false;
            }

            foreach (var server in additions)
                configuredServers.Add(server);
            addedServers = additions.ToArray();
            error = string.Empty;
            return true;
        }

        public static string CreatePromptKey(string conversationId, CopilotMcpClientServerConfig server) =>
            (conversationId ?? string.Empty).Trim()
            + "|streamable_http|"
            + (server?.Endpoint ?? string.Empty).Trim();

        private static string FormatStatus(
            CopilotAgentSkillMcpDependencyStatus status,
            string error) => status switch
        {
            CopilotAgentSkillMcpDependencyStatus.MissingInstallMetadata => "缺少 URL",
            CopilotAgentSkillMcpDependencyStatus.UnsupportedTransport => "当前仅支持 streamable_http",
            CopilotAgentSkillMcpDependencyStatus.InvalidConfiguration => "URL 或 server 名称不符合安全配置规则",
            _ when !string.IsNullOrWhiteSpace(error) => error,
            _ => "元数据不完整或不受支持",
        };
    }
}
