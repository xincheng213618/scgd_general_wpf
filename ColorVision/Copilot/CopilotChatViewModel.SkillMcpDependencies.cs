using ColorVision.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;

namespace ColorVision.Copilot
{
    public partial class CopilotChatViewModel
    {
        private readonly HashSet<string> _continuedSkillMcpDependencyKeys = new(StringComparer.OrdinalIgnoreCase);

        private bool TryPrepareExplicitSkillMcpDependencies(
            string prompt,
            CopilotAgentSkillReference? skillReference,
            CopilotProjectInstructionDiscoveryOptions codexConfigOptions,
            string conversationId)
        {
            if (!codexConfigOptions.ConfiguredSkillMcpDependencyInstallEnabled)
                return true;

            var skills = CopilotAgentSkillMcpDependencyInstaller.ResolveExplicitSkills(
                prompt,
                skillReference,
                DiscoverAgentSkillCatalog(includeDisabled: false, forceReload: false));
            if (skills.Count == 0)
                return true;

            var plan = CopilotAgentSkillMcpDependencyInstaller.CreatePlan(
                skills.SelectMany(skill => skill.Dependencies),
                _config.ExternalMcpServers);
            var unpromptedServers = plan.Servers
                .Where(server => !_continuedSkillMcpDependencyKeys.Contains(
                    CopilotAgentSkillMcpDependencyInstaller.CreatePromptKey(conversationId, server)))
                .ToArray();
            if (unpromptedServers.Length == 0)
                return true;

            var promptPlan = new CopilotAgentSkillMcpDependencyInstallPlan(
                unpromptedServers,
                plan.Issues);
            var decision = MessageBox.Show(
                Application.Current.GetActiveWindow(),
                FormatSkillMcpDependencyInstallPrompt(skills, promptPlan),
                "ColorVision · Skill MCP 依赖",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);
            if (decision == MessageBoxResult.Cancel || decision == MessageBoxResult.None)
            {
                SetPendingActionFeedback("已取消本次发送；草稿与 MCP 配置均未改变。");
                return false;
            }

            if (decision == MessageBoxResult.No)
            {
                foreach (var server in unpromptedServers)
                {
                    _continuedSkillMcpDependencyKeys.Add(
                        CopilotAgentSkillMcpDependencyInstaller.CreatePromptKey(conversationId, server));
                }
                SetPendingActionFeedback("本次会话将继续执行，但未写入 Skill 声明的 MCP 依赖。");
                return true;
            }

            if (!CopilotAgentSkillMcpDependencyInstaller.TryInstall(
                promptPlan,
                _config.ExternalMcpServers,
                out var addedServers,
                out var error))
            {
                MessageBox.Show(
                    Application.Current.GetActiveWindow(),
                    error,
                    "ColorVision · MCP 配置未改变",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            try
            {
                PersistConfig();
            }
            catch (Exception ex)
            {
                foreach (var server in addedServers)
                    _config.ExternalMcpServers.Remove(server);
                try
                {
                    PersistConfig();
                }
                catch
                {
                }

                MessageBox.Show(
                    Application.Current.GetActiveWindow(),
                    "MCP 配置保存失败；本次发送已取消。" + Environment.NewLine
                    + CopilotUserFacingErrorFormatter.Sanitize(ex.Message),
                    "ColorVision · MCP 配置未保存",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            SetPendingActionFeedback(
                $"已配置 {addedServers.Count} 个 Skill MCP server；本轮将以逐工具需审批策略使用。如服务需要认证，请在设置中补充 bearer token 环境变量。");
            return true;
        }

        private static string FormatSkillMcpDependencyInstallPrompt(
            IReadOnlyList<CopilotAgentSkillCatalogItem> skills,
            CopilotAgentSkillMcpDependencyInstallPlan plan)
        {
            var builder = new StringBuilder();
            builder.Append("显式点名的 Skill ")
                .Append(string.Join(", ", skills.Select(skill => "$" + skill.Name)))
                .AppendLine(" 声明了尚未配置的 MCP 依赖：")
                .AppendLine();
            foreach (var server in plan.Servers)
            {
                builder.Append("- ")
                    .Append(server.Name)
                    .Append(" · ")
                    .AppendLine(server.Endpoint);
            }
            if (plan.Issues.Count > 0)
            {
                builder.AppendLine()
                    .AppendLine("未纳入自动配置的依赖：");
                foreach (var issue in plan.Issues)
                    builder.Append("- ").AppendLine(issue);
            }

            builder.AppendLine()
                .AppendLine("是：写入这些 streamable HTTP server，并默认对每个工具要求审批。")
                .AppendLine("否：本次会话继续执行，但不修改 MCP 配置。")
                .Append("取消：保留草稿，不发送请求。");
            return builder.ToString();
        }
    }
}
