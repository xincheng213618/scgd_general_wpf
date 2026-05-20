using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using ColorVision.UI.Languages;

namespace ColorVision.Copilot
{
    public sealed class CopilotSetLanguageTool : ICopilotTool
    {
        public string Name => "SetLanguage";

        public string Description => "按用户要求切换界面语言；input.query 可填写语言或文化名，例如 英文、中文、en-US、zh-Hans。切换时可能提示确认并重启应用。";

        public bool CanHandle(CopilotAgentRequest request)
        {
            return request != null
                && request.Mode != CopilotAgentMode.Chat
                && Application.Current != null
                && CopilotApplicationControlSupport.HasLanguageIntent(request.UserText);
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

            if (!CopilotApplicationControlSupport.TryResolveLanguage(sourceText, out var targetCulture))
            {
                return new CopilotToolResult
                {
                    ToolName = Name,
                    Success = false,
                    Summary = "未识别目标语言。",
                    Content = $"[可用语言] {CopilotApplicationControlSupport.GetLanguageOptionsText()}",
                    ErrorMessage = "请明确指定目标语言，例如中文、英文、zh-Hans 或 en-US。",
                };
            }

            if (Application.Current == null)
            {
                return new CopilotToolResult
                {
                    ToolName = Name,
                    Success = false,
                    Summary = "当前没有可用的应用实例，无法切换语言。",
                    ErrorMessage = "Application.Current 为空。",
                };
            }

            var targetLanguageLabel = CopilotApplicationControlSupport.GetLanguageDisplayName(targetCulture);
            var currentCulture = string.Empty;
            var changed = false;

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                currentCulture = Thread.CurrentThread.CurrentUICulture.Name;
                if (string.Equals(currentCulture, targetCulture, StringComparison.OrdinalIgnoreCase))
                    return;

                changed = LanguageManager.Current.LanguageChange(targetCulture);
            });

            if (string.Equals(currentCulture, targetCulture, StringComparison.OrdinalIgnoreCase))
            {
                return new CopilotToolResult
                {
                    ToolName = Name,
                    Success = true,
                    Summary = $"当前已是 {targetLanguageLabel}，无需切换。",
                    Content = $"[当前语言] {targetLanguageLabel}({targetCulture})",
                };
            }

            if (!changed)
            {
                return new CopilotToolResult
                {
                    ToolName = Name,
                    Success = false,
                    Summary = $"已取消切换到 {targetLanguageLabel}。",
                    Content = $"[可用语言] {CopilotApplicationControlSupport.GetLanguageOptionsText()}",
                    ErrorMessage = "语言切换需要用户确认并重启应用；本次未完成变更。",
                };
            }

            return new CopilotToolResult
            {
                ToolName = Name,
                Success = true,
                Summary = $"已切换界面语言为 {targetLanguageLabel}，应用将重启。",
                Content = $"[目标语言] {targetLanguageLabel}({targetCulture})",
            };
        }
    }
}