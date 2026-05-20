using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using ColorVision.Themes;
using ColorVision.UI;

namespace ColorVision.Copilot
{
    public sealed class CopilotSetThemeTool : ICopilotTool
    {
        public string Name => "SetTheme";

        public string Description => "按用户要求切换应用主题；input.query 可填写目标主题，例如 dark、light、pink、cyan 或 跟随系统。";

        public bool CanHandle(CopilotAgentRequest request)
        {
            return request != null
                && request.Mode != CopilotAgentMode.Chat
                && Application.Current != null
                && CopilotApplicationControlSupport.HasThemeIntent(request.UserText);
        }

        public async Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var sourceText = string.IsNullOrWhiteSpace(toolInput?.Query)
                ? request.UserText
                : toolInput.Query;

            if (!CopilotApplicationControlSupport.TryResolveTheme(sourceText, out var targetTheme))
            {
                return new CopilotToolResult
                {
                    ToolName = Name,
                    Success = false,
                    Summary = "未识别目标主题。",
                    Content = $"[可用主题] {CopilotApplicationControlSupport.GetThemeOptionsText()}",
                    ErrorMessage = "请明确指定目标主题，例如深色、浅色、粉色、青色或跟随系统。",
                };
            }

            if (Application.Current == null)
            {
                return new CopilotToolResult
                {
                    ToolName = Name,
                    Success = false,
                    Summary = "当前没有可用的应用实例，无法切换主题。",
                    ErrorMessage = "Application.Current 为空。",
                };
            }

            var targetThemeLabel = CopilotApplicationControlSupport.GetThemeDisplayName(targetTheme);
            var currentTheme = Theme.UseSystem;

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                currentTheme = ThemeConfig.Instance.Theme;
                if (currentTheme == targetTheme)
                    return;

                ThemeConfig.Instance.Theme = targetTheme;
                Application.Current.ApplyTheme(targetTheme);
                ConfigService.Instance.SaveConfigs();
            });

            if (currentTheme == targetTheme)
            {
                return new CopilotToolResult
                {
                    ToolName = Name,
                    Success = true,
                    Summary = $"当前已是 {targetThemeLabel} 主题，无需切换。",
                    Content = $"[当前主题] {targetThemeLabel}",
                };
            }

            return new CopilotToolResult
            {
                ToolName = Name,
                Success = true,
                Summary = $"已切换应用主题为 {targetThemeLabel}。",
                Content = string.Join(Environment.NewLine, new[]
                {
                    $"[已应用主题] {targetThemeLabel}",
                    $"[可用主题] {CopilotApplicationControlSupport.GetThemeOptionsText()}",
                }),
            };
        }
    }
}